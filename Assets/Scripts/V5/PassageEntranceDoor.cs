using UnityEngine;
using UnityEngine.Rendering;
using PuzzleApple.V5.Cognition;

namespace PuzzleApple.V5
{
    // The far-room entrance mirrors the authored white leaf, including its room-side finish.
    public sealed class PassageEntranceDoor : MonoBehaviour,ISwitchableDoor,IProgressBoundary,
        ISentenceOutputState<HingeState>
    {
        public PassageJourney journey;
        public Transform Leaf {get;private set;}
        internal SentenceStateOutput DoorOutput;
        public SentenceMotionOutput MotionOutput {get;internal set;}
        HingeState hinge = new HingeState {cycleOpening = true};
        public float Progress => hinge.progress;
        public float BoundaryProgress => Progress;
        public bool AllowsConnection => Progress > 0;
        public event System.Action BoundaryChanged;
        public bool Open {get;private set;}
        public bool Driving {get;private set;}
        public PassageJourney.LightSpace ControlSpaces=>ProgressLock.DoorBoundary(this);
        public bool CanCycle=>journey && journey.enabled && journey.entry.room.Opened && !journey.entry.ConfirmingExit &&
            (!journey.entry.portableClosure || !journey.entry.portableClosure.Isolated);
        Renderer leaf,pin,cover,backWall;
        Collider[] parts;
        Renderer[] backEdges;
        CharacterController player;
        Material coverMaterial;
        MaterialPropertyBlock frontInk,backInk,coverInk;
        bool ignoringPlayer;
        float Width=>Leaf.localScale.x;
        float Height=>Leaf.localScale.y;
        public static PassageEntranceDoor Build(PassageJourney owner,Transform whiteHinge,Renderer wall){
            var hinge=Instantiate(whiteHinge,owner.Root,false);hinge.name="Room 3 entrance hinge";
            foreach(var gate in hinge.GetComponentsInChildren<ProgressLock>())DestroyImmediate(gate.gameObject);
            var door=hinge.gameObject.AddComponent<PassageEntranceDoor>();door.journey=owner;door.backWall=wall;
            door.frontInk=new MaterialPropertyBlock();door.backInk=new MaterialPropertyBlock();door.coverInk=new MaterialPropertyBlock();
            door.Leaf=hinge.Find("White hinged door");door.Leaf.name="Room 3 entrance door";
            hinge.localPosition=new Vector3(door.Width*.5f,0,owner.RoomStart-.23f);
            hinge.localRotation=Quaternion.identity;hinge.localScale=Vector3.one;
            // These primitives are symmetric: mirror their placements, keeping native collider scales positive.
            foreach(Transform part in hinge){var p=part.localPosition;p.x=-p.x;part.localPosition=p;}
            door.leaf=door.Leaf.GetComponent<Renderer>();door.pin=hinge.Find("Door hinge pin").GetComponent<Renderer>();
            // Instantiate retains shared materials but not the runtime property block on the handle.
            var sourceHandle=whiteHinge.Find("Round handle").GetComponent<Renderer>();
            var handle=hinge.Find("Round handle").GetComponent<Renderer>();
            var handleInk=new MaterialPropertyBlock();sourceHandle.GetPropertyBlock(handleInk);
            handle.sharedMaterial=sourceHandle.sharedMaterial;handle.SetPropertyBlock(handleInk);
            var materials=door.leaf.sharedMaterials;materials[1]=wall.sharedMaterial;door.leaf.sharedMaterials=materials;
            door.cover=door.Leaf.Find("Door room-side finish").GetComponent<Renderer>();
            door.coverMaterial=new Material(door.cover.sharedMaterial);door.cover.sharedMaterial=door.coverMaterial;
            door.coverMaterial.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);door.coverMaterial.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            door.coverMaterial.SetFloat("_ZWrite",0);door.coverMaterial.renderQueue=(int)RenderQueue.Transparent;
            door.parts=hinge.GetComponentsInChildren<Collider>();door.player=owner.entry.player.GetComponent<CharacterController>();
            var rear=new System.Collections.Generic.List<Renderer>();
            foreach(Transform t in owner.Root)if((t.name=="Passage upright"||t.name=="Passage crossbar") && t.localPosition.z>owner.RoomStart && t.localPosition.z<owner.RoomStart+.2f)rear.Add(t.GetComponent<Renderer>());
            door.backEdges=rear.ToArray();
            var template=Resources.Load<ProgressLock>("V5/RoomEntranceProgressLock");
            if(template){var gate=Instantiate(template,hinge,false);gate.BindBoundary(door);}
            door.Appearance();return door;
        }
        public bool Owns(Collider target)=>target && target.transform.IsChildOf(transform);
        public bool WouldChange(bool open)=>open?Progress<1:Progress>0;
        public void SetOpen(bool open){Open=open;Driving=true;}
        public void Hold(){Driving=false;}
        public void Release(){Driving=false;}
        public HingeState CaptureState() => hinge;
        public bool TryRestoreState(HingeState state)
        {
            if (!journey || !Leaf || !player || !(state.progress >= 0 && state.progress <= 1)) return false;
            if (state.progress < Progress && BlocksClosing(state.progress)) return false;
            hinge.cycleOpening = state.cycleOpening;
            ApplyProgress(state.progress);
            Physics.SyncTransforms();
            if (ignoringPlayer && !OverlapsPlayer()) IgnorePlayer(false);
            Appearance();
            return true;
        }

