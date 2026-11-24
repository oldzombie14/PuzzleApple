using System;
using System.Collections.Generic;
using System.Linq;

namespace PuzzleApple.Cognition
{
    public enum CognitionChange { Add, Layout, Join, Split, Swap }

    // Pure state: visual positions and persistent world facts do not belong here.
    public sealed class CognitionState
    {
        public sealed class Word
        {
            public int Id { get; }
            public WordDefinition Definition { get; }
            public string Text => Definition.DisplayText;
            public string Meaning => Definition.Id;
            internal Word(int id, WordDefinition definition) { Id = id; Definition = definition; }
        }

        public sealed class Group
        {
            public int Id { get; }
            internal readonly List<Word> words;
            public IReadOnlyList<Word> Words => words;
            public bool Independent => words.Count == 1;
            public bool Recognized { get; internal set; }
            public bool Conflicted { get; internal set; }
            public bool Effective => Recognized && !Conflicted;
            public CognitionSignal Signal { get; internal set; }
            public string RuleId { get; internal set; }
            internal Group(int id, IEnumerable<Word> source) { Id = id; words = source.ToList(); }
        }

        readonly List<Group> groups = new List<Group>();
        readonly Dictionary<string, (string Id, CognitionSignal Effect)> rules = new Dictionary<string, (string, CognitionSignal)>();
        readonly Dictionary<string, WordDefinition> vocabulary;
        readonly (CognitionSignal First, CognitionSignal Second)[] conflicts;
        readonly HashSet<string> acquisitions = new HashSet<string>();
        int nextWord, nextGroup;
        public IReadOnlyList<Group> Groups => groups;
        public event Action<CognitionChange> Changed;
        public bool AppleMoving => HasEffect(CognitionSignal.AppleMove);
        public bool PlayerMoving => HasEffect(CognitionSignal.PlayerMove);
        public bool PlayerMovementBlocked => HasEffect(CognitionSignal.PlayerNoMove);
        public bool CanConsumeApple => HasEffect(CognitionSignal.ConsumeApple);
        public bool CanOpen => HasEffect(CognitionSignal.Open);
        bool HasEffect(CognitionSignal signal) => groups.Any(g => g.Effective && g.Signal == signal);

        public CognitionState(CognitionCatalog catalog)
        {
            if (!catalog) throw new ArgumentNullException(nameof(catalog));
            catalog.ValidateOrThrow();
            vocabulary = catalog.Words.ToDictionary(w => w.Id);
            foreach (var rule in catalog.Rules)
                rules.Add(string.Join(" ", rule.Words.Select(w => w.Id)), (rule.Id, rule.Effect));
            conflicts = catalog.Conflicts.Select(c => (c.First, c.Second)).ToArray();
        }

        public static string[] Tokenize(string text) => (text ?? "").Trim().TrimEnd('.', '。')
            .Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        static string Key(IEnumerable<Word> words) => string.Join(" ", words.Select(w => w.Meaning));
        public bool HasAcquired(string sourceId) => acquisitions.Contains(sourceId);
        public bool TryAcquireWord(string sourceId, WordDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(sourceId) || definition == null) throw new ArgumentException("Missing acquisition data.");
            if (!vocabulary.TryGetValue(definition.Id, out var registered) || registered != definition)
                throw new ArgumentException("Word is not registered in this cognition catalog: " + definition.Id);
            if (!acquisitions.Add(sourceId)) return false;
            NewGroup(new[] { new Word(++nextWord, definition) });
            Commit(CognitionChange.Add);
            return true;
        }
        public bool TryAcquireSentence(string sourceId, string text)
        {
            var tokens = Tokenize(text);
            if (string.IsNullOrWhiteSpace(sourceId) || tokens.Length < 2) throw new ArgumentException("Missing sentence acquisition data.");
            if (acquisitions.Contains(sourceId)) return false;
            // Resolve all definitions before committing: malformed acquisitions must not consume their source.
            var definitions = tokens.Select(t => vocabulary.TryGetValue(t.ToLowerInvariant(), out var word) ? word
                : throw new ArgumentException("Unknown cognition word ID: " + t)).ToArray();
            acquisitions.Add(sourceId);
            NewGroup(definitions.Select(d => new Word(++nextWord, d)));
            Commit(CognitionChange.Add);
            return true;
        }
        Group NewGroup(IEnumerable<Word> words)
        {
            var group = new Group(++nextGroup, words);
            groups.Add(group);
            return group;
        }
        public Group Find(int id) => groups.Find(g => g.Id == id);
        public Group Owning(int wordId) => groups.Find(g => g.words.Any(w => w.Id == wordId));

        // Removing a middle word preserves the two remaining connected runs.
        public Group Detach(int groupId, int wordId)
        {
            var group = Find(groupId);
            if (group == null) return null;
            int index = group.words.FindIndex(w => w.Id == wordId);
            if (index < 0 || group.Independent) return group;
            var word = group.words[index];
            var right = group.words.Skip(index + 1).ToArray();
            group.words.RemoveRange(index, group.words.Count - index);
            if (group.words.Count == 0) groups.Remove(group);
            if (right.Length > 0) NewGroup(right);
            var detached = NewGroup(new[] { word });
            Commit(CognitionChange.Split);
            return detached;
        }
        public void Swap(int groupId, int firstWordId, int secondWordId)
        {
            var group = Find(groupId);
            if (group == null) return;
            int a = group.words.FindIndex(w => w.Id == firstWordId);
            int b = group.words.FindIndex(w => w.Id == secondWordId);
            if (a < 0 || b < 0 || a == b) return;
            var word = group.words[a]; group.words[a] = group.words[b]; group.words[b] = word;
            Commit(CognitionChange.Swap);
        }
        // Joins are whole-group operations. A word still owned by a sentence cannot cross-swap.
        public void Join(int movingGroupId, int targetGroupId, bool prepend)
        {
            var source = Find(movingGroupId); var target = Find(targetGroupId);
            if (source == null || target == null || source == target) return;
            target.words.InsertRange(prepend ? 0 : target.words.Count, source.words);
            groups.Remove(source);
            Commit(CognitionChange.Join);
        }
        void Commit(CognitionChange kind)
        {
            foreach (var group in groups)
            {
                var key = Key(group.words);
                group.Recognized = !group.Independent && rules.ContainsKey(key);
                group.Signal = group.Recognized ? rules[key].Effect : CognitionSignal.Empty;
                group.RuleId = group.Recognized ? rules[key].Id : null;
                group.Conflicted = false;
            }
            // Resolve against all recognized meanings before publishing any world effects.
            foreach (var conflict in conflicts) ResolveConflict(conflict.First, conflict.Second);
            Changed?.Invoke(kind);
        }
        void ResolveConflict(CognitionSignal first, CognitionSignal second)
        {
            if (!groups.Any(g => g.Recognized && g.Signal == first) ||
                !groups.Any(g => g.Recognized && g.Signal == second)) return;
            foreach (var group in groups)
                if (group.Recognized && (group.Signal == first || group.Signal == second)) group.Conflicted = true;
        }
    }
}
