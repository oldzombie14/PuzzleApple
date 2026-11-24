using System.Collections;
using PuzzleApple.Cognition;
using UnityEngine;

namespace PuzzleApple
{
    public sealed class CognitionDoor : CognitionInteractable
    {
        [SerializeField] CognitionBoard board;
        [SerializeField] Transform hinge;
        [SerializeField] float openAngle = -100;
        [SerializeField, Min(.1f)] float duration = 1.2f;
        public const string SourceId = "tutorial.door.word";
        public bool Learned => board && board.State != null && board.State.HasAcquired(SourceId);
        public bool Opened { get; private set; }
        public override CognitionHover Hover => Opened || !board || board.State == null ? CognitionHover.Forbidden
            : !Learned ? CognitionHover.Collect : board.State.CanOpen ? CognitionHover.Question : CognitionHover.Forbidden;
        public override void Interact(CognitionWorldInteraction interaction)
        {
            if (Hover == CognitionHover.Collect) interaction.LearnFromDoor(this);
            else TryOpen();
        }
        public bool TryOpen()
        {
            if (Hover != CognitionHover.Question || !hinge) return false;
            Opened = true;
            StartCoroutine(Open());
            return true;
        }
        IEnumerator Open()
        {
            var start = hinge.localRotation;
            var end = start * Quaternion.Euler(0, openAngle, 0);
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                hinge.localRotation = Quaternion.Slerp(start, end, Mathf.SmoothStep(0, 1, t / duration));
                yield return null;
            }
            hinge.localRotation = end;
        }
    }
}
