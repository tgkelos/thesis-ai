namespace Core
{
    public record PieceState(
        int Id,
        PieceType Type,
        int Hp,
        int MaxHp,
        bool IsStunned = false,
        bool HasActedThisRound = false,
        // ... spell durations, cooldowns, buffs, debuffs, etc.
        int ShieldWallDuration = 0,
        int PowerInfusionDuration = 0,
        int BleedDuration = 0,
        int BleedPower = 0,
        int IncreasedDamageTaken = 0,
        int CleanseDuration = 0,
        int CooldownStun = 0,
        int CooldownRend = 0,
        int CooldownCleanse = 0,
        int CooldownSpecialST = 0,
        int CooldownSpecialAOE = 0
    );
}
