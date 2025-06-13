using System;
using System.Collections.Generic;
using System.Linq;

namespace Core
{
    public class RuleEngine : IRuleEngine
    {
        private readonly Dictionary<SpellType, int> _spellManaCosts = new()
        {
            { SpellType.Thunderclap, 2 }, { SpellType.ShieldWall, 4 }, { SpellType.Stun, 5 }, { SpellType.Rend, 6 },
            { SpellType.Smite, 2 }, { SpellType.FlashHeal, 4 }, { SpellType.PowerInfusion, 3 }, { SpellType.Cleanse, 4 },
            { SpellType.Fireball, 3 }, { SpellType.Blastwave, 5 }, { SpellType.SpecialST, 7 }, { SpellType.SpecialAOE, 8 }
        };

        private readonly Dictionary<SpellType, int> _spellCooldowns = new()
        {
            { SpellType.Rend, 3 }, { SpellType.Cleanse, 3 }, { SpellType.SpecialST, 3 }, { SpellType.SpecialAOE, 3 }
        };

        public bool IsGameOver(GameState state)
        {
            bool p1Dead = state.Player1.Pieces.All(p => p.Hp <= 0);
            bool p2Dead = state.Player2.Pieces.All(p => p.Hp <= 0);
            return p1Dead || p2Dead;
        }

        public bool IsTieBreaker(GameState state)
        {
            return state.MovesSinceLastKill >= 30;
        }

        public List<Move> GenerateLegalMoves(GameState state, int actingPlayer)
        {
            // Determine which player is acting
            var player = actingPlayer == 1 ? state.Player1 : state.Player2;
            int count = player.Pieces.Count;
            int start = state.CurrentPieceIndex;
            int idx = start;

            // 1) Find the next ready piece (alive, not stunned, not acted)
            for (int i = 0; i < count; i++)
            {
                var p = player.Pieces[idx];
                if (p.Hp > 0 && !p.IsStunned && !p.HasActedThisRound)
                    break;
                idx = (idx + 1) % count;
            }

            // 2) Generate all legal spells for that piece
            var moves = new List<Move>();
            var piece = player.Pieces[idx];
            if (piece.Hp > 0 && !piece.IsStunned && !piece.HasActedThisRound)
            {
                foreach (var spell in GetSpellsForPiece(piece.Type))
                {
                    if (!IsSpellAvailable(piece, spell, player.Mana))
                        continue;

                    foreach (var (tgtPlayer, tgtIndex) in GetValidTargets(spell, idx, state, actingPlayer))
                    {
                        moves.Add(new Move(actingPlayer, idx, spell, tgtPlayer, tgtIndex));
                    }
                }

                // Allow a Pass only for the active piece
                moves.Add(Move.PassMove(actingPlayer));
            }

            return moves;
        }



        private IEnumerable<SpellType> GetSpellsForPiece(PieceType type)
        {
            return type switch
            {
                PieceType.Tank => new[] { SpellType.Thunderclap, SpellType.ShieldWall, SpellType.Stun, SpellType.Rend },
                PieceType.Healer => new[] { SpellType.Smite, SpellType.FlashHeal, SpellType.PowerInfusion, SpellType.Cleanse },
                PieceType.DPS => new[] { SpellType.Fireball, SpellType.Blastwave, SpellType.SpecialST, SpellType.SpecialAOE },
                _ => Array.Empty<SpellType>()
            };
        }

        private bool IsSpellAvailable(PieceState piece, SpellType spell, int playerMana)
        {
            if (!_spellManaCosts.TryGetValue(spell, out var manaCost))
                return false;
            if (playerMana < manaCost)
                return false;

            return spell switch
            {
                SpellType.Rend => piece.CooldownRend == 0,
                SpellType.Cleanse => piece.CooldownCleanse == 0,
                SpellType.SpecialST => piece.CooldownSpecialST == 0,
                SpellType.SpecialAOE => piece.CooldownSpecialAOE == 0,
                _ => true
            };
        }

