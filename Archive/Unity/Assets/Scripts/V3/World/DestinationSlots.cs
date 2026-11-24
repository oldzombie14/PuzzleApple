using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PuzzleApple.V3
{
    // Places belong to physical participants, never to a word, sentence or Motion instance.
    // Occupancy survives instruction changes. Reservations describe only an intended arrival.
    public sealed class DestinationSlots : MonoBehaviour
    {
        public Transform[] places=new Transform[0];
        public BoxCollider[] sensors=new BoxCollider[0];
        public float halfWidth=.38f,heightTolerance=.12f;
        public struct Presence
        {
            public Component owner;
            public Vector3 feet;
            public Presence(Component owner,Vector3 feet){this.owner=owner;this.feet=feet;}
        }
        public sealed class Request
        {
            public Component owner,companion;
            public Vector3 origin;
            public Func<Vector3,Vector3> companionPoint;
        }
        Component[] occupants=new Component[0];
        readonly Dictionary<Component,Transform> reservations=new Dictionary<Component,Transform>();
        Dictionary<Component,Component> participants=new Dictionary<Component,Component>();
        bool dirty=true;
        public Component Occupant(int index)=>index<occupants.Length?occupants[index]:null;
        public Transform OccupiedPlace(Component owner)
        {
            for(int i=0;i<occupants.Length;i++)if(occupants[i]==owner)return places[i];
            return null;
        }
        public Transform PlaceFor(Component owner)=>OccupiedPlace(owner)??
            (reservations.TryGetValue(owner,out var place)?place:null);
        bool Contains(Transform place,Vector3 feet)=>Mathf.Abs(feet.x-place.position.x)<halfWidth&&
            Mathf.Abs(feet.z-place.position.z)<halfWidth&&Mathf.Abs(feet.y-place.position.y)<heightTolerance;
        public bool Contains(int index,Component owner)
        {
            if(!owner||!owner.gameObject.activeInHierarchy)return false;
            var obj=owner as OpeningObject;
            var colliderBody=owner.GetComponent<Collider>();
            var bounds=obj?obj.PhysicalBounds:colliderBody?colliderBody.bounds:new Bounds(owner.transform.position,Vector3.zero);
            var feet=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            if(index>=sensors.Length||!sensors[index])return Contains(places[index],feet);
            var sensor=sensors[index];var volume=sensor.bounds;
            // Count contact on top, never an object touching the pan from below.
            if(feet.y<places[index].position.y-.08f||feet.y>volume.max.y||
                Mathf.Abs(feet.x-volume.center.x)>volume.extents.x||Mathf.Abs(feet.z-volume.center.z)>volume.extents.z)return false;
            foreach(var collider in owner.GetComponentsInChildren<Collider>())
                if(collider.enabled&&!collider.isTrigger&&Physics.ComputePenetration(sensor,sensor.transform.position,sensor.transform.rotation,
                    collider,collider.transform.position,collider.transform.rotation,out _,out _))return true;
            return false;
        }
        public void RefreshOccupancy(IEnumerable<Presence> presence)
        {
            if(occupants.Length!=places.Length){occupants=new Component[places.Length];dirty=true;}
            var bodies=presence.Where(p=>p.owner&&p.owner.gameObject.activeInHierarchy).ToArray();
            for(int i=0;i<places.Length;i++)
            {
                var previous=occupants[i];
                // Keep the current occupant while physically present, even without any instruction or identity.
                if(previous&&bodies.Any(p=>p.owner==previous&&Contains(i,p.owner)))continue;
                occupants[i]=bodies.Where(p=>Contains(i,p.owner))
                    .OrderBy(p=>(p.feet-places[i].position).sqrMagnitude).Select(p=>p.owner).FirstOrDefault();
                if(occupants[i]!=previous)dirty=true;
            }
        }
        public void Reconcile(IEnumerable<Request> source)
        {
            var requests=source.Where(r=>r.owner).ToArray();
            var next=requests.ToDictionary(r=>r.owner,r=>r.companion);
            bool changed=next.Count!=participants.Count||next.Any(p=>!participants.TryGetValue(p.Key,out var partner)||partner!=p.Value);
            if(!dirty&&!changed)return;
            dirty=false;participants=next;reservations.Clear();
            if(places.Length==0)return;
            var claims=new Dictionary<Component,Transform>();
            for(int i=0;i<places.Length;i++)if(occupants[i])claims[occupants[i]]=places[i];
            // Only unoccupied reservations may be replanned when participants change.
            // Allocation is atomic for linked arrivals (for example, a reflected pair).
            foreach(var request in requests.OrderBy(r=>claims.ContainsKey(r.owner)?-1:places.Min(t=>(t.position-r.origin).sqrMagnitude)))
            {
                foreach(var place in places.OrderBy(t=>(t.position-request.origin).sqrMagnitude))
                {
                    if(!Available(claims,request.owner,place))continue;
                    Transform companionPlace=null;
                    if(request.companion)
                    {
                        if(request.companionPoint==null)continue;
                        var point=request.companionPoint(place.position);
                        companionPlace=places.OrderBy(t=>(t.position-point).sqrMagnitude).FirstOrDefault();
                        if(companionPlace==place||!companionPlace||Vector3.Distance(companionPlace.position,point)>.5f||
                            !Available(claims,request.companion,companionPlace))continue;
                    }
                    claims[request.owner]=place;reservations[request.owner]=place;
                    if(request.companion){claims[request.companion]=companionPlace;reservations[request.companion]=companionPlace;}
                    break;
                }
            }
        }
        static bool Available(Dictionary<Component,Transform> claims,Component owner,Transform place)=>
            (!claims.TryGetValue(owner,out var own)||own==place)&&!claims.Any(p=>p.Key!=owner&&p.Value==place);
    }
}
