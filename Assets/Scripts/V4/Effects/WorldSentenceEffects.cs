using UnityEngine;
using PuzzleApple.V4.Cognition;

namespace PuzzleApple.V4
{
    public sealed class WorldSentenceEffects : MonoBehaviour
    {
        public NightEnvironment environment;
        public Collider ground;
        public FirstPersonController player;
        public ThresholdDoor door;
        public LightSentenceEffects lights;
        [Min(.1f)] public float riseSpeed=2.5f;
        [Min(.02f)] public float recheckInterval=.15f;
        public bool Waiting { get; private set; }
        CognitionSignal motion;
        float nextCheck, targetHeight;
        readonly RaycastHit[] hits=new RaycastHit[64];
        public void Apply(CognitionState state)
        {
            motion=state.HasEffect(CognitionSignal.WorldLightMove)?CognitionSignal.WorldLightMove:
                state.HasEffect(CognitionSignal.WorldDoorMove)?CognitionSignal.WorldDoorMove:CognitionSignal.Empty;
            if(environment) environment.WorldMoving=state.HasEffect(CognitionSignal.WorldMove);
            nextCheck=0; Waiting=false;
        }
        void FixedUpdate()
        {
            if(!ground || !player || motion==CognitionSignal.Empty) return;
            if(Waiting && Time.time<nextCheck) return;
            if(Time.time>=nextCheck)
            {
                nextCheck=Time.time+recheckInterval;
                targetHeight=motion==CognitionSignal.WorldLightMove && lights?lights.LowestBottom():
                    motion==CognitionSignal.WorldDoorMove && door && door.Revealed?door.transform.position.y:float.PositiveInfinity;
            }
            float top=ground.bounds.max.y;
            // No reachable reference means wait; never launch the ground into unbounded space.
            float travel=float.IsInfinity(targetHeight)?0:Mathf.Min(riseSpeed*Time.fixedDeltaTime,Mathf.Max(0,targetHeight-top-.025f));
            if(travel>.00001f)
            {
                var b=ground.bounds;
                int count=Physics.BoxCastNonAlloc(b.center,b.extents*.999f,Vector3.up,hits,Quaternion.identity,travel+.02f,~0,QueryTriggerInteraction.Ignore);
                if(count==hits.Length) travel=0;
                for(int i=0;i<count;i++)
                {
                    var hit=hits[i];
                    if(hit.collider==ground || hit.collider.transform.IsChildOf(player.transform)) continue;
                    // Ignore initial side contacts; only bodies above the surface can block ascent.
                    if(hit.collider.bounds.max.y<=top+.001f) continue;
                    travel=Mathf.Min(travel,Mathf.Max(0,hit.collider.bounds.min.y-top-.015f));
                }
                var controller=player.GetComponent<CharacterController>();
                // Also respect a ceiling above the carried player.
                var pb=controller.bounds;
                int ceilings=Physics.BoxCastNonAlloc(pb.center,pb.extents*.95f,Vector3.up,hits,Quaternion.identity,travel+.02f,~0,QueryTriggerInteraction.Ignore);
                for(int i=0;i<ceilings;i++)
                    if(hits[i].collider!=ground && !hits[i].collider.transform.IsChildOf(player.transform) &&
                        !hits[i].collider.GetComponentInParent<OpeningLight>() && Vector3.Dot(hits[i].normal,Vector3.up)<-.1f)
                        travel=Mathf.Min(travel,Mathf.Max(0,hits[i].distance-.02f));
                if(travel>.00001f)
                {
                    bool enabled=controller.enabled; controller.enabled=false;
                    player.transform.position+=Vector3.up*travel;
                    ground.transform.position+=Vector3.up*travel;
                    controller.enabled=enabled;
                    Physics.SyncTransforms();
                }
            }
            Waiting=travel<=.00001f;
        }
    }
}
