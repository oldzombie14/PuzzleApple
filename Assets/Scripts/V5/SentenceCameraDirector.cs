using PuzzleApple.V5.Cognition;
using UnityEngine;

namespace PuzzleApple.V5
{
    public static class SentenceCameraDirector
    {
        // Stable registration order breaks ties. Selection never changes the effect's scope.
        // Resolve at the end of feedback, so removed or no-longer-changing targets are skipped.
        public static Vector3? Resolve(SentenceEntityRegistry registry, CognitionState.Group group,
            SentenceSpaceSet currentSpace, SentenceSpaceSet effectScope)
        {
            if (group == null) return null;
            foreach (var source in registry.Query<SentenceFocusSource>(currentSpace.Intersect(effectScope)))
            {
                var point = source.Request(group);
                if (point.HasValue) return point;
            }
            return null;
        }
    }
}
