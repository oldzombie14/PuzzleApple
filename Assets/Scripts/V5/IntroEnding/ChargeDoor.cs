using System.Collections;
using System.Collections.Generic;
using PuzzleApple.V5.Cognition;
using UnityEngine;

namespace PuzzleApple.V5
{
    public sealed class ChargeDoor : MonoBehaviour, ISwitchableDoor
    {
        [Min(.1f)] public float width=3.6f,height=3.8f,chargeRadius=2.2f;
        [Min(.01f)] public float chargeRate=.19f,decayRate=.12f,bonus=1.5f;
        [Min(.1f)] public float riseSeconds=2.2f,lowerSeconds=1.6f,warningSeconds=.4f,fallSeconds=.5f;
        public AudioClip strainClip,slamClip;
        public Transform Leaf {get;private set;}
        public float Lift {get;private set;}
        public bool Charged=>energy[0]>=1 && energy[1]>=1 && energy[2]>=1;
        public bool Falling=>fallTime>=0;
        public bool Stunned {get;private set;}
        public float Energy(int rail)=>energy[rail];
        public float NicheBloomWeight(Transform eye)
        {
            var toDoor=transform.TransformPoint(new Vector3(0,height*.5f,LeafZ))-eye.position;
            float near=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(6,14,toDoor.magnitude));
            float facing=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.1f,.75f,Vector3.Dot(eye.forward,toDoor.normalized)));
            float charge=Mathf.Min(energy[0],Mathf.Min(energy[1],energy[2]));
            return Mathf.Lerp(1,.18f,near*facing*charge);
        }
        readonly float[] energy=new float[3];
        readonly Transform[] fills=new Transform[3];
        readonly Transform[] grooves=new Transform[3];
        Renderer frame,passageFloor;
        Mesh frameMesh;
        Material glow,inscription;
        MaterialPropertyBlock ink;
        PassageJourney journey;
        AudioSource audioSource;
        bool command,raise,cyclingUp=true;
        long cycle=-1;
        float fallTime=-1,fallFrom,flash,glyphGlow;
        public Vector3 RoomLanding=>transform.TransformPoint(new Vector3(0,.08f,-2));
        public Vector3 StairLanding=>transform.TransformPoint(new Vector3(0,.12f,2.35f));
        const float WallHalfDepth=.15f,Bevel=.20f,LeafDepth=.38f,RailWidth=.055f;
        public float MouthWidth=>width+2*Bevel;
        public float MouthHeight=>height+Bevel;
        public float LeafZ=>WallHalfDepth+LeafDepth*.5f;
        float RailX=>(MouthWidth+RailWidth)*.5f;
        public bool Owns(Collider hit)=>hit && (hit.transform==Leaf || hit.transform==frame.transform);
        public bool OnStairSide(Vector3 local)=>local.z>=journey.RoomStart+journey.roomSize.z && local.z<journey.RoomStart+journey.roomSize.z+(journey.Ascent?journey.Ascent.TopZ+4:4) && Mathf.Abs(local.x)<=width;
        public bool Paused=>!journey.enabled || !journey.entry.room.Opened || journey.entry.ConfirmingExit ||
            journey.entry.portableClosure&&journey.entry.portableClosure.Isolated;

        public void Build(PassageJourney owner)
        {
            journey=owner;ink=new MaterialPropertyBlock();
            transform.SetParent(owner.Root,false);
            transform.localPosition=Vector3.forward*(owner.RoomStart+owner.roomSize.z);
            glow=new Material(owner.lampMaterial);
            inscription=new Material(Resources.Load<Shader>("V5/DoorInscription"));
            audioSource=gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake=false;audioSource.spatialBlend=1;
            frame=BuildFrame();
            float run=WhiteAscent.StairStart;
            passageFloor=Block("Doorway floor",new Vector3(0,-.15f,run*.5f),new Vector3(MouthWidth,.3f,run),owner.roomMaterial,true).GetComponent<Renderer>();
            Leaf=Block("Charge door slab",new Vector3(0,height*.5f,LeafZ),new Vector3(width,height,LeafDepth),owner.roomMaterial,true);
            for(int i=0;i<3;i++){
                string word=new[]{"light","move","door"}[i];
                var mark=GameObject.CreatePrimitive(PrimitiveType.Quad);
                mark.name="Carved "+word;mark.transform.SetParent(transform,false);
                mark.transform.localPosition=new Vector3((i-1)*.93f,height*.52f,LeafZ-LeafDepth*.5f-.003f);
                mark.transform.localScale=Vector3.one*.78f;
                mark.transform.SetParent(Leaf,true);
                var col=mark.GetComponent<Collider>();col.enabled=false;Destroy(col);
                var glyph=mark.GetComponent<Renderer>();glyph.sharedMaterial=inscription;
                glyph.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                ink.Clear();ink.SetTexture("_MainTex",owner.entry.board.Catalog.Word(word).Symbol.texture);
                glyph.SetPropertyBlock(ink);
            }
            for(int i=0;i<fills.Length;i++){
                grooves[i]=Rail("Empty charge groove",owner.blackSurround);
                fills[i]=Rail("Charge fill",glow);
            }
            Draw();
        }
        Renderer BuildFrame()
        {
            // The three bevels share exact corner positions. The back of the bevel meets the slab face.
            Vector3[] Profile(float span,float top,float z)=>new[]{
                new Vector3(-span,0,z),new Vector3(-span,top,z),new Vector3(span,top,z),new Vector3(span,0,z)};
            var mouth=Profile(MouthWidth*.5f,MouthHeight,-WallHalfDepth);
            var opening=Profile(width*.5f,height,WallHalfDepth);
            var back=Profile(MouthWidth*.5f,MouthHeight,WallHalfDepth);
            var vertices=new List<Vector3>();var triangles=new List<int>();
            void Face(Vector3 a,Vector3 b,Vector3 c,Vector3? d=null){
                int first=vertices.Count;
                vertices.Add(a);vertices.Add(b);vertices.Add(c);
                triangles.Add(first);triangles.Add(first+1);triangles.Add(first+2);
                if(d.HasValue){
                    vertices.Add(d.Value);
                    triangles.Add(first);triangles.Add(first+2);triangles.Add(first+3);
                }
            }
            for(int i=0;i<3;i++){
                int next=i+1;
                Face(mouth[i],mouth[next],opening[next],opening[i]);
                Face(back[i],opening[i],opening[next],back[next]);
                Face(back[i],back[next],mouth[next],mouth[i]);
            }
            Face(mouth[0],opening[0],back[0]);
            Face(mouth[3],back[3],opening[3]);
            frameMesh=new Mesh{name="Three-sided door reveal"};
            frameMesh.SetVertices(vertices);frameMesh.SetTriangles(triangles,0);frameMesh.RecalculateNormals();
            var obj=new GameObject("Stone reveal",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));
            obj.transform.SetParent(transform,false);
            obj.GetComponent<MeshFilter>().sharedMesh=frameMesh;obj.GetComponent<MeshCollider>().sharedMesh=frameMesh;
            return obj.GetComponent<Renderer>();
        }
        Transform Rail(string label,Material material)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Quad);obj.name=label;
            obj.transform.SetParent(transform,false);
            var collider=obj.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            obj.GetComponent<Renderer>().sharedMaterial=material;
            obj.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            return obj.transform;
        }
        public void BindSurfaces(Renderer wall,Renderer floor)
        {
            Vector3 sample=transform.TransformPoint(new Vector3(0,height*.5f,-.3f));
            void Bind(Renderer piece,Renderer source,bool slab=false){
                piece.gameObject.layer=source.gameObject.layer;piece.sharedMaterial=source.sharedMaterial;
                ink.Clear();
                ink.SetVector("_NicheFillAnchor",new Vector4(sample.x,sample.y,sample.z,1));
                ink.SetFloat("_IsDoorLeaf",slab?2:0);
                // The bevel is one piece; the floor continues level through the open bottom.
                if(!slab)ink.SetFloat("_EdgeWidth",0);
                piece.SetPropertyBlock(ink);
            }
            Bind(frame,wall);Bind(Leaf.GetComponent<Renderer>(),wall,true);Bind(passageFloor,floor);
        }
        public Transform Block(string label,Vector3 position,Vector3 size,Material mat,bool solid)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=label;
            g.transform.SetParent(transform,false);g.transform.localPosition=position;g.transform.localScale=size;
            g.GetComponent<Renderer>().sharedMaterial=mat;
            if(!solid){var col=g.GetComponent<Collider>();col.enabled=false;Destroy(col);}
            return g.transform;
        }
        public Vector3 Slot(int index,int count)
        {
            // Distribute the perimeter by arc length, starting at the left foot.
            float arc=(index+.5f)/Mathf.Max(1,count)*(MouthHeight*2+2*RailX);
            Vector3 p=arc<MouthHeight?new Vector3(-RailX-.35f,arc,-.67f):
                arc<MouthHeight+2*RailX?new Vector3(arc-MouthHeight-RailX,MouthHeight+.38f,-.67f):
                new Vector3(RailX+.35f,2*MouthHeight+2*RailX-arc,-.67f);
            return transform.TransformPoint(p);
        }
        public bool WouldChange(bool open)=>Charged && !Falling && (open?Lift<1:Lift>0);
        public void SetOpen(bool open){
            if(!Charged || Falling){Reject();return;}
            command=true;raise=open;
        }
        public bool Driving=>command;
        public void Hold()=>command=false;
        public void Release()=>command=false;
        public void StartCycle(long established){
            if(!Charged || Falling){Reject();return;}
            cycle=established;
            if(Lift>=1)cyclingUp=false;
            else if(Lift<=0)cyclingUp=true;
        }
        public bool CanCycle=>Charged && !Falling && !command;
        public bool AcceptedCycle(long established)=>cycle==established;
        public void Reject(){flash=.35f;}
        float Weight(Vector3 p,int rail){
            var closest=rail==2?new Vector3(Mathf.Clamp(p.x,-RailX,RailX),MouthHeight,-WallHalfDepth):
                new Vector3(rail==0?-RailX:RailX,Mathf.Clamp(p.y,0,MouthHeight),-WallHalfDepth);
            return Mathf.Max(0,1-Vector3.Distance(closest,p)/chargeRadius);
        }
        void Update(){
            if(!Leaf || Paused || Time.deltaTime<=0)return;
            float dt=Time.deltaTime;
            var runtime=journey.entry.sentenceRuntime;
            const PassageJourney.LightSpace room=PassageJourney.LightSpace.FarRoom;
            float gain=chargeRate;
            if(runtime.HasEffect(CognitionSignal.IlluminateLight,room))gain*=bonus;
            if(runtime.HasEffect(CognitionSignal.IlluminateDoor,room))gain*=bonus;
            for(int i=0;i<3;i++){
                float power=0;
                if(journey.Brightness>0)foreach(var lamp in journey.Lamps)
                    if(lamp)power+=journey.Brightness*Weight(transform.InverseTransformPoint(lamp.transform.position),i);
                energy[i]=Mathf.Clamp01(energy[i]+(power>0?power*gain:-decayRate)*dt);
            }
            flash=Mathf.Max(0,flash-dt);
            float before=SlabFraction;
            if(!Charged && Lift>0 && !Falling){
                fallTime=0;fallFrom=before;command=false;cycle=-1;
                flash=warningSeconds;
                if(strainClip)audioSource.PlayOneShot(strainClip);
            }
            if(Falling){
                fallTime+=dt;
                float t=Mathf.Clamp01((fallTime-warningSeconds)/fallSeconds);
                Lift=Mathf.Max(0,fallFrom*(1-t*t)-Mathf.Min(fallFrom,.008f)*Mathf.Clamp01(fallTime/warningSeconds));
                if(t>=1 || Lift<=0){Lift=0;fallTime=-1;}
            }else if(Charged){
                bool swing=cycle>=0 && runtime.Owns(CognitionSignal.DoorMove,room,cycle);
                if(command || swing){
                    bool up=command?raise:cyclingUp;
                    Lift=Mathf.MoveTowards(Lift,up?1:0,dt/(up?riseSeconds:lowerSeconds));
                    if(Lift==(up?1:0))cyclingUp=!up;
                }
            }
            Leaf.localPosition=new Vector3(0,height*(.5f+SlabFraction),LeafZ);
            if(before>SlabFraction){
                CheckImpact(before);
                if(Lift==0){
                    if(journey.floorSurface)journey.floorSurface.Impact(transform.TransformPoint(Vector3.zero),Vector3.up);
                    if(slamClip)audioSource.PlayOneShot(slamClip);
                }
            }
            Draw();
        }
        void Draw(){
            var rt=journey.entry.sentenceRuntime;
            // The engraving remains visible below its gradually illuminated surface.
            inscription.SetFloat("_ExposureGain",rt?Mathf.Pow(2,-rt.WorldExposure):1);
            bool warm=rt && (rt.HasEffect(CognitionSignal.IlluminateDoor,PassageJourney.LightSpace.FarRoom)||rt.HasEffect(CognitionSignal.IlluminateLight,PassageJourney.LightSpace.FarRoom));
            Color color=warm?new Color(1,.31f,.10f):new Color(1,.94f,.81f);
            glow.SetColor("_EmissionColor",color*(flash>0?5:3));glow.SetColor("_BaseColor",color);
            glyphGlow=Mathf.MoveTowards(glyphGlow,Charged?1:0,Time.deltaTime/.8f);
            inscription.SetColor("_GlyphEmission",color*(3*glyphGlow));
            float topWidth=2*RailX+RailWidth;
            float progress=(energy[0]+energy[1]+energy[2])/3f;
            float remaining=Mathf.Max(flash>0?.015f:0,progress)*(2*MouthHeight+topWidth);
            for(int i=0;i<fills.Length;i++){
                float length=i==1?topWidth:MouthHeight;
                float t=Mathf.Clamp01(remaining/length);
                remaining-=length;
                DrawRail(fills[i],i,0,t);
                DrawRail(grooves[i],i,t,1);
            }
        }
        void DrawRail(Transform strip,int index,float from,float to)
        {
            bool vertical=index!=1;
            float length=vertical?MouthHeight:2*RailX+RailWidth;
            float span=(to-from)*length;
            strip.gameObject.SetActive(span>.00001f);
            if(span<=.00001f)return;
            float mid=(from+to)*.5f;
            float x=vertical?(index==0?-RailX:RailX):(mid-.5f)*length;
            float y=vertical?(index==0?mid:1-mid)*length:MouthHeight+RailWidth*.5f;
            strip.localPosition=new Vector3(x,y,-WallHalfDepth-.003f);
            strip.localScale=vertical?new Vector3(RailWidth,span,1):new Vector3(span,RailWidth,1);
        }
        float SlabFraction=>Falling?Lift:Mathf.SmoothStep(0,1,Lift);
        void CheckImpact(float before){
            if(Stunned)return;
            var player=journey.entry.player;
            var capsule=player.GetComponent<CharacterController>();
            var centre=transform.InverseTransformPoint(capsule.bounds.center);
            float head=centre.y+capsule.bounds.extents.y;
            if(Mathf.Abs(centre.x)>width*.5f+capsule.radius || Mathf.Abs(centre.z-LeafZ)>.2f+capsule.radius ||
                head<SlabFraction*height || centre.y-capsule.bounds.extents.y>before*height)return;
            StartCoroutine(Stun(centre.z>LeafZ));
        }
        IEnumerator Stun(bool stairs){
            Stunned=true;
            var e=journey.entry;var p=e.player;var panel=e.board.Panel;
            bool move=p.MovementLocked,look=p.CinematicLocked,input=panel.InputBlocked;
            var output=e.chamberLight.GetComponent<ReticleBackground>();
            p.MovementLocked=p.CinematicLocked=panel.InputBlocked=true;
            var start=p.view.rotation;var up=Quaternion.AngleAxis(-35,p.view.right)*start;
            try{
                for(float t=0;t<.38f;t+=Time.unscaledDeltaTime){
                    p.view.rotation=Quaternion.Slerp(start,up,Mathf.Clamp01(t/.3f));
                    output.WorldVisibility=1-Mathf.SmoothStep(0,1,t/.38f);yield return null;
                }
                output.WorldVisibility=0;
                p.Teleport(stairs?StairLanding:RoomLanding,transform.rotation);
                yield return null;
                for(float t=0;t<.5f;t+=Time.unscaledDeltaTime){output.WorldVisibility=Mathf.SmoothStep(0,1,t/.5f);yield return null;}
            }finally{output.WorldVisibility=1;p.MovementLocked=move;p.CinematicLocked=look;panel.InputBlocked=input;p.SuppressInteractionThisFrame();Stunned=false;}
        }
        void OnDestroy(){if(frameMesh)Destroy(frameMesh);if(glow)Destroy(glow);if(inscription)Destroy(inscription);}
    }
}