        private IEnumerable<(int tgtPlayer, int tgtIndex)> GetValidTargets(SpellType spell, int selfIndex, GameState state, int actingPlayer)
        {
            var player = actingPlayer == 1 ? state.Player1 : state.Player2;
            var enemy = actingPlayer == 1 ? state.Player2 : state.Player1;

            switch (spell)
            {
                case SpellType.Thunderclap:
                case SpellType.Stun:
                case SpellType.Rend:
                case SpellType.Smite:
                case SpellType.Fireball:
                case SpellType.SpecialST:
                    return enemy.Pieces
                        .Select((p, i) => (actingPlayer == 1 ? 2 : 1, i))
                        .Where(t => enemy.Pieces[t.i].Hp > 0);

                case SpellType.FlashHeal:
                case SpellType.PowerInfusion:
                case SpellType.Cleanse:
                    return player.Pieces
                        .Select((p, i) => (actingPlayer, i))
                        .Where(t => player.Pieces[t.i].Hp > 0);

                case SpellType.ShieldWall:
                    return new List<(int, int)> { (actingPlayer, selfIndex) };

                case SpellType.Blastwave:
                case SpellType.SpecialAOE:
                    return new List<(int, int)> { (actingPlayer == 1 ? 2 : 1, -1) };

                default:
                    return Array.Empty<(int, int)>();
            }
        }

        public GameState ApplyMove(GameState state, Move move)
        {
            // 1. Clone and apply move logic
            var p1 = state.Player1 with { Pieces = state.Player1.Pieces.Select(p => p with { }).ToList() };
            var p2 = state.Player2 with { Pieces = state.Player2.Pieces.Select(p => p with { }).ToList() };
            bool isP1 = state.IsPlayer1Turn;
            var player = isP1 ? p1 : p2;
            var enemy = isP1 ? p2 : p1;

            bool pieceDiedThisMove = false;
            if (move.Spell == SpellType.Pass)
            {
                player = player with
                {
                    Pieces = player.Pieces
                        .Select(p => p.Hp > 0 && !p.HasActedThisRound
                                      ? p with { HasActedThisRound = true }
                                      : p)
                        .ToList()
                };
            }
            else
            {
                var piece = player.Pieces[move.PieceIndex];
                int cost = _spellManaCosts[move.Spell];
                player = player with { Mana = player.Mana - cost };
                piece = piece with { HasActedThisRound = true };
                piece = move.Spell switch
                {
                    SpellType.Rend => piece with { CooldownRend = _spellCooldowns[SpellType.Rend] },
                    SpellType.Cleanse => piece with { CooldownCleanse = _spellCooldowns[SpellType.Cleanse] },
                    SpellType.SpecialST => piece with { CooldownSpecialST = _spellCooldowns[SpellType.SpecialST] },
                    SpellType.SpecialAOE => piece with { CooldownSpecialAOE = _spellCooldowns[SpellType.SpecialAOE] },
                    _ => piece
                };
                (player, enemy, pieceDiedThisMove) = ApplySpellEffect(move, piece, player, enemy);
            }

            var newP1 = isP1 ? player : enemy;
            var newP2 = isP1 ? enemy : player;

            bool roundEnded = AllActed(newP1) && AllActed(newP2);

            // Wrap the activation slot back into [0..7] so we never go out of range
            int pieceCount = (isP1 ? state.Player1 : state.Player2).Pieces.Count;
            int nextPieceIndex = roundEnded ? 0 : (state.CurrentPieceIndex + 1) % pieceCount;
            bool nextRoundStarter = roundEnded ? !state.RoundStarterIsP1 : state.RoundStarterIsP1;
            int nextRoundNumber = roundEnded ? state.RoundNumber + 1 : state.RoundNumber;

            // on even activation slots, it's the round starter; on odd, it's the other player
            bool nextTurnIsP1 = (nextPieceIndex % 2 == 0)
                ? nextRoundStarter
                : !nextRoundStarter;

            int movesSinceLastKill = pieceDiedThisMove ? 0 : state.MovesSinceLastKill + 1;

            // TickRound if new round
            if (roundEnded)
            {
                newP1 = TickRound(newP1, manaToAdd: 6);
                newP2 = TickRound(newP2, manaToAdd: 6);
            }

            return state with
            {
                Player1 = newP1,
                Player2 = newP2,
                MovesSinceLastKill = movesSinceLastKill,
                IsPlayer1Turn = nextTurnIsP1,
                RoundStarterIsP1 = nextRoundStarter,
                RoundNumber = nextRoundNumber,
                CurrentPieceIndex = nextPieceIndex
            };
        }

