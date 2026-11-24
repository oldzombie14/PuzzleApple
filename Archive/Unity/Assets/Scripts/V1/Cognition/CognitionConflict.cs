using UnityEngine;

namespace PuzzleApple.Cognition
{
    [CreateAssetMenu(menuName = "PuzzleApple/Cognition/Conflict", fileName = "Conflict")]
    public sealed class CognitionConflict : ScriptableObject
    {
        [SerializeField] string id;
        [Tooltip("Effects currently identify their subject as well as behavior (PlayerMove differs from AppleMove). All supporting sentences on both sides become ineffective.")]
        [SerializeField] CognitionSignal first;
        [SerializeField] CognitionSignal second;
        public string Id => id;
        public CognitionSignal First => first;
        public CognitionSignal Second => second;
    }
}
