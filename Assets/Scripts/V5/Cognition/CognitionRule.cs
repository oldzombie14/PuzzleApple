using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace PuzzleApple.V5.Cognition
{
    // Effects name the existing world handlers, not the supported sentence combinations.
    public enum CognitionSignal { Empty = 0, WorldOn = 1, WorldOff = 7, LightOn = 8, LightOff = 9, DoorOn = 10, LightMove = 11, LightFloorMove = 12, DoorOff = 13, LightWorldMove = LightFloorMove, LightDoorMove = 14, LightLightMove = 15, DoorMove = 16, DoorWorldMove = 17, DoorLightMove = 18, WorldMove = 19, WorldLightMove = 20, WorldDoorMove = 21, IlluminateWorld = 22, IlluminateDoor = 23, IlluminateFloor = 24, FreezeMotion = 25, IlluminateLight = 26 }
    public enum TriggerMode { Direct, Interaction }
    // Sustained: holds while the sentence stays effective. Command: applies once, then the sentence retires.
    // WorldFlow: opens or closes the world (WorldOn/WorldOff) through WorldEntry.
    public enum SentenceKind { Sustained = 0, Command = 1, WorldFlow = 2 }

    [CreateAssetMenu(menuName = "PuzzleApple/V5/Cognition/Rule", fileName = "Rule")]
    public sealed class CognitionRule : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] WordDefinition[] words = new WordDefinition[0];
        [Tooltip("World behavior enabled while this rule is effective. Interaction effects only grant permission; completed world facts are owned by the world objects.")]
        [SerializeField] CognitionSignal effect;
        [SerializeField] CognitionSignal[] additionalEffects = new CognitionSignal[0];
        [SerializeField] TriggerMode trigger;
        [Tooltip("How the runtime schedules this sentence: sustained effect, one-shot command, or world open/close flow.")]
        [SerializeField] SentenceKind kind;
        [Tooltip("Optional exclusive action channel (e.g. light.state, door.motion). Earlier established rules in the same channel win; identical rules share the effect. Channels ending in .motion pause under FreezeMotion.")]
        [FormerlySerializedAs("exclusionKey")]
        [SerializeField] string channel;
        public string Channel => channel;
        public SentenceKind Kind => kind;
        public bool Motion => !string.IsNullOrEmpty(channel) && channel.EndsWith(".motion");
        public TriggerMode Trigger => trigger;
        public string Id => id;
        public IReadOnlyList<WordDefinition> Words => words;
        public CognitionSignal Effect => effect;
        public IEnumerable<CognitionSignal> Effects
        {
            get
            {
                if (effect != CognitionSignal.Empty) yield return effect;
                foreach (var signal in additionalEffects)
                    if (signal != CognitionSignal.Empty && signal != effect) yield return signal;
            }
        }
    }
}
