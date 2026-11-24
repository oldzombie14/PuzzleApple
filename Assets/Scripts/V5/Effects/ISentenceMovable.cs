namespace PuzzleApple.V5
{
    // Reports whether the physical controller can produce visible motion for this subject and target.
    public interface ISentenceMovable
    {
        bool CanMove(SentenceMotion motion);
    }
}
