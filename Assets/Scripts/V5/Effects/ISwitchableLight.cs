namespace PuzzleApple.V5
{
    // A light source that "light on" / "light off" can command. Each object keeps its own
    // presentation (fade, twinkle, shrink); LightStateEffect only decides which ones are in scope.
    public interface ISwitchableLight
    {
        // Commanding this state now would produce a visible change (feedback and shots).
        bool WouldChange(bool on);
        // The commanded transition has not settled yet; the command sentence stays until it has.
        bool Settling(bool on);
        void SetOn(bool on);
        // Stop at the current level when another command takes over.
        void Hold();
    }
}
