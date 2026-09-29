using UnityEngine;

namespace PuzzleApple.V2.Cognition
{
    // Semantic identity is independent of the written language and of each draggable token.
    [CreateAssetMenu(menuName = "PuzzleApple/V2/Cognition/Word", fileName = "Word")]
    public sealed class WordDefinition : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayText;
        [SerializeField] Sprite symbol;
        public Sprite Symbol => symbol;
        public string Id => id;
        public string DisplayText => displayText;
    }
}
