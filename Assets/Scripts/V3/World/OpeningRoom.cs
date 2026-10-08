using System.Collections.Generic;
using System.Linq;
using PuzzleApple.V3.Cognition;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PuzzleApple.V3
{
    [DefaultExecutionOrder(300)]
    public sealed class OpeningRoom : MonoBehaviour
    {
        public CognitionBoard board;
        public WordLibrary library;
        public FirstPersonController player;
        public V3Presentation presentation;
        public OpeningMirrorView mirrorView;
        public MainRoute mainRoute;
        public PlayerBalance playerBalance;
        public WordAnalyzer analyzer;
        public WordTransformTarget[] transformTargets=new WordTransformTarget[0];
        public OpeningObject[] objects;
        public OpeningObject doorwayMirror;
        public Transform playerSpawn;
        public GameObject secretSeal;
        [Tooltip("Trigger volume inside the optional room, used to avoid trapping the player when its passage disappears.")]
        public BoxCollider secretRoomVolume;
        public BoxCollider movementVolume;
        [Min(.1f)] public float interactionDistance=4.5f;
        [Min(.05f)] public float movementSpeed=.65f;
        [Min(.05f)] public float appleMovementSpeed=.9f;
        [Min(.3f)] public float comeDistance=1.25f;
        [Min(1)] public float awayDistance=12;
        public CognitionState State=>board.State;
        public bool SecretAvailable { get; private set; }
        public IReadOnlyDictionary<OpeningObject,OpeningObject> Copies=>copies;
        readonly Dictionary<OpeningObject,OpeningObject> copies=new Dictionary<OpeningObject,OpeningObject>();
        readonly Dictionary<string,Motion> motion=new Dictionary<string,Motion>();
        readonly Dictionary<OpeningObject,Motion> objectMotion=new Dictionary<OpeningObject,Motion>();
        Motion playerMotion;
        readonly HashSet<string> resets=new HashSet<string>();
        readonly HashSet<string> collecting=new HashSet<string>();
        // Reserve the first discovery from the first actual step, not just when its symbol appears.
        string firstDiscovery;
        bool released;
        sealed class Motion
        {
            public long established;
            public string rule,mode,destination;
            public Vector3 target,direction;
            public Transform slot;
            public DestinationSlots support;
            public bool settled;
            public bool arrivalStarted;
            public readonly SupportApproach approach=new SupportApproach();
        }
        void Start()
        {
            player.SetPresentationLocked(true);board.Panel.InputBlocked=true;secretSeal.SetActive(true);
            presentation.WordCollected+=CompleteCollection;
        }
        public bool Learn(string id)
        {
            if(State.Knows(id)||collecting.Contains(id))return false;
            if((id=="mirror"&&firstDiscovery=="move")||(id=="move"&&firstDiscovery=="mirror"))return false;
            var word=board.Catalog.Word(id);
            collecting.Add(id);
            if(id=="move"||id=="mirror")firstDiscovery=id;
            if(id=="mirror")player.MovementLocked=true;
            library.Remember(id);
            presentation.ShowWord(word);return true;
        }
        void CompleteCollection(WordDefinition word)
        {
            if(!collecting.Remove(word.Id))return;
            State.TryAcquireWord("opening."+word.Id,word);
            if(firstDiscovery==word.Id)firstDiscovery=null;
            if(word.Id=="mirror")player.MovementLocked=false;
        }
        void Update()
        {
            if(presentation.SelfRevealReady&&!State.Knows("i"))Learn("i");
            if(!presentation.Ready){presentation.SetHover(-1);return;}
            if(!released){released=true;player.SetPresentationLocked(false);board.Panel.InputBlocked=false;}
            UpdateMovementDiscovery();
            ApplySentences();
        }
        void UpdateMovementDiscovery()
        {
            if(firstDiscovery==null&&!State.Knows("move")&&player.WalkedDistance>.005f)firstDiscovery="move";
            if(player.WalkedDistance>=player.learnMoveDistance&&!State.Knows("move"))Learn("move");
        }
        public void ApplySentences()
        {
            var active=State.Groups.Where(g=>g.Effective).OrderBy(g=>g.Established).ToArray();
            if(playerBalance){playerBalance.SetIdentity(State.HasEffect(CognitionSignal.EqualBalance));playerBalance.SyncPose();}
            var mirrored=new HashSet<string>(active.Where(g=>g.Signal==CognitionSignal.OpeningMirror).Select(g=>g.Words[1].Meaning));
            var reset=new HashSet<string>(active.Where(g=>g.Signal==CognitionSignal.OpeningReset).Select(g=>g.Words[1].Meaning));
            bool core=State.HasEffect(CognitionSignal.AppleNegative)&&!reset.Contains("apple");
            bool shapeChanged=false;
            foreach(var obj in objects)
            {var appleState=obj.GetComponent<AppleState>();if(appleState){shapeChanged|=appleState.IsCore!=core;appleState.SetCore(core);}}
            foreach(var obj in objects)
            {
                bool wants=mirrored.Contains(obj.wordId)&&obj.GetComponent<ObjectReflection>().isActiveAndEnabled;
                if(!wants&&copies.ContainsKey(obj))RemoveMirror(obj);
                if(reset.Contains(obj.wordId)&&!resets.Contains(obj.wordId))ResetObject(obj);
                if(wants&&!copies.ContainsKey(obj))AddMirror(obj);
            }
            foreach(var copy in copies.Values)
            {var appleState=copy.GetComponent<AppleState>();if(appleState)appleState.SetCore(core);}
            if(shapeChanged)foreach(var obj in objects)
                if(obj.GetComponent<AppleState>())obj.GetComponent<ObjectReflection>().ResolveShapeChange(obj,mirrorView);
            if(shapeChanged&&analyzer)analyzer.RefreshAnalysis();
            foreach(var target in transformTargets.Concat(copies.Values.Select(o=>o.GetComponent<WordTransformTarget>())))
                if(target)target.SetRed(!reset.Contains(target.wordId)&&active.Any(g=>g.Signal==CognitionSignal.OpeningTransform&&g.Words[0].Meaning=="red"&&g.Words[1].Meaning==target.wordId));
            bool self=mirrored.Contains("i");
            // Dismantling the self reflection restores the original view, without a second controller.
            mirrorView.SetReflected(self);
            if(reset.Contains("i")&&!resets.Contains("i"))player.ResetPose(playerSpawn.position,playerSpawn.rotation);
            player.AppleIdentity=State.HasEffect(CognitionSignal.EqualApple);
            player.AppleCore=player.AppleIdentity&&core;
            resets.Clear();resets.UnionWith(reset);
            var moving=new HashSet<string>();
            foreach(var group in active.Where(g=>g.Signal==CognitionSignal.OpeningMove||g.Signal==CognitionSignal.PlayerMove))
            {
                string subject=group.Words[0].Meaning;
                if(!moving.Add(subject))continue;
                if(!motion.TryGetValue(subject,out var m)||m.rule!=group.RuleId||m.established!=group.Established)
                {
                    motion.Remove(subject);
                    foreach(var old in objectMotion.Keys.Where(o=>o.wordId==subject).ToArray())objectMotion.Remove(old);
                    var words=group.Words;
                    m=new Motion{established=group.Established,rule=group.RuleId,mode=words.Count==2?"wander":words[1].Meaning=="move"?"to":words[1].Meaning=="positive"?"away":"come",
                        destination=words.Count==3&&words[1].Meaning=="move"?words[2].Meaning:null};
                    var obj=objects.FirstOrDefault(o=>o.wordId==subject);
                    if(obj)obj.ClearContacts();
                    Vector3 origin=subject=="i"?player.transform.position:obj?obj.transform.position:Vector3.zero;
                    if(m.mode=="away")m.target=CastAway(origin,obj);
                    if(m.mode=="to")m.slot=ChooseSlot(m.destination,origin,subject);
                    if(m.mode=="wander")m.direction=InitialWander(origin);
                    motion[subject]=m;
                }
            }
            foreach(var key in motion.Keys.Where(k=>!moving.Contains(k)).ToArray())motion.Remove(key);
            // Resolve the physical player once, across its native name and effective identities.
            // Earliest applicable movement wins; never run two controllers for two names.
            var playerRule=active.FirstOrDefault(g=>(g.Signal==CognitionSignal.OpeningMove||g.Signal==CognitionSignal.PlayerMove)&&
                SubjectIdentity.Matches("i",g.Words[0].Meaning,active));
            if(playerRule==null)playerMotion=null;
            else if(playerMotion==null||playerMotion.rule!=playerRule.RuleId||playerMotion.established!=playerRule.Established)
            {
                playerMotion=null;
                var source=motion[playerRule.Words[0].Meaning];
                if(playerRule.Words[0].Meaning=="i")playerMotion=source;
                else
                {
                    var own=new Motion{established=source.established,rule=source.rule,mode=source.mode,destination=source.destination};
                    if(own.mode=="away")own.target=CastAway(player.transform.position,null);
                    if(own.mode=="to")own.slot=ChooseSlot(own.destination,player.transform.position,"i");
                    if(own.mode=="wander")own.direction=InitialWander(player.transform.position);
                    playerMotion=own;
                }
            }
            foreach(var obj in objects)
            {
                if(!motion.TryGetValue(obj.wordId,out var source)){objectMotion.Remove(obj);continue;}
                if(objectMotion.TryGetValue(obj,out var existing)&&existing.established==source.established&&existing.rule==source.rule)
                    continue;
                if(objects.First(o=>o.wordId==obj.wordId)==obj)objectMotion[obj]=source;
                else
                {
                    var own=new Motion{established=source.established,rule=source.rule,mode=source.mode,destination=source.destination};
                    if(own.mode=="away")own.target=CastAway(obj.transform.position,obj);
                    if(own.mode=="to")own.slot=ChooseSlot(own.destination,obj.transform.position,obj.wordId);
                    if(own.mode=="wander")own.direction=InitialWander(obj.transform.position);
                    obj.ClearContacts();objectMotion[obj]=own;
                }
            }
            if(analyzer)analyzer.SetRequest(active.FirstOrDefault(g=>g.Signal==CognitionSignal.OpeningMove&&g.Words.Count==3&&g.Words[2].Meaning=="analyzer"));
            ReconcileDestinations();
            player.SentenceForward=playerMotion!=null&&playerMotion.mode=="wander";
            player.SentenceTravel=playerMotion!=null&&playerMotion.mode!="wander";
            if(player.SentenceTravel)player.SentenceTarget=Target(playerMotion,player.transform.position,"i");
            player.Carried=player.AppleIdentity&&player.SentenceTravel&&playerMotion.mode=="to"&&playerMotion.slot;
            player.CarrySettled=player.Carried&&playerMotion.destination=="equal"&&mainRoute.trayPlaces.OccupiedPlace(player)==playerMotion.slot&&player.transform.position.y<playerMotion.slot.position.y+.13f;
            if(player.Carried)player.CarryTarget=player.SentenceTarget;
            foreach(var obj in objects)if(!motion.ContainsKey(obj.wordId))obj.Stop();
            if(playerBalance)foreach(var pair in objectMotion)
                if(pair.Value.mode!="to"||pair.Value.destination!="equal")
                {playerBalance.Release(pair.Key);if(copies.TryGetValue(pair.Key,out var copy))playerBalance.Release(copy);}
        }
        public bool IsSettledOnSupport(OpeningObject obj)
        {
            var original=copies.FirstOrDefault(p=>p.Value==obj).Key;
            return !objectMotion.TryGetValue(original?original:obj,out var m)||m.settled;
        }
        void ReconcileDestinations()
        {
            if(!mainRoute)return;
            mainRoute.RefreshTrayOccupancy();
            var supports=new List<DestinationSlots>{mainRoute.trayPlaces};
            if(playerBalance&&playerBalance.Active)supports.Add(playerBalance.slots);
            var requests=supports.ToDictionary(s=>s,s=>new List<DestinationSlots.Request>());
            var arrivals=objectMotion.Where(p=>p.Value.mode=="to"&&p.Value.destination=="equal").OrderBy(p=>p.Value.support&&supports.Contains(p.Value.support)?0:1).ToArray();
            foreach(var pair in arrivals)
            {
                var obj=pair.Key;var m=pair.Value;
                var companion=copies.TryGetValue(obj,out var copy)?copy:null;
                if(!m.support||!supports.Contains(m.support))
                {
                    m.support=supports.OrderBy(s=>s.OccupiedPlace(obj)?-1:s.places.Min(t=>(t.position-obj.transform.position).sqrMagnitude))
                        .FirstOrDefault(s=>Enumerable.Range(0,s.places.Length).Select(s.Occupant)
                            .Concat(requests[s].SelectMany(q=>new[]{q.owner,q.companion})).Concat(new Component[]{obj,companion}).Where(c=>c).Distinct().Count()<=s.places.Length);
                    m.settled=false;m.arrivalStarted=false;
                }
                if(!m.support){m.slot=null;continue;}
                var selected=m.support;
                requests[selected].Add(new DestinationSlots.Request{owner=obj,origin=obj.transform.position,companion=companion,
                    companionPoint=p=>selected.places.OrderByDescending(t=>(t.position-p).sqrMagnitude).First().position});
                if(companion&&(playerBalance&&selected==playerBalance.slots||obj.GetComponent<ObjectReflection>().HasCustomAxis))
                    obj.GetComponent<ObjectReflection>().RetargetAxis(playerBalance&&selected==playerBalance.slots?playerBalance.axis:obj.reflectionPlane);
            }
            if(playerMotion!=null&&playerMotion.mode=="to"&&playerMotion.destination=="equal")
                requests[mainRoute.trayPlaces].Add(new DestinationSlots.Request{owner=player,origin=player.transform.position});
            foreach(var support in supports)support.Reconcile(requests[support]);
            foreach(var pair in arrivals)
            {
                var m=pair.Value;var slot=m.support?m.support.PlaceFor(pair.Key):null;
                if(m.slot!=slot)m.settled=false;
                m.slot=slot;
                bool occupied=slot&&m.support.OccupiedPlace(pair.Key)==slot;
                if(!occupied)m.settled=false;
                else if(!m.arrivalStarted)m.settled=true;
            }
            if(playerMotion!=null&&playerMotion.mode=="to"&&playerMotion.destination=="equal")
                playerMotion.slot=mainRoute.trayPlaces.PlaceFor(player);
        }
        public void AddMirror(OpeningObject obj)
        {
            if(playerBalance&&playerBalance.Carries(obj))obj.GetComponent<ObjectReflection>().TrackAxis(playerBalance.axis);
            // Eligibility is sampled once, before any subsequent movement; copies never add doors later.
            if(obj==doorwayMirror){SecretAvailable=obj.AtHome();secretSeal.SetActive(!SecretAvailable);}
            var copy=obj.GetComponent<ObjectReflection>().Create(obj,mirrorView);
            if(copy)copies.Add(obj,copy);
            else if(obj==doorwayMirror){SecretAvailable=false;secretSeal.SetActive(true);}
        }
        public void RemoveMirror(OpeningObject obj)
        {
            obj.GetComponent<ObjectReflection>().Remove();copies.Remove(obj);
            if(obj==doorwayMirror)
            {
                if(SecretAvailable&&secretRoomVolume.bounds.Contains(player.transform.position+Vector3.up*.5f))
                    player.Teleport(playerSpawn.position);
                SecretAvailable=false;secretSeal.SetActive(true);
            }
        }
        public void ResetObject(OpeningObject obj){RemoveMirror(obj);obj.Restore();}
        Vector3 ReflectPoint(OpeningObject obj,Vector3 point)=>obj.GetComponent<ObjectReflection>().Point(obj,point,mirrorView);
        Vector3 InitialWander(Vector3 point)
        {
            var inward=movementVolume.bounds.center-point;inward.y=0;
            return (inward.normalized+Vector3.right*.55f+Vector3.up*.15f).normalized;
        }
        void FixedUpdate()
        {
            if(!presentation.Ready)return;
            // Supports may have moved since the previous physics step, even while
            // the movement sentence is absent. Refresh that shared starting pose.
            foreach(var pair in copies)pair.Key.GetComponent<ObjectReflection>().Sync(pair.Key,mirrorView,false);
            Physics.SyncTransforms();
            foreach(var obj in objects)
            {
                if(!objectMotion.TryGetValue(obj,out var m)){obj.Stop();continue;}
                obj.GetComponent<ObjectReflection>().AdvanceAxis(obj,mirrorView,Time.fixedDeltaTime);
                if(m.settled){obj.Stop();continue;}
                m.arrivalStarted=true;
                float speed=obj.wordId=="apple"?appleMovementSpeed:movementSpeed;
                var rb=obj.Body;
                rb.isKinematic=copies.ContainsKey(obj)||m.slot;rb.useGravity=false;
                Vector3 velocity;
                if(m.mode=="to"&&m.destination=="analyzer"&&analyzer)
                {
                    if(!analyzer.TryTarget(obj,out var target)){obj.Stop();continue;}
                    velocity=Vector3.ClampMagnitude((target-obj.transform.position)*2,speed);
                }
                else if(m.mode=="wander")
                {
                    m.direction=obj.ReflectMotion(m.direction);
                    velocity=m.direction*speed;
                }
                else
                {
                    var target=Target(m,obj.transform.position,obj.wordId);
                    if(m.slot)
                    {
                        var bounds=obj.PhysicalBounds;
                        target+=new Vector3(obj.transform.position.x-bounds.center.x,obj.transform.position.y-bounds.min.y+.015f,obj.transform.position.z-bounds.center.z);
                        target=m.approach.Next(obj,bounds,target,.45f);
                    }
                    velocity=Vector3.ClampMagnitude((target-obj.transform.position)*2,speed);
                }
                var reflection=obj.GetComponent<ObjectReflection>();var before=rb.position;
                if(reflection.Copy)
                {
                    var normal=reflection.MovePair(obj,mirrorView,velocity*Time.fixedDeltaTime);
                    if(m.mode=="wander"&&normal.sqrMagnitude>.5f&&Vector3.Dot(m.direction,normal)<0)
                        m.direction=Vector3.Reflect(m.direction,normal).normalized;
                }
                else if(m.slot)obj.MoveToSupport(velocity*Time.fixedDeltaTime);
                else rb.linearVelocity=velocity;
                if(m.slot&&m.destination=="equal"&&velocity.y<0&&(rb.position-before).sqrMagnitude<.0000001f&&m.support&&m.support.OccupiedPlace(obj)==m.slot)m.settled=true;
            }
        }
        Vector3 Target(Motion m,Vector3 origin,string subject)
        {
            if(m.mode=="away")return m.target;
            if(m.mode=="come")return FrontOfPlayer(origin.y);
            if(m.slot)return m.slot.position;
            if(m.destination==subject)return origin;
            if(m.destination=="i")return FrontOfPlayer(origin.y);
            if(mainRoute&&m.destination=="door")
            {var doorTarget=mainRoute.gateFrame.position-mainRoute.transform.right*1.1f;doorTarget.y=origin.y;return doorTarget;}
            var obj=objects.Where(o=>o.wordId==m.destination).OrderBy(o=>(o.transform.position-origin).sqrMagnitude).FirstOrDefault();
            if(!obj)return origin;
            var direction=Vector3.ProjectOnPlane(origin-obj.transform.position,Vector3.up).normalized;
            if(direction.sqrMagnitude<.01f)direction=obj.transform.forward;
            var p=obj.Bounds.ClosestPoint(origin)+direction*.55f;
            if(subject=="i")p.y=origin.y;
            return p;
        }
        public bool MovesToAnalyzer(OpeningObject obj)=>motion.TryGetValue(obj.wordId,out var m)&&m.mode=="to"&&m.destination=="analyzer";
        public SentenceOutcome Outcome(CognitionState.Group group)
        {
            if(!group.Recognized)return SentenceOutcome.InvalidGrammar;
            if(group.Conflicted)return SentenceOutcome.Conflicted;
            if(group.Signal==CognitionSignal.OpeningTransform)
            {
                string noun=group.Words[1].Meaning;
                if(!transformTargets.Any(t=>t&&t.wordId==noun&&t.Supports(group.Words[0].Meaning)))return SentenceOutcome.Unsupported;
                if(State.Groups.Any(g=>g.Effective&&g.Signal==CognitionSignal.OpeningReset&&g.Words[1].Meaning==noun))return SentenceOutcome.Resetting;
            }
            if(analyzer&&group.Signal==CognitionSignal.OpeningMove&&group.Words.Count==3&&group.Words[2].Meaning=="analyzer"&&!analyzer.Reserved)
                return SentenceOutcome.WaitingForPlace;
            return SentenceOutcome.Active;
        }
        Vector3 FrontOfPlayer(float height)
        {
            Vector3 p=player.transform.position+player.transform.forward*comeDistance;p.y=height;
            var origin=player.transform.position+Vector3.up*Mathf.Max(.5f,height-player.transform.position.y);
            foreach(var hit in Physics.RaycastAll(origin,player.transform.forward,comeDistance,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
            {
                if(hit.collider.GetComponentInParent<OpeningObject>()||hit.collider.GetComponentInParent<FirstPersonController>())continue;
                p=hit.point-player.transform.forward*.45f;p.y=height;break;
            }
            return p;
        }
        Vector3 CastAway(Vector3 origin,OpeningObject subject)
        {
            var direction=Vector3.ProjectOnPlane(origin-player.transform.position,Vector3.up).normalized;
            if(direction.sqrMagnitude<.01f)direction=player.transform.forward;
            float distance=awayDistance;
            foreach(var hit in Physics.RaycastAll(origin+Vector3.up*.05f,direction,awayDistance,~0,QueryTriggerInteraction.Ignore))
            {
                if(hit.collider.GetComponentInParent<FirstPersonController>())continue;
                if(subject&&hit.collider.GetComponentInParent<OpeningObject>()==subject)continue;
                distance=Mathf.Min(distance,Mathf.Max(0,hit.distance-(subject?subject.Bounds.extents.magnitude:.4f)));
            }
            return origin+direction*distance;
        }
        Transform ChooseSlot(string word,Vector3 origin,string subject)
        {
            // Capacity destinations resolve once for all physical recipients in ReconcileDestinations.
            if(mainRoute&&word=="equal")return null;
            var reserved=motion.Values.Concat(objectMotion.Values).Concat(playerMotion==null?new Motion[0]:new[]{playerMotion}).Distinct().ToArray();
            return objects.Where(o=>o.wordId==word).SelectMany(o=>o.destinationSlots).Where(t=>t)
                .Where(t=>!reserved.Any(m=>m.slot==t)&&!objects.Any(o=>o.wordId!=subject&&Vector3.Distance(o.transform.position,t.position)<.4f))
                .OrderBy(t=>(t.position-origin).sqrMagnitude).FirstOrDefault();
        }
        // Interaction is read after the reflected camera has been posed; collecting is the only click action.
        void LateUpdate()
        {
            // Render after moving supports, using the source's interpolated pose. Never carry a copy twice.
            foreach(var pair in copies)pair.Key.GetComponent<ObjectReflection>().Sync(pair.Key,mirrorView,false);
            if(!presentation.Ready||!player.CanInteractWithWorld){presentation.SetHover(-1);return;}
            var view=presentation.view.transform;
            bool found=Physics.Raycast(view.position,view.forward,out var hit,interactionDistance,~0,QueryTriggerInteraction.Ignore);
            var hitObject=found?hit.collider.GetComponentInParent<OpeningObject>():null;
            var collectible=found?hit.collider.GetComponentInParent<OpeningCollectible>():null;
            var screen=found?hit.collider.GetComponentInParent<AnalyzerScreen>():null;
            var words=hitObject?new[]{hitObject.wordId}:collectible?collectible.words:new string[0];
            if(screen&&screen.analyzer.HasResult)words=words.Concat(screen.analyzer.ResultWords()).Distinct().ToArray();
            bool canLearn=words.Any(id=>!State.Knows(id)&&!collecting.Contains(id)&&!(id=="mirror"&&firstDiscovery=="move"));
            presentation.SetHover(canLearn?1:0);
            if(canLearn&&Mouse.current!=null&&Mouse.current.leftButton.wasPressedThisFrame)foreach(var id in words)Learn(id);
        }
        void OnDestroy()
        {
            if(presentation)presentation.WordCollected-=CompleteCollection;
            if(player){player.MovementLocked=false;player.SentenceForward=false;}
        }
    }
}
