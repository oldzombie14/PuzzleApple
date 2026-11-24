using System.Collections.Generic;
using System.Linq;

namespace PuzzleApple.V5.Cognition
{
    public sealed class SentenceParseResult
    {
        public SentenceIntent Intent { get; }
        public CognitionRule Rule { get; }
        public string Source { get; }
        public string Diagnostic { get; }
        public IReadOnlyList<CognitionSignal> Effects { get; }
        public CognitionSignal Signal => !Recognized ? CognitionSignal.Empty :
            Intent.Action == SentenceAction.Legacy ? Rule.Effect : Effects.FirstOrDefault();
        public bool Recognized => Rule != null;
        internal SentenceParseResult(SentenceIntent intent, CognitionRule rule, string source, string diagnostic,
            CognitionSignal[] effects = null)
        { Intent = intent; Rule = rule; Source = source; Diagnostic = diagnostic; Effects = effects ?? System.Array.Empty<CognitionSignal>(); }
    }

    // The grammar produces intent; this adapter supplies the existing execution signals/channels.
    // Keep it separate so migrating an executor does not change sentence recognition.
    public sealed class SentenceGrammarResolver
    {
        readonly SentenceGrammar grammar;
        readonly Dictionary<string, CognitionRule> bindings;
        readonly HashSet<WordDefinition> vocabulary;
        public SentenceGrammarResolver(CognitionCatalog catalog)
        {
            grammar = catalog.Grammar;
            bindings = catalog.Rules.ToDictionary(r => Key(r.Words));
            vocabulary = new HashSet<WordDefinition>(catalog.Words);
        }
        public static string Key(IEnumerable<WordDefinition> words) => string.Join(" ", words.Select(w => w.Id));
        public SentenceParseResult Resolve(IReadOnlyList<WordDefinition> words)
        {
            if (words.Count < 2 || words.Any(w => !w || !vocabulary.Contains(w)))
                return new SentenceParseResult(null, null, "", "Incomplete or unregistered words");
            bindings.TryGetValue(Key(words), out var rule);
            string source = "Legacy catalog";
            var intent = grammar ? grammar.Match(words, out source) : rule ?
                new SentenceIntent(SentenceAction.Legacy, null, null, rule.Kind, SentenceScopePolicy.ExistingController) : null;
            if (intent == null) return new SentenceParseResult(null, null, source, "No accepted syntax");
            if (!rule) return new SentenceParseResult(intent, null, source, "Missing execution binding");
            if (intent.Kind != rule.Kind) return new SentenceParseResult(intent, null, source, "Intent/binding sentence kind mismatch");
            if (!SentenceLegacySignalBridge.TryResolve(intent, rule, out var effects))
                return new SentenceParseResult(intent, null, source, "Missing semantic property or controller adapter");
            return new SentenceParseResult(intent, rule, source, "", effects);
        }
    }
}
