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
        readonly Dictionary<DriftingLight,float> blocked=new Dictionary<DriftingLight,float>();
        CognitionSignal Motion=>journey.entry.sentenceRuntime.HasEffect(CognitionSignal.LightDoorMove,PassageJourney.LightSpace.FarRoom)?CognitionSignal.LightDoorMove:
            journey.entry.sentenceRuntime.HasEffect(CognitionSignal.LightLightMove,PassageJourney.LightSpace.FarRoom)?CognitionSignal.LightLightMove:CognitionSignal.Empty;
        bool InRange(DriftingLight lamp)=>lamp && lamp.isActiveAndEnabled;
        public bool CanMove(CognitionSignal motion)
        {
            if(!journey||journey.Brightness<=0)return false;
            foreach(var lamp in journey.Lamps)
                if(InRange(lamp)&&!(motion==CognitionSignal.LightDoorMove && lamp.Docked)&&(motion!=previous || !blocked.TryGetValue(lamp,out var held) || held<.6f)&&TryTarget(lamp,lamp.GetComponent<Rigidbody>(),motion,false,out _,out _))return true;
            return false;
        }
        bool TryTarget(DriftingLight lamp,Rigidbody body,CognitionSignal motion,bool remember,out Vector3 delta,out float stop)
        {
            delta=Vector3.zero;
            var collider=lamp.GetComponent<Collider>();Vector3 target;stop=.16f;
            if(motion==CognitionSignal.LightDoorMove)
            {
                var door=journey.EndingDoor;
                if(!door)return false;
                target=door.Slot(lamp.SlotIndex,journey.Lamps.Count);
            }
            else
            {
                DriftingLight best=null;float closest=float.PositiveInfinity;
                foreach(var other in journey.Lamps)if(other!=lamp&&InRange(other))
                {float d=(other.transform.position-body.position).sqrMagnitude;if(d<closest){closest=d;best=other;}}
                neighbours.TryGetValue(lamp,out var old);
                if(old&&old!=lamp&&InRange(old)&&best!=old&&closest>=(old.transform.position-body.position).sqrMagnitude*targetSwitchRatio*targetSwitchRatio)best=old;
                if(remember)neighbours[lamp]=best;if(!best)return false;
                target=best.transform.position;stop=spacing+collider.bounds.extents.magnitude+best.GetComponent<Collider>().bounds.extents.magnitude;
            }
            delta=target-body.position;return delta.magnitude>stop+.003f;
        }
        public bool Step(DriftingLight lamp,Rigidbody body,Collider[] colliders)
        {
            if(!journey||journey.Brightness<=0||!journey.entry.sentenceRuntime)return false;
            var motion=Motion;
            if(motion!=previous){blocked.Clear();previous=motion;}
            if(motion==CognitionSignal.Empty)return false;
            if(!body.isKinematic){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;body.isKinematic=true;}
            if(!InRange(lamp))return true;
            if(!TryTarget(lamp,body,motion,true,out var delta,out var stop)){
                if(motion==CognitionSignal.LightDoorMove)lamp.DockAtDoor();
                return true;
            }
            float distance=delta.magnitude;
            Vector3 separation=Vector3.zero;
            foreach(var other in journey.Lamps)if(other&&other!=lamp)
            {var away=body.position-other.transform.position;float d=away.magnitude;if(d>.001f&&d<spacing)separation+=away/d*(1-d/spacing);}
            var direction=(delta/distance+separation*2).normalized;
            Vector3 step = direction*Mathf.Min((motion==CognitionSignal.LightLightMove?clusterSpeed:directedSpeed)*Time.fixedDeltaTime,distance-stop);
            if (motion == CognitionSignal.LightDoorMove)
            {
                var sphere = lamp.GetComponent<SphereCollider>();
                Vector3 door = journey.EndingDoor.transform.position;
                float priority = (body.position-door).sqrMagnitude;
                step = DirectedMotion.ApproachStep(step, offset =>
                {
                    foreach (var other in journey.Lamps)
                    {
                        if (other == lamp || !other) continue;
                        var centre = other.GetComponent<Rigidbody>().position;
                        float otherPriority = (centre-door).sqrMagnitude;
                        if (otherPriority > priority || (otherPriority == priority && other.GetInstanceID() > lamp.GetInstanceID())) continue;
                        float clearance = sphere.bounds.extents.x + other.GetComponent<SphereCollider>().bounds.extents.x
                            + 2 * directedSpeed * Time.fixedDeltaTime;
                        var gap = body.position-centre;
                        if ((gap+offset).sqrMagnitude < Mathf.Min(clearance*clearance,gap.sqrMagnitude)) return false;
                    }
                    return DirectedMotion.SphereStepClear(sphere,offset,true);
                });
            }
            float travel=DirectedMotion.Move(body,colliders,step,true);
            blocked.TryGetValue(lamp,out var stalled);
            blocked[lamp]=travel<.0001f?stalled+Time.fixedDeltaTime:0;
            return true;
        }
    }
}
