using System.Collections.Generic;
using UnityEngine;

namespace PuzzleApple.V5.Cognition
{
    // Effects name the existing world handlers, not the supported sentence combinations.
    public enum CognitionSignal { Empty = 0, WorldOn = 1, WorldOff = 7, LightOn = 8, LightOff = 9, DoorOn = 10, LightMove = 11, LightFloorMove = 12, DoorOff = 13, LightWorldMove = LightFloorMove, LightDoorMove = 14, LightLightMove = 15, DoorMove = 16, DoorWorldMove = 17, DoorLightMove = 18, WorldMove = 19, WorldLightMove = 20, WorldDoorMove = 21, IlluminateWorld = 22, IlluminateDoor = 23, IlluminateFloor = 24, IlluminateLight = 25 }
    public enum TriggerMode { Direct, Interaction }

    [CreateAssetMenu(menuName = "PuzzleApple/V5/Cognition/Rule", fileName = "Rule")]
    public sealed class CognitionRule : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] WordDefinition[] words = new WordDefinition[0];
        [Tooltip("World behavior enabled while this rule is effective. Interaction effects only grant permission; completed world facts are owned by the world objects.")]
        [SerializeField] CognitionSignal effect;
        [SerializeField] CognitionSignal[] additionalEffects = new CognitionSignal[0];
        [SerializeField] TriggerMode trigger;
        [Tooltip("Optional exclusive action channel. Earlier established rules in the same channel win; identical rules share the effect.")]
        [SerializeField] string exclusionKey;
        public string ExclusionKey => exclusionKey;
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
        public static string Channel(CognitionSignal signal)
        {
            switch(signal)
            {
                case CognitionSignal.LightOn: case CognitionSignal.LightOff: return "light.state";
                case CognitionSignal.DoorOn: case CognitionSignal.DoorOff: return "door.state";
                case CognitionSignal.LightMove: case CognitionSignal.LightFloorMove:
                case CognitionSignal.LightDoorMove: case CognitionSignal.LightLightMove: return "light.motion";
                case CognitionSignal.DoorMove: case CognitionSignal.DoorWorldMove: case CognitionSignal.DoorLightMove: return "door.motion";
                case CognitionSignal.WorldMove: case CognitionSignal.WorldLightMove: case CognitionSignal.WorldDoorMove: return "world.motion";
                default: return null;
            }
        }
    }
}


