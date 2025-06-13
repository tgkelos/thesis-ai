namespace Core
{
    public enum PieceType
    {
        Tank,
        Healer,
        DPS
    }

    public enum SpellType
    {
        // Tank
        Thunderclap,
        ShieldWall,
        Stun,
        Rend,
        // Healer
        FlashHeal,
        PowerInfusion,
        Smite,
        Cleanse,
        // DPS
        Fireball,
        Blastwave,
        SpecialST,
        SpecialAOE,
        Pass
    }

    public enum GameResult
    {
        Ongoing,
        Player1Win,
        Player2Win,
        Tie
    }
}
