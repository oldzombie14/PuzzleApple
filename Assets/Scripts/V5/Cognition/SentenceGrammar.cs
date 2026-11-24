using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PuzzleApple.V5.Cognition
{
    [CreateAssetMenu(menuName = "PuzzleApple/V5/Cognition/Grammar", fileName = "SentenceGrammar")]
    public sealed class SentenceGrammar : ScriptableObject
    {
        [Serializable] public sealed class Slot
        {
            public SentenceWordClass classes;
            public WordDefinition[] words = Array.Empty<WordDefinition>();
            public bool Matches(WordDefinition word) => word &&
                (classes == SentenceWordClass.None || (word.Classes & classes) != 0) &&
                (words.Length == 0 || words.Contains(word));
        }
        [Serializable] public sealed class IntentPattern
        {
            public SentenceAction action;
            public int subject = -1, target = -1;
            public SentenceKind kind;
            public SentenceScopePolicy scope;
            public SentenceIntent Create(IReadOnlyList<WordDefinition> words) =>
                new SentenceIntent(action, subject < 0 ? null : words[subject], target < 0 ? null : words[target], kind, scope);
        }
        [Serializable] public sealed class Template
        {
            public string id;
            public Slot[] slots = Array.Empty<Slot>();
            public IntentPattern intent = new IntentPattern();
            public bool Matches(IReadOnlyList<WordDefinition> words) => words.Count == slots.Length &&
                slots.Select((slot, i) => slot.Matches(words[i])).All(matches => matches);
        }
        [Serializable] public sealed class ExceptionRule
        {
            public string id;
            public WordDefinition[] words = Array.Empty<WordDefinition>();
            public bool reject;
            public IntentPattern intent = new IntentPattern();
        }
        public Template[] templates = Array.Empty<Template>();
        public ExceptionRule[] exceptions = Array.Empty<ExceptionRule>();
        [Tooltip("Explicitly deferred rules. They keep their existing execution and do not enter templates.")]
        public CognitionRule[] legacyRules = Array.Empty<CognitionRule>();

        public SentenceIntent Match(IReadOnlyList<WordDefinition> words, out string source)
        {
            source = "No matching template";
            foreach (var exception in exceptions)
                if (exception.words.SequenceEqual(words))
                { source = "Exception: " + exception.id; return exception.reject ? null : exception.intent.Create(words); }
            var matches = templates.Where(template => template.Matches(words)).ToArray();
            var legacy = legacyRules.FirstOrDefault(rule => rule.Words.SequenceEqual(words));
            if (matches.Length > 1 || matches.Length > 0 && legacy)
                throw new InvalidOperationException("Ambiguous grammar: " + string.Join(" ", words.Select(w => w.Id)));
            if (matches.Length == 1) { source = "Template: " + matches[0].id; return matches[0].intent.Create(words); }
            if (legacy) { source = "Deferred: " + legacy.Id; return new SentenceIntent(SentenceAction.Legacy, null, null, legacy.Kind, SentenceScopePolicy.ExistingController); }
            return null;
        }

        // Expand only template slots, not all vocabulary permutations, when validating assets.
        IEnumerable<WordDefinition[]> Expand(Template template, IReadOnlyList<WordDefinition> vocabulary, WordDefinition[] row, int index)
        {
            if (index == row.Length) { yield return (WordDefinition[])row.Clone(); yield break; }
            foreach (var word in vocabulary.Where(template.slots[index].Matches))
            {
                row[index] = word;
                foreach (var result in Expand(template, vocabulary, row, index + 1)) yield return result;
            }
        }
        public List<string> Validate(CognitionCatalog catalog)
        {
            var errors = new List<string>(); var ids = new HashSet<string>(); var exceptionKeys = new HashSet<string>();
            if (templates == null || exceptions == null || legacyRules == null)
            { errors.Add("Missing grammar collections."); return errors; }
            void Id(string id) { if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) errors.Add("Missing/duplicate grammar entry ID: " + id); }
            void Intent(IntentPattern intent, int length, string id)
            {
                if (intent == null || intent.subject < -1 || intent.subject >= length || intent.target < -1 || intent.target >= length ||
                    !Enum.IsDefined(typeof(SentenceAction), intent.action) || !Enum.IsDefined(typeof(SentenceKind), intent.kind) ||
                    !Enum.IsDefined(typeof(SentenceScopePolicy), intent.scope)) errors.Add("Invalid intent: " + id);
            }
            foreach (var template in templates)
            {
                if (template == null) { errors.Add("Missing template."); continue; }
                Id(template.id);
                if (template.slots == null || template.slots.Length < 2) { errors.Add("Template needs two or more slots: " + template.id); continue; }
                foreach (var slot in template.slots)
                    if (slot == null || slot.words == null || slot.words.Any(w => !w || !catalog.Words.Contains(w)) || !catalog.Words.Any(slot.Matches))
                        errors.Add("Invalid or empty slot: " + template.id);
                Intent(template.intent, template.slots.Length, template.id);
            }
            foreach (var exception in exceptions)
            {
                if (exception == null) { errors.Add("Missing exception."); continue; }
                Id(exception.id);
                if (exception.words == null || exception.words.Length < 2 || exception.words.Any(w => !w || !catalog.Words.Contains(w)))
                { errors.Add("Invalid exception words: " + exception.id); continue; }
                if (!exceptionKeys.Add(SentenceGrammarResolver.Key(exception.words))) errors.Add("Duplicate exception pattern: " + exception.id);
                if (!exception.reject) Intent(exception.intent, exception.words.Length, exception.id);
            }
            if (legacyRules.Any(r => !r || !catalog.Rules.Contains(r)) || legacyRules.Distinct().Count() != legacyRules.Length)
                errors.Add("Missing or duplicate deferred rule.");
            if (errors.Count > 0) return errors;
            var resolver = new SentenceGrammarResolver(catalog);
            var candidates = templates.SelectMany(t => Expand(t, catalog.Words, new WordDefinition[t.slots.Length], 0))
                .Concat(exceptions.Select(e => e.words)).Concat(catalog.Rules.Select(r => r.Words.ToArray()));
            var seen = new HashSet<string>();
            foreach (var words in candidates)
            {
                string key = SentenceGrammarResolver.Key(words); if (!seen.Add(key)) continue;
                try
                {
                    var parsed = resolver.Resolve(words);
                    bool bound = catalog.Rules.Any(r => r.Words.SequenceEqual(words));
                    if (parsed.Intent != null && !parsed.Recognized || bound && !parsed.Recognized) errors.Add(key + ": " + parsed.Diagnostic);
                }
                catch (InvalidOperationException error) { errors.Add(error.Message); }
            }
            return errors;
        }
    }
}
