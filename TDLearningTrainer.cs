using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Core;

public class TDLearningTrainer
{

    public void SaveWeights(string weightsFile, double[] weights)
    {
        File.WriteAllText(
            weightsFile,
            string.Join(",",
                weights.Select(w => w.ToString("F6", CultureInfo.InvariantCulture))
            )
        );
    }
    public void Train(
        int numGames,
        double alpha = 0.0005,
        double gamma = 0.95,
        int depth = 3,
        string weightsFile = "weights.csv",
        string logFile = "training_log.csv",
        string covFile = "covariance.csv"
    )
    {
        var engine = new RuleEngine();
        int numWeights = Evaluator.FeatureCount;               // =16
        var evaluator = new Evaluator();
        evaluator.LoadWeightsFromFile(weightsFile);            // override defaults if file exists

        var solver = new MinimaxSolver(evaluator, engine);
        var random = new Random();

        var weightSnapshots = new List<double[]>();
        int p1First = 0, p2First = 0, p1Second = 0, p2Second = 0, ties = 0;

        using var writer = new StreamWriter(logFile, false);
        writer.WriteLine(
            "game,alpha,gamma,depth," +
            "p1FirstWins,p2FirstWins,p1SecondWins,p2SecondWins,ties," +
            string.Join(",", Enumerable.Range(0, numWeights).Select(i => $"w{i}"))
        );

        const int reportEvery = 1000;
        const int barLength = 40;
        const double epsStart = 0.20;
        const double epsEnd = 0.05;
        const int annealTill = 20000;

        for (int game = 0; game < numGames; game++)
        {
            // anneal epsilon
            double epsilon = game < annealTill
                ? epsStart + (epsEnd - epsStart) * (game / (double)annealTill)
                : epsEnd;
            bool p1Starts = (game % 2 == 0);

            // init 8v8
            var p1 = MakeDefaultPlayer();
            var p2 = MakeDefaultPlayer();
            var state = new GameState(p1, p2, p1Starts, 0, 0, p1Starts, 1);
            int currentPlayer = state.IsPlayer1Turn ? 1 : 2;

            var history = new List<(GameState, double[], int)>();
            GameResult result;

            // play
            while (!engine.IsTerminal(state, out result))
            {
                var legal = engine.GenerateLegalMoves(state, currentPlayer) ?? new List<Move>();
                if (legal.Count == 0)
                    legal.Add(Move.PassMove(currentPlayer));

                Move move;
                if (random.NextDouble() < epsilon)
                    move = legal[random.Next(legal.Count)];
                else
                    move = solver.GetBestMove(state, currentPlayer, depth)
                           ?? legal[random.Next(legal.Count)];

                var feats = evaluator.GetFeatures(state, currentPlayer);
                history.Add((state, feats, currentPlayer));

                state = engine.ApplyMove(state, move);
                currentPlayer = state.IsPlayer1Turn ? 1 : 2;
            }

            // TD update
            double rewardP1 = result == GameResult.Player1Win ? 1.0
                              : result == GameResult.Player2Win ? -1.0
                                                                   : 0.0;
            double rewardP2 = -rewardP1;
            double nextP1 = rewardP1, nextP2 = rewardP2;

            for (int t = history.Count - 1; t >= 0; t--)
            {
                var (st, feats, actor) = history[t];
                if (actor == 1)
                {
                    double val = evaluator.Evaluate(st, 1);
                    double delta = nextP1 - val;
                    for (int i = 0; i < numWeights; i++)
                        evaluator.Weights[i] = Math.Clamp(
                            evaluator.Weights[i] + alpha * delta * feats[i],
                            -1000, 1000
                        );
                    nextP1 = val;
                }
                else
                {
                    double val = evaluator.Evaluate(st, 2);
                    double delta = nextP2 - val;
                    for (int i = 0; i < numWeights; i++)
                        evaluator.Weights[i] = Math.Clamp(
                            evaluator.Weights[i] + alpha * delta * feats[i],
                            -1000, 1000
                        );
                    nextP2 = val;
                }
            }

            // tally
            if (p1Starts)
            {
                if (result == GameResult.Player1Win) p1First++;
                else if (result == GameResult.Player2Win) p2Second++;
                else ties++;
            }
            else
            {
                if (result == GameResult.Player2Win) p2First++;
                else if (result == GameResult.Player1Win) p1Second++;
                else ties++;
            }

            // progress bar
            if (game % 10 == 0 || game == numGames - 1)
            {
                double pct = (game + 1) / (double)numGames;
                int done = (int)(pct * barLength);
                Console.Write($"\r[{new string('=', done)}{new string(' ', barLength - done)}] "
                              + $"{game + 1}/{numGames} games | "
                              + $"P1⇆P2: {p1First},{p2First}  2⇆1: {p1Second},{p2Second}  Ties={ties}");
            }

            // periodic logging
            if ((game + 1) % reportEvery == 0 || game == numGames - 1)
            {
                writer.WriteLine($"{game + 1},{alpha},{gamma},{depth},"
                    + $"{p1First},{p2First},{p1Second},{p2Second},{ties},"
                    + string.Join(",", evaluator.Weights.Select(w =>
                        w.ToString("F6", CultureInfo.InvariantCulture)))
                );
                writer.Flush();
                Console.WriteLine();
                Console.WriteLine($"After {game + 1} games: Weights = "
                                + string.Join(", ", evaluator.Weights
                                    .Select(w => w.ToString("F4", CultureInfo.InvariantCulture))));
            }

            weightSnapshots.Add(evaluator.Weights.ToArray());
        }

        // final
        Console.WriteLine("\nTraining complete! Final weights:");
        Console.WriteLine(string.Join(", ",
            evaluator.Weights.Select(w => w.ToString("F4", CultureInfo.InvariantCulture))));
        SaveWeights(weightsFile, evaluator.Weights);
        Console.WriteLine($"Weights saved to {weightsFile}");
        Console.WriteLine($"Log saved to {logFile}");

        // covariance
        var cov = ComputeCovariance(weightSnapshots);
        // Save covariance matrix to CSV
        SaveCovarianceMatrix(covFile, cov);
        Console.WriteLine($"Covariance matrix saved to {covFile}");
        
    }

