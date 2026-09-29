using PuzzleApple.Cognition;
using UnityEngine;

namespace PuzzleApple
{
    public sealed class CognitionKey : CognitionInteractable
    {
        [SerializeField] CognitionBoard board;
        [SerializeField] Transform interactionFocus;
        [SerializeField] Vector3 appleLocalPosition;
        [SerializeField] Vector3 appleLocalEulerAngles;
        public const string SourceId = "tutorial.apple-key.open";
        public bool Revealed { get; private set; }
        public bool Learned => board && board.State.HasAcquired(SourceId);
        public override CognitionHover Hover => Revealed && !Learned ? CognitionHover.Question : CognitionHover.Forbidden;
        public Vector3 FocusPoint => interactionFocus ? interactionFocus.position : transform.position;
        public void Reveal(Transform bittenApple)
        {
            transform.SetParent(bittenApple, false);
            transform.localPosition = appleLocalPosition;
            transform.localRotation = Quaternion.Euler(appleLocalEulerAngles);
            Revealed = true; gameObject.SetActive(true);
        }
        public override void Interact(CognitionWorldInteraction interaction)
        {
            if (Hover == CognitionHover.Question) interaction.LearnFromKey(this);
        }
    }
}
