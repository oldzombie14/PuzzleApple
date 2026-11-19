using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PuzzleApple.V5.Cognition;

namespace PuzzleApple.V5
{
    [DefaultExecutionOrder(60)]
    public sealed class MirrorStructure : MonoBehaviour
    {
        public ChapterRoom room;
        public Transform[] originals;
        public Transform splitter,foldingWall;
        public Vector3 planeLocal;
        public Vector2 mirrorSize=new Vector2(1.2f,.08f);
        public Material guideInk;
        public LineRenderer[] seams;
        [Min(.01f)] public float appearTime=.8f,disappearTime=.6f,guideFade=.4f,breathPeriod=1.6f;
        [Min(0)] public float wanderSpeed=.6f,sideMargin=2;
        public float foldedCentre=32.9f;
        [Min(.01f)] public float seamFlashSeconds=.2f;
        [Min(.1f)] public float rippleSeconds=.8f;
        const float PlaneTolerance=.005f;
        public bool HasTargets {
            get {
                if(originals==null)return false;
                var centre=planeLocal;
                if(room && room.box && !room.box.Opened)centre.x=foldedCentre;
                var origin=transform.TransformPoint(centre);
                return originals.Any(source=>source && Mathf.Abs(Vector3.Dot(source.position-origin,transform.right))>PlaneTolerance);
            }
        }
        public bool CopiesActive {get;private set;}
        public Transform Copy(int index)=>twins[index].solid;
        sealed class Twin
        {
            public Transform source,root,solid;
            public Collider[] colliders;
            public LineRenderer outline;
            public LineRenderer[] rings;
            public MeshRenderer fill;
        }
        readonly List<Twin> twins=new List<Twin>();
        float reveal,preview,seam;
        Vector2 velocity;
        Vector3 initial;
        Matrix4x4 closedWall;
        readonly Collider[] overlaps=new Collider[32];
        FoldingInterior interior;
        readonly Dictionary<Renderer,Material> mirrorFinishes=new Dictionary<Renderer,Material>();
        readonly Dictionary<Material,Material> sinkingFinishes=new Dictionary<Material,Material>();
        bool cutting,foldedCopy;
        readonly HashSet<long> retiring=new HashSet<long>();
        MaterialPropertyBlock ink;
        WorldEntry Entry=>room.Entry;
        void Start()
        {
            initial=originals[0].localPosition;
            if(foldingWall){closedWall=foldingWall.worldToLocalMatrix;interior=room.box.GetComponent<FoldingInterior>();}
            ink=new MaterialPropertyBlock();
            foreach(var source in originals){
                var owner=source.GetComponent<SentenceOwnership>();
                if(!owner)owner=source.gameObject.AddComponent<SentenceOwnership>();owner.room=room;
                var t=new Twin{source=source};
                t.root=new GameObject(source.name+" reflection").transform;t.root.SetParent(transform,false);
                t.solid=Instantiate(source,t.root);t.solid.localPosition=Vector3.zero;t.solid.localRotation=Quaternion.identity;
                t.solid.GetComponent<SentenceOwnership>().source=owner;
                t.solid.localScale=RelativeScale(source,t.root);t.colliders=t.solid.GetComponentsInChildren<Collider>();
                if(interior)foreach(var surface in t.solid.GetComponentsInChildren<MeshRenderer>()){
                    var finish=surface.sharedMaterial;
                    mirrorFinishes[surface]=finish;
                    if(sinkingFinishes.ContainsKey(finish))continue;
                    var cut=new Material(finish){shader=Resources.Load<Shader>("V5/FoldingMirror")};
                    cut.SetFloat("_MirrorGlass",finish.HasProperty("_ReflectionTex")?1:0);
                    sinkingFinishes.Add(finish,cut);
                }
                var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.name="Chalk silhouette";
                quad.GetComponent<Collider>().enabled=false;Destroy(quad.GetComponent<Collider>());
                quad.transform.SetParent(t.root,false);quad.transform.localScale=new Vector3(mirrorSize.x,mirrorSize.y,1);
                t.fill=quad.GetComponent<MeshRenderer>();t.fill.sharedMaterial=guideInk;
                t.outline=new GameObject("Mirror outline",typeof(LineRenderer)).GetComponent<LineRenderer>();
                t.outline.transform.SetParent(t.root,false);t.outline.useWorldSpace=false;t.outline.loop=true;
                t.outline.sharedMaterial=guideInk;t.outline.positionCount=4;
                for(int i=0;i<4;i++)t.outline.SetPosition(i,new Vector3((i<2?-1:1)*mirrorSize.x*.5f,(i==0 || i==3?-1:1)*mirrorSize.y*.5f,-.012f));
                t.rings=new LineRenderer[2];
                for(int ring=0;ring<2;ring++){
                    var ripple=new GameObject("Mirror ripple").AddComponent<LineRenderer>();
                    ripple.transform.SetParent(t.root,false);ripple.useWorldSpace=false;ripple.loop=true;
                    ripple.sharedMaterial=guideInk;ripple.positionCount=48;
                    for(int i=0;i<48;i++){float a=i*Mathf.PI/24;ripple.SetPosition(i,new Vector3(Mathf.Cos(a),Mathf.Sin(a),-.02f));}
                    t.rings[ring]=ripple;
                }
                t.root.gameObject.SetActive(false);twins.Add(t);
            }
        }
        void SetCut(bool active){
            if(active!=cutting){
                foreach(var pair in mirrorFinishes)pair.Key.sharedMaterial=active?sinkingFinishes[pair.Value]:pair.Value;
                cutting=active;
            }
            if(active)foreach(var material in sinkingFinishes.Values){
                material.SetFloat("_PanelOpacity",room.box.PanelCoverage);
                material.SetVector("_PanelSink",interior.SinkProfile);
                material.SetFloat("_PanelGroundY",room.box.floorPanel.position.y);
            }
        }
        void OnDestroy(){foreach(var material in sinkingFinishes.Values)Destroy(material);}
        public bool IsMirror(Collider c)
        {
            if(splitter && c.transform.IsChildOf(splitter))return true;
            foreach(var twin in twins)if(c.transform.IsChildOf(twin.source) || CopiesActive && c.transform.IsChildOf(twin.solid))return true;
            return false;
        }
        public bool CrossesCopy(Ray ray,float distance)
        {
            if(!CopiesActive || !room.box)return false;
            var glass=twins[0].solid.Find(room.mirror.name);
            return glass && glass.GetComponent<Collider>().Raycast(ray,out _,distance);
        }
        public void ResetEntrance(){
            if(!room.box || twins.Count==0)return;
            originals[0].localPosition=initial;velocity=Vector2.zero;wanderTime=0;
            foldedCopy=false;retiring.Clear();CopiesActive=false;reveal=preview=0;
            foreach(var twin in twins)twin.root.gameObject.SetActive(false);
        }
        void Wander(float dt,bool folded)
        {
            if(dt<=0)return;
            if(velocity.sqrMagnitude<.0001f)velocity=Random.insideUnitCircle.normalized;
            float angle=(Mathf.PerlinNoise((float)wanderTime*.35f,initial.y)-.5f)*140*dt;
            wanderTime+=dt;velocity=Quaternion.Euler(0,0,angle)*velocity;
            var source=originals[0];var p=source.localPosition;
            float scale=Mathf.Abs(transform.lossyScale.x);
            float z=(folded?3/scale:10.5f)-Mathf.Max(mirrorSize.x*.5f,folded?.1f/scale:sideMargin);
            float minY=(folded?.1f/scale:0)+mirrorSize.y*.5f,maxY=(folded?6/scale:22)-mirrorSize.y*.5f;
            p.z+=velocity.x*wanderSpeed*dt/scale;p.y+=velocity.y*wanderSpeed*dt/scale;
            if(p.z<=-z || p.z>=z)velocity.x=p.z<0?Mathf.Abs(velocity.x):-Mathf.Abs(velocity.x);
            if(p.y<=minY || p.y>=maxY)velocity.y=p.y<minY?Mathf.Abs(velocity.y):-Mathf.Abs(velocity.y);
            p.z=Mathf.Clamp(p.z,-z,z);p.y=Mathf.Clamp(p.y,minY,maxY);source.localPosition=p;
        }
        float wanderTime,rippleAge=10;
        static Vector3 RelativeScale(Transform source,Transform parent){
            var a=source.lossyScale;var b=parent.lossyScale;
            return new Vector3(a.x/b.x,a.y/b.y,a.z/b.z);
        }
        void Tint(Renderer r,Color color){
            ink.Clear();ink.SetColor("_BaseColor",color);ink.SetColor("_EmissionColor",Color.black);
            ink.SetVector("_CorridorAxis",Vector4.zero);ink.SetFloat("_FogInfluence",0);r.SetPropertyBlock(ink);
        }
        float PixelWidth(Vector3 position){
            var cam=Entry.player.view.GetComponent<Camera>();
            return Mathf.Max(.001f,Vector3.Distance(position,cam.transform.position)*2*Mathf.Tan(cam.fieldOfView*Mathf.Deg2Rad*.5f)*1.5f/Mathf.Max(1,cam.pixelHeight));
        }
        void LateUpdate(){
            if(!room.Entry || !Entry.outdoor.InChapter || twins.Count==0)return;
            var runtime=Entry.sentenceRuntime;var space=room.Space;var board=Entry.board;
            bool box=room.box && !room.box.Opened;
            bool copy=runtime.HasEffect(CognitionSignal.MirrorCopy,space) && HasTargets;
            if(room.box && room.box.Opened && foldedCopy){
                foldedCopy=false;
                foreach(var g in board.State.Groups.Where(g=>g.Effective && g.Signal==CognitionSignal.MirrorCopy && (runtime.Scope(g)&space)!=0).ToArray())
                {
                    retiring.Add(g.Established);
                    runtime.RetireFoldedReflection(g);
                }
                copy=false;
            }
            if(retiring.Count>0){
                retiring.RemoveWhere(id=>!board.State.Groups.Any(g=>g.Established==id));
                copy=copy && board.State.Groups.Any(g=>g.Effective && g.Signal==CognitionSignal.MirrorCopy &&
                    (runtime.Scope(g)&space)!=0 && !retiring.Contains(g.Established));
            }
            if(copy && box)foldedCopy=true;
            if(!copy)foldedCopy=false;
            if(room.box && runtime.HasEffect(CognitionSignal.MirrorMove,space) && !runtime.MotionFrozen)Wander(Time.deltaTime,box);
            bool editing=board.Panel.IsOpen && Entry.journey.PlayerSpace==space;
            bool hasWord=editing && board.State.Groups.Any(g=>g.Words.Any(w=>w.Meaning=="mirror"));
            bool draft=editing && (board.State.RequirePeriod?board.State.Groups.Any(g=>!g.Spoken && g.Signal==CognitionSignal.MirrorCopy):board.PreviewMirrorCopy);
            bool moveDraft=editing && (board.State.RequirePeriod?board.State.Groups.Any(g=>!g.Spoken && g.Signal==CognitionSignal.MirrorMove):board.PreviewMirrorMove);
            float dt=Time.unscaledDeltaTime;
            float target=draft||moveDraft?.8f+.05f*Mathf.Cos(Time.unscaledTime*2*Mathf.PI/breathPeriod):
                hasWord && !copy && !runtime.HasEffect(CognitionSignal.MirrorMove,space)?.5f:.12f;
            bool appearing=copy && !CopiesActive;
            if(appearing){rippleAge=0;if(preview>.8f)reveal=.55f;}
            if(copy && rippleAge<seamFlashSeconds)target=Mathf.Max(target,.9f*(1-rippleAge/seamFlashSeconds));
            seam=Mathf.MoveTowards(seam,target,dt/(copy && rippleAge<seamFlashSeconds?seamFlashSeconds*.35f:guideFade));
            Color edge=room.Brightness>.12f?Color.black:Color.white;edge.a=seam;
            foreach(var line in seams){Tint(line,edge);line.widthMultiplier=PixelWidth(line.transform.TransformPoint(line.GetPosition(0)));}
            rippleAge+=dt;
            CopiesActive=copy;
            reveal=Mathf.MoveTowards(reveal,copy?1:0,dt/(copy?appearTime:disappearTime));
            preview=Mathf.MoveTowards(preview,draft&&!copy?1:0,dt/guideFade);
            if(room.box && room.box.Opened && !copy && retiring.Count>0){reveal=0;preview=0;}
            bool onFoldingWall=box || retiring.Count>0 && !copy && reveal>0;
            SetCut(interior && onFoldingWall && room.box.PanelCoverage<1);
            Vector3 centre=planeLocal;if(onFoldingWall)centre.x=foldedCentre;
            var plane=transform.TransformPoint(centre);var normal=transform.right;
            foreach(var t in twins){
                float w=Mathf.Max(preview,Mathf.Sin(reveal*Mathf.PI));
                bool separate=Mathf.Abs(Vector3.Dot(t.source.position-plane,normal))>PlaneTolerance;
                t.root.gameObject.SetActive(separate && !(cutting && room.box.PanelCoverage<=0) && (copy || reveal>0 || preview>0));
                if(!t.root.gameObject.activeSelf)continue;
                var p=plane+Vector3.Reflect(t.source.position-plane,normal);
                var q=Quaternion.LookRotation(Vector3.Reflect(t.source.forward,normal),Vector3.Reflect(t.source.up,normal));
                if(onFoldingWall && foldingWall){
                    var matrix=foldingWall.localToWorldMatrix*closedWall;
                    p=matrix.MultiplyPoint3x4(p);q=matrix.rotation*q;
                }
                t.root.SetPositionAndRotation(p,q);
                if(appearing){
                    int count=Physics.OverlapBoxNonAlloc(p,Vector3.Scale(new Vector3(mirrorSize.x*.5f,mirrorSize.y*.5f,.025f),transform.lossyScale),overlaps,q,
                        Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                    for(int i=0;i<count;i++)if(!overlaps[i].transform.IsChildOf(t.root)){
                        Debug.LogWarning("Mirror reflection overlaps "+overlaps[i].name+" at "+t.source.name,this);break;
                    }
                }
                for(int i=0;i<2;i++){
                    float u=rippleAge/rippleSeconds-i*.19f;
                    var ring=t.rings[i];ring.enabled=!cutting && copy && u>=0 && u<1;
                    if(ring.enabled){
                        ring.transform.localScale=Vector3.one*Mathf.Lerp(.06f,mirrorSize.x*.43f,u);
                        ring.widthMultiplier=PixelWidth(p);Tint(ring,new Color(1,1,1,(1-u)*.65f));
                    }
                }
                t.solid.gameObject.SetActive(copy || reveal>.5f);
                t.solid.localScale=Vector3.Scale(RelativeScale(t.source,t.root),new Vector3(1,Mathf.Max(.001f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.32f,1,reveal))),1));
                foreach(var c in t.colliders)c.enabled=copy;
                t.outline.enabled=t.fill.enabled=!cutting && w>.001f;
                t.outline.loop=preview>.97f || reveal>=.5f;
                if(t.outline.enabled){
                    float edgeProgress=Mathf.Max(preview,Mathf.Clamp01(reveal/.5f));
                    t.outline.positionCount=5;
                    for(int i=0;i<=4;i++){
                        float s=Mathf.Min(i,edgeProgress*4);int a=(int)s%4,b=(a+1)%4;
                        Vector3 Corner(int n)=>new Vector3((n<2?-1:1)*mirrorSize.x*.5f,(n==0||n==3?-1:1)*mirrorSize.y*.5f,-.012f);
                        t.outline.SetPosition(i,Vector3.Lerp(Corner(a),Corner(b),s-Mathf.Floor(s)));
                    }
                }
                edge.a=w;Tint(t.outline,edge);Tint(t.fill,new Color(1,1,1,.15f*w));
                t.outline.widthMultiplier=PixelWidth(p);
            }
        }
    }
}
