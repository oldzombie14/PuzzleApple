using System.Collections.Generic;

namespace PuzzleApple.V5
{
    // Native space identity has no bit count or scene-name dependency.
    public sealed class SentenceScopePool
    {
        readonly List<ProgressLock> boundaries = new List<ProgressLock>();
        readonly Dictionary<SentenceSpace, HashSet<SentenceSpace>> edges = new Dictionary<SentenceSpace, HashSet<SentenceSpace>>();
        HashSet<SentenceSpace> accessible, initial;
        SentenceSpaceSet chapter, previousPlayer, previousMembrane, result;
        int topology = -1;
        public int GraphBuilds { get; private set; }
        public int Resolutions { get; private set; }

        public SentenceSpaceSet Resolve(SentenceSpaceSet chapterSpaces, SentenceSpaceSet playerSpace, SentenceSpaceSet membraneRegion)
        {
            int revision = ProgressLock.TopologyVersion;
            if (topology != revision || !chapterSpaces.SetEquals(chapter))
            {
                chapter = chapterSpaces; topology = revision;
                BuildGraph(); result = null;
            }
            if (result != null && playerSpace.SetEquals(previousPlayer) && membraneRegion.SetEquals(previousMembrane)) return result;
            var reached = new HashSet<SentenceSpace>(playerSpace);
            reached.IntersectWith(accessible);
            if (reached.Count == 0) reached.UnionWith(initial);
            var pending = new Queue<SentenceSpace>(reached);
            while (pending.Count > 0)
            {
                if (!edges.TryGetValue(pending.Dequeue(), out var adjacent)) continue;
                foreach (var next in adjacent) if (reached.Add(next)) pending.Enqueue(next);
            }
            reached.IntersectWith(membraneRegion);
            result = new SentenceSpaceSet(reached);
            previousPlayer = playerSpace; previousMembrane = membraneRegion; Resolutions++;
            return result;
        }

        void BuildGraph()
        {
            GraphBuilds++;
            ProgressLock.CopyActiveTo(boundaries);
            initial = new HashSet<SentenceSpace>(chapter);
            foreach (var boundary in boundaries) initial.ExceptWith(boundary.UnlockedRegion);
            accessible = new HashSet<SentenceSpace>(initial);
            int before;
            do
            {
                before = accessible.Count;
                foreach (var boundary in boundaries)
                    if (boundary.Unlocked && accessible.IsSupersetOf(boundary.RequiredRegion))
                        foreach (var space in boundary.UnlockedRegion) if (chapter.Contains(space)) accessible.Add(space);
            } while (before != accessible.Count);
            edges.Clear();
            Join(initial);
            foreach (var boundary in boundaries)
            {
                Join(boundary.UnlockedRegion);
                // An empty/foreign locked side is not a bridge between prerequisite regions.
                if (boundary.Connected && accessible.Overlaps(boundary.UnlockedRegion)) Join(boundary.BoundaryRegion);
            }
        }

        void Join(IEnumerable<SentenceSpace> members)
        {
            var region = new HashSet<SentenceSpace>(members);
            region.IntersectWith(accessible);
            foreach (var space in region)
            {
                if (!edges.TryGetValue(space, out var adjacent)) edges.Add(space, adjacent = new HashSet<SentenceSpace>());
                adjacent.UnionWith(region);
            }
        }
    }
}
