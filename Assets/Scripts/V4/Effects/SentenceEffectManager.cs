using UnityEngine;
using PuzzleApple.V4.Cognition;

namespace PuzzleApple.V4
{
    [DefaultExecutionOrder(310)]
    public sealed class SentenceEffectManager : MonoBehaviour
    {
        public CognitionBoard board;
        public LightSentenceEffects lights;
        public DoorSentenceEffects door;
        public WorldSentenceEffects world;
        public IntroPresentation presentation;
        public bool Suspended { get; private set; }
        void Start()
        {
            board.State.Changed+=Apply;
            Apply(CognitionChange.Add);
        }
        public void Suspend()
        {
            Suspended=true;
            if(presentation)presentation.Cancel();
            // A separate empty snapshot restores defaults and stops motion without discarding learned words.
            var empty=new CognitionState(board.Catalog);
            lights.Apply(empty); door.Apply(empty); world.Apply(empty);
        }
        void Apply(CognitionChange change)
        {
            if(board.State.HasEffect(CognitionSignal.WorldOff)) { WorldEntry.QuitApplication(); return; }
            if(Suspended) return;
            if(presentation) presentation.Apply(change,ApplyWorld);
            else ApplyWorld();
        }
        void ApplyWorld(){lights.Apply(board.State);door.Apply(board.State);world.Apply(board.State);}
        public void DoorRevealed() { if(!Suspended && door) door.RefreshMotion(); }
        void OnDestroy() { if(board && board.State!=null) board.State.Changed-=Apply; }
    }
}
