using System.Collections.Generic;
using UnityEngine;

namespace PuzzleApple.V5
{
    public sealed class PassageJourney : MonoBehaviour
    {
        public WorldEntry entry;
        public Material whitePath,blackSurround,roomMaterial,lampMaterial;
        public Shader floorPortalShader;
        public UnityEngine.Rendering.Volume glowVolume;
        [Min(8)] public float pathLength=26;
        [Min(0)] public float platformDockGap=.8f;
        [Min(.1f)] public float platformSpeed=2.2f;
        public PassagePlatform Platform {get;private set;}
        public Vector3 roomSize=new Vector3(16,8,18);
        [Range(2,24)] public int lampCount=10;
        public Vector2 lampScaleRange=new Vector2(.12f,.35f);
        [Tooltip("Shared indoor lighting rule: lamp output from lamp size, room fill from total light. Empty uses Resources/V5/RoomLightStyle.")]
        public RoomLightStyle lightStyle;
        public RoomLightStyle LightStyle=>lightStyle?lightStyle:lightStyle=RoomLightStyle.Default;
        public RoomLightBalance FarRoomFill {get;private set;}
        [Range(0,2)] public float emissionStrength=.35f;
        [Min(.1f)] public float lampSpeed=1.2f,lightFadeDuration=1.5f,doorDuration=1.65f;
        [Range(90,170)] public float openAngle=135f;
        [Min(0)] public float doorCollectionDistance=10f;
        [Range(0,.1f)] public float fogDensity=.018f;
        [Range(0,.05f)] public float corridorFogDensity=.012f;
        [Header("Corridor art trials")]
        public bool pureWhiteCorridor=true;
        public bool ViewBeyondChamber {get;private set;}
        public float CorridorBlend {get;private set;}
        public bool collideLamps=true;
        public ObservableBehaviour movementObservation;
        public FloorImpactSurface floorSurface;
        public FloorDirectedMotion floorMotion;
        public bool LampsMoving=>entry && entry.sentenceRuntime && entry.sentenceRuntime.ControlsLamps(LightSpace.FarRoom)
            ? Brightness>0 && entry.sentenceRuntime.HasEffect(Cognition.CognitionSignal.LightMove,LightSpace.FarRoom)
            : DemonstrationLampMoving(Brightness>0);
        // A brightness command preserves an autonomous drift until the player changes the motion sentence.
        public int RelitDriftRevision(LightSpace space)=>entry.sentenceRuntime.HasLampMotion(space)?-1:entry.sentenceRuntime.LightMotionRevision(space);
        // Shared by the drifting room and the corridor stars: learning only the verb
        // does not transfer the preset motion to player-owned sentences.
        LightSpace roomsIntroduced, roomsWithMoveKnown;
        void IntroduceRoom(LightSpace space)
        {
            if((roomsIntroduced & space)!=0)return;
            roomsIntroduced |= space;
            if(entry.board.State.Knows("move"))roomsWithMoveKnown |= space;
        }
        public bool DemonstrationLampMoving(bool isOn,int relitRevision=-1,LightSpace space=LightSpace.FarRoom)
        {
            bool approachingWithMove=space==LightSpace.FarRoom && (roomsIntroduced&space)==0 &&
                entry && entry.board && entry.board.State!=null && entry.board.State.Knows("move");
            if(approachingWithMove || (roomsWithMoveKnown & space)!=0)
                return isOn && entry.sentenceRuntime.HasEffect(Cognition.CognitionSignal.LightMove,space);
            return entry&&entry.board&&entry.board.State!=null&&movementObservation&&
            (entry.sentenceRuntime && entry.sentenceRuntime.ControlsLamps(space)
                ? !isOn||relitRevision==entry.sentenceRuntime.LightMotionRevision(space)||entry.sentenceRuntime.HasEffect(Cognition.CognitionSignal.LightMove,space)
                : movementObservation.Demonstrates(entry.board.Catalog,Cognition.CognitionSignal.LightMove));
        }
        [Tooltip("Prototype choice, not a final design restriction.")]
        public bool allowCorridorClosure=true;
        public bool BeyondChamber {get;private set;}
        public bool InSecondRoom {get;private set;}
        public MoveTutorialRoom Tutorial {get;private set;}
        public float RoomStart=>Tutorial?Tutorial.ExitZ+pathLength:pathLength;
        public bool InTutorial {get;private set;}
        public bool InReturnPath {get;private set;}
        public bool InCorridor=>BeyondChamber&&!InSecondRoom&&!InTutorial;
        public CorridorStars ReturnStars=>secondStars;
        public CorridorStars ActiveStars=>InReturnPath?secondStars:Stars;
        [SerializeField] CorridorStars firstStars, secondStars;
        public float DoorwayLighting {get;private set;}
        public float RoomBloom {get;private set;}
        public bool DoorOpen {get;private set;}
        public float DoorProgress {get;private set;}
        public bool LightsOn {get;private set;}
        public bool LampsLit=>Brightness>0;
        public float Brightness {get;private set;}
        public bool LightTransitionEnabled {get;private set;}
        public bool DoorTransitionEnabled {get;private set;}
        public Transform Root {get;private set;}
        public WhiteAscent Ascent {get;private set;}
        public ChargeDoor EndingDoor {get;private set;}
        public Transform Door {get;private set;}
        public IReadOnlyList<DriftingLight> Lamps=>lamps;
        readonly List<DriftingLight> lamps=new List<DriftingLight>();
        Material lampInk;
        Material doorEdge,corridorMaterial,returnEdge,returnMaterial;
        WallPassage passage;
        public CorridorStars Stars => firstStars;
        public float CorridorBrightness=>ActiveStars?ActiveStars.Brightness:1;
        public bool CorridorOn=>!ActiveStars||ActiveStars.On;
        public bool CorridorLightTransitionEnabled=>ActiveStars&&ActiveStars.TransitionEnabled;
        public bool CorridorLightChanging=>(firstStars&&firstStars.Changing)||(secondStars&&secondStars.Changing);
        readonly List<Collider> rails=new List<Collider>();
        public bool IsRail(Collider collider)=>rails.Contains(collider);
        Transform doorHinge;
        Collider[] doorColliders;
        CharacterController doorPlayer;
        bool ignoringDoorPlayer;
        bool cycleOpening = true;

