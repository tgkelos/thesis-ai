using System;
using System.Collections.Generic;

namespace Core
{
    public class MinimaxSolver
    {
        private readonly Evaluator _evaluator;
        private readonly IRuleEngine _engine;

        public MinimaxSolver(Evaluator evaluator, IRuleEngine engine)
        {
            _evaluator = evaluator;
            _engine = engine;
        }

        public Move GetBestMove(GameState state, int forPlayer, int depth)
        {
            double bestScore = double.NegativeInfinity;
            Move bestMove = null;
            var moves = _engine.GenerateLegalMoves(state, forPlayer);

            foreach (var move in moves)
            {
                var next = _engine.ApplyMove(state, move);
                double score = MinValue(next, depth - 1, double.NegativeInfinity, double.PositiveInfinity, forPlayer);
                if (score > bestScore || bestMove == null)
                {
                    bestScore = score;
                    bestMove = move;
                }
            }
            return bestMove;
        }

        private double MaxValue(GameState state, int depth, double alpha, double beta, int forPlayer)
        {
            GameResult result;
            if (depth == 0 || _engine.IsTerminal(state, out result))
                return _evaluator.Evaluate(state, forPlayer);

            double value = double.NegativeInfinity;
            var moves = _engine.GenerateLegalMoves(state, forPlayer);

            foreach (var move in moves)
            {
                var next = _engine.ApplyMove(state, move);
                value = Math.Max(value, MinValue(next, depth - 1, alpha, beta, forPlayer));
                if (value >= beta)
                    return value;
                alpha = Math.Max(alpha, value);
            }
            return value;
        }

        private double MinValue(GameState state, int depth, double alpha, double beta, int forPlayer)
        {
            int oppPlayer = forPlayer == 1 ? 2 : 1;
            GameResult result;
            if (depth == 0 || _engine.IsTerminal(state, out result))
                return _evaluator.Evaluate(state, forPlayer);

            double value = double.PositiveInfinity;
            var moves = _engine.GenerateLegalMoves(state, oppPlayer);

            foreach (var move in moves)
            {
                var next = _engine.ApplyMove(state, move);
                value = Math.Min(value, MaxValue(next, depth - 1, alpha, beta, forPlayer));
                if (value <= alpha)
                    return value;
                beta = Math.Min(beta, value);
            }
            return value;
        }
    }
}
