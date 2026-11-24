using UnityEngine;
using PuzzleApple.V5.Cognition;

namespace PuzzleApple.V5.Outdoor
{
    public sealed class WorldSentenceEffects : MonoBehaviour, ISentenceOutputState<WorldSentenceEffects.MotionState>
    {
        public NightEnvironment environment;
        public Collider ground;
        public FirstPersonController player;
        public ThresholdDoor door;
        public LightSentenceEffects lights;
        [Min(.1f)] public float riseSpeed=2.5f;
        [Min(.02f)] public float recheckInterval=.15f;
        public bool Waiting { get; private set; }
        public SentenceMotionOutput MotionOutput {get;internal set;}
        [Min(.02f)] public float feedbackProximity=.5f;
        SentenceMotion motion;
        float nextCheck, targetHeight;
        readonly RaycastHit[] hits=new RaycastHit[64];
        [System.Serializable]
        public struct MotionState
        {
            public float groundY;
            public Quaternion stars;
            public CloudLightCookie.MotionState clouds;
        }
        public MotionState CaptureState() => new MotionState {
            groundY = ground.transform.position.y, stars = environment.StarRotation,
            clouds = environment.clouds.CaptureMotion()
        };
        public bool TryRestoreState(MotionState state)
        {
            if (!ground || !environment || !environment.clouds || !player || !float.IsFinite(state.groundY) ||
                !state.clouds.Valid || !(Mathf.Abs(Quaternion.Dot(state.stars, state.stars) - 1) < .001f)) return false;
            float travel = state.groundY - ground.transform.position.y;
            bool riding = ground.gameObject.activeInHierarchy && player.isActiveAndEnabled && player.StandingOn(ground);
            if (travel != 0 && ground.gameObject.activeInHierarchy &&
                !CanRestoreHeight(travel, riding)) return false;
            if (travel != 0) MoveGround(travel, riding);
            environment.RestoreRotation(state.stars);
            environment.clouds.RestoreMotion(state.clouds);
            nextCheck = 0;
            Waiting = false;
            return true;
        }

        bool CanRestoreHeight(float travel, bool riding)
        {
            var bounds = ground.bounds;
            var direction = travel > 0 ? Vector3.up : Vector3.down;
            float distance = Mathf.Abs(travel);
            // Only sweep the leading face. The broad slab can touch neighbouring scenery along its sides.
            float face = travel > 0 ? bounds.max.y : bounds.min.y;
            var centre = new Vector3(bounds.center.x, face, bounds.center.z) - direction * .005f;
            var half = new Vector3(bounds.extents.x * .999f, .005f, bounds.extents.z * .999f);
            foreach (var hit in Physics.BoxCastAll(centre, half, direction, Quaternion.identity, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(ground.transform) || riding && hit.collider.transform.IsChildOf(player.transform)) continue;
                if (travel > 0 ? hit.collider.bounds.max.y <= face + .001f : hit.collider.bounds.min.y >= face - .001f) continue;
                if (hit.distance + .001f < distance) return false;
            }
            if (!riding) return true;
            var rider = player.GetComponent<CharacterController>().bounds;
            foreach (var hit in Physics.BoxCastAll(rider.center, rider.extents * .98f, direction, Quaternion.identity, distance, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider != ground && !hit.collider.transform.IsChildOf(player.transform) && hit.distance < distance) return false;
            return true;
        }

        void MoveGround(float travel, bool carryPlayer)
        {
            var offset = Vector3.up * travel;
            var controller = carryPlayer ? player.GetComponent<CharacterController>() : null;
            bool wasEnabled = controller && controller.enabled;
            if (wasEnabled) controller.enabled = false;
            if (carryPlayer) player.transform.position += offset;
            ground.transform.position += offset;
            if (wasEnabled) controller.enabled = true;
            Physics.SyncTransforms();
        }

        public void Apply(System.Func<SentenceMotion,bool> hasMotion)
        {
            motion=hasMotion(SentenceMotion.FloorToLight)?SentenceMotion.FloorToLight:
                hasMotion(SentenceMotion.FloorToDoor)?SentenceMotion.FloorToDoor:default;
            if(environment) environment.WorldMoving=hasMotion(SentenceMotion.Floor);
            nextCheck=0; Waiting=false;
        }
        public bool CanRise(SentenceMotion requested)
        {
            if(!ground||!player)return false;
            float target=requested==SentenceMotion.FloorToLight&&lights&&lights.CanOperate?lights.LowestBottom():
                requested==SentenceMotion.FloorToDoor&&door&&door.Revealed?door.transform.position.y:float.PositiveInfinity;
            if(float.IsInfinity(target)||target-ground.bounds.max.y<=.03f)return false;
            return UnblockedTravel(.04f)>.004f;
        }
        float UnblockedTravel(float travel)
        {
            float top=ground.bounds.max.y;
            var b=ground.bounds;
            int count=Physics.BoxCastNonAlloc(b.center,b.extents*.999f,Vector3.up,hits,Quaternion.identity,travel+.02f,~0,QueryTriggerInteraction.Ignore);
            if(count==hits.Length) travel=0;
            for(int i=0;i<count;i++)
            {
                var hit=hits[i];
                if(hit.collider.transform.IsChildOf(ground.transform) || hit.collider.transform.IsChildOf(player.transform)) continue;
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
            return travel;
        }
        void FixedUpdate()
        {
            if(!ground || !player || motion==default) return;
            if(motion==SentenceMotion.FloorToLight&&lights&&!lights.CanOperate){Waiting=true;return;}
            if(Waiting && Time.time<nextCheck) return;
            if(Time.time>=nextCheck)
            {
                nextCheck=Time.time+recheckInterval;
                targetHeight=motion==SentenceMotion.FloorToLight && lights?lights.LowestBottom():
                    motion==SentenceMotion.FloorToDoor && door && door.Revealed?door.transform.position.y:float.PositiveInfinity;
            }
            float top=ground.bounds.max.y;
            // No reachable reference means wait; never launch the ground into unbounded space.
            float remaining=float.IsInfinity(targetHeight)?0:Mathf.Max(0,targetHeight-top-.025f);
            float step=riseSpeed*Time.fixedDeltaTime;
            float travel=Mathf.Min(MotionOutput?.DirectedStep(step,remaining,Time.fixedDeltaTime)??step,remaining);
            if(travel>.00001f)
            {
                travel=UnblockedTravel(travel);
                if(travel>.00001f)
                {
                    MoveGround(travel, true);
                }
            }
            Waiting=travel<=.00001f;
        }
    }
}
