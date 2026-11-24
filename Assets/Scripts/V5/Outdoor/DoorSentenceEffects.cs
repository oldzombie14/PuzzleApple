using UnityEngine;
using PuzzleApple.V5.Cognition;

namespace PuzzleApple.V5.Outdoor
{
    public sealed class DoorSentenceEffects : MonoBehaviour
    {
        public ThresholdDoor door;
        public DoorStateEffects appearance;
        public Collider ground;
        bool moving;
        public SentenceMotionOutput MotionOutput {get;internal set;}
        public void SetState(bool on){if(appearance)appearance.SetState(on,!on);}
        public void HoldState(){if(appearance)appearance.HoldCurrentState();}
        public void ApplyMotion(System.Func<SentenceMotion,bool> hasMotion)
        {
            moving = hasMotion(SentenceMotion.Door);
            RefreshMotion();
        }
        public void RefreshMotion()
        {
            if(door && door.Revealed) door.SetMotion(moving,false);
        }
        void FixedUpdate()
        {
            if(!door) return;
            if(ground) door.groundHeight=ground.bounds.max.y+.02f;
        }
    }
}