        void ApplyProgress(float next)
        {
            if (Progress == next) return;
            if (next > Progress && !ignoringPlayer) IgnorePlayer(true);
            hinge.progress = next;
            transform.localRotation = Quaternion.Euler(0, -Angle(next), 0);
            BoundaryChanged?.Invoke();
        }
        float Angle(float value)=>journey.openAngle*Mathf.SmoothStep(0,1,value);
        void IgnorePlayer(bool ignore){foreach(var part in parts)if(part)Physics.IgnoreCollision(part,player,ignore);ignoringPlayer=ignore;}
        bool OverlapsPlayer(){
            foreach(var part in parts)if(part && Physics.ComputePenetration(part,part.transform.position,part.transform.rotation,player,player.transform.position,player.transform.rotation,out _,out _))return true;
            return false;
        }
        bool BlocksClosing(float next){
            var centre=journey.Root.InverseTransformPoint(player.bounds.center)-transform.localPosition;
            centre.x=-centre.x;
            if(centre.y-player.height*.5f>Height || centre.y+player.height*.5f<0)return false;
            float padding=player.radius+.16f,z=Leaf.localPosition.z,half=Leaf.localScale.z*.5f;
            if(Mathf.Abs(centre.z-z)<padding && centre.x>=-padding && centre.x<=Width+padding)return true;
            float angle=Mathf.Clamp(Mathf.Atan2(-centre.z,centre.x)*Mathf.Rad2Deg,Angle(Mathf.Min(next,Progress)),Angle(Mathf.Max(next,Progress)));
            var local=Quaternion.Euler(0,-angle,0)*centre;
            return new Vector2(local.x-Mathf.Clamp(local.x,0,Width),local.z-Mathf.Clamp(local.z,z-half,z+half)).sqrMagnitude<padding*padding;
        }
        void Update(){
            if(!journey || !Leaf)return;
            if(CanCycle){
                bool cycling = !Driving && MotionOutput?.HasMotion(SentenceMotion.Door) == true;
                if (cycling) Open = hinge.CycleTarget();
                float target=Open?1:0,next=Driving||cycling?SentenceStateOutput.Advance(Progress,target,Time.deltaTime/Mathf.Max(.01f,journey.doorDuration),DoorOutput):Progress;
                bool opening=next>Progress;
                if(opening || next<Progress&&!BlocksClosing(next))ApplyProgress(next);
                if(Progress==target)Driving=false;
                if(!opening&&ignoringPlayer&&!OverlapsPlayer())IgnorePlayer(false);
            }
            Appearance();
        }
        void Appearance(){
            if(!leaf)return;
            frontInk.Clear();frontInk.SetFloat("_IsDoorLeaf",1);frontInk.SetFloat("_CorridorPureWhite",journey.pureWhiteCorridor?1:0);
            frontInk.SetFloat("_DoorBackBrightness",1);leaf.SetPropertyBlock(frontInk);
            frontInk.SetFloat("_RoundOutline",2);pin.SetPropertyBlock(frontInk);
            backWall.GetPropertyBlock(backInk);leaf.SetPropertyBlock(backInk,1);
            float open=Mathf.SmoothStep(0,1,Progress);cover.enabled=open>0;
            foreach(var edge in backEdges)if(edge)edge.enabled=open>0;
            coverInk.Clear();coverInk.SetFloat("_IsDoorLeaf",1);coverInk.SetFloat("_CorridorPureWhite",journey.pureWhiteCorridor?1:0);coverInk.SetFloat("_Dissolve",1-open);cover.SetPropertyBlock(coverInk);
        }
        void OnDestroy(){if(coverMaterial)Destroy(coverMaterial);}
    }
}
