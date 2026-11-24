namespace PuzzleApple.V5.Cognition
{
    public enum SentenceAction { Legacy, On, Off, Move, Illuminate, Freeze }
    // ExistingController marks paths whose scope has not yet migrated to a common policy.
    public enum SentenceScopePolicy { ExistingController, CurrentRegion, CommandRegion, Global }

    public sealed class SentenceIntent
    {
        public SentenceAction Action { get; }
        public WordDefinition Subject { get; }
        public WordDefinition Target { get; }
        public SentenceKind Kind { get; }
        public SentenceScopePolicy Scope { get; }
        public SentenceProperty SubjectProperty { get; }
        public SentenceProperty TargetProperty { get; }
        public SentenceProperty StateProperty { get; }
        public SentenceMotion Motion => Action == SentenceAction.Move ? new SentenceMotion(SubjectProperty, TargetProperty) : default;
        public bool Is(SentenceAction action, SentenceProperty subject) =>
            Action == action && subject && SubjectProperty == subject;
        public bool Moves(SentenceProperty subject, SentenceProperty target = null) =>
            Is(SentenceAction.Move, subject) && TargetProperty == target;
        public bool SameMeaning(SentenceIntent other) => other != null && Action == other.Action &&
            SubjectProperty == other.SubjectProperty && TargetProperty == other.TargetProperty &&
            StateProperty == other.StateProperty && Kind == other.Kind && Scope == other.Scope;
        public SentenceIntent(SentenceAction action, WordDefinition subject, WordDefinition target,
            SentenceKind kind, SentenceScopePolicy scope)
        {
            Action = action; Subject = subject; Target = target; Kind = kind; Scope = scope;
            // Capture semantic references with the parse, just like action and scope.
            SubjectProperty = subject ? subject.Property : null;
            StateProperty = subject ? subject.StateProperty : null;
            TargetProperty = target ? target.Property : null;
        }
    }
}
