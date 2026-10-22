using UnityEngine;
using PuzzleApple.V4.Cognition;

namespace PuzzleApple.V4
{
    public sealed class DoorSentenceEffects : MonoBehaviour
    {
        public ThresholdDoor door;
        public DoorStateEffects appearance;
        public LightSentenceEffects lights;
        public Collider ground;
        [Min(.1f)] public float pursuitSpeed=3.5f;
        [Min(.1f)] public float arrivalDistance=1.4f;
        public OpeningLight Target { get; private set; }
        CognitionSignal motion;
        Collider[] solids;
        float nextTargetUpdate;
        void Awake() { if(door) solids=door.GetComponentsInChildren<Collider>(true); }
        public void Apply(CognitionState state)
        {
            if(appearance) appearance.SetState(state.HasEffect(CognitionSignal.DoorOn),state.HasEffect(CognitionSignal.DoorOff));
            motion=state.HasEffect(CognitionSignal.DoorMove)?CognitionSignal.DoorMove:
                state.HasEffect(CognitionSignal.DoorWorldMove)?CognitionSignal.DoorWorldMove:
                state.HasEffect(CognitionSignal.DoorLightMove)?CognitionSignal.DoorLightMove:CognitionSignal.Empty;
            RefreshMotion();
        }
        public void RefreshMotion()
        {
            if(door && door.Revealed) door.SetMotion(motion==CognitionSignal.DoorMove,motion==CognitionSignal.DoorWorldMove);
        }
        void FixedUpdate()
        {
            if(!door) return;
            if(ground) door.groundHeight=ground.bounds.max.y+.02f;
            if(!door.Revealed || motion!=CognitionSignal.DoorLightMove || !lights) return;
            if(Time.time>=nextTargetUpdate)
            {
                nextTargetUpdate=Time.time+.25f;
                Target=lights.Nearest(door.transform.position,Target);
            }
            if(!Target || !Target.isActiveAndEnabled) return;
            var body=door.GetComponent<Rigidbody>();
            var destination=Target.transform.position;
            destination.y=Mathf.Max(door.groundHeight,destination.y);
            var delta=destination-body.position;
            float distance=delta.magnitude;
            if(distance<=arrivalDistance) return;
            DirectedMotion.Move(body,solids,delta.normalized*Mathf.Min(pursuitSpeed*Time.fixedDeltaTime,distance-arrivalDistance),false);
        }
    }
}
