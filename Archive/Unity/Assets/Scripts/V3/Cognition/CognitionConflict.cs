using UnityEngine;

namespace PuzzleApple.V3.Cognition
{
    [CreateAssetMenu(menuName = "PuzzleApple/V3/Cognition/Conflict", fileName = "Conflict")]
    public sealed class CognitionConflict : ScriptableObject
    {
        [SerializeField] string id;
        [Tooltip("Default: both sides are ineffective. Earliest wins is explicit per conflict, not global.")]
        [SerializeField] CognitionSignal first;
        [SerializeField] CognitionSignal second;
        [SerializeField] bool earliestWins;
        public bool EarliestWins => earliestWins;
        public string Id => id;
        public CognitionSignal First => first;
        public CognitionSignal Second => second;
    }
}
