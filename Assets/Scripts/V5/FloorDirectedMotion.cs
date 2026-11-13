using UnityEngine;

namespace PuzzleApple.V5
{
    public sealed class FloorDirectedMotion : MonoBehaviour
    {
        public PassageJourney journey;
        [Min(.1f)] public float speed=2.5f;
        [Min(0)] public float surfaceClearance=.025f,stopDistance=.04f;
        public bool Active => journey && journey.Brightness > 0 && journey.entry && journey.entry.board && journey.entry.board.State != null &&
            (journey.entry.sentenceRuntime ? journey.entry.sentenceRuntime.HasEffect(Cognition.CognitionSignal.LightFloorMove) :
                journey.entry.board.State.HasEffect(Cognition.CognitionSignal.LightFloorMove));
        public int ArrivedCount {get;private set;}
        public bool Completed=>journey&&journey.floorSurface&&journey.floorSurface.HoleOpen;
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
            DirectedMotion.Move(body,colliders,Vector3.down*Mathf.Min(speed*Time.fixedDeltaTime,remaining-stopDistance),true);
        }
        void LateUpdate()
        {
            if(!journey||!journey.floorSurface||!journey.floorSurface.Surface||Completed)return;
            ArrivedCount=0;
            foreach(var lamp in journey.Lamps)if(IsArrived(lamp))ArrivedCount++;
            if(journey.Lamps.Count>0&&ArrivedCount==journey.Lamps.Count&&journey.LightsOn)journey.floorSurface.OpenHole();
        }
    }
}


