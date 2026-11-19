using UnityEngine;

namespace PuzzleApple.V5
{
    [CreateAssetMenu(menuName="Puzzle Apple/Chapter layout")]
    public sealed class ChapterLayoutSettings : ScriptableObject
    {
        [Range(.65f,1)] public float uniformScale=.8f;
        [Min(.4f)] public float sillHeight=1.05f;
        [Min(1)] public float walkingMultiplier=1.3f;
        [Min(.1f)] public float speedTransition=.4f;
    }
}
