using System;
using System.Collections.Generic;
using System.Linq;

namespace PuzzleApple.V5.Cognition
{
    public enum CognitionChange { Add, Layout, Join, Split, Swap, Situation }

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
            public int RejectedAttempts {get; internal set;}
            public bool Spoken { get; internal set; }
            public bool ExecutionLocked { get; internal set; }
            internal bool FirstSuccessfulAttempt { get; set; }
            internal bool AwaitingRetry { get; set; }
            internal bool OneShot => Intent?.Kind == SentenceKind.Command || Intent?.Kind == SentenceKind.WorldFlow;
            public bool Effective => Recognized && Spoken && !Conflicted;
            public CognitionSignal Signal { get; internal set; }
            public IReadOnlyList<CognitionSignal> Signals { get; internal set; } = Array.Empty<CognitionSignal>();
            public string RuleId { get; internal set; }
            public CognitionRule Rule { get; internal set; }
            public SentenceIntent Intent { get; internal set; }
            public long Established { get; internal set; }
            internal Group(int id, IEnumerable<Word> source) { Id = id; words = source.ToList(); }
        }

        readonly List<Group> groups = new List<Group>();
        readonly SentenceGrammarResolver grammar;
        readonly Dictionary<string, WordDefinition> vocabulary;
        readonly Dictionary<string, string> channels;
        readonly (CognitionSignal First, CognitionSignal Second, bool Earliest)[] conflicts;
        readonly HashSet<string> learned = new HashSet<string>();
        long chronology;
        readonly HashSet<string> acquisitions = new HashSet<string>();
        readonly Dictionary<string, string[]> acquisitionWords = new Dictionary<string, string[]>();
        int nextWord, nextGroup;
        public IReadOnlyList<Group> Groups => groups;
        public sealed class HistoryEntry
        {
            public WordDefinition[] Words { get; internal set; }
            public bool Effective { get; internal set; }
        }
        readonly List<HistoryEntry> history = new List<HistoryEntry>();
        readonly Dictionary<int, string> lastEvaluations = new Dictionary<int, string>();
        public IReadOnlyList<HistoryEntry> History => history;
        public int HistoryVersion { get; private set; }
        public event Action<CognitionChange> Changed;
        public event Action<int> CommandBlocked;
        internal Func<Group,IReadOnlyList<Group>,Group> MotionBlocker {get;set;}
        internal Func<Group,bool> CanStartCommand {get;set;}
        public bool JudgementLocked => RequirePeriod && groups.Any(g=>g.ExecutionLocked);
        public bool CanPrompt(Group group)
        {
            return RequirePeriod && group != null && !group.Spoken && group.Recognized && !JudgementLocked;
        }
        bool CanStart(Group group) => !group.OneShot || CanStartCommand == null || CanStartCommand(group);
        public bool HasEffect(CognitionSignal signal) => groups.Any(g => g.Effective && g.Signals.Contains(signal));
        public bool Knows(string id) => learned.Contains(id);

        public bool RequirePeriod {get;private set;}
        public void SetPeriodMode(bool enabled){
            if(RequirePeriod==enabled)return;
            RequirePeriod=enabled;
            foreach(var group in groups)group.RejectedAttempts=0;
            Commit(CognitionChange.Join);
        }
        public CognitionState(CognitionCatalog catalog,bool requirePeriod=false)
        {
            if (!catalog) throw new ArgumentNullException(nameof(catalog));
            RequirePeriod=requirePeriod;
            catalog.ValidateOrThrow();
            vocabulary = catalog.Words.ToDictionary(w => w.Id);
            channels = catalog.Rules.ToDictionary(r => r.Id, r => r.Channel);
            grammar = new SentenceGrammarResolver(catalog);
            conflicts = catalog.Conflicts.Select(c => (c.First, c.Second, c.EarliestWins)).ToArray();
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
            acquisitionWords[sourceId] = new[] { definition.Id };
            if (!learned.Add(definition.Id)) return false;
            // Acquisitions enter the permanent left-hand vocabulary; tokens are
            // created only when the player drags a word into the workspace.
            Commit(CognitionChange.Add);
            return true;
        }
        public bool TryAcquireSentence(string sourceId, string text)=>TryAcquireSentence(sourceId,text,out _);
        public bool TryAcquireSentence(string sourceId, string text,out Group acquired)
        {
            acquired=null;
            var tokens = Tokenize(text);
            if (string.IsNullOrWhiteSpace(sourceId) || tokens.Length < 2) throw new ArgumentException("Missing sentence acquisition data.");
            if (acquisitions.Contains(sourceId)) return false;
            // Resolve all definitions before committing: malformed acquisitions must not consume their source.
            var definitions = tokens.Select(t => vocabulary.TryGetValue(t.ToLowerInvariant(), out var word) ? word
                : throw new ArgumentException("Unknown cognition word ID: " + t)).ToArray();
            acquisitions.Add(sourceId);
            acquisitionWords[sourceId] = definitions.Select(d => d.Id).ToArray();
            foreach (var d in definitions) learned.Add(d.Id);
            acquired=NewGroup(definitions.Select(d => new Word(++nextWord, d)));
            acquired.Spoken=!JudgementLocked;
            if(acquired.Spoken && RequirePeriod && !grammar.Resolve(definitions).Recognized)acquired.RejectedAttempts=1;
            Commit(CognitionChange.Add,acquired.Spoken ? acquired : null);
            return true;
        }
        Group NewGroup(IEnumerable<Word> words)
        {
            var group = new Group(++nextGroup, words);
            groups.Add(group);
            return group;
        }
        public Group Find(int id) => groups.Find(g => g.Id == id);
        public Group Spawn(WordDefinition word)
        {
            if (!word || !learned.Contains(word.Id)) return null;
            var group = NewGroup(new[] { new Word(++nextWord, word) });
            Commit(CognitionChange.Add); return group;
        }
        public bool Speak(int groupId)
        {
            var g=Find(groupId);
            if(!RequirePeriod || JudgementLocked || g==null || g.Independent || g.Spoken)return false;
            if(!g.Recognized)g.RejectedAttempts++;
            else g.RejectedAttempts=0;
            g.Spoken=true;
            Commit(CognitionChange.Join,g);
            return g.Spoken;
        }
        public bool Unsay(int groupId)
        {
            var g=Find(groupId);
            if(!RequirePeriod || g==null || !g.Spoken || g.ExecutionLocked)return false;
            g.Spoken=false;
            g.Conflicted=false;
            g.RejectedAttempts=0;
            Commit(CognitionChange.Split);
            return true;
        }
        void Edit(Group group)
        {
            if(!RequirePeriod)return;
            group.Spoken=false;
            group.Conflicted=false;
            group.AwaitingRetry=false;
            group.RejectedAttempts=0;
        }
        public void ReturnToLibrary(int groupId, int wordId, bool whole)
        {
            var group = Find(groupId); if (group == null || group.ExecutionLocked) return;
            if (whole || group.Independent) groups.Remove(group);
            else
            {
                int index = group.words.FindIndex(w => w.Id == wordId); if (index < 0) return;
                Edit(group);
                var right = group.words.Skip(index + 1).ToArray();
                group.words.RemoveRange(index, group.words.Count - index);
                if (group.words.Count == 0) groups.Remove(group);
                if (right.Length > 0) NewGroup(right);
            }
            Commit(CognitionChange.Split);
        }
        public void ClearWorkspace()
        {
            if (groups.RemoveAll(g => !g.ExecutionLocked) > 0) Commit(CognitionChange.Split);
        }
        internal void ClearForChapterTransition()
        {
            if (groups.Count == 0) return;
            groups.Clear();
            Commit(CognitionChange.Split);
        }
        internal void RejectBeforeExecution(Group group)
        {
            if(!group.ExecutionLocked || !groups.Contains(group))return;
            group.ExecutionLocked=false;
            group.Spoken=false;
            group.AwaitingRetry=true;
            if(group.FirstSuccessfulAttempt)
                history.RemoveAll(h=>h.Effective && h.Words.SequenceEqual(group.words.Select(w=>w.Definition)));
            RecordAttempt(group);
            Commit(CognitionChange.Situation);
            CommandBlocked?.Invoke(group.Id);
        }
        internal void CompleteExecution(Group group, long established)
        {
            if (group.Established == established && groups.Remove(group)) Commit(CognitionChange.Split);
        }
        // Remove affected sentences as a whole: never accidentally form a new rule
        // by concatenating the words on either side of an expired word.
        public void ForgetWords(IEnumerable<string> ids)
        {
            var expired = new HashSet<string>(ids ?? Array.Empty<string>());
            if (expired.Count == 0) return;
            groups.RemoveAll(g => g.words.Any(w => expired.Contains(w.Meaning)));
            learned.ExceptWith(expired);
            // Acquisition IDs are tracked per word so revisiting a scene can collect it again.
            foreach (var source in acquisitionWords.Where(p => p.Value.Any(expired.Contains)).Select(p => p.Key).ToArray())
            { acquisitions.Remove(source); acquisitionWords.Remove(source); }
            Commit(CognitionChange.Split);
        }
        public Group Owning(int wordId) => groups.Find(g => g.words.Any(w => w.Id == wordId));

        // Removing a middle word preserves the two remaining connected runs.
        public Group Detach(int groupId, int wordId)
        {
            var group = Find(groupId);
            if (group == null || group.ExecutionLocked) return null;
            int index = group.words.FindIndex(w => w.Id == wordId);
            if (index < 0 || group.Independent) return group;
            Edit(group);
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
            if (group == null || group.ExecutionLocked) return;
            int a = group.words.FindIndex(w => w.Id == firstWordId);
            int b = group.words.FindIndex(w => w.Id == secondWordId);
            if (a < 0 || b < 0 || a == b) return;
            Edit(group);
            var word = group.words[a]; group.words[a] = group.words[b]; group.words[b] = word;
            Commit(CognitionChange.Swap);
        }
        // Joins are whole-group operations. A word still owned by a sentence cannot cross-swap.
        public void Join(int movingGroupId, int targetGroupId, bool prepend)
        {
            var source = Find(movingGroupId); var target = Find(targetGroupId);
            if (source == null || target == null || source == target || source.ExecutionLocked || target.ExecutionLocked) return;
            Edit(target);
            target.words.InsertRange(prepend ? 0 : target.words.Count, source.words);
            groups.Remove(source);
            Commit(CognitionChange.Join);
        }
        void Commit(CognitionChange kind,Group attempt=null)
        {
            foreach (var group in groups)
            {
                var parsed = grammar.Resolve(group.words.Select(w => w.Definition).ToArray());
                string previousRule = group.RuleId;
                group.Recognized = parsed.Recognized;
                group.Signal = parsed.Signal;
                group.Signals = group.Recognized ? parsed.Effects : Array.Empty<CognitionSignal>();
                group.Rule = parsed.Rule;
                group.RuleId = group.Recognized ? parsed.Rule.Id : null;
                group.Intent = group.Recognized ? parsed.Intent : null;
                if(!RequirePeriod)group.Spoken=group.Recognized;
                if(!group.Recognized || !group.Spoken)group.Established=0;
                else if(group.RuleId!=previousRule || group.Established==0)group.Established=++chronology;
            }
            bool attemptBlocked=ResolveConflicts(attempt);
            if(RequirePeriod){
                if(attempt!=null && !attempt.Independent)RecordAttempt(attempt);
                Changed?.Invoke(kind);
                if (attemptBlocked) CommandBlocked?.Invoke(attempt.Id);
                return;
            }
            foreach (var id in lastEvaluations.Keys.Where(id => Find(id) == null).ToArray()) lastEvaluations.Remove(id);
            foreach (var group in groups)
            {
                if (group.Independent) { lastEvaluations.Remove(group.Id); continue; }
                string evaluation = Key(group.words) + ":" + group.Effective;
                if (lastEvaluations.TryGetValue(group.Id, out var previous) && previous == evaluation) continue;
                lastEvaluations[group.Id] = evaluation;
                // Keep one entry per sentence/result, newest first; splitting never erases history.
                RecordAttempt(group);
            }
            Changed?.Invoke(kind);
        }
        bool UsesSceneMotion(Group group) => RequirePeriod && MotionBlocker!=null && group.Intent?.Action==SentenceAction.Move;

        bool ResolveConflicts(Group attempt)
        {
            if (RequirePeriod)
            {
                var owners = new List<Group>();
                bool rejected = false;
                foreach (var group in groups.Where(g=>g.Recognized && g.Spoken).OrderBy(g=>g.Established))
                {
                    if (!group.ExecutionLocked && (FindBlocker(group,owners)!=null || (group==attempt && !CanStart(group))))
                    {
                        if(group.OneShot){
                            group.Spoken=false;
                            group.Established=0;
                            group.AwaitingRetry=true;
                            rejected |= group==attempt;
                        }else{
                            group.Conflicted=true;
                        }
                        continue;
                    }
                    group.AwaitingRetry=false;
                    group.Conflicted=false;
                    group.ExecutionLocked=group.OneShot;
                    owners.Add(group);
                }
                return rejected;
            }
            foreach(var group in groups)group.Conflicted=false;
            var accepted = new List<Group>();
            foreach (var conflict in conflicts.Where(c => !c.Earliest))
            {
                if (!groups.Any(g => g.Recognized && g.Spoken && g.Signals.Contains(conflict.First)) ||
                    !groups.Any(g => g.Recognized && g.Spoken && g.Signals.Contains(conflict.Second))) continue;
                foreach (var g in groups.Where(g => g.Recognized && g.Spoken))
                    if (!g.ExecutionLocked && (g.Signals.Contains(conflict.First) || g.Signals.Contains(conflict.Second))) g.Conflicted = true;
            }
            foreach(var group in groups.Where(g=>g.Recognized && g.Spoken && !g.Conflicted).OrderBy(g=>g.Established))
            {
                if (accepted.Any(other=>ConflictsWith(group,other))) group.Conflicted=true;
                else accepted.Add(group);
            }
            return false;
        }

        Group FindBlocker(Group group,IReadOnlyList<Group> accepted)
        {
            bool sceneMotion=UsesSceneMotion(group);
            var blocker=accepted.FirstOrDefault(other=>(!sceneMotion || !UsesSceneMotion(other)) && ConflictsWith(group,other));
            return blocker ?? (sceneMotion ? MotionBlocker(group,accepted) : null);
        }

        internal void ReevaluateMotionConflicts()
        {
            if(MotionBlocker==null || !RequirePeriod || groups.Count(g=>g.Spoken && UsesSceneMotion(g))<2)return;
            var before=groups.Select(g=>(g.Conflicted,g.Spoken,g.ExecutionLocked)).ToArray();
            ResolveConflicts(null);
            if(!before.SequenceEqual(groups.Select(g=>(g.Conflicted,g.Spoken,g.ExecutionLocked))))
                Changed?.Invoke(CognitionChange.Situation);
        }

        void RecordAttempt(Group group)
        {
            var words=group.words.Select(w=>w.Definition).ToArray();
            int removed=history.RemoveAll(h=>h.Effective==group.Effective && h.Words.SequenceEqual(words));
            group.FirstSuccessfulAttempt=group.ExecutionLocked && removed==0;
            history.Insert(0,new HistoryEntry{Words=words,Effective=group.Effective});
            HistoryVersion++;
        }
        bool ConflictsWith(Group a, Group b)
        {
            foreach (var x in a.Signals) foreach (var y in b.Signals)
                if (conflicts.Any(c => (c.First == x && c.Second == y) || (c.First == y && c.Second == x))) return true;
            // Rules in one channel conflict unless they apply the same effects (CognitionRule.Channel).
            string channel = channels[a.RuleId];
            return !string.IsNullOrEmpty(channel) && channel == channels[b.RuleId] &&
                !a.Signals.OrderBy(s => s).SequenceEqual(b.Signals.OrderBy(s => s));
        }
    }
}
