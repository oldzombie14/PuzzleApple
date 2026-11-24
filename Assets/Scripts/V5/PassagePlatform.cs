using System.Collections;
using UnityEngine;

namespace PuzzleApple.V5
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(BoxCollider),typeof(Rigidbody))]
    public sealed class PassagePlatform : MonoBehaviour, ISentenceOutputState<PassagePlatform.RailState>
    {
        public BoxCollider Deck {get;private set;}
        public SentenceMotionOutput MotionOutput {get;internal set;}
        PassageJourney journey;
        Rigidbody body;
        [System.Serializable]
        public struct RailState
        {
            // Metres around one complete out-and-back trip, including the return leg.
            public float phase;
        }

        float nearZ, travelLength;
        RailState travel;
        public RailState CaptureState() => travel;

        public bool TryRestoreState(RailState state)
        {
            if (!journey || !Deck || travelLength <= 0 || float.IsNaN(state.phase) ||
                state.phase < 0 || state.phase >= 2 * travelLength) return false;
            ApplyRailState(state);
            return true;
        }

        void ApplyRailState(RailState state)
        {
            float z = nearZ + Mathf.PingPong(state.phase, travelLength);
            var destination = journey.Root.TransformPoint(new Vector3(0, -.15f, z));
            journey.entry.player.CarryOn(Deck, destination - transform.position);
            travel = state;
            transform.position = destination;
            Physics.SyncTransforms();
        }
        bool visitedGarage, fallingFromTrack, recoverInGarage;
        Coroutine respawn;
        ReticleBackground output;
        public bool Respawning => respawn != null;
        public Vector3 TravelDirection=>travel.phase<travelLength?journey.Root.forward:-journey.Root.forward;

        public void Bind(PassageJourney owner,float start,float end)
        {
            journey=owner;
            output=owner.entry.chamberLight.GetComponent<ReticleBackground>();
            nearZ=start;
            travelLength=end-start;
            travel.phase=Mathf.Clamp(owner.Root.InverseTransformPoint(transform.position).z-start,0,travelLength);
            Deck=GetComponent<BoxCollider>();
            body=GetComponent<Rigidbody>();
            body.isKinematic=true;
            body.useGravity=false;
        }

        void Update()
        {
            if(!journey||!journey.isActiveAndEnabled)return;
            var entry=journey.entry;
            if(entry.portableClosure&&entry.portableClosure.Isolated)return;
            var player=entry.player;
            if(travelLength>0 && Time.deltaTime>0 && MotionOutput!=null && MotionOutput.HasMotion(SentenceMotion.Floor))
            {
                float nextPhase = Mathf.Repeat(travel.phase + journey.platformSpeed * Time.deltaTime, 2 * travelLength);
                var lift=journey.GarageDoor;
                if (lift && player.StandingOn(Deck)) {
                    var capsule=player.GetComponent<CharacterController>();
                    var centre=journey.Root.InverseTransformPoint(capsule.bounds.center);
                    float z=nearZ+Mathf.PingPong(travel.phase,travelLength);
                    float nextZ=nearZ+Mathf.PingPong(nextPhase,travelLength);
                    float step=nextZ-z, side=centre.z-journey.GarageStart;
                    // Reflect the rail phase before the rider's leading edge touches the slab.
                    // The board's own nose can pass under the closed lift.
                    float clearance=capsule.radius+capsule.skinWidth+.13f;
                    if (side*step<0 && Mathf.Sign(side)*(side+step)<=clearance &&
                        lift.Clearance<centre.y+capsule.bounds.extents.y+capsule.skinWidth) {
                        nextPhase=Mathf.Repeat(2*travelLength-travel.phase+Mathf.Abs(step),2*travelLength);
                    }
                }
                ApplyRailState(new RailState { phase = nextPhase });
            }

            var feet=journey.Root.InverseTransformPoint(player.transform.position);
            if(feet.y>=-.3f)
            {
                fallingFromTrack=false;
                if(feet.z>=journey.GarageStart && feet.z<journey.GarageExit)visitedGarage=true;
                return;
            }
            // Remember the fall even if the player steers underneath a threshold on the way down.
            if(!fallingFromTrack && feet.z>=journey.Tutorial.ExitZ-.3f && feet.z<journey.RoomStart+.15f) {
                fallingFromTrack=true;
                recoverInGarage=feet.z>=journey.GarageStart || visitedGarage &&
                    feet.z-journey.Tutorial.ExitZ > journey.GarageStart-feet.z;
            }
            if(Respawning||!fallingFromTrack||feet.y>-4)return;
            respawn=StartCoroutine(Respawn(recoverInGarage));
        }

        IEnumerator Respawn(bool far)
        {
            yield return FadeWorld(1, 0, .35f);
            // Keep the view black across the camera cut, including the first frame at the landing.
            if (journey.isActiveAndEnabled && !(journey.entry.portableClosure && journey.entry.portableClosure.Isolated))
            {
                var facing=far?-journey.Root.forward:journey.Root.forward;
                journey.entry.player.Teleport(LandingPoint(far),Quaternion.LookRotation(facing,journey.Root.up));
            }
            yield return null;
            yield return FadeWorld(0, 1, .5f);
            fallingFromTrack=false;
            respawn=null;
        }

        IEnumerator FadeWorld(float from, float to, float duration)
        {
            for (float t=0; t<duration; t+=Time.unscaledDeltaTime)
            {
                output.WorldVisibility=Mathf.SmoothStep(from,to,t/duration);
                yield return null;
            }
            output.WorldVisibility=to;
        }

        void OnDisable()
        {
            if (!Respawning) return;
            StopCoroutine(respawn);
            respawn=null;
            if (output) output.WorldVisibility=1;
        }

        Vector3 LandingPoint(bool far)
        {
            var motor=journey.entry.player.GetComponent<CharacterController>();
            if(far)return journey.RailRoom.SafeLanding;
            float doorZ=journey.Tutorial.ExitZ;
            const float intoRoom=-1;
            // The moving spheres can occupy the centre of the room 2 doorway.
            for(int row=0;row<3;row++)
                for(int lane=0;lane<3;lane++)
                {
                    float x=lane==0?0:(lane==1?-1:1)*journey.PathWidth;
                    var feet=journey.Root.TransformPoint(new Vector3(x,.12f,doorZ+intoRoom*(1.6f+row*2)));
                    var bottom=feet+Vector3.up*motor.radius;
                    var top=feet+Vector3.up*(motor.height-motor.radius);
                    if(!Physics.CheckCapsule(bottom,top,motor.radius,~0,QueryTriggerInteraction.Ignore))
                        return feet;
                }
            return journey.Root.TransformPoint(new Vector3(0,.12f,doorZ+intoRoom*6));
        }
    }
}
