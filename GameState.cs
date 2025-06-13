namespace Core
{
    public record GameState(
        PlayerState Player1,
        PlayerState Player2,
        bool IsPlayer1Turn,      // Whose action is next
        int MovesSinceLastKill,  // For tie rule
        int CurrentPieceIndex,   // Whose turn in the activation order (0-9)
        bool RoundStarterIsP1,   // Who starts the current round
        int RoundNumber          // Current round number (starts at 1)
    );
}
