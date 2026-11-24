using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PuzzleApple.V5.Cognition;
using PuzzleApple.V5.Outdoor;
using PuzzleApple.V5.Dependencies;
using UnityEngine.Rendering;
using Space = PuzzleApple.V5.PassageJourney.LightSpace;

namespace PuzzleApple.V5
{
    public sealed class ChapterLesson : MonoBehaviour, ISwitchableDoor, ISwitchableLight
    {
        [Header("Charge door")]
        [Min(1)] public int threshold = 4;
        [Min(0)] public int lampPoints = 1, createdLampPoints = 1, reflectionPoints = 1, doorPoints = 2;
        [Min(.1f)] public float recognitionRadius = 2.4f;
        [Min(.1f)] public float chargeSeconds = 1.2f;
        [Header("Lesson surfaces")]
        public Material panelMaterial, mirrorMaterial;
        public ObservationLesson mirrorLesson;
        public StarLift lift;
        public OpeningLightField Field { get; private set; }
        public WorldDropRegion Region { get; private set; }
        public Collider Ground { get; private set; }
        public bool InStarfield {
            get {
                if(!entry)return false;
                var p=transform.InverseTransformPoint(entry.player.transform.position);
                return p.x>3.16f || p.x< -3.1f || Mathf.Abs(p.z)>3.1f || p.y<-.3f || p.y>6;
            }
        }
        public bool DoorLit => Effect(CognitionSignal.IlluminateDoor);
        public float StarBrightness => source.Brightness;
        public bool TransferringMirror { get; private set; }
        public int Energy { get; private set; }
        public float Charge { get; private set; }
        public float Openness { get; private set; }
        public const Space LessonSpace = Space.ChapterLesson;
        WorldEntry entry;
        OpeningLight source;
        Transform room, anchor;
        Renderer mirror, twin, leaf;
        Renderer[] reflectedFrame;
        Material ink;
        MaterialPropertyBlock props;
        Renderer[] shell;
        Renderer[] chargeMarks;
        float mirrorOpacity = 1, targetLift;
        SentenceMotionOutput doorMotion;
        SentenceStateOutput doorOutput;
        bool exterior;
        readonly Dictionary<OpeningLight, Reflection> reflections = new Dictionary<OpeningLight, Reflection>();
        readonly List<OpeningLight> expired = new List<OpeningLight>();
        sealed class Reflection
        {
            public Renderer surface;
            public Light glow;
            public float opacity;
        }
        internal bool Effect(CognitionSignal signal) => entry.sentenceRuntime.HasEffect(signal, LessonSpace);
        public void Enter(OutdoorPassage passage)
        {
            if (!entry)
            {
                entry = passage.entry;
                props = new MaterialPropertyBlock();
                BuildRoom();
                BuildLights();
                BindEffects();
                lift.Build(this, passage, ink);
            }
            entry.player.Teleport(transform.TransformPoint(new Vector3(0, .08f, -.8f)), transform.rotation);
            var lighting = FindFirstObjectByType<RoomLighting>(FindObjectsInactive.Include);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = lighting ? lighting.closedAmbient : Color.black;
            RenderSettings.fog = false;
            ShowExterior(false);
            Physics.SyncTransforms();
        }
        internal static Renderer Block(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool solid = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            go.GetComponent<Collider>().enabled = solid;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }
        internal static Transform Child(Transform parent, string name, Vector3 position)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = position;
            return child;
        }
        internal static void Tint(Renderer r, MaterialPropertyBlock block, Color color)
        {
            block.Clear();
            block.SetColor("_BaseColor", color);
            r.SetPropertyBlock(block);
        }
        void BuildRoom()
        {
            room = Child(transform, "Mirror chamber", Vector3.zero);
            ink = new Material(Resources.Load<Shader>("V5/LessonInk"));
            Ground = Block(room, "Box floor", new Vector3(0,-.1f,0), new Vector3(6.2f,.2f,6.2f), panelMaterial).GetComponent<Collider>();
            Block(room,"Ceiling",new Vector3(0,6.1f,0),new Vector3(6.2f,.2f,6.2f),panelMaterial);
            foreach (int sign in new[]{-1,1})
            {
                Block(room,"Mirror wall",new Vector3(0,3,sign*3.1f),new Vector3(6.2f,6,.2f),panelMaterial);
                Block(room,"Door jamb",new Vector3(3.1f,3,sign*2.15f),new Vector3(.2f,6,1.7f),panelMaterial);
            }
            Block(room,"Left wall",new Vector3(-3.1f,3,0),new Vector3(.2f,6,6.2f),panelMaterial);
            Block(room,"Door header",new Vector3(3.1f,4.5f,0),new Vector3(.2f,3,2.6f),panelMaterial);
            var intro = entry.room.GetComponent<FoldingInterior>();
            var folding = room.gameObject.AddComponent<FoldingInterior>();
            folding.darkLevel=intro.darkLevel;folding.coreBrightness=intro.coreBrightness;
            folding.edgeWidthPixels=intro.edgeWidthPixels;folding.closedLightingBlend=intro.closedLightingBlend;
            folding.surfaces=room.GetComponentsInChildren<Renderer>().Select(surface=>{
                var p=surface.transform.localPosition;
                Vector3 normal=p.y<0?Vector3.up:p.y>6?Vector3.down:Mathf.Abs(p.x)>3?
                    Vector3.left*Mathf.Sign(p.x):Vector3.back*Mathf.Sign(p.z);
                surface.sharedMaterial=intro.surfaces[0].renderer.sharedMaterial;
                return new FoldingInterior.Surface{renderer=surface,inwardNormal=normal,preserveMeshDepth=true};
            }).ToArray();
            folding.Apply();
            var axis = new List<Renderer>();
            foreach(int sign in new[]{-1,1})
            {
                axis.Add(Block(room,"Symmetry line",new Vector3(sign*2.992f,3,0),new Vector3(.012f,6,.021f),ink,false));
                axis.Add(Block(room,"Symmetry line",new Vector3(0,3+sign*2.992f,0),new Vector3(6,.012f,.021f),ink,false));
            }
            // Leave the door opening clear while retaining the symmetry mark on the moving leaf.
            axis[2].transform.localScale = new Vector3(.012f,3,.021f);
            axis[2].transform.localPosition += Vector3.up*1.5f;
            mirror = MakeMirror("Original mirror",-1);
            twin = MakeMirror("Reflected mirror",1);
            reflectedFrame=twin.GetComponentsInChildren<Renderer>().Where(r=>r!=twin).ToArray();
            leaf=Block(room,"Charge door",new Vector3(3.05f,1.5f,0),new Vector3(.19f,3,2.6f),entry.journey.roomMaterial);
            chargeMarks=new Renderer[threshold];
            for(int i=0;i<chargeMarks.Length;i++)
                chargeMarks[i]=Block(leaf.transform,"Charge segment", new Vector3(-.51f,0,(i+.5f)/chargeMarks.Length*.78f-.39f),
                    new Vector3(.04f,.018f,.67f/chargeMarks.Length),ink,false);
            var stripe=Block(leaf.transform,"Symmetry line on door",new Vector3(-.52f,0,0),new Vector3(.01f,1,.008f),ink,false);
            Tint(stripe,props,Color.white*.65f+new Color(0,0,0,.35f));
            anchor=Child(room,"Lamp gathering point",new Vector3(2.15f,1.45f,0));
            shell=room.GetComponentsInChildren<Renderer>();
            RoomLightBalance.Create(room,"Box diffuse fill",new Vector3(0,3,0),Vector3.one*6,entry.journey.LightStyle).reflectance=.1f;
        }
        Renderer MakeMirror(string label,int side)
        {
            var r=Block(room,label,new Vector3(0,1.65f,side*2.96f),new Vector3(1.65f,2.5f,.055f),mirrorMaterial);
            if(side>0)r.transform.localRotation=Quaternion.Euler(0,180,0);
            r.gameObject.AddComponent<PlanarMirror>().SourceCamera=entry.player.view.GetComponent<Camera>();
            var frameColor=new Color(.82f,.82f,.82f,1);
            foreach(int sign in new[]{-1,1}){
                var rail=Block(r.transform,"Mirror jamb",new Vector3(sign*.509f,0,0),new Vector3(.018f,1.026f,1.1f),ink,false);
                Tint(rail,props,frameColor);
                rail=Block(r.transform,"Mirror rail",new Vector3(0,sign*.507f,0),new Vector3(1,.014f,1.1f),ink,false);
                Tint(rail,props,frameColor);
            }
            return r;
        }
        void BuildLights()
        {
            Region=gameObject.AddComponent<WorldDropRegion>();
            Region.allowInstantiation=true;
            Field=Child(room,"Chamber lamps",Vector3.zero).gameObject.AddComponent<OpeningLightField>();
            var master=Block(Field.transform,"Lamp appearance",Vector3.zero,Vector3.one,entry.journey.lampMaterial,false);
            master.enabled=false;
            source=master.gameObject.AddComponent<OpeningLight>();
            Field.stateSource=source;
            Field.ground=Ground;
            Field.observer=entry.player.view;
            Field.indoorStyle=entry.journey.LightStyle;
            Field.Spawn(transform.TransformPoint(new Vector3(1.65f,1.45f,1.35f)),.42f);
        }
        void BindEffects()
        {
            var registry=entry.Entities;
            var caps=SentenceCapabilityCatalog.Default;
            source.LightOutput=SentenceSceneBindings.BindLight(registry,source,this,()=>LessonSpace,()=>source.Brightness);
            Field.BindSentenceOutput(entry,()=>LessonSpace,source.LightOutput);
            doorOutput=SentenceSceneBindings.BindDoor(registry,leaf,this,()=>LessonSpace,caps.lift,()=>Openness,
                open=>Mathf.Abs(Openness-(open?1:0))>.001f && (open?Charge>=1:true));
            var motion=Field.gameObject.AddComponent<LightSentenceEffects>();
            motion.field=Field;motion.stateSource=source;motion.ground=Ground;motion.observer=entry.player.view;
            motion.gatheringAnchor=anchor;motion.gatheringRadius=new Vector2(.4f,.7f);
            motion.spacing=.5f;
            motion.MotionOutput=SentenceMotionOutput.Bind(registry,motion,()=>LessonSpace,caps.position,
                m=>m==SentenceMotion.Light || m==SentenceMotion.LightToDoor || m==SentenceMotion.LightToFloor || m==SentenceMotion.LightToLight,
                (m,g)=>motion.CanMove(m),apply:o=>motion.Apply(o.HasMotion),emits:()=>Field.AnyEmitting);
            doorMotion=SentenceMotionOutput.Bind(registry,leaf,()=>LessonSpace,caps.lift,m=>m==SentenceMotion.Door,
                (m,g)=>Charge>=1);
            doorMotion.BindIllumination(SentenceMotion.Door,()=>DoorLit);
        }
        public bool HandlesMirror(CognitionSignal signal) => signal==CognitionSignal.MirrorCopy ||
            signal==CognitionSignal.MirrorLight || signal==CognitionSignal.FloorMirrorLight;
        public bool MirrorWouldChange(CognitionSignal signal) => signal==CognitionSignal.FloorMirrorLight ? Openness>0 || InStarfield :
            signal==CognitionSignal.MirrorCopy || Field.ActiveCount>0;
        public bool CollectMirror()
        {
            if(entry.board.State.HasAcquired(mirrorLesson.acquisitionId))return false;
            TransferringMirror=true;
            try { entry.chamberLight.CollectObservation(mirrorLesson,showWord:false); }
            finally { TransferringMirror=false; }
            return true;
        }
        public string WordAt(Ray ray,float reach,out Renderer emitter,out Collider target)
        {
            emitter=null;target=null;
            if(lift.Pick(ray,reach,out var star)){emitter=star;target=star.GetComponent<Collider>();return "light";}
            if(!Physics.Raycast(ray,out var hit,reach,~0,QueryTriggerInteraction.Collide))return null;
            target=hit.collider;
            if(target==mirror.GetComponent<Collider>() || target==twin.GetComponent<Collider>())return exterior?null:"mirror";
            if(target==leaf.GetComponent<Collider>())return "door";
            if(target==Ground || lift.IsDeck(target))return "floor";
            if(target.GetComponent<OpeningLight>() || target.transform.parent==reflectionRoot){
                emitter=target.GetComponent<Renderer>();
                return source.EmitsLight?"light":null;
            }
            return null;
        }
        Transform reflectionRoot;
        Reflection CreateReflection(OpeningLight lamp)
        {
            if(!reflectionRoot)reflectionRoot=Child(room,"Reflected lamps",Vector3.zero);
            var go=new GameObject("Reflected "+lamp.name,typeof(MeshFilter),typeof(MeshRenderer),typeof(SphereCollider));
            go.transform.SetParent(reflectionRoot,false);
            go.GetComponent<MeshFilter>().sharedMesh=lamp.GetComponent<MeshFilter>().sharedMesh;
            go.transform.localScale=lamp.transform.lossyScale;
            var surface=go.GetComponent<Renderer>();
            surface.sharedMaterial=ink;
            Physics.IgnoreCollision(go.GetComponent<Collider>(),entry.player.GetComponent<CharacterController>());
            // Visual images follow the source, never perturb its physics.
            go.layer=2;
            go.GetComponent<Collider>().isTrigger=true;
            var glow=Child(go.transform,"Reflected glow",Vector3.zero).gameObject.AddComponent<Light>();
            glow.range=lamp.glow.range;
            return new Reflection{surface=surface,glow=glow};
        }
        void UpdateReflections(float dt)
        {
            bool active=Effect(CognitionSignal.MirrorLight);
            if(active)foreach(var lamp in Field.ActiveLamps)
                if(lamp && !reflections.ContainsKey(lamp))reflections.Add(lamp,CreateReflection(lamp));
            expired.Clear();
            foreach(var pair in reflections){
                var lamp=pair.Key;var reflection=pair.Value;
                var instance=lamp?lamp.GetComponent<PlayerInstance>():null;
                bool wanted=active && lamp && !(instance && instance.Recovering);
                reflection.opacity=Mathf.MoveTowards(reflection.opacity,wanted?1:0,dt/.55f);
                if(!wanted && reflection.opacity<=0){Destroy(reflection.surface.gameObject);expired.Add(lamp);continue;}
                if(lamp){
                    var local=transform.InverseTransformPoint(lamp.transform.position);
                    local.z=-local.z;
                    var image=reflection.surface.transform;
                    image.position=transform.TransformPoint(local);
                    image.rotation=Quaternion.LookRotation(Vector3.Reflect(lamp.transform.forward,transform.forward),
                        Vector3.Reflect(lamp.transform.up,transform.forward));
                    reflection.glow.intensity=lamp.glow.intensity*reflection.opacity;
                    reflection.glow.range=lamp.glow.range;
                    if(wanted && lamp.EmitsLight && InRange(image.position))Energy+=reflectionPoints;
                }else reflection.glow.intensity=0;
                var color=Color.white*(source.Brightness*entry.sentenceRuntime.LampBoost(LessonSpace));
                color.a=reflection.opacity;
                Tint(reflection.surface,props,color);
                reflection.surface.forceRenderingOff=exterior;
            }
            foreach(var lamp in expired)reflections.Remove(lamp);
        }
        bool InRange(Vector3 p) => (transform.InverseTransformPoint(p)-new Vector3(3.05f,1.5f,0)).sqrMagnitude<=recognitionRadius*recognitionRadius;
        bool cyclingUp=true,doorCommand;
        void Update()
        {
            if(!entry)return;
            if(InStarfield!=exterior)ShowExterior(InStarfield);
            var dt=Time.deltaTime;
            Energy=DoorLit?doorPoints:0;
            foreach(var lamp in Field.ActiveLamps){
                if(lamp.EmitsLight && InRange(lamp.transform.position))
                    Energy+=lamp.TryGetComponent<PlayerInstance>(out _)?createdLampPoints:lampPoints;
                lamp.surface.forceRenderingOff=exterior;
            }
            source.SetLampMultiplier(entry.sentenceRuntime.LampBoost(LessonSpace));
            UpdateReflections(dt);
            bool copy=!entry.board.State.HasAcquired(mirrorLesson.acquisitionId)||Effect(CognitionSignal.MirrorCopy);
            mirrorOpacity=Mathf.MoveTowards(mirrorOpacity,copy?1:0,dt/.55f);
            twin.gameObject.SetActive(mirrorOpacity>0);
            twin.GetPropertyBlock(props);props.SetFloat("_LessonVisibility",mirrorOpacity);twin.SetPropertyBlock(props);
            foreach(var rail in reflectedFrame)Tint(rail,props,new Color(.82f,.82f,.82f,mirrorOpacity));
            Charge=Mathf.MoveTowards(Charge,Mathf.Clamp01((float)Energy/threshold),dt/chargeSeconds);
            for(int i=0;i<chargeMarks.Length;i++){
                float value=.09f+Mathf.Clamp01(Charge*chargeMarks.Length-i)*1.1f;
                Tint(chargeMarks[i],props,new Color(value,value,value,1));
            }
            leaf.GetPropertyBlock(props);props.SetFloat("_ChargeDoorEmission",DoorLit?.9f:0);
            leaf.SetPropertyBlock(props);
            if(!doorCommand && doorMotion.HasMotion(SentenceMotion.Door) && Charge>=1){
                if(Openness>=1)cyclingUp=false;else if(Openness<=0)cyclingUp=true;
                targetLift=cyclingUp?1:0;
            }
            if(Charge<1)targetLift=0;
            var playerPos=transform.InverseTransformPoint(entry.player.transform.position);
            if(targetLift<Openness && Mathf.Abs(playerPos.x-3)<.65f && Mathf.Abs(playerPos.z)<1.7f)return;
            Openness=SentenceStateOutput.Advance(Openness,targetLift,dt*.72f,doorOutput);
            leaf.transform.localPosition=new Vector3(3.05f,1.5f+3.05f*Openness,0);
        }
        void ShowExterior(bool value)
        {
            exterior=value;
            foreach(var surface in shell)surface.forceRenderingOff=value;
            mirror.GetComponent<PlanarMirror>().enabled=!value;
            twin.GetComponent<PlanarMirror>().enabled=!value;
        }
        public void Respawn()=>lift.Respawn();
        bool ISwitchableDoor.WouldChange(bool open)=>open?Charge>=1 && Openness<1:Openness>0;
        void ISwitchableDoor.SetOpen(bool open){doorCommand=true;targetLift=open && Charge>=1?1:0;}
        void ISwitchableDoor.Release(){doorCommand=false;}
        void ISwitchableDoor.Hold(){targetLift=Openness;doorCommand=false;}
        bool ISwitchableLight.WouldChange(bool on)=>source.Brightness!=(on?1:0);
        bool ISwitchableLight.Settling(bool on)=>source.TargetOn==on && source.TransitionEnabled && source.Brightness!=(on?1:0);
        void ISwitchableLight.SetOn(bool on)=>source.SetOn(on);
        void ISwitchableLight.Hold()=>source.HoldCurrentState();
        void OnDestroy(){if(ink)Destroy(ink);}
    }
}