        private bool AllActed(PlayerState p)
        {
            return p.Pieces.Where(pc => pc.Hp > 0).All(pc => pc.HasActedThisRound);
        }

        public PlayerState TickRound(PlayerState player, int manaToAdd)
        {
            var newPieces = player.Pieces.Select(piece =>
            {
                var cooldownRend = Math.Max(0, piece.CooldownRend - 1);
                var cooldownCleanse = Math.Max(0, piece.CooldownCleanse - 1);
                var cooldownSpecialST = Math.Max(0, piece.CooldownSpecialST - 1);
                var cooldownSpecialAOE = Math.Max(0, piece.CooldownSpecialAOE - 1);
                var shieldWall = Math.Max(0, piece.ShieldWallDuration - 1);
                var powerInf = Math.Max(0, piece.PowerInfusionDuration - 1);

                int hp = piece.Hp;
                int bleedDur = piece.BleedDuration;
                int bleedPower = piece.BleedPower;
                if (bleedDur > 0 && hp > 0)
                {
                    hp = Math.Max(0, hp - bleedPower);
                    bleedDur = Math.Max(0, bleedDur - 1);
                    if (bleedDur == 0) bleedPower = 0;
                }
                int incDmg = bleedDur > 0 ? 10 : 0;

                bool stunned = piece.IsStunned;
                int stunDur = piece.IsStunned ? 1 : 0;
                if (piece.IsStunned && piece.ShieldWallDuration == 0)
                    stunned = false;

                int cleanse = piece.CleanseDuration;
                if (cleanse == 1)
                {
                    stunned = false;
                    bleedDur = 0;
                    bleedPower = 0;
                    incDmg = 0;
                    cleanse = 0;
                }
                else if (cleanse > 0)
                {
                    cleanse = Math.Max(0, cleanse - 1);
                }

                bool hasActed = false;

                return piece with
                {
                    Hp = hp,
                    CooldownRend = cooldownRend,
                    CooldownCleanse = cooldownCleanse,
                    CooldownSpecialST = cooldownSpecialST,
                    CooldownSpecialAOE = cooldownSpecialAOE,
                    ShieldWallDuration = shieldWall,
                    PowerInfusionDuration = powerInf,
                    BleedDuration = bleedDur,
                    BleedPower = bleedPower,
                    IncreasedDamageTaken = incDmg,
                    IsStunned = stunned,
                    CleanseDuration = cleanse,
                    HasActedThisRound = hasActed
                };
            }).ToList();

            int newMana = player.Mana + manaToAdd;
            return player with { Mana = newMana, Pieces = newPieces };
        }

        public bool IsTerminal(GameState s, out GameResult result)
        {
            bool p1Alive = s.Player1.Pieces.Any(p => p.Hp > 0);
            bool p2Alive = s.Player2.Pieces.Any(p => p.Hp > 0);

            if (!p1Alive && !p2Alive)
            {
                result = GameResult.Tie;
                return true;
            }
            if (!p1Alive)
            {
                result = GameResult.Player2Win;
                return true;
            }
            if (!p2Alive)
            {
                result = GameResult.Player1Win;
                return true;
            }
            if (s.MovesSinceLastKill >= 30)
            {
                int hp1 = s.Player1.Pieces.Sum(p => Math.Max(0, p.Hp));
                int hp2 = s.Player2.Pieces.Sum(p => Math.Max(0, p.Hp));
                if (hp1 == hp2)
                    result = GameResult.Tie;
                else
                    result = hp1 > hp2 ? GameResult.Player1Win : GameResult.Player2Win;
                return true;
            }
            result = GameResult.Ongoing;
            return false;
        }

