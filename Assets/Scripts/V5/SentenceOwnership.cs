using UnityEngine;
using Space=PuzzleApple.V5.PassageJourney.LightSpace;

namespace PuzzleApple.V5
{
    // A reflected object can retain its source's ownership independently of its physical position.
    public sealed class SentenceOwnership : MonoBehaviour
    {
        public Space space;
        public ChapterRoom room;
        public SentenceOwnership source;
        public Space Space=>source && source!=this?source.Space:room && room.Entry?room.Space:space;
    }
}
