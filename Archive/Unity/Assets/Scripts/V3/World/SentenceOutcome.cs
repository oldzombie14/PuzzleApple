namespace PuzzleApple.V3
{
    // Runtime outcome is separate from grammar recognition and does not change permission.
    // The board can adopt a presentation later without changing world handlers.
    public enum SentenceOutcome { InvalidGrammar, Conflicted, Active, WaitingForPlace, Unsupported, Resetting }
}
