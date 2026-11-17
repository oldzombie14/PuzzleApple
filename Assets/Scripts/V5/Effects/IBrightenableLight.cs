namespace PuzzleApple.V5
{
    // A light source that "light light" brightens. Each object applies the boost itself by reading
    // SentenceRuntime.LampMultiplier; IlluminationEffects only asks whether a lit source is in scope.
    public interface IBrightenableLight
    {
        // A lit source is present, so brightening it now is a visible change.
        bool Lit {get;}
    }
}
