using UnityEngine;

namespace PuzzleApple.V5
{
    public sealed class FloorDirectedMotion : MonoBehaviour
    {
        public PassageJourney journey;
        public bool openHoleWhenGrounded;
        [Min(.1f)] public float speed=2.5f;
        [Min(0)] public float surfaceClearance=.025f,stopDistance=.04f;
        public bool Active => journey && journey.Brightness > 0 &&
            journey.FarRoomMotionOutput?.HasMotion(SentenceMotion.LightToFloor)==true;
        public int ArrivedCount {get;private set;}
        public bool Completed=>journey && journey.Lamps.Count>0 && ArrivedCount==journey.Lamps.Count;
        public bool IsArrived(DriftingLight lamp)
        {
            if(!lamp||!lamp.isActiveAndEnabled||!journey||!journey.floorSurface||!journey.floorSurface.Surface)return false;
            var collider=lamp.GetComponent<Collider>();
            return collider.bounds.min.y<=journey.floorSurface.Surface.bounds.max.y+surfaceClearance+stopDistance+.001f;
        }
        public void Step(DriftingLight lamp,Rigidbody body,Collider[] colliders)
        {
            if(!body.isKinematic){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;body.isKinematic=true;}
            var collider=lamp.GetComponent<Collider>();
            // V4's LightWorldMove: keep X/Z, move vertically to the floor's top,
            // accounting for collider extent and its original clearance/tolerance.
            float target=journey.floorSurface.Surface.bounds.max.y+(body.position.y-collider.bounds.min.y)+surfaceClearance;
            float remaining=body.position.y-target;
            if(remaining<=stopDistance)return;
            float step=journey.FarRoomMotionOutput.DirectedStep(speed*Time.fixedDeltaTime,remaining-stopDistance,Time.fixedDeltaTime);
            float travel=DirectedMotion.Move(body,colliders,Vector3.down*Mathf.Min(step,remaining-stopDistance),true);
            if(travel>.0001f || IsArrived(lamp))return;
            // A lamp directly below can block a vertical descent. Slide around its footprint.
            foreach(var other in journey.Lamps){
                if(other==lamp || !other)continue;
                Vector3 gap=body.position-other.GetComponent<Rigidbody>().position;
                float clearance=collider.bounds.extents.x+other.GetComponent<Collider>().bounds.extents.x+.08f;
                var lateral=Vector3.ProjectOnPlane(gap,Vector3.up);
                if(gap.y<=0 || gap.y>clearance+step || lateral.sqrMagnitude>clearance*clearance)continue;
                if(lateral.sqrMagnitude<.0001f)lateral=Quaternion.AngleAxis(lamp.SlotIndex*137.5f,Vector3.up)*journey.Root.right;
                DirectedMotion.Move(body,colliders,lateral.normalized*step,true);
                break;
            }
        }
        void LateUpdate()
        {
            if(!journey||!journey.floorSurface||!journey.floorSurface.Surface)return;
            ArrivedCount=0;
            foreach(var lamp in journey.Lamps)if(IsArrived(lamp))ArrivedCount++;
            if(openHoleWhenGrounded&&Completed&&journey.LightsOn)journey.floorSurface.OpenHole();
        }
    }
}