        readonly List<Renderer> roomEdges=new List<Renderer>();
        Renderer doorLeaf,hingePin;
        MaterialPropertyBlock doorSurface;
        Mesh doorMesh;
        MaterialPropertyBlock backSurface, coverSurface;
        // The room-side face keeps the wall's material; an overlay with the handle side's finish fades in as the door opens.
        Renderer doorCover;
        Material coverMaterial;

        Color emission;
        Color initialBackground;
        UnityEngine.Rendering.Universal.UniversalAdditionalCameraData cameraData;
        bool initialPostProcessing;
        float pathWidth;
        public float PathWidth=>pathWidth;
        float chamberBoundary;
        public void Build(Transform entrance,float width,float openingHeight,float wallThickness=.4f,Material openingEdgeMaterial=null)
        {
            Root=new GameObject("Passage and dark room - runtime").transform;Root.SetParent(transform,false);
            Root.SetPositionAndRotation(entrance.position,entrance.rotation);pathWidth=width;
            chamberBoundary=-wallThickness*.5f;
            passage=entrance.GetComponentInParent<WallPassage>();
            initialBackground=entry.player.view.GetComponent<Camera>().backgroundColor;
            cameraData=entry.player.view.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            initialPostProcessing=cameraData&&cameraData.renderPostProcessing;
            // The sill supplies the walking surface across the full wall thickness.
            float pathStart=wallThickness*.5f;
            corridorMaterial=new Material(whitePath);
            if(openingEdgeMaterial)doorEdge=new Material(openingEdgeMaterial);
            BuildPath(pathStart, pathLength-.15f, openingHeight, corridorMaterial, doorEdge);
            lampInk=new Material(lampMaterial);emission=lampInk.GetColor("_EmissionColor");
            lampInk.EnableKeyword("_EMISSION");
            lampInk.SetFloat("_EnvironmentReflections",0);lampInk.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            lampInk.SetFloat("_SpecularHighlights",0);lampInk.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            Tutorial = gameObject.AddComponent<MoveTutorialRoom>();
            Tutorial.Build(this, openingHeight,lampInk);
            returnMaterial=new Material(whitePath);
            if(openingEdgeMaterial)returnEdge=new Material(openingEdgeMaterial);
            BuildPath(Tutorial.ExitZ+.15f, RoomStart-.15f, openingHeight, returnMaterial, returnEdge, true);
            // The entrance, path, door leaf and destination opening share one size.
            float doorWidth=width,doorHeight=openingHeight;
            doorHinge=new GameObject("Door hinge").transform;
            doorHinge.SetParent(Root,false);
            doorHinge.localPosition=new Vector3(-doorWidth*.5f,0,pathLength-.23f);
            doorLeaf=Block("White hinged door",new Vector3(0,doorHeight*.5f,pathLength),new Vector3(doorWidth,doorHeight,.3f),corridorMaterial);
            Door=doorLeaf.transform;
            Door.SetParent(doorHinge,true);
            PrepareDoorBack();
            Door.gameObject.layer=29;
            var knob=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            knob.name="Round handle";
            knob.transform.SetParent(doorHinge,false);
            knob.transform.localPosition=new Vector3(doorWidth-.32f,doorHeight*.43f,-.02f);
            knob.transform.localScale=Vector3.one*.26f;
            knob.gameObject.layer=29;
            var knobRenderer=knob.GetComponent<Renderer>();
            knobRenderer.sharedMaterial=corridorMaterial;
            var knobInk=new MaterialPropertyBlock();
            knobInk.SetFloat("_CorridorPureWhite",pureWhiteCorridor?1:0);
            knobInk.SetFloat("_RoundOutline",1);
            knobRenderer.SetPropertyBlock(knobInk);
            var pin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pin.name = "Door hinge pin";
            pin.transform.SetParent(doorHinge, false);
            pin.transform.localPosition = Vector3.up * (doorHeight * .5f);
            pin.transform.localScale = new Vector3(.25f, doorHeight * .39f, .25f);
            pin.layer = Door.gameObject.layer;
            hingePin=pin.GetComponent<Renderer>();
            hingePin.sharedMaterial=doorLeaf.sharedMaterial;
            doorColliders = doorHinge.GetComponentsInChildren<Collider>();
            doorPlayer = entry.player.GetComponent<CharacterController>();
            UpdateDoorAppearance();
            if(openingEdgeMaterial)
            {
                doorEdge.SetFloat("_Visibility",1);
                // Keep only narrow flowing outlines, outside the stationary white leaf.
                foreach(float depth in new[]{.16f,-.16f})foreach(float side in new[]{-1f,1f})
                {
                    var upright=WallPassage.CreateFilament(Root,doorEdge,"Open door vertical filaments",new Vector3(side*(doorWidth*.5f-.05f),doorHeight*.5f,pathLength+depth),doorHeight);
                    var cross=WallPassage.CreateFilament(Root,doorEdge,"Open door horizontal filaments",new Vector3(0,side<0?.05f:doorHeight-.05f,pathLength+depth),doorWidth);
                    cross.transform.localRotation=Quaternion.Euler(0,0,90);
                    if(depth>0){roomEdges.Add(upright);roomEdges.Add(cross);upright.enabled=cross.enabled=false;}
                }
            }
            float roomStart=RoomStart-.15f;
            float sideWidth=(roomSize.x-doorWidth)*.5f;
            foreach(float side in new[]{-1f,1f})
                RoomBlock("Door wall",new Vector3(side*(doorWidth*.5f+sideWidth*.5f),roomSize.y*.5f,RoomStart),new Vector3(sideWidth,roomSize.y,.3f),Vector3.forward);
            RoomBlock("Door lintel",new Vector3(0,(doorHeight+roomSize.y)*.5f,RoomStart),new Vector3(doorWidth,roomSize.y-doorHeight,.3f),Vector3.forward);
            RoomBlock("Room floor",new Vector3(0,-.15f,(roomStart+RoomStart+roomSize.z)*.5f),new Vector3(roomSize.x,.3f,RoomStart+roomSize.z-roomStart),Vector3.up);
            if(floorSurface)floorSurface.Build(Root.Find("Room floor").GetComponent<Renderer>());
            RoomBlock("Room ceiling",new Vector3(0,roomSize.y+.15f,RoomStart+roomSize.z*.5f),new Vector3(roomSize.x,.3f,roomSize.z),Vector3.down);
            EndingDoor=Instantiate(Resources.Load<GameObject>("V5/IntroEnding"),Root,false).GetComponent<ChargeDoor>();
            EndingDoor.Build(this);
            Ascent=EndingDoor.GetComponentInChildren<WhiteAscent>();Ascent.Build(this,EndingDoor);
            float farZ=RoomStart+roomSize.z,opening=EndingDoor.MouthWidth,top=EndingDoor.MouthHeight;
            float wing=(roomSize.x-opening)*.5f;
            foreach(float side in new[]{-1f,1f})RoomBlock("Room far wall",new Vector3(side*(opening+wing)*.5f,roomSize.y*.5f,farZ),new Vector3(wing,roomSize.y,.3f),Vector3.back);
            RoomBlock("Room far wall",new Vector3(0,(top+roomSize.y)*.5f,farZ),new Vector3(opening,roomSize.y-top,.3f),Vector3.back);
            foreach(float side in new[]{-1f,1f})RoomBlock("Room side wall",new Vector3(side*roomSize.x*.5f,roomSize.y*.5f,RoomStart+roomSize.z*.5f),new Vector3(.3f,roomSize.y,roomSize.z),Vector3.left*side);
            if(floorSurface)floorSurface.BuildWalls();
            EndingDoor.BindSurfaces(Root.Find("Room far wall").GetComponent<Renderer>(),Root.Find("Room floor").GetComponent<Renderer>());
            FarRoomFill=RoomLightBalance.Create(Root,"Far room light balance",new Vector3(0,roomSize.y*.5f,RoomStart+roomSize.z*.5f),roomSize,LightStyle);
            for(int i=0;i<lampCount;i++)
            {
                float scale=Random.Range(Mathf.Max(.05f,lampScaleRange.x),Mathf.Max(lampScaleRange.x,lampScaleRange.y));
                var r=Block("Drifting light "+i,new Vector3(Random.Range(-roomSize.x*.4f,roomSize.x*.4f),Random.Range(1,roomSize.y-1),RoomStart+Random.Range(1,roomSize.z-1)),Vector3.one*scale,lampInk);
                var go=r.gameObject;go.layer=29;go.transform.rotation=Quaternion.FromToRotation(Vector3.one.normalized,Vector3.down);
                go.GetComponent<BoxCollider>().enabled=false;Destroy(go.GetComponent<BoxCollider>());var sphere=go.AddComponent<SphereCollider>();sphere.radius=.65f;
                var motion=go.AddComponent<DriftingLight>();motion.journey=this;motion.speed=lampSpeed;motion.SlotIndex=i;
                var lightObject=new GameObject("Soft local light",typeof(Light));lightObject.transform.SetParent(go.transform,false);
                var light=lightObject.GetComponent<Light>();light.type=LightType.Point;light.range=LightStyle.LampRange(LightStyle.LampIntensity(scale));light.intensity=0;light.shadows=LightShadows.Soft;light.cullingMask=~0;motion.glow=light;
                r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;lamps.Add(motion);
            }
            if(!collideLamps)for(int a=0;a<lamps.Count;a++)for(int b=a+1;b<lamps.Count;b++)Physics.IgnoreCollision(lamps[a].GetComponent<SphereCollider>(),lamps[b].GetComponent<SphereCollider>());
            RefreshCollisionExclusions();
            if(movementObservation)
            {
                movementObservation.members=lamps.ConvertAll(l=>l.GetComponent<SphereCollider>()).ToArray();
                movementObservation.Available=false;
            }
            firstStars.Bind(this, 0, openingHeight, wallThickness, corridorMaterial, doorEdge);
            secondStars.Bind(this, Tutorial.ExitZ, openingHeight, .3f, returnMaterial, returnEdge);
            ApplyLight();
        }
        void BuildPath(float pathStart,float roomStart,float h,Material pathMaterial,Material edge,bool movingFloor=false)
        {
            float centre=(pathStart+roomStart)*.5f, length=roomStart-pathStart;
            float inset=Mathf.Min(length*.5f,pathWidth*.5f+platformDockGap);
            var path=Block(movingFloor?"Rail platform":"White path",new Vector3(0,-.15f,movingFloor?pathStart+inset:centre),
                new Vector3(pathWidth,.3f,movingFloor?pathWidth:length),pathMaterial);
            if(movingFloor)
            {
                Platform=path.gameObject.AddComponent<PassagePlatform>();
                float stopInset=Mathf.Min(length*.5f,pathWidth*.5f+.02f);
                Platform.Bind(this,pathStart+stopInset,roomStart-stopInset);
            }
            var pathSurface=new MaterialPropertyBlock();pathSurface.SetFloat("_FrontWhite",0);
            pathSurface.SetColor("_ShadeColor",new Color(.42f,.42f,.42f,1));
            pathSurface.SetFloat("_CorridorPureWhite",pureWhiteCorridor?1:0);path.SetPropertyBlock(pathSurface);
            // Black enclosure hides the chamber and sky, while invisible rails retain the path boundary.
            foreach(float side in new[]{-1f,1f})
            {
                Block("Black surround",new Vector3(side*14,5,centre),new Vector3(.2f,14,length),blackSurround);
                if(movingFloor)continue;
                var rail=Block("Path boundary",new Vector3(side*(pathWidth*.5f+.1f),2,centre),new Vector3(.2f,4,length),blackSurround);
                rail.enabled=false;
                rails.Add(rail.GetComponent<Collider>());
            }
            Block("Black above",new Vector3(0,12,centre),new Vector3(28,.2f,length),blackSurround);
            var below=Block("Black below",new Vector3(0,movingFloor?-8:-2,centre),new Vector3(28,.2f,length),blackSurround);
            if(movingFloor)below.GetComponent<Collider>().enabled=false;
            if(!edge)return;
            if(movingFloor)
            {
                foreach(float end in new[]{Tutorial.ExitZ,RoomStart})
                foreach(float depth in new[]{-.16f,.16f})
                foreach(float sign in new[]{-1f,1f})
                {
                    WallPassage.CreateFilament(Root,edge,"Passage upright",new Vector3(sign*(pathWidth*.5f-.05f),h*.5f,end+depth),h);
                    var bar=WallPassage.CreateFilament(Root,edge,"Passage crossbar",new Vector3(0,sign<0?.05f:h-.05f,end+depth),pathWidth);
                    bar.transform.localRotation=Quaternion.Euler(0,0,90);
                }
            }
            foreach(float x in new[]{-pathWidth*.5f,pathWidth*.5f})
            {
                var line=WallPassage.CreateFilament(Root,edge,"Path edge",new Vector3(x,.012f,centre),length);
                line.transform.localRotation=Quaternion.Euler(90,0,0);
            }
        }
        public void RemoveLamp(DriftingLight lamp)
        {
            lamps.Remove(lamp);
            if(movementObservation)movementObservation.members=lamps.ConvertAll(l=>l.GetComponent<SphereCollider>()).ToArray();
        }
        public void RefreshCollisionExclusions()
        {
            var player=entry.player.GetComponent<CharacterController>();
            foreach(var lamp in lamps)Physics.IgnoreCollision(lamp.GetComponent<SphereCollider>(),player,true);
            if (ignoringDoorPlayer) SetDoorPlayerCollision(true);
        }
        Renderer Block(string name,Vector3 position,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(Root,false);go.transform.localPosition=position;go.transform.localScale=size;
            var r=go.GetComponent<Renderer>();r.sharedMaterial=material;return r;
        }
        void RoomBlock(string name,Vector3 p,Vector3 size,Vector3 inward)
        {
            var surface=Block(name,p,size,roomMaterial);
            surface.gameObject.layer=29;
            var properties=new MaterialPropertyBlock();
            properties.SetVector("_RoomShellInward",inward);
            surface.SetPropertyBlock(properties);
        }
        public void OpenDoor()=>SetDoor(true);
        public void SetDoor(bool value){DoorOpen=value;DoorTransitionEnabled=true;}
        public void HoldDoor()=>DoorTransitionEnabled=false;
        WhiteDoorState whiteDoor;
        // The white door between corridor and tutorial room, as commanded by on/off door.
        public ISwitchableDoor WhiteDoor => whiteDoor ??= new WhiteDoorState(this);
        sealed class WhiteDoorState : ISwitchableDoor
        {
            readonly PassageJourney journey;
            public WhiteDoorState(PassageJourney owner) { journey=owner; }
            public bool WouldChange(bool open) => open ? journey.DoorProgress<.999f : journey.DoorProgress>.001f;
            public void SetOpen(bool open) => journey.SetDoor(open);
            public void Hold() => journey.HoldDoor();
            public void Release() { }
        }
        // Motion-sentence targets per space; each object keeps its own motion presentation.
        MotionTarget whiteDoorMotion, corridorMotion, farRoomMotion;
        public ISentenceMovable WhiteDoorMotion => whiteDoorMotion ??= new MotionTarget(s => s==Cognition.CognitionSignal.DoorMove && CanCycleDoor);
        public ISentenceMovable CorridorMotion => corridorMotion ??= new MotionTarget(s => s==Cognition.CognitionSignal.LightMove && CorridorBrightness>0);
        public ISentenceMovable FarRoomMotion => farRoomMotion ??= new MotionTarget(FarRoomCanMove);
        bool FarRoomCanMove(Cognition.CognitionSignal s)
        {
            switch (s)
            {
                case Cognition.CognitionSignal.LightMove: return Brightness>0 && Lamps.Count>0;
                case Cognition.CognitionSignal.LightDoorMove: case Cognition.CognitionSignal.LightLightMove:
                    return TryGetComponent<RoomLightGathering>(out var gathering) && gathering.CanMove(s);
                case Cognition.CognitionSignal.LightFloorMove:
                    return Brightness>0 && floorMotion && System.Linq.Enumerable.Any(Lamps, l=>!floorMotion.IsArrived(l));
                default: return false;
            }
        }
        sealed class MotionTarget : ISentenceMovable
        {
            readonly System.Func<Cognition.CognitionSignal,bool> canMove;
            public MotionTarget(System.Func<Cognition.CognitionSignal,bool> test) { canMove=test; }
            public bool CanMove(Cognition.CognitionSignal motion) => canMove(motion);
        }
        // "light light" targets per space; the lamps read SentenceRuntime.LampMultiplier themselves.
        BrightenTarget corridorGlow, farRoomGlow;
        public IBrightenableLight CorridorGlow => corridorGlow ??= new BrightenTarget(() => CorridorBrightness>0);
        public IBrightenableLight FarRoomGlow => farRoomGlow ??= new BrightenTarget(() => Brightness>0);
        sealed class BrightenTarget : IBrightenableLight
        {
            readonly System.Func<bool> lit;
            public BrightenTarget(System.Func<bool> test) { lit=test; }
            public bool Lit => lit();
        }
        [System.Flags]
        public enum LightSpace { Chamber=1, FirstPath=2, SphereRoom=4, ReturnPath=8, FarRoom=16, All=31, Outdoor=32, Gallery=64, Garden=128, Vitruvian=256 }

