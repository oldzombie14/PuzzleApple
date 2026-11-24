using System.Collections.Generic;
using UnityEngine;

namespace PuzzleApple.V3.Cognition
{
    // Effects name the existing world handlers, not the supported sentence combinations.
    public enum CognitionSignal { Empty, AppleMove, AppleStop, PlayerMove, PlayerNoMove, MirrorMove, MirrorStop,
        DoorPositive, DoorNegative, MirrorApple, MirrorMirror, MirrorSelf, EssenceApple, EssenceMirror, EssenceSelf, EqualApple,
        OpeningMove, OpeningMirror, OpeningReset, OpeningTransform, AppleNegative, EqualBalance }
    public enum TriggerMode { Direct, Interaction }

    [CreateAssetMenu(menuName = "PuzzleApple/V3/Cognition/Rule", fileName = "Rule")]
    public sealed class CognitionRule : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] WordDefinition[] words = new WordDefinition[0];
        [Tooltip("World behavior enabled while this rule is effective. Interaction effects only grant permission; completed world facts are owned by the world objects.")]
        [SerializeField] CognitionSignal effect;
        [SerializeField] TriggerMode trigger;
        [Tooltip("Optional exclusive action channel. Earlier established rules in the same channel win; identical rules share the effect.")]
        [SerializeField] string exclusionKey;
        public string ExclusionKey => exclusionKey;
        public TriggerMode Trigger => trigger;
        public string Id => id;
        public IReadOnlyList<WordDefinition> Words => words;
        public CognitionSignal Effect => effect;
    }
}
