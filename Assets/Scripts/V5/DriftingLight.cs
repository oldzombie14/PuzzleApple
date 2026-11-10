using UnityEngine;

namespace PuzzleApple.V5
{
    [RequireComponent(typeof(Rigidbody),typeof(SphereCollider))]
    public sealed class DriftingLight : MonoBehaviour
    {
        public PassageJourney journey;
        public Vector3 direction;
        public float speed=1.2f;
        public Light glow;
        Rigidbody body;
        RoomLightGathering gathering;
        Collider[] motionColliders;
        public int BounceCount {get;private set;}
        void Awake()
        {
            motionColliders=GetComponentsInChildren<Collider>();body=GetComponent<Rigidbody>();body.useGravity=false;body.constraints=RigidbodyConstraints.FreezeRotation;
            body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
        }
        void Start()
        {
            direction=Random.onUnitSphere;
            gathering=journey.GetComponent<RoomLightGathering>();
            if(!gathering){gathering=journey.gameObject.AddComponent<RoomLightGathering>();gathering.journey=journey;}
            Physics.IgnoreCollision(GetComponent<SphereCollider>(),journey.entry.player.GetComponent<CharacterController>());
        }
        void FixedUpdate()
        {
            if(!journey||!journey.Root)return;
            if(gathering&&gathering.Step(this,body,motionColliders))return;
            if(journey.floorMotion && journey.floorMotion.Active){journey.floorMotion.Step(this,body,motionColliders);return;}
            if(!journey.LampsMoving){if(!body.isKinematic){body.linearVelocity=Vector3.zero;body.isKinematic=true;}return;}
            if(body.isKinematic)body.isKinematic=false;
            // Bounds also seal the doorway for lamps without blocking the player.
            var local=journey.Root.InverseTransformPoint(body.position);
            var lo=new Vector3(-journey.roomSize.x*.5f+.4f,.4f,journey.pathLength+.4f);
            // With the interactive floor, let the native collider produce real contacts.
            // The old fixed .4 m limit reflected lamps before they ever touched it.
            if(journey.floorSurface)lo.y=-10000;
            var hi=new Vector3(journey.roomSize.x*.5f-.4f,journey.roomSize.y-.4f,journey.pathLength+journey.roomSize.z-.4f);
            if(journey.floorSurface&&journey.floorSurface.HasWalls)
            {
                lo.x=-10000;hi.x=10000;hi.y=10000;hi.z=10000;
                // Retain only the invisible doorway seal; actual walls now supply contacts.
                if(Mathf.Abs(local.x)>journey.PathWidth*.5f+.4f)lo.z=-10000;
            }
            var d=journey.Root.InverseTransformDirection(direction);
            bool corrected=false;
            for(int axis=0;axis<3;axis++)
            {
                if(local[axis]<lo[axis]){local[axis]=lo[axis];d[axis]=Mathf.Abs(d[axis]);corrected=true;}
                if(local[axis]>hi[axis]){local[axis]=hi[axis];d[axis]=-Mathf.Abs(d[axis]);corrected=true;}
            }
            if(corrected){body.position=journey.Root.TransformPoint(local);direction=journey.Root.TransformDirection(d).normalized;BounceCount++;}
            body.linearVelocity=direction*speed;
        }
        void OnCollisionEnter(Collision c)
        {
            if(journey&&journey.floorSurface&&!journey.floorSurface.Owns(c.collider)&&c.contactCount>0)
            {var contact=c.GetContact(0);journey.floorSurface.ImpactWall(c.collider,contact.point,contact.normal);}
            if(journey&&journey.floorSurface&&journey.floorSurface.Owns(c.collider))
                foreach(var contact in c.contacts)if(Vector3.Dot(contact.normal,Vector3.up)>.6f)
                {journey.floorSurface.Impact(contact.point);break;}
            Reflect(c);
        }
        void OnCollisionStay(Collision c)=>Reflect(c);
        void Reflect(Collision c)
        {
            foreach(var contact in c.contacts)
                if(Vector3.Dot(direction,contact.normal)<-.001f){direction=Vector3.Reflect(direction,contact.normal).normalized;BounceCount++;}
        }
    }
}

