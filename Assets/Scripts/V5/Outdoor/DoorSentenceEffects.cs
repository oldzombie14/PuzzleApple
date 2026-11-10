using UnityEngine;
using PuzzleApple.V5.Cognition;

namespace PuzzleApple.V5.Outdoor
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
        bool stateDriven;
        void Awake() { if(door) solids=door.GetComponentsInChildren<Collider>(true); }
        public void Apply(CognitionState state,bool split=false)
        {
            bool on=state.HasEffect(CognitionSignal.DoorOn), off=state.HasEffect(CognitionSignal.DoorOff);
            if(appearance && (on || off)) { appearance.SetState(on,off); stateDriven=true; }
            else if(appearance && stateDriven) { appearance.HoldCurrentState(); stateDriven=false; }
            var previousMotion=motion;
            motion=state.HasEffect(CognitionSignal.DoorMove)?CognitionSignal.DoorMove:
                state.HasEffect(CognitionSignal.DoorWorldMove)?CognitionSignal.DoorWorldMove:
                state.HasEffect(CognitionSignal.DoorLightMove)?CognitionSignal.DoorLightMove:CognitionSignal.Empty;
            RefreshMotion();
            if(split&&previousMotion==CognitionSignal.DoorMove&&motion==CognitionSignal.Empty&&door)door.SettleAfterFreeMotion();
        }
        public void RefreshMotion()
        {
            if(door && door.Revealed) door.SetMotion(motion==CognitionSignal.DoorMove,motion==CognitionSignal.DoorWorldMove);
        }
        public float TargetClearance(OpeningLight target)
        {
            if(!door||!target)return 0;
            var bounds=new Bounds(door.transform.position,Vector3.zero);
            foreach(var solid in solids)if(solid&&solid.enabled&&!solid.isTrigger)bounds.Encapsulate(solid.bounds);
            var lampBounds=target.GetComponent<Collider>().bounds;
            return Mathf.Max(0,Vector3.Distance(bounds.ClosestPoint(lampBounds.center),lampBounds.center)-lampBounds.extents.magnitude);
        }
        public bool CanPursue(OpeningLight target)=>lights&&lights.CanOperate&&target&&TargetClearance(target)>arrivalDistance;
        void FixedUpdate()
        {
            if(!door) return;
            if(ground) door.groundHeight=ground.bounds.max.y+.02f;
            if(!door.Revealed || motion!=CognitionSignal.DoorLightMove || !lights || !lights.CanOperate) {Target=null;return;}
            if(Time.time>=nextTargetUpdate)
            {
                nextTargetUpdate=Time.time+.25f;
                Target=lights.Nearest(door.transform.position,Target);
            }
            if(!Target || !Target.isActiveAndEnabled || !CanPursue(Target)) return;
            var body=door.GetComponent<Rigidbody>();
            var destination=Target.transform.position;
            destination.y=Mathf.Max(door.groundHeight,destination.y);
            var delta=destination-body.position;
            float distance=delta.magnitude;
            if(distance<=arrivalDistance) return;
            DirectedMotion.Move(body,solids,delta.normalized*Mathf.Min(pursuitSpeed*Time.fixedDeltaTime,Mathf.Min(distance-arrivalDistance,TargetClearance(Target)-arrivalDistance)),false);
        }
    }
}
