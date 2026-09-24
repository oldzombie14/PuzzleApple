using UnityEngine;

namespace PuzzleApple
{
    public enum CognitionHover { Forbidden, Collect, Question }

    public abstract class CognitionInteractable : MonoBehaviour
    {
        public abstract CognitionHover Hover { get; }
        public abstract void Interact(CognitionWorldInteraction interaction);
    }
}
