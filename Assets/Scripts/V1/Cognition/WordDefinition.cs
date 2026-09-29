using UnityEngine;

namespace PuzzleApple.Cognition
{
    // Semantic identity is independent of the written language and of each draggable token.
    [CreateAssetMenu(menuName = "PuzzleApple/Cognition/Word", fileName = "Word")]
    public sealed class WordDefinition : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayText;
        public string Id => id;
        public string DisplayText => displayText;
    }
}
