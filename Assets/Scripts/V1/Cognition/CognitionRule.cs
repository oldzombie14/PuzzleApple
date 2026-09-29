using System.Collections.Generic;
using UnityEngine;

namespace PuzzleApple.Cognition
{
    // Effects name the existing world handlers, not the supported sentence combinations.
    public enum CognitionSignal { Empty, AppleMove, ConsumeApple, Open, PlayerMove, PlayerNoMove }

    [CreateAssetMenu(menuName = "PuzzleApple/Cognition/Rule", fileName = "Rule")]
    public sealed class CognitionRule : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] WordDefinition[] words = new WordDefinition[0];
        [Tooltip("World behavior enabled while this rule is effective. Interaction effects only grant permission; completed world facts are owned by the world objects.")]
        [SerializeField] CognitionSignal effect;
        public string Id => id;
        public IReadOnlyList<WordDefinition> Words => words;
        public CognitionSignal Effect => effect;
    }
}
