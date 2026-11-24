using UnityEngine;

namespace PuzzleApple.V5.Cognition
{
    [System.Flags] public enum SentenceWordClass { None = 0, State = 1, Noun = 2, Verb = 4 }
    // Semantic identity is independent of the written language and of each draggable token.
    [CreateAssetMenu(menuName = "PuzzleApple/V5/Cognition/Word", fileName = "Word")]
    public sealed class WordDefinition : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayText;
        [SerializeField] Sprite symbol;
        [SerializeField] SentenceWordClass classes;
        [SerializeField] SentenceProperty property;
        [Tooltip("Optional state-command target. Light switches potential emitters, not every currently luminous object.")]
        [SerializeField] SentenceProperty stateProperty;
        public SentenceProperty Property => property;
        public SentenceProperty StateProperty => stateProperty ? stateProperty : property;
        public SentenceWordClass Classes => classes;
        public Sprite Symbol => symbol;
        public string Id => id;
        public string DisplayText => displayText;
    }
}