        [Min(1)] public float maxScopeDistance=30;
        [Range(5,90)] public float scopeHalfAngle=60;
        [Tooltip("Distance from an open passage mouth at which a sustained sentence begins affecting the adjacent space.")]
        [Min(0)] public float approachDistance=30;
        [Tooltip("Let an existing sentence enter the tutorial from the first path as soon as the door opens. Off preserves the first-opening demonstration until the player enters.")]
        public bool reachTutorialBeforeEntry;
        public LightSpace PlayerSpace {
            get {
                var outdoor=entry.outdoor;
                if(outdoor && outdoor.InChapter)return outdoor.CurrentRoom.Space;
                if(outdoor && outdoor.IsOutside)return LightSpace.Outdoor;
                if(!BeyondChamber || entry.portableClosure && entry.portableClosure.Isolated)return LightSpace.Chamber;
                if(InSecondRoom)return LightSpace.FarRoom;
                if(InTutorial)return LightSpace.SphereRoom;
                return InReturnPath?LightSpace.ReturnPath:LightSpace.FirstPath;
            }
        }

        bool InSight(Vector3 point)
        {
            var eye=entry.player.view;
            var delta=point-eye.position;
            if(delta.magnitude>maxScopeDistance || Vector3.Angle(Vector3.ProjectOnPlane(eye.forward,Vector3.up),Vector3.ProjectOnPlane(delta,Vector3.up))>scopeHalfAngle)return false;
            return !Physics.Raycast(eye.position,delta.normalized,Mathf.Max(0,delta.magnitude-.25f),Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
        }
        public LightSpace SentenceScope()
        {
            var here=PlayerSpace;
            if(here==LightSpace.Outdoor)return here;
            if(here==LightSpace.Gallery || here==LightSpace.Garden || here==LightSpace.Vitruvian)return here;
            if(!Root || !Tutorial)return here;
            var connected=ConnectedLights();
            var scope=here;
            float z=Root.InverseTransformPoint(entry.player.view.position).z;
            void Include(LightSpace room,float near,float far) {
                if(room==here || (connected&room)==0)return;
                var point=Root.TransformPoint(new Vector3(0,1.4f,Mathf.Clamp(z,near,far)));
                if(InSight(point))scope|=room;
            }
            Include(LightSpace.Chamber,-2,-.2f);
            Include(LightSpace.FirstPath,.2f,pathLength-.3f);
            Include(LightSpace.SphereRoom,pathLength+.3f,Tutorial.ExitZ-.25f);
            Include(LightSpace.ReturnPath,Tutorial.ExitZ+.25f,RoomStart-.25f);
            Include(LightSpace.FarRoom,RoomStart+.3f,RoomStart+roomSize.z);
            if(scope==here && (here==LightSpace.FirstPath || here==LightSpace.ReturnPath)){
                bool first=here==LightSpace.FirstPath;
                float middle=first?pathLength*.5f:(Tutorial.ExitZ+RoomStart)*.5f;
                var end=z<middle?(first?LightSpace.Chamber:LightSpace.SphereRoom):(first?LightSpace.SphereRoom:LightSpace.FarRoom);
                scope|=connected&end;
            }
            return scope;
        }

        public LightSpace ConnectedLights()
        {
            if(!Root || (entry.portableClosure && entry.portableClosure.Isolated)) return LightSpace.Chamber;
            LightSpace scope = LightSpace.All;
            const LightSpace beforeDoor = LightSpace.Chamber | LightSpace.FirstPath;
            const LightSpace beforeSphere = beforeDoor | LightSpace.SphereRoom;
            if (!passage || passage.Reveal<=0)
                scope &= BeyondChamber ? ~LightSpace.Chamber : LightSpace.Chamber;
            if (DoorProgress<=0)
                scope &= InTutorial || InReturnPath || InSecondRoom ? ~beforeDoor : beforeDoor;
            if (Tutorial.ExitBlocked)
                scope &= InReturnPath || InSecondRoom ? ~beforeSphere : beforeSphere;
            return scope;
        }

        public LightSpace SustainedReach()
        {
            if(entry.portableClosure && entry.portableClosure.Isolated)return 0;
            var here=PlayerSpace;
            if((here&LightSpace.All)==0 || !Root || !Tutorial || !entry.room.Opened)return here;
            var connected=ConnectedLights();
            var p=Root.InverseTransformPoint(entry.player.view.position);
            var reached=here;
            void Across(LightSpace a,LightSpace b,float z,bool allowForward=true){
                var pair=a|b;
                if((connected&pair)!=pair || (here&pair)==0 || (here==a && !allowForward))return;
                float x=Mathf.Max(0,Mathf.Abs(p.x)-PathWidth*.5f);
                if(x*x+(p.z-z)*(p.z-z)<=approachDistance*approachDistance)reached|=pair;
            }
            Across(LightSpace.Chamber,LightSpace.FirstPath,0);
            Across(LightSpace.FirstPath,LightSpace.SphereRoom,pathLength,reachTutorialBeforeEntry);
            Across(LightSpace.SphereRoom,LightSpace.ReturnPath,Tutorial.ExitZ);
            Across(LightSpace.ReturnPath,LightSpace.FarRoom,RoomStart);
            return reached;
        }

        // Switchable light sources by space, in a fixed order. A new light source joins here.
        public IEnumerable<ISwitchableLight> Lights(LightSpace scope)
        {
            if ((scope & LightSpace.Chamber)!=0 && entry.chamberLight) yield return entry.chamberLight;
            if ((scope & LightSpace.FirstPath)!=0 && firstStars) yield return firstStars;
            if ((scope & LightSpace.SphereRoom)!=0 && Tutorial) yield return Tutorial;
            if ((scope & LightSpace.ReturnPath)!=0 && secondStars) yield return secondStars;
            if ((scope & LightSpace.FarRoom)!=0) yield return FarRoom;
        }
        FarRoomLights farRoom;
        public ISwitchableLight FarRoom => farRoom ??= new FarRoomLights(this);
        sealed class FarRoomLights : ISwitchableLight
        {
            readonly PassageJourney journey;
            public FarRoomLights(PassageJourney owner) { journey=owner; }
            public bool WouldChange(bool on) => Mathf.Abs(journey.Brightness-(on?1:0))>.001f;
            public bool Settling(bool on) => journey.Brightness!=(on?1:0);
            public void SetOn(bool on)
            {
                journey.LightsOn=on;
                journey.LightTransitionEnabled=true;
            }
            public void Hold() { journey.LightTransitionEnabled=false; }
        }

        public bool LightWouldChange(LightSpace scope,bool on)
        {
            foreach (var light in Lights(scope)) if (light.Settling(on)) return true;
            return false;
        }

        public void SetLights(bool value)=>SetLights(value,ConnectedLights());
        public void SetLights(bool value,LightSpace scope)
        {
            foreach (var light in Lights(scope)) light.SetOn(value);
        }
        public void HoldLights()
        {
            foreach (var light in Lights(LightSpace.All & ~LightSpace.Chamber)) light.Hold();
        }
        public string WordAt(Collider target,float distance)
        {
            if(EndingDoor && target.transform.IsChildOf(EndingDoor.transform) && distance<=doorCollectionDistance && EndingDoor.Owns(target))return "door";
            if(InTutorial&&Tutorial.IsLit(target))return "light";
            if(target.transform.IsChildOf(doorHinge)&&distance<=doorCollectionDistance)
                return DoorProgress<=0&&Root.InverseTransformPoint(entry.player.view.position).z>pathLength?null:"door";
            if(Platform && target==Platform.Deck && distance<=7)return "floor";
            if(distance<=7 && entry.board.State.Knows("floor") && ((floorSurface && floorSurface.Owns(target)) || target.transform==Root.Find("White path") || Tutorial.IsFloor(target)))return "floor";
            if(InSecondRoom&&LampsLit&&target.GetComponent<DriftingLight>())return "light";
            return null;
        }
        void Update()
        {
            if(!Root)return;
            if(!entry.portableClosure||!entry.portableClosure.Isolated)
            {
                var p=Root.InverseTransformPoint(entry.player.transform.position);
                BeyondChamber=p.z>chamberBoundary&&(p.z<RoomStart+roomSize.z+1 || EndingDoor && EndingDoor.OnStairSide(p))&&Mathf.Abs(p.x)<(p.z>=pathLength&&p.z<=Tutorial.ExitZ?Tutorial.HalfWidth:roomSize.x*.5f)+1;
                InSecondRoom=BeyondChamber&&p.z>RoomStart+.3f;
                if(InSecondRoom)IntroduceRoom(LightSpace.FarRoom);
                InTutorial=BeyondChamber&&p.z>pathLength+.15f&&p.z<Tutorial.ExitZ;
                InReturnPath=InCorridor&&p.z>=Tutorial.ExitZ;
            }

            float viewZ=Root.InverseTransformPoint(entry.player.view.position).z;
            ViewBeyondChamber=BeyondChamber&&viewZ>chamberBoundary;
            CorridorBlend=ViewBeyondChamber?Mathf.SmoothStep(0,1,Mathf.InverseLerp(chamberBoundary,chamberBoundary+2,viewZ)):0;
            float viewDepth=viewZ-RoomStart;
            // The far room is visible along the whole second corridor, so its glow and fog build up over that walk.
            DoorwayLighting=BeyondChamber?Mathf.SmoothStep(0,1,Mathf.InverseLerp(Tutorial.ExitZ+2-RoomStart,2,viewDepth)):0;
            UpdateDoorSwing();
            // Both doorway faces remain visible while open, regardless of the viewing side.
            foreach(var edge in roomEdges)edge.enabled=DoorProgress>0;
            if(LightTransitionEnabled)Brightness=RoomLighting.AdvanceLevel(Brightness,LightsOn?1:0,Time.unscaledDeltaTime/Mathf.Max(.01f,lightFadeDuration));ApplyLight();
            UpdateDoorAppearance();
            if(movementObservation)movementObservation.Available=InSecondRoom&&!entry.sentenceRuntime.MotionFrozen&&LampsMoving&&(!entry.portableClosure||!entry.portableClosure.Isolated);
            float sphereRoomBloom = InTutorial ? 1 : 0;
            if (InCorridor)
                sphereRoomBloom=InReturnPath ? Mathf.SmoothStep(0,1,Mathf.InverseLerp(Tutorial.ExitZ+2,Tutorial.ExitZ,viewZ))
                    : DoorProgress; // seen through the open door, the sphere room keeps its in-room glow
            // Ten bright spheres can overlap in the entrance view. Blend in only a little of the wide lamp bloom.
            RoomBloom=Mathf.Max(.065f*sphereRoomBloom,DoorwayLighting);
            if(glowVolume){
                float niche=InSecondRoom && EndingDoor?EndingDoor.NicheBloomWeight(entry.player.view):1;
                glowVolume.weight=entry.portableClosure&&entry.portableClosure.Isolated?0:RoomBloom*niche;
            }
            entry.player.view.GetComponent<Camera>().backgroundColor=ViewBeyondChamber?Color.black:initialBackground;
            if(cameraData)cameraData.renderPostProcessing=initialPostProcessing;
        }
        float SwingAngle(float progress)=>openAngle*Mathf.SmoothStep(0,1,progress);
        public bool CanCycleDoor => Door && (!entry.outdoor || !entry.outdoor.IsOutside && !entry.outdoor.InChapter)
            && (!entry.portableClosure || !entry.portableClosure.Isolated);

        void UpdateDoorSwing()
        {
            var runtime = entry.sentenceRuntime;
            bool cycling = !DoorTransitionEnabled && CanCycleDoor && runtime &&
                runtime.HasEffect(Cognition.CognitionSignal.DoorMove,LightSpace.FirstPath|LightSpace.SphereRoom) &&
                !runtime.HasEffect(Cognition.CognitionSignal.DoorOn,LightSpace.FirstPath|LightSpace.SphereRoom) && !runtime.HasEffect(Cognition.CognitionSignal.DoorOff,LightSpace.FirstPath|LightSpace.SphereRoom);
            if (cycling)
            {
                if (DoorProgress <= 0) cycleOpening = true;
                if (DoorProgress >= 1) cycleOpening = false;
                DoorOpen = cycleOpening;
            }
            float target = DoorOpen ? 1 : 0;
            float next = DoorTransitionEnabled || cycling
                ? Mathf.MoveTowards(DoorProgress, target, Time.deltaTime / Mathf.Max(.01f, doorDuration))
                : DoorProgress;
            bool opening = next > DoorProgress;
            if(opening)IntroduceRoom(LightSpace.SphereRoom);
            if (opening && !ignoringDoorPlayer) SetDoorPlayerCollision(true);
            if (opening || next < DoorProgress && !PlayerBlocksSwing(next))
            {
                DoorProgress = next;
                doorHinge.localRotation = Quaternion.Euler(0, SwingAngle(next), 0);
            }
            if (DoorProgress == target) DoorTransitionEnabled = false;
            if (!opening && ignoringDoorPlayer && !DoorOverlapsPlayer()) SetDoorPlayerCollision(false);
        }

        void SetDoorPlayerCollision(bool ignore)
        {
            foreach (var part in doorColliders) Physics.IgnoreCollision(part, doorPlayer, ignore);
            ignoringDoorPlayer = ignore;
        }

        bool DoorOverlapsPlayer()
        {
            foreach (var part in doorColliders)
                if (Physics.ComputePenetration(part, part.transform.position, part.transform.rotation,
                    doorPlayer, doorPlayer.transform.position, doorPlayer.transform.rotation, out _, out _)) return true;
            return false;
        }

        bool PlayerBlocksSwing(float next)
        {
            var motor=entry.player.GetComponent<CharacterController>();
            Vector3 centre=Root.InverseTransformPoint(motor.bounds.center)-doorHinge.localPosition;
            if(centre.y-motor.height*.5f>Door.localScale.y||centre.y+motor.height*.5f<0)return false;
            float padding=motor.radius+.16f;
            float leafZ=Door.localPosition.z,halfDepth=Door.localScale.z*.5f;
            if(Mathf.Abs(centre.z-leafZ)<padding&&centre.x>=-padding&&centre.x<=pathWidth+padding)return true;
            float angle=Mathf.Atan2(-centre.z,centre.x)*Mathf.Rad2Deg;
            angle=Mathf.Clamp(angle,SwingAngle(Mathf.Min(next,DoorProgress)),SwingAngle(Mathf.Max(next,DoorProgress)));
            Vector3 local=Quaternion.Euler(0,-angle,0)*centre;
            float x=Mathf.Clamp(local.x,0,pathWidth);
            float z=Mathf.Clamp(local.z,leafZ-halfDepth,leafZ+halfDepth);
            return new Vector2(local.x-x,local.z-z).sqrMagnitude<padding*padding;
        }
        void ApplyLight()
        {
            if(!lampInk)return;
            float amount=Mathf.SmoothStep(0,1,Brightness);
            float boosted=amount*(entry.sentenceRuntime?entry.sentenceRuntime.LampBoost(LightSpace.FarRoom):1);
            lampInk.SetColor("_BaseColor",Color.Lerp(new Color(.12f,.12f,.12f),Color.white,amount));
            lampInk.SetColor("_EmissionColor",emission*(boosted*emissionStrength));
            foreach(var l in lamps)l.glow.intensity=LightStyle.LampIntensity(l.transform.localScale.x)*boosted;
        }
        void UpdateDoorAppearance()
        {
            if(doorSurface==null)doorSurface=new MaterialPropertyBlock();
            doorSurface.SetFloat("_IsDoorLeaf",1);
            doorSurface.SetFloat("_CorridorPureWhite",pureWhiteCorridor?1:0);
            doorSurface.SetFloat("_DoorBackBrightness",1);
            var wallInk=floorSurface?floorSurface.floorMaterial:null;
            doorSurface.SetColor("_DoorBackColor",wallInk&&wallInk.HasProperty("_FloorColor")?wallInk.GetColor("_FloorColor"):Color.white);
            doorSurface.SetFloat("_LightSteps",floorSurface?floorSurface.lightSteps:4);
            doorSurface.SetFloat("_LightStepBlend",floorSurface&&floorSurface.steppedLighting?floorSurface.lightStepBlend:0);
            doorSurface.SetFloat("_LightBandEdge",floorSurface?floorSurface.lightBandEdge:.45f);
            doorLeaf.SetPropertyBlock(doorSurface);
            doorSurface.SetFloat("_RoundOutline",2);
            hingePin.SetPropertyBlock(doorSurface);
            doorSurface.SetFloat("_RoundOutline",0);
            // Closed, the room side is indistinguishable from the entrance wall. Opening fades in
            // the handle side's white finish; closing fades it back out. No material swap, no jump.
            Tutorial.EntranceWall.GetPropertyBlock(backSurface);
            doorLeaf.SetPropertyBlock(backSurface, 1);
            float open = Mathf.SmoothStep(0, 1, DoorProgress);
            doorCover.enabled = open > 0;
            if (!doorCover.enabled) return;
            coverMaterial.CopyPropertiesFromMaterial(corridorMaterial);
            SetCoverBlend();
            coverSurface.Clear();
            coverSurface.SetFloat("_IsDoorLeaf",1);
            coverSurface.SetFloat("_CorridorPureWhite",pureWhiteCorridor?1:0);
            coverSurface.SetFloat("_Dissolve",1-open);
            doorCover.SetPropertyBlock(coverSurface);
        }
        void SetCoverBlend()
        {
            coverMaterial.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            coverMaterial.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            coverMaterial.SetFloat("_ZWrite",0);
            coverMaterial.renderQueue=(int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        void PrepareDoorBack()
        {
            backSurface = new MaterialPropertyBlock();
            var filter = Door.GetComponent<MeshFilter>();
            doorMesh = Instantiate(filter.sharedMesh);
            var triangles = doorMesh.triangles;
            var normals = doorMesh.normals;
            var front = new List<int>();
            var back = new List<int>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var face = normals[triangles[i]].z > .5f ? back : front;
                face.Add(triangles[i]);
                face.Add(triangles[i+1]);
                face.Add(triangles[i+2]);
            }
            doorMesh.subMeshCount = 2;
            doorMesh.SetTriangles(front, 0);
            doorMesh.SetTriangles(back, 1);
            filter.sharedMesh = doorMesh;
            doorLeaf.sharedMaterials = new[] {corridorMaterial, Tutorial.EntranceWall.sharedMaterial};
            coverSurface = new MaterialPropertyBlock();
            coverMaterial = new Material(corridorMaterial);
            SetCoverBlend();
            var cover = GameObject.CreatePrimitive(PrimitiveType.Quad);
            cover.name = "Door room-side finish";
            DestroyImmediate(cover.GetComponent<Collider>());
            cover.layer = 29;
            cover.transform.SetParent(Door, false);
            // Just proud of the room-side face, facing into the room.
            cover.transform.localPosition = new Vector3(0, 0, .5f + .01f);
            cover.transform.localRotation = Quaternion.Euler(0, 180, 0);
            doorCover = cover.GetComponent<Renderer>();
            doorCover.sharedMaterial = coverMaterial;
            doorCover.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            doorCover.enabled = false;
        }
        public Vector3 ConstrainMovement(Vector3 position,Vector3 motion)
        {
            if(!Root||!InCorridor||InReturnPath)return motion;
            var p=Root.InverseTransformPoint(position);var delta=Root.InverseTransformDirection(motion);
            // Restrict horizontal travel to the path, retaining vertical motion for the entry jump.
            var desired=p+delta;desired.x=Mathf.Clamp(desired.x,-pathWidth*.5f+.3f,pathWidth*.5f-.3f);
            return Root.TransformVector(desired-p);
        }
        void OnDestroy(){if(returnMaterial)Destroy(returnMaterial);if(returnEdge)Destroy(returnEdge);if(doorMesh)Destroy(doorMesh);if(corridorMaterial)Destroy(corridorMaterial);if(coverMaterial)Destroy(coverMaterial);if(lampInk)Destroy(lampInk);if(doorEdge)Destroy(doorEdge);}
    }
}
