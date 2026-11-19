using System.Collections.Generic;
using System.Linq;
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
        readonly Dictionary<DriftingLight,Vector3> doorSlots = new Dictionary<DriftingLight,Vector3>();
        readonly HashSet<DriftingLight> entranceLamps = new HashSet<DriftingLight>();

        void Update()
        {
            if (doorSlots.Count > 0 && Motion != CognitionSignal.LightDoorMove && !journey.Lamps.Any(l=>l && l.Docked))
                doorSlots.Clear();
        }

        void AssignDoors()
        {
            if (doorSlots.Count > 0) return;
            entranceLamps.Clear();
            var entrance = journey.Root.TransformPoint(new Vector3(0,2,journey.RoomStart+.8f));
            var slab = journey.EndingDoor.Leaf.position;
            var lamps = journey.Lamps.Where(l => InRange(l) && !l.Docked)
                .OrderBy(l => Vector3.Distance(l.transform.position,entrance)-Vector3.Distance(l.transform.position,slab)).ToList();
            float available = journey.Lamps.Where(l => InRange(l) && (!l.Docked || l.AtChargeDoor)).Sum(l => l.ChargePower);
            foreach (var lamp in lamps)
            {
                if (available-lamp.ChargePower < journey.ChargePowerRequired) continue;
                if (entranceLamps.Count > 0 && Vector3.Distance(lamp.transform.position,entrance) >= Vector3.Distance(lamp.transform.position,slab)) break;
                entranceLamps.Add(lamp);
                available -= lamp.ChargePower;
            }
            var chargeSlots = Enumerable.Range(0,lamps.Count-entranceLamps.Count)
                .Select(i => journey.EndingDoor.Slot(i,lamps.Count-entranceLamps.Count)).ToList();
            int entryIndex=0;
            foreach (var lamp in lamps)
            {
                if (entranceLamps.Contains(lamp))
                {
                    float side=entryIndex%2==0?-1:1;
                    doorSlots[lamp]=journey.Root.TransformPoint(new Vector3(side*(journey.PathWidth*.5f+.45f),
                        .8f+entryIndex/2*.72f,journey.RoomStart+.8f));
                    entryIndex++;
                }
                else
                {
                    var closest=chargeSlots.OrderBy(p => (p-lamp.transform.position).sqrMagnitude).First();
                    doorSlots[lamp]=closest;
                    chargeSlots.Remove(closest);
                }
            }
        }

        bool InRange(DriftingLight lamp)=>lamp && lamp.isActiveAndEnabled;
        public bool CanMove(CognitionSignal motion)
        {
            if(!journey||journey.Brightness<=0)return false;
            if (motion==CognitionSignal.LightDoorMove)
                return journey.Lamps.Any(l => InRange(l) && !l.Docked);
            foreach(var lamp in journey.Lamps)
                if(InRange(lamp)&&(motion!=previous || !blocked.TryGetValue(lamp,out var held) || held<.6f)&&TryTarget(lamp,lamp.GetComponent<Rigidbody>(),motion,false,out _,out _))return true;
            return false;
        }
        bool TryTarget(DriftingLight lamp,Rigidbody body,CognitionSignal motion,bool remember,out Vector3 delta,out float stop)
        {
            delta=Vector3.zero;
            var collider=lamp.GetComponent<Collider>();Vector3 target;stop=.16f;
            if(motion==CognitionSignal.LightDoorMove)
            {
                AssignDoors();
                if(!doorSlots.TryGetValue(lamp,out target))return false;
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
                if(motion==CognitionSignal.LightDoorMove)lamp.DockAtDoor(!entranceLamps.Contains(lamp));
                return true;
            }
            float distance=delta.magnitude;
            Vector3 separation=Vector3.zero;
            foreach(var other in journey.Lamps)if(other&&other!=lamp)
            {var away=body.position-other.transform.position;float d=away.magnitude;if(d>.001f&&d<spacing)separation+=away/d*(1-d/spacing);}
            // Distinct docking slots already supply the spacing. Close to a slot,
            // a neighbour's hovering must not repel a lamp from its own destination.
            float avoidance=motion==CognitionSignal.LightDoorMove?Mathf.InverseLerp(.3f,1.5f,distance):1;
            var direction=(delta/distance+separation*(2*avoidance)).normalized;
            Vector3 step = direction*Mathf.Min((motion==CognitionSignal.LightLightMove?clusterSpeed:directedSpeed)*Time.fixedDeltaTime,distance-stop);
            if (motion == CognitionSignal.LightDoorMove)
            {
                var sphere = lamp.GetComponent<SphereCollider>();
                Vector3 door = doorSlots[lamp];
                float priority = (body.position-door).sqrMagnitude;
                bool Clear(Vector3 offset)
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
                }
                var direct=step;
                step=DirectedMotion.ApproachStep(direct,Clear);
                if(step==Vector3.zero){
                    // Overlapping horizontal paths can also pass above or below one another.
                    var sideways=Vector3.Cross(direct,Vector3.up);
                    if(sideways.sqrMagnitude<.000001f)sideways=journey.Root.right;
                    foreach(float angle in climbAngles){
                        var alternate=Quaternion.AngleAxis(angle,sideways)*direct;
                        if(Clear(alternate)){step=alternate;break;}
                    }
                }
                // Clear tests the actual sphere at the next physics pose. An additional
                // box cast here would snag the corners of its larger bounding box.
                body.MovePosition(body.position+step);
                return true;
            }
            float travel=DirectedMotion.Move(body,colliders,step,true);
            blocked.TryGetValue(lamp,out var stalled);
            blocked[lamp]=travel<.0001f?stalled+Time.fixedDeltaTime:0;
            return true;
        }
        static readonly float[] climbAngles={45,-45,80,-80};
    }
}
