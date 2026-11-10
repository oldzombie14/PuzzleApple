using System.Linq;
using UnityEngine;

namespace PuzzleApple.V5.Cognition
{
    [CreateAssetMenu(menuName="PuzzleApple/V5/Cognition/Observation lesson")]
    public sealed class ObservationLesson : ScriptableObject
    {
        public string acquisitionId="observe.light.move";
        public WordDefinition verb;
        public CognitionRule example;
        [Min(.1f)] public float holdDuration=2;
        public string Sentence=>string.Join(" ",example.Words.Select(w=>w.Id));
    }
}
