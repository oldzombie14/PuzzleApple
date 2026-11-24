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
        SphereCollider sphere;
        bool fallingThrough, parked, docked;
        Vector3 dockPosition;
        float dockTime;
        public bool Docked=>docked;
        public float ChargePower => journey.LightStyle.LampIntensity(transform.localScale.x);
        public bool AtChargeDoor { get; private set; }
        [System.Serializable]
        public struct State
        {
            public int slot, neighbourSlot;
            public bool hasNeighbour;
            public DriftPose pose;
            public bool falling, docked, chargeDoor, hasDoorTarget, targetsEntrance;
            public Vector3 velocity, dockPosition, doorTarget;
            public float dockTime;
            public bool Valid => slot >= 0 && (!hasNeighbour || neighbourSlot >= 0 && neighbourSlot != slot) && pose.Valid && DriftPose.Finite(velocity) &&
                DriftPose.Finite(dockPosition) && DriftPose.Finite(doorTarget) &&
                float.IsFinite(dockTime) && dockTime >= 0;
        }

        internal State CaptureMotion()
        {
            var state = new State {
                slot = SlotIndex, pose = new DriftPose {position = body.position, direction = direction, parked = parked},
                falling = fallingThrough, velocity = fallingThrough ? body.linearVelocity : Vector3.zero,
                docked = docked, chargeDoor = AtChargeDoor, dockPosition = dockPosition, dockTime = dockTime
            };
            if (gathering) gathering.CaptureTargets(this, ref state);
            return state;
        }
        internal void RestoreMotion(State state)
        {
            if (!body.isKinematic) body.linearVelocity = Vector3.zero;
            DriftPose.Place(body, state.pose.position);
            direction = state.pose.direction;
            parked = state.pose.parked;
            docked = state.docked;
            AtChargeDoor = state.chargeDoor;
            dockPosition = state.dockPosition;
            dockTime = state.dockTime;
            fallingThrough = state.falling && journey.floorSurface && journey.floorSurface.HoleProgress > 0;
            body.useGravity = fallingThrough;
            body.isKinematic = !fallingThrough;
            if (fallingThrough) body.linearVelocity = state.velocity;
            if (gathering) gathering.RestoreTargets(this, state);
        }

        public int SlotIndex {get;set;}
        public void DockAtDoor(bool atChargeDoor=true){
            if(docked)return;
            AtChargeDoor=atChargeDoor;docked=true;dockPosition=body.position;dockTime=0;
        }
        void Hover(){
            dockTime+=Time.fixedDeltaTime;
            float phase=SlotIndex*2.4f;
            body.MovePosition(dockPosition+journey.Root.up*.09f*(Mathf.Sin(dockTime*1.8f+phase)-Mathf.Sin(phase)));
        }

        public int BounceCount {get;private set;}
        void Awake()
        {
            sphere=GetComponent<SphereCollider>();
            motionColliders=GetComponentsInChildren<Collider>();body=GetComponent<Rigidbody>();body.useGravity=false;body.constraints=RigidbodyConstraints.FreezeRotation;
            body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
        }
        void Start()
        {
            if (direction == Vector3.zero) direction = Random.onUnitSphere;
            gathering=journey.GetComponent<RoomLightGathering>();
            if(!gathering){gathering=journey.gameObject.AddComponent<RoomLightGathering>();gathering.journey=journey;}
            Physics.IgnoreCollision(GetComponent<SphereCollider>(),journey.entry.player.GetComponent<CharacterController>());
        }
        void FixedUpdate()
        {
            if(!journey||!journey.Root)return;
            if(!journey.enabled)return;
            var floor=journey.floorSurface;
            var passage=journey.entry.outdoor;
            if(passage&&floor&&floor.HoleProgress>0)
            {
                var position=journey.Root.InverseTransformPoint(body.position);
                if(!fallingThrough&&floor.FitsOpening(position,sphere.bounds.extents.x))
                {
                    fallingThrough=true;body.isKinematic=false;
                    body.linearVelocity=Vector3.zero;body.useGravity=true;
                }
                if(fallingThrough)
                {
                    if(position.y<=OutdoorPassage.TransferDepth&&passage.TransferLamp(this,position,-body.linearVelocity.y))
                    {
                        journey.RemoveLamp(this);gameObject.SetActive(false);Destroy(gameObject);
                    }
                    return;
                }
            }
            if(journey.LampsLit){
                if((journey.FarRoomMotionOutput?.HasMotion(SentenceMotion.LightToFloor)==true)||(journey.FarRoomMotionOutput?.HasMotion(SentenceMotion.LightToDoor)==true)||(journey.FarRoomMotionOutput?.HasMotion(SentenceMotion.LightToLight)==true))parked=true;
                if((journey.FarRoomMotionOutput?.HasMotion(SentenceMotion.Light)==true))parked=false;
                if(docked && ((journey.FarRoomMotionOutput?.HasMotion(SentenceMotion.Light)==true) || (journey.FarRoomMotionOutput?.HasMotion(SentenceMotion.LightToFloor)==true) || (journey.FarRoomMotionOutput?.HasMotion(SentenceMotion.LightToLight)==true)))docked=false;
            }
            if(docked){Hover();return;}
            if(journey.EndingDoor && journey.EndingDoor.Paused){if(!body.isKinematic){body.linearVelocity=Vector3.zero;body.isKinematic=true;}return;}
            if(gathering&&gathering.Step(this,body,motionColliders))return;
            if(journey.floorMotion && journey.floorMotion.Active){journey.floorMotion.Step(this,body,motionColliders);return;}
            if(parked||!journey.LampsMoving){if(!body.isKinematic){body.linearVelocity=Vector3.zero;body.isKinematic=true;}return;}
            if(body.isKinematic)body.isKinematic=false;
            // Bounds also seal the doorway for lamps without blocking the player.
            var local=journey.Root.InverseTransformPoint(body.position);
            var lo=new Vector3(-journey.roomSize.x*.5f+.4f,.4f,journey.RoomStart+.4f);
            // With the interactive floor, let the native collider produce real contacts.
            // The old fixed .4 m limit reflected lamps before they ever touched it.
            if(journey.floorSurface)lo.y=-10000;
            var hi=new Vector3(journey.roomSize.x*.5f-.4f,journey.roomSize.y-.4f,journey.RoomStart+journey.roomSize.z-.4f);
            if(journey.floorSurface&&journey.floorSurface.HasWalls)
            {
                lo.x=-10000;hi.x=10000;hi.y=10000;
                if(Mathf.Abs(local.x)>journey.PathWidth*.5f+1)hi.z=10000;
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
                {journey.floorSurface.Impact(contact.point,contact.normal);break;}
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

