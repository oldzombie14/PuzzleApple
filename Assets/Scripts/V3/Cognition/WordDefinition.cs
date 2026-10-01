using UnityEngine;

namespace PuzzleApple.V3.Cognition
{
    public enum WordRole { Reference, Predicate, Modifier, Transform, Relation }
    [System.Serializable] public struct WordSense { public string meaning; public WordRole role; }
    // Semantic identity is independent of the written language and of each draggable token.
    [CreateAssetMenu(menuName = "PuzzleApple/V3/Cognition/Word", fileName = "Word")]
    public sealed class WordDefinition : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayText;
        [SerializeField] Sprite symbol;
        [SerializeField] WordSense[] senses;
        public System.Collections.Generic.IReadOnlyList<WordSense> Senses => senses;
        public Sprite Symbol => symbol;
        public string Id => id;
        public string DisplayText => displayText;
    }
}
