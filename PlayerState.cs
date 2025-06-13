using System.Collections.Generic;

namespace Core
{
    public record PlayerState(
        List<PieceState> Pieces,   
        int Mana
    );
}
