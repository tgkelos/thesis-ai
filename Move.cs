namespace Core
{
    public record Move(
        int ActingPlayer,
        int PieceIndex,
        SpellType Spell,
        int? TargetPlayer,
        int? TargetPieceIndex
    )
    {
        public static Move PassMove(int actingPlayer)
            => new Move(actingPlayer, 0, SpellType.Pass, null, null);
    }
}
