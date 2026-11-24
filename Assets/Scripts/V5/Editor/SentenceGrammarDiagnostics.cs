using System;
using System.Collections.Generic;
using System.Linq;
using PuzzleApple.V5.Cognition;

namespace PuzzleApple.V5.Editor
{
    public static class SentenceGrammarDiagnostics
    {
        public sealed class Row
        {
            public string words, source, intent, diagnostic;
            public bool accepted;
        }
        public static IEnumerable<WordDefinition[]> Combinations(IReadOnlyList<WordDefinition> vocabulary, int length)
        {
            var row = new WordDefinition[length];
            return Expand(0);
            IEnumerable<WordDefinition[]> Expand(int index)
            {
                if (index == length) { yield return (WordDefinition[])row.Clone(); yield break; }
                foreach (var word in vocabulary)
                {
                    row[index] = word;
                    foreach (var result in Expand(index + 1)) yield return result;
                }
            }
        }
        public static List<Row> BuildRows(CognitionCatalog source)
        {
            var resolver = new SentenceGrammarResolver(source); var result = new List<Row>();
            int max = Math.Max(3, source.Rules.Max(r => r.Words.Count));
            if (source.Grammar && source.Grammar.templates.Length > 0) max = Math.Max(max, source.Grammar.templates.Max(t => t.slots.Length));
            // Longer combinations can be inspected separately; avoid an accidental exponential editor stall.
            if (max > 4) throw new InvalidOperationException("组合面板支持最多四词句型。");
            for (int length = 2; length <= max; length++)
                foreach (var words in Combinations(source.Words, length))
                {
                    var row = new Row { words = SentenceGrammarResolver.Key(words) };
                    try
                    {
                        var parsed = resolver.Resolve(words); var intent = parsed.Intent;
                        row.accepted = parsed.Recognized; row.source = parsed.Source; row.diagnostic = parsed.Diagnostic;
                        row.intent = intent == null ? "" : $"{intent.Action}  {intent.Subject?.Id ?? "—"} → {intent.Target?.Id ?? "—"}  / {intent.Kind} / {intent.Scope}";
                    }
                    catch (InvalidOperationException error) { row.diagnostic = error.Message; }
                    result.Add(row);
                }
            return result;
        }
    }
}
