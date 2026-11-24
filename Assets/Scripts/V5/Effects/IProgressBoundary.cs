using System;

namespace PuzzleApple.V5
{
    // Geometry owns its progress; the gate owns unlock policy and region connectivity.
    public interface IProgressBoundary
    {
        float BoundaryProgress { get; }
        bool AllowsConnection { get; }
        event Action BoundaryChanged;
    }
}
