using PuzzleApple.V5.Cognition;

namespace PuzzleApple.V5
{
    // Something a motion sentence (light/door/floor ... move) can visibly move in its space.
    // Objects keep their own motion presentation and read the applied effects themselves
    // (SentenceRuntime.HasEffect / OutdoorWorld.ApplyMotion); MotionEffect only asks whether the
    // sentence would move something here now, which drives feedback and shots.
    public interface ISentenceMovable
    {
        bool CanMove(CognitionSignal motion);
    }
}
