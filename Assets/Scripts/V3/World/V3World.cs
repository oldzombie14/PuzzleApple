using System.Collections.Generic;
using System.Linq;
using PuzzleApple.V3.Cognition;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PuzzleApple.V3
{
    [DefaultExecutionOrder(-80)]
    public sealed class V3World : MonoBehaviour
    {
        public CognitionBoard board;
        public WordLibrary library;
        public FirstPersonController player;
        public V3Presentation presentation;
        public V3Object originalApple, originalMirror;
        public Transform appleReference, mirrorReference;
        public Transform[] doorLeaves, trays, trayHangers;
        public Transform balanceBeam, passageSeal;
        public Renderer indicator;
        [Min(.1f)] public float interactionDistance=4.5f;
        [Min(0)] public float mirrorMoveSpeed=.65f;
        public float blinkInterval=1.6f, transportSpeed=2.2f, hoverHeight=.38f;
        public bool Solved { get; private set; }
        public bool ReachedExit { get; private set; }
        public bool GateOpen { get; private set; }
        public V3Object Hovered { get; private set; }
        public IReadOnlyList<V3Object> Apples => apples;
        public IReadOnlyList<V3Object> Mirrors => mirrors;
        public CognitionState State => board.State;
        readonly List<V3Object> apples=new List<V3Object>(), mirrors=new List<V3Object>();
        readonly Dictionary<ObjectKind,int> attempts=new Dictionary<ObjectKind,int>();
        Vector3[] closed, trayRest;
        Vector3 sealRest;
        Quaternion beamRest;
        int nextIndex=1,playerSlot=-1;
        float gateAmount,stableTime,tilt;
        bool learnedSelf,controlsReleased;
        Material lampMaterial;
        void Start()
        {
            Register(originalApple,apples);Register(originalMirror,mirrors);
            player.gameObject.AddComponent<V3PlayerAppearance>().Initialize(player,originalApple,presentation.view);
            closed=doorLeaves.Select(t=>t.localPosition).ToArray();trayRest=trays.Select(t=>t.position).ToArray();
            beamRest=balanceBeam.localRotation;sealRest=passageSeal.position;
            lampMaterial=new Material(indicator.sharedMaterial);lampMaterial.EnableKeyword("_EMISSION");indicator.sharedMaterial=lampMaterial;
            player.SetPresentationLocked(true);board.Panel.InputBlocked=true;
        }
        void Register(V3Object obj,List<V3Object> group)
        {
            obj.index=nextIndex++;obj.slot=-1;obj.body=obj.GetComponent<Rigidbody>();
            if(obj.kind==ObjectKind.Mirror&&!obj.body)
            {
                obj.body=obj.gameObject.AddComponent<Rigidbody>();obj.body.isKinematic=true;
                obj.body.mass=8;obj.body.interpolation=RigidbodyInterpolation.Interpolate;
                obj.body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            }
            group.Add(obj);
        }
        public bool Learn(string id)
        {
            if(!State.TryAcquireWord("v3."+id,board.Catalog.Word(id)))return false;
            library.Remember(id);presentation.ShowWord(board.Catalog.Word(id));return true;
        }
        void Update()
        {
            if(!learnedSelf&&presentation.SelfRevealReady){learnedSelf=true;Learn("i");}
            if(!presentation.Ready){presentation.SetHover(-1);return;}
            if(!controlsReleased){controlsReleased=true;player.SetPresentationLocked(false);board.Panel.InputBlocked=false;}
            if(player.WalkedDistance>=player.learnMoveDistance&&!State.Knows("move"))Learn("move");
            player.AppleIdentity=State.HasEffect(CognitionSignal.EqualApple);
            presentation.SetSplit(State.HasEffect(CognitionSignal.MirrorSelf));
            UpdateGate();UpdateMotion();UpdateBalance();UpdateInteraction();
            if(Solved)
            {
                passageSeal.position=Vector3.MoveTowards(passageSeal.position,sealRest+Vector3.up*3.8f,Time.deltaTime*1.4f);
                if(!ReachedExit&&player.transform.position.x < -20f){ReachedExit=true;presentation.Notify("Passage unlocked · V3 complete");}
            }
        }
        void UpdateGate()
        {
            bool lit=State.HasEffect(CognitionSignal.DoorPositive)||
                (!State.HasEffect(CognitionSignal.DoorNegative)&&((int)(Time.time/blinkInterval)&1)==0);
            lampMaterial.SetColor("_BaseColor",lit?Color.white:new Color(.015f,.015f,.015f));lampMaterial.SetColor("_EmissionColor",lit?Color.white*.8f:Color.black);
            bool target=lit;
            // Never close through the player; close as soon as the doorway is clear again.
            if(Mathf.Abs(player.transform.position.x-2)<.65f&&Mathf.Abs(player.transform.position.z)<1.85f)target=true;
            gateAmount=Mathf.MoveTowards(gateAmount,target?1:0,Time.deltaTime*1.6f);GateOpen=gateAmount>.95f;
            for(int i=0;i<doorLeaves.Length;i++)
                doorLeaves[i].localPosition=closed[i]+Vector3.forward*(i==0?1:-1)*1.68f/doorLeaves[i].parent.lossyScale.z*Mathf.SmoothStep(0,1,gateAmount);
        }
        int Assign(Vector3 position)
        {
            var used=new HashSet<int>(apples.Where(a=>a&&a.slot>=0).Select(a=>a.slot));if(playerSlot>=0&&player.AppleIdentity)used.Add(playerSlot);
            var choices=Enumerable.Range(0,8).Where(i=>!used.Contains(i)).ToArray();
            if(choices.Length==0)return -1;
            int tier=choices.Min(i=>i/2);
            return choices.Where(i=>i/2==tier).OrderBy(i=>(trays[i%2].position-position).sqrMagnitude).ThenBy(i=>i).First();
        }
        public Vector3 SlotPoint(int slot) => trays[slot%2].position+Vector3.up*(slot/2*.62f);
        void UpdateMotion()
        {
            bool move=State.HasEffect(CognitionSignal.AppleMove);
            foreach(var apple in apples.Where(a=>a).OrderBy(a=>a.index))
            {
                var rb=apple.body;
                if(move)
                {
                    if(apple.slot<0)apple.slot=Assign(apple.Bounds.center);
                    if(apple.slot<0)continue;
                    if(!rb.isKinematic){rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;rb.isKinematic=true;}
                    var b=apple.Bounds;var offset=b.center-apple.transform.position;
                    var target=SlotPoint(apple.slot)+Vector3.up*(b.extents.y+hoverHeight+.035f*Mathf.Sin(Time.time*2+apple.index))-offset;
                    if(apple.transform.position.y<target.y-.07f&&Vector2.Distance(new Vector2(target.x,target.z),new Vector2(apple.transform.position.x,apple.transform.position.z))>.4f)
                        target=new Vector3(apple.transform.position.x,target.y,apple.transform.position.z);
                    apple.transform.position=Vector3.MoveTowards(apple.transform.position,target,transportSpeed*Time.deltaTime);
                    apple.wasMoving=true;
                }
                else
                {
                    if(rb.isKinematic){rb.isKinematic=false;rb.useGravity=true;rb.linearVelocity=Vector3.zero;}
                    if(apple.slot>=0)
                    {
                        var p=SlotPoint(apple.slot);var b=apple.Bounds;
                        float horizontal=Vector2.Distance(new Vector2(b.center.x,b.center.z),new Vector2(p.x,p.z));
                        if(horizontal<.18f&&b.min.y<p.y+.12f&&b.min.y>p.y-.12f&&Mathf.Abs(rb.linearVelocity.y)<1.5f)
                        {
                            // A small landing magnet corrects drift without teleporting distant apples.
                            rb.isKinematic=true;apple.transform.position+=p-new Vector3(b.center.x,b.min.y-.015f,b.center.z);
                        }
                        if(!apple.wasMoving&&horizontal>.65f)apple.slot=-1;
                    }
                    apple.wasMoving=false;
                }
                if(apple.transform.position.y < -3)
                {rb.isKinematic=true;apple.transform.position=new Vector3(-6.3f, .1f, apple.copy?.75f:-.75f);apple.slot=-1;}
            }
            bool flying=player.AppleIdentity&&(move||State.HasEffect(CognitionSignal.PlayerMove))&&!State.HasEffect(CognitionSignal.PlayerNoMove)&&!State.HasEffect(CognitionSignal.AppleStop);
            if(player.AppleIdentity&&playerSlot<0&&flying)playerSlot=Assign(player.transform.position);
            if(!player.AppleIdentity)playerSlot=-1;
            player.Carried=flying&&playerSlot>=0;
            if(player.Carried)player.CarryTarget=SlotPoint(playerSlot)+Vector3.up*hoverHeight;
        }
        void FixedUpdate()
        {
            if(!presentation.Ready)return;
            bool mirrorMoves=State.HasEffect(CognitionSignal.MirrorMove);
            foreach(var mirror in mirrors.Where(m=>m))
            {
                var rb=mirror.body;
                if(mirrorMoves)
                {
                    // Detach the hung frame slightly from the wall so its depth clears the doorway jamb.
                    if(rb.isKinematic&&Mathf.Abs(rb.position.z)>1.4f&&Mathf.Abs(rb.position.z)<1.7f)
                    {
                        var detached=rb.position;detached.z-=Mathf.Sign(detached.z)*.18f;
                        rb.position=detached;mirror.transform.position=detached;
                    }
                    rb.isKinematic=false;rb.useGravity=false;
                    rb.constraints=RigidbodyConstraints.FreezeRotation|RigidbodyConstraints.FreezePositionY|RigidbodyConstraints.FreezePositionZ;
                    rb.angularVelocity=Vector3.zero;rb.linearVelocity=Vector3.left*mirrorMoveSpeed;
                }
                else if(mirror.wasMoving)
                {
                    rb.isKinematic=false;rb.constraints=RigidbodyConstraints.None;rb.useGravity=true;
                    rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;rb.WakeUp();
                }
                mirror.wasMoving=mirrorMoves;
            }
        }
        int Weight(int side)
        {
            var p=trays[side].position;int result=0;
            foreach(var a in apples.Where(a=>a))
            {
                var b=a.Bounds;
                if(!State.AppleMoving&&Mathf.Abs(b.center.x-p.x)<.4f&&Mathf.Abs(b.center.z-p.z)<.4f&&b.min.y>=p.y-.12f&&b.min.y<p.y+2.1f&&(a.body.isKinematic||a.body.linearVelocity.sqrMagnitude<.08f))result++;
            }
            var pos=player.transform.position;
            if(player.AppleIdentity&&!player.Carried&&Mathf.Abs(pos.x-p.x)<.4f&&Mathf.Abs(pos.z-p.z)<.4f&&Mathf.Abs(pos.y-p.y)<.14f)result++;
            return result;
        }
        void UpdateBalance()
        {
            int left=Weight(0),right=Weight(1);float desired=Solved?0:Mathf.Clamp(left-right,-1,1)*7f;
            tilt=Mathf.MoveTowards(tilt,desired,Time.deltaTime*12);
            balanceBeam.localRotation=beamRest*Quaternion.Euler(tilt,0,0);
            for(int i=0;i<2;i++)
            {
                var old=trays[i].position;
                var next=trayRest[i]+Vector3.up*((i==0?-1:1)*Mathf.Sin(tilt*Mathf.Deg2Rad)*.8f);
                trays[i].position=next;
                foreach(var a in apples.Where(a=>a&&a.slot>=0&&a.slot%2==i&&a.body.isKinematic&&!State.AppleMoving))a.transform.position+=next-old;
                if(player.AppleIdentity&&!player.Carried&&playerSlot>=0&&playerSlot%2==i&&Mathf.Abs(player.transform.position.y-old.y)<.15f)
                    player.GetComponent<CharacterController>().Move(next-old);
            }
            if(!Solved)
            {
                stableTime=left>0&&left==right?stableTime+Time.deltaTime:0;
                if(stableTime>1.2f){Solved=true;presentation.Notify("Balanced");}
            }
        }
        string WordFor(ObjectKind kind)
        {
            switch(kind){case ObjectKind.Apple:return "apple";case ObjectKind.Mirror:return "mirror";case ObjectKind.Door:return "door";case ObjectKind.Indicator:return "positive";default:return "equal";}
        }
        public bool CanTransform(V3Object obj)
        {
            if(obj.kind==ObjectKind.Apple)return State.HasEffect(CognitionSignal.MirrorApple)||State.HasEffect(CognitionSignal.EssenceApple);
            if(obj.kind==ObjectKind.Mirror)return State.HasEffect(CognitionSignal.MirrorMirror)||State.HasEffect(CognitionSignal.EssenceMirror);
            return false;
        }
        void UpdateInteraction()
        {
            Hovered=null;
            if(player.CanInteractWithWorld&&Cursor.lockState==CursorLockMode.Locked&&Physics.Raycast(presentation.view.ViewportPointToRay(Vector3.one*.5f),out var hit,interactionDistance,~0,QueryTriggerInteraction.Ignore))
                Hovered=hit.collider.GetComponentInParent<V3Object>();
            int status=0;
            if(Hovered)status=!State.Knows(WordFor(Hovered.kind))?1:CanTransform(Hovered)?2:3;
            presentation.SetHover(status);
            if(Hovered&&Mouse.current!=null&&Mouse.current.leftButton.wasPressedThisFrame)Interact(Hovered);
        }
        public void Interact(V3Object target)
        {
            if(!target)return;
            if(!State.Knows(WordFor(target.kind)))
            {
                Learn(WordFor(target.kind));if(target.kind==ObjectKind.Indicator)Learn("negative");return;
            }
            if(target.kind==ObjectKind.Apple)
            {if(State.HasEffect(CognitionSignal.EssenceApple))Restore(ObjectKind.Apple);else if(State.HasEffect(CognitionSignal.MirrorApple))Mirror(ObjectKind.Apple);}
            else if(target.kind==ObjectKind.Mirror)
            {if(State.HasEffect(CognitionSignal.EssenceMirror))Restore(ObjectKind.Mirror);else if(State.HasEffect(CognitionSignal.MirrorMirror))Mirror(ObjectKind.Mirror);}
        }
        public int Mirror(ObjectKind kind)
        {
            var group=kind==ObjectKind.Apple?apples:mirrors;var reference=kind==ObjectKind.Apple?appleReference:mirrorReference;
            int attempt=attempts.TryGetValue(kind,out var n)?n:0;if(attempt>=3){presentation.Notify("镜像方向已用尽");return 0;}
            attempts[kind]=attempt+1;int axis=new[]{0,2,1}[attempt];int created=0;
            foreach(var source in group.Where(a=>a).ToArray())
            {
                var local=reference.InverseTransformPoint(source.transform.position);local[axis]=-local[axis];var destination=reference.TransformPoint(local);
                var bounds=source.Bounds;var offset=bounds.center-source.transform.position;int landing=-1;
                if(kind==ObjectKind.Apple&&source.slot>=0&&!State.AppleMoving&&source.body.isKinematic)
                {
                    int other=source.slot^1;var p=SlotPoint(other);
                    if(Vector2.Distance(new Vector2(destination.x+offset.x,destination.z+offset.z),new Vector2(p.x,p.z))<.35f)
                    {destination=p+Vector3.up*(bounds.extents.y+.015f)-offset;landing=other;}
                }
                if(group.Any(a=>a&&Vector3.Distance(a.transform.position,destination)<.08f))continue;
                var blockers=Physics.OverlapBox(destination+offset,bounds.extents*.96f,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
                bool blocked=blockers.Any(c=>!c.transform.IsChildOf(source.transform)&&!(kind==ObjectKind.Mirror&&c.name=="走廊-左右")&&!(landing>=0&&c.transform.IsChildOf(trays[landing%2])));
                if(blocked)continue;
                var copy=Instantiate(source, destination, source.transform.rotation);copy.name=source.kind+" mirror "+nextIndex;copy.copy=true;
                // Reflect the facing direction without negative collider scales.
                Vector3 normal=reference.TransformDirection(axis==0?Vector3.right:axis==1?Vector3.up:Vector3.forward);
                var forward=Vector3.Reflect(source.transform.forward,normal);var up=Vector3.Reflect(source.transform.up,normal);
                if(kind==ObjectKind.Mirror)copy.transform.rotation=Quaternion.LookRotation(forward,up);
                Register(copy,group);copy.slot=landing;
                if(copy.body&&kind==ObjectKind.Apple){copy.body.isKinematic=landing>=0||State.AppleMoving;copy.body.useGravity=true;}
                Physics.SyncTransforms();created++;
            }
            presentation.Notify(created>0?"镜像":"这个方向没有可用空间");return created;
        }
        public void Restore(ObjectKind kind)
        {
            var group=kind==ObjectKind.Apple?apples:mirrors;
            foreach(var obj in group.Where(a=>a&&a.copy).ToArray()){group.Remove(obj);obj.gameObject.SetActive(false);Destroy(obj.gameObject);}
            attempts[kind]=0;presentation.Notify("本质化");
        }
        void OnDestroy(){if(lampMaterial)Destroy(lampMaterial);}
    }
}
