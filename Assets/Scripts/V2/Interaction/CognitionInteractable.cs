using UnityEngine;

namespace PuzzleApple.V2
{
    public enum CognitionHover { Forbidden, Collect, Question }

    public abstract class CognitionInteractable : MonoBehaviour
    {
        public abstract CognitionHover Hover { get; }
        public abstract void Interact(CognitionWorldInteraction interaction);
    }
}