        private int CalculateDamage(PieceState target, PieceState attacker, int baseDmg)
        {
            double dmg = baseDmg;
            if (target.IncreasedDamageTaken > 0)
                dmg *= 1.10;
            if (attacker.PowerInfusionDuration > 0)
                dmg *= 1.20;
            if (target.ShieldWallDuration > 0)
                dmg *= 0.70;
            return (int)Math.Round(dmg);
        }

        private List<PieceState> ApplyToTargetPiece(List<PieceState> pieces, int idx, PieceState updated)
        {
            var list = pieces.ToList();
            list[idx] = updated;
            return list;
        }

        private (PlayerState player, PlayerState enemy, bool pieceDied) ApplySpellEffect(
Move move, PieceState piece, PlayerState player, PlayerState enemy)
        {
            bool pieceDied = false;

            // For convenience
            var pList = player.Pieces.ToList();
            var eList = enemy.Pieces.ToList();

            int actingPlayer = move.ActingPlayer; // 1 or 2 (ensure Move has this field)
            int? tgtPlayer = move.TargetPlayer;
            int? tgtIndex = move.TargetPieceIndex;
            int attackerIdx = move.PieceIndex;

            // Determine if a target is on the enemy team
            bool TargetIsEnemy = tgtPlayer != null && tgtPlayer != actingPlayer;
            bool TargetIsAlly = tgtPlayer != null && tgtPlayer == actingPlayer;

            switch (move.Spell)
            {
                // --- ENEMY TARGETED ---
                case SpellType.Thunderclap:
                    if (TargetIsEnemy && tgtIndex != null)
                    {
                        var target = eList[tgtIndex.Value];
                        int dmg = CalculateDamage(target, piece, 7);
                        int newHp = Math.Max(0, target.Hp - dmg);
                        if (newHp == 0 && target.Hp > 0) pieceDied = true;
                        target = target with { Hp = newHp };
                        eList = ApplyToTargetPiece(eList, tgtIndex.Value, target);
                    }
                    break;

                case SpellType.Stun:
                    if (TargetIsEnemy && tgtIndex != null)
                    {
                        var target = eList[tgtIndex.Value];
                        int dmg = CalculateDamage(target, piece, 1);
                        int newHp = Math.Max(0, target.Hp - dmg);
                        if (newHp == 0 && target.Hp > 0) pieceDied = true;
                        target = target with { Hp = newHp, IsStunned = true };
                        eList = ApplyToTargetPiece(eList, tgtIndex.Value, target);
                    }
                    break;

                case SpellType.Rend:
                    if (TargetIsEnemy && tgtIndex != null)
                    {
                        var target = eList[tgtIndex.Value];
                        int dmg = CalculateDamage(target, piece, 5);
                        int newHp = Math.Max(0, target.Hp - dmg);
                        if (newHp == 0 && target.Hp > 0) pieceDied = true;
                        target = target with
                        {
                            Hp = newHp,
                            BleedDuration = 3,
                            BleedPower = 12,
                            IncreasedDamageTaken = 10
                        };
                        eList = ApplyToTargetPiece(eList, tgtIndex.Value, target);
                    }
                    break;

                case SpellType.Smite:
                    if (TargetIsEnemy && tgtIndex != null)
                    {
                        var target = eList[tgtIndex.Value];
                        int dmg = CalculateDamage(target, piece, 6);
                        int newHp = Math.Max(0, target.Hp - dmg);
                        if (newHp == 0 && target.Hp > 0) pieceDied = true;
                        target = target with { Hp = newHp };
                        eList = ApplyToTargetPiece(eList, tgtIndex.Value, target);
                    }
                    break;

                case SpellType.Fireball:
                    if (TargetIsEnemy && tgtIndex != null)
                    {
                        var target = eList[tgtIndex.Value];
                        int dmg = CalculateDamage(target, piece, 9);
                        int newHp = Math.Max(0, target.Hp - dmg);
                        if (newHp == 0 && target.Hp > 0) pieceDied = true;
                        target = target with { Hp = newHp };
                        eList = ApplyToTargetPiece(eList, tgtIndex.Value, target);
                    }
                    break;

                case SpellType.SpecialST:
                    if (TargetIsEnemy && tgtIndex != null)
                    {
                        var target = eList[tgtIndex.Value];
                        int dmg = CalculateDamage(target, piece, 12);
                        int newHp = Math.Max(0, target.Hp - dmg);
                        if (newHp == 0 && target.Hp > 0) pieceDied = true;
                        target = target with { Hp = newHp };
                        eList = ApplyToTargetPiece(eList, tgtIndex.Value, target);
                    }
                    break;

                // --- SELF/ALLY TARGETED ---
                case SpellType.ShieldWall:
                    pList = ApplyToTargetPiece(pList, attackerIdx, pList[attackerIdx] with { ShieldWallDuration = 2 });
                    break;

                case SpellType.FlashHeal:
                    if (TargetIsAlly && tgtIndex != null)
                    {
                        var target = pList[tgtIndex.Value];
                        int healAmount = 7;
                        int clamped = Math.Min(healAmount, target.MaxHp - target.Hp);
                        target = target with { Hp = target.Hp + clamped };
                        pList = ApplyToTargetPiece(pList, tgtIndex.Value, target);
                    }
                    break;

                case SpellType.PowerInfusion:
                    if (TargetIsAlly && tgtIndex != null)
                    {
                        var target = pList[tgtIndex.Value];
                        target = target with { PowerInfusionDuration = 2 };
                        pList = ApplyToTargetPiece(pList, tgtIndex.Value, target);
                    }
                    break;

                case SpellType.Cleanse:
                    if (TargetIsAlly && tgtIndex != null)
                    {
                        var target = pList[tgtIndex.Value];
                        target = target with { CleanseDuration = 1 };
                        pList = ApplyToTargetPiece(pList, tgtIndex.Value, target);
                    }
                    break;

                // --- MULTI-TARGET (NO TARGETPLAYER NEEDED) ---
                case SpellType.Blastwave:
                    {
                        var rand = new Random();
                        var aliveIndexes = eList
                            .Select((p, idx) => (p, idx))
                            .Where(x => x.p.Hp > 0)
                            .Select(x => x.idx)
                            .ToList();

                        var targets = aliveIndexes.OrderBy(_ => rand.Next()).Take(5);
                        foreach (var idx in targets)
                        {
                            var target = eList[idx];
                            int dmg = CalculateDamage(target, piece, 7);
                            int newHp = Math.Max(0, target.Hp - dmg);
                            if (newHp == 0 && target.Hp > 0) pieceDied = true;
                            target = target with { Hp = newHp };
                            eList = ApplyToTargetPiece(eList, idx, target);
                        }
                    }
                    break;

                case SpellType.SpecialAOE:
                    for (int idx = 0; idx < eList.Count; idx++)
                    {
                        var target = eList[idx];
                        if (target.Hp > 0)
                        {
                            int dmg = CalculateDamage(target, piece, 10);
                            int newHp = Math.Max(0, target.Hp - dmg);
                            if (newHp == 0 && target.Hp > 0) pieceDied = true;
                            target = target with { Hp = newHp };
                            eList = ApplyToTargetPiece(eList, idx, target);
                        }
                    }
                    break;
            }

            // Return updated PlayerStates and death flag
            var playerOut = player with { Pieces = pList };
            var enemyOut = enemy with { Pieces = eList };
            return (playerOut, enemyOut, pieceDied);
        }






    }
}
