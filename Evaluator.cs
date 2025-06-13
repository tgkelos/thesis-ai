using System;
using System.IO;
using System.Linq;

namespace Core
{
    public class Evaluator
    {
        public double[] Weights;                 // <- public for TD-learning
        public static readonly int FeatureCount = 16;

        public Evaluator()
        {
            // Start all weights at 1.0; they will be learned.
            Weights = Enumerable.Repeat(1.0, FeatureCount).ToArray();
        }

        public double Evaluate(GameState state, int forPlayer)
        {
            var f = GetFeatures(state, forPlayer);
            double score = 0;
            for (int i = 0; i < f.Length; i++) score += f[i] * Weights[i];
            return score;
        }

        public double[] GetFeatures(GameState state, int forPlayer)
        {
            var me = forPlayer == 1 ? state.Player1 : state.Player2;
            var opp = forPlayer == 1 ? state.Player2 : state.Player1;

            double maxHpMe = me.Pieces.Sum(p => p.MaxHp);
            double maxHpOpp = opp.Pieces.Sum(p => p.MaxHp);
            const double maxMana = 20.0;
            const double maxPieces = 8.0;

            // ------- helpers -------
            int OwnActed = me.Pieces.Count(p => p.HasActedThisRound);
            int OppActed = opp.Pieces.Count(p => p.HasActedThisRound);
            int OwnReady = me.Pieces.Count(p => p.Hp > 0 && !p.HasActedThisRound && !p.IsStunned);
            int OppReady = opp.Pieces.Count(p => p.Hp > 0 && !p.HasActedThisRound && !p.IsStunned);
            int CritOwn = me.Pieces.Count(p => p.Hp > 0 && p.Hp <= p.MaxHp * 0.25);

            double CoolReadyRatio(PlayerState ps) =>
                ps.Pieces.Where(p => p.Hp > 0).Select(p =>
                {
                    int cdSum = p.CooldownRend + p.CooldownCleanse + p.CooldownSpecialST + p.CooldownSpecialAOE;
                    int max = 3 + 3 + 3 + 3; // all 3-turn CDs
                    return 1.0 - cdSum / (double)max;
                }).DefaultIfEmpty(1.0).Average();

            // ------- build feature vector -------
            var feats = new double[FeatureCount];

            feats[0] = (me.Pieces.Sum(p => Math.Max(0, p.Hp)) - opp.Pieces.Sum(p => Math.Max(0, p.Hp)))
                        / (maxHpMe + maxHpOpp);                          // HP diff
            feats[1] = (me.Pieces.Count(p => p.Hp > 0) - opp.Pieces.Count(p => p.Hp > 0)) / maxPieces;
            feats[2] = (me.Mana - opp.Mana) / maxMana;
            feats[3] = (OwnActed - OppActed) / maxPieces;
            feats[4] = (OwnReady - OppReady) / maxPieces;
            feats[5] = me.Pieces.Count(p => p.ShieldWallDuration > 0) / maxPieces;
            feats[6] = me.Pieces.Count(p => p.PowerInfusionDuration > 0) / maxPieces;
            feats[7] = opp.Pieces.Count(p => p.BleedDuration > 0) / maxPieces;
            feats[8] = (opp.Pieces.Count(p => p.IsStunned) - me.Pieces.Count(p => p.IsStunned)) / maxPieces;
            feats[9] = CritOwn / maxPieces;
            feats[10] = state.RoundNumber <= 3 ? 1.0 : 0.0;
            feats[11] = (state.RoundStarterIsP1 ^ (forPlayer == 1)) ? 1.0 : 0.0;   // started second?
            feats[12] = (state.IsPlayer1Turn == (forPlayer == 1)) ? 1.0 : 0.0;     // is my turn
            feats[13] = CoolReadyRatio(me) - CoolReadyRatio(opp);
            feats[14] = opp.Pieces.Sum(p => p.IncreasedDamageTaken) / 80.0;
            feats[15] = opp.Pieces.Sum(p => p.BleedPower) / 80.0;

            return feats;
        }

        
        public void LoadWeightsFromFile(string file)
        {
            if (!File.Exists(file)) return;
            var parts = File.ReadAllText(file).Split(',');
            Weights = parts.Take(FeatureCount)
                           .Select(s => double.TryParse(s, out var d) ? d : 1.0)
                           .ToArray();
        }
    }
}
