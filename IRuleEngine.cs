namespace Core
{
    public interface IRuleEngine
    {
        List<Move> GenerateLegalMoves(GameState state, int actingPlayer);
        GameState ApplyMove(GameState state, Move move);
        bool IsTerminal(GameState state, out GameResult result);
    }
}
