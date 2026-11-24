using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PuzzleApple.Cognition
{
    [CreateAssetMenu(menuName = "PuzzleApple/Cognition/Catalog", fileName = "CognitionCatalog")]
    public sealed class CognitionCatalog : ScriptableObject
    {
        [SerializeField] WordDefinition[] words = new WordDefinition[0];
        [SerializeField] CognitionRule[] rules = new CognitionRule[0];
        [SerializeField] CognitionConflict[] conflicts = new CognitionConflict[0];
        public IReadOnlyList<WordDefinition> Words => words;
        public IReadOnlyList<CognitionRule> Rules => rules;
        public IReadOnlyList<CognitionConflict> Conflicts => conflicts;

        public WordDefinition Word(string id)
        {
            var word = words.FirstOrDefault(w => w && w.Id == id);
            if (!word) throw new ArgumentException("Unknown cognition word ID: " + id, nameof(id));
            return word;
        }

        public List<string> Validate()
        {
            var errors = new List<string>();
            var ids = new HashSet<string>();
            void CheckId(string id, string label)
            {
                if (string.IsNullOrWhiteSpace(id) || id != id.Trim().ToLowerInvariant() || id.Any(char.IsWhiteSpace))
                    errors.Add(label + ": use a nonempty lowercase ID without whitespace.");
                else if (!ids.Add(id)) errors.Add(label + ": duplicate ID " + id);
            }
            if (words.Length == 0) errors.Add("Catalog needs words.");
            if (rules.Length == 0) errors.Add("Catalog needs rules.");
            foreach (var word in words)
            {
                if (!word) { errors.Add("Missing word reference."); continue; }
                CheckId(word.Id, word.name);
                if (string.IsNullOrWhiteSpace(word.DisplayText)) errors.Add(word.name + ": missing display text.");
            }
            var patterns = new HashSet<string>();
            var effects = new HashSet<CognitionSignal>();
            foreach (var rule in rules)
            {
                if (!rule) { errors.Add("Missing rule reference."); continue; }
                CheckId(rule.Id, rule.name);
                if (rule.Words.Count < 2) errors.Add(rule.name + ": a sentence needs at least two words.");
                if (rule.Words.Any(w => !w || !words.Contains(w))) errors.Add(rule.name + ": word is missing or outside this catalog.");
                else if (!patterns.Add(string.Join(" ", rule.Words.Select(w => w.Id)))) errors.Add(rule.name + ": duplicate sentence pattern.");
                if (!Enum.IsDefined(typeof(CognitionSignal), rule.Effect)) errors.Add(rule.name + ": unknown effect.");
                effects.Add(rule.Effect);
            }
            var pairs = new HashSet<string>();
            foreach (var conflict in conflicts)
            {
                if (!conflict) { errors.Add("Missing conflict reference."); continue; }
                CheckId(conflict.Id, conflict.name);
                if (conflict.First == conflict.Second || conflict.First == CognitionSignal.Empty || conflict.Second == CognitionSignal.Empty)
                    errors.Add(conflict.name + ": conflict needs two different nonempty effects.");
                if (!effects.Contains(conflict.First) || !effects.Contains(conflict.Second)) errors.Add(conflict.name + ": effect has no rule in this catalog.");
                int a = (int)conflict.First, b = (int)conflict.Second;
                if (!pairs.Add(Math.Min(a,b) + ":" + Math.Max(a,b))) errors.Add(conflict.name + ": duplicate conflict pair.");
            }
            return errors;
        }
        public void ValidateOrThrow()
        {
            var errors = Validate();
            if (errors.Count > 0) throw new InvalidOperationException(name + ":\n" + string.Join("\n", errors));
        }
    }
}
