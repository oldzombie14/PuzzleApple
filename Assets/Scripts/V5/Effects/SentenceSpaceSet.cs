using System.Collections;
using System.Collections.Generic;

namespace PuzzleApple.V5
{
    // Immutable membership, shared by scope caches and command/ownership snapshots.
    public sealed class SentenceSpaceSet : IReadOnlyCollection<SentenceSpace>
    {
        readonly HashSet<SentenceSpace> spaces;
        public static readonly SentenceSpaceSet Empty = new SentenceSpaceSet(new SentenceSpace[0]);
        public int Count => spaces.Count;
        public SentenceSpaceSet(IEnumerable<SentenceSpace> source)
        {
            spaces = new HashSet<SentenceSpace>();
            if (source != null) foreach (var space in source) if (space) spaces.Add(space);
        }
        public bool Contains(SentenceSpace space) => spaces.Contains(space);
        public bool Overlaps(SentenceSpaceSet other) => other != null && spaces.Overlaps(other.spaces);
        public bool SetEquals(SentenceSpaceSet other) => ReferenceEquals(this, other) || other != null && spaces.SetEquals(other.spaces);
        public SentenceSpaceSet Intersect(SentenceSpaceSet other)
        {
            var result = new HashSet<SentenceSpace>(spaces);
            result.IntersectWith(other.spaces);
            return new SentenceSpaceSet(result);
        }
        public IEnumerator<SentenceSpace> GetEnumerator() => spaces.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
