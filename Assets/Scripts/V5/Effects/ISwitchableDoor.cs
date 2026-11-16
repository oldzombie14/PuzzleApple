namespace PuzzleApple.V5
{
    // A door that "door on" (open / lit) / "door off" can command. The object keeps its own
    // presentation (swing, glow); DoorStateEffect only decides which door is in scope.
    public interface ISwitchableDoor
    {
        // Commanding this state now would produce a visible change.
        bool WouldChange(bool open);
        void SetOpen(bool open);
        // Stop at the current state when the command is removed.
        void Hold();
        // The command sentence retired; the door keeps its final state.
        void Release();
    }
}
