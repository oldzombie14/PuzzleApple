using System.Collections.Generic;
using PuzzleApple.V5.Cognition;
using UnityEngine;

namespace PuzzleApple.V5
{
    // V4 LightSentenceEffects' nearest-neighbour / radial-door modes on V5 room bodies.
    public sealed class RoomLightGathering : MonoBehaviour
    {
        public PassageJourney journey;
        public float influenceRadius=50,directedSpeed=2.5f,clusterSpeed=1.2f,spacing=1.1f,targetSwitchRatio=.8f;
        public Vector2 gatheringRadius=new Vector2(2.5f,5);
        readonly Dictionary<DriftingLight,DriftingLight> neighbours=new Dictionary<DriftingLight,DriftingLight>();
        CognitionSignal previous;
        CognitionSignal Motion=>journey.entry.sentenceRuntime.HasEffect(CognitionSignal.LightDoorMove)?CognitionSignal.LightDoorMove:
            journey.entry.sentenceRuntime.HasEffect(CognitionSignal.LightLightMove)?CognitionSignal.LightLightMove:CognitionSignal.Empty;
        bool InRange(DriftingLight lamp)=>lamp&&(lamp.transform.position-journey.entry.player.transform.position).sqrMagnitude<=influenceRadius*influenceRadius;
        public bool Step(DriftingLight lamp,Rigidbody body,Collider[] colliders)
        {
            if(!journey||!journey.entry.sentenceRuntime)return false;
            var motion=Motion;
            if(motion!=previous){neighbours.Clear();previous=motion;}
            if(motion==CognitionSignal.Empty)return false;
            if(!body.isKinematic){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;body.isKinematic=true;}
            if(!InRange(lamp))return true;
            var collider=lamp.GetComponent<Collider>();Vector3 target;float stop=.16f;
            if(motion==CognitionSignal.LightDoorMove)
            {
                uint id=unchecked((uint)lamp.GetInstanceID()*2654435761u);
                float a=(id&65535)/65535f*Mathf.PI*2,y=((id>>16)&65535)/65535f*2-1,r=Mathf.Lerp(gatheringRadius.x,gatheringRadius.y,(id%997)/997f);
                float ring=Mathf.Sqrt(Mathf.Max(0,1-y*y));
                target=journey.Door.position+new Vector3(Mathf.Cos(a)*ring,y,Mathf.Sin(a)*ring)*r;
                if(journey.floorSurface)target.y=Mathf.Max(target.y,journey.floorSurface.Surface.bounds.max.y+collider.bounds.extents.y+.1f);
            }
            else
            {
                DriftingLight best=null;float closest=float.PositiveInfinity;
                foreach(var other in journey.Lamps)if(other!=lamp&&InRange(other))
                {float d=(other.transform.position-body.position).sqrMagnitude;if(d<closest){closest=d;best=other;}}
                neighbours.TryGetValue(lamp,out var old);
                if(old&&old!=lamp&&InRange(old)&&best!=old&&closest>=(old.transform.position-body.position).sqrMagnitude*targetSwitchRatio*targetSwitchRatio)best=old;
                neighbours[lamp]=best;if(!best)return true;
                target=best.transform.position;stop=spacing+collider.bounds.extents.magnitude+best.GetComponent<Collider>().bounds.extents.magnitude;
            }
            var delta=target-body.position;float distance=delta.magnitude;if(distance<=stop)return true;
            Vector3 separation=Vector3.zero;
            foreach(var other in journey.Lamps)if(other&&other!=lamp)
            {var away=body.position-other.transform.position;float d=away.magnitude;if(d>.001f&&d<spacing)separation+=away/d*(1-d/spacing);}
            var direction=(delta/distance+separation*2).normalized;
            DirectedMotion.Move(body,colliders,direction*Mathf.Min((motion==CognitionSignal.LightLightMove?clusterSpeed:directedSpeed)*Time.fixedDeltaTime,distance-stop),true);
            return true;
        }
    }
}