    private static PlayerState MakeDefaultPlayer()
    {
        var list = new List<PieceState>();
        for (int i = 0; i < 8; i++)
        {
            var t = i < 2 ? PieceType.Tank
                    : i < 4 ? PieceType.Healer
                            : PieceType.DPS;
            int hp = (t == PieceType.Tank) ? 27
                   : (t == PieceType.Healer) ? 18
                                              : 22;
            list.Add(new PieceState(i, t, hp, hp));
        }
        return new PlayerState(list, Mana: 20);
    }

    private static double[,] ComputeCovariance(List<double[]> snaps)
    {
        int n = snaps.Count, m = snaps[0].Length;
        var mean = new double[m];
        foreach (var w in snaps)
            for (int i = 0; i < m; i++)
                mean[i] += w[i];
        for (int i = 0; i < m; i++)
            mean[i] /= n;

        var cov = new double[m, m];
        foreach (var w in snaps)
            for (int i = 0; i < m; i++)
                for (int j = 0; j < m; j++)
                    cov[i, j] += (w[i] - mean[i]) * (w[j] - mean[j]);
        for (int i = 0; i < m; i++)
            for (int j = 0; j < m; j++)
                cov[i, j] /= (n - 1);

        return cov;
    }
    // New method to write covariance matrix to CSV
    private void SaveCovarianceMatrix(string filePath, double[,] cov)
    {
        int dim = cov.GetLength(0);
        using var writer = new StreamWriter(filePath, false);
        for (int i = 0; i < dim; i++)
        {
            var row = new string[dim];
            for (int j = 0; j < dim; j++)
            {
                row[j] = cov[i, j].ToString("F6", CultureInfo.InvariantCulture);
            }
            writer.WriteLine(string.Join(",", row));
        }
    }
}
