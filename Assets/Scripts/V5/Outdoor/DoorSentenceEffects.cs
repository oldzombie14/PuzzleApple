using UnityEngine;
using PuzzleApple.V5.Cognition;

namespace PuzzleApple.V5.Outdoor
{
    public sealed class DoorSentenceEffects : MonoBehaviour
    {
        public ThresholdDoor door;
        public DoorStateEffects appearance;
        public Collider ground;
        CognitionSignal motion;
        bool stateDriven;
        public void Apply(System.Func<CognitionSignal,bool> hasEffect,bool split=false)
        {
            bool on=hasEffect(CognitionSignal.DoorOn), off=hasEffect(CognitionSignal.DoorOff);
            if(appearance && (on || off)) { appearance.SetState(on,off); stateDriven=true; }
            else if(appearance && stateDriven) { appearance.HoldCurrentState(); stateDriven=false; }
            motion=hasEffect(CognitionSignal.DoorMove)?CognitionSignal.DoorMove:CognitionSignal.Empty;
            RefreshMotion();
        }
        public void ReleaseStateCommand()=>stateDriven=false;
        public void RefreshMotion()
        {
            if(door && door.Revealed) door.SetMotion(motion==CognitionSignal.DoorMove,false);
        }
        void FixedUpdate()
        {
            if(!door) return;
            if(ground) door.groundHeight=ground.bounds.max.y+.02f;
        }
    }
}
