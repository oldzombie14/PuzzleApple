using System.Collections;
using System.Collections.Generic;
using PuzzleApple.V5.Cognition;
using UnityEngine;

namespace PuzzleApple.V5
{
    public sealed class ChargeDoor : MonoBehaviour, ISwitchableDoor
    {
        [Min(.1f)] public float width=3.6f,height=3.8f;
        [Min(.01f)] public float chargeRate=.19f,decayRate=.12f,bonus=1.5f;
        [Min(.1f)] public float riseSeconds=2.2f,lowerSeconds=1.6f,warningSeconds=.4f,fallSeconds=.5f;
        public AudioClip strainClip,slamClip;
        public ObservationLesson lesson;
        [Header("Energy sources")]
        [Range(0,1)] public float baseEnergy=.03f,doorCapacity=.4f,dockedCapacity=.57f,passingCapacity=.1f;
        [Range(0,1)] public float initialEnergy=.15f,incompleteLimit=.95f;
        [Min(.1f)] public float passingRadius=4,doorEmission=1.5f,colorFadeSeconds=1.5f;
        [Min(0)] public float railGap=.18f;
        public Color fullWarmColor=new Color(1,.90f,.85f);
        public Transform Leaf {get;private set;}
        public float Lift {get;private set;}
        public float TotalEnergy {get;private set;}
        public float DoorGlow {get;private set;}=1;
        public float DoorEnergy=>doorEnergy;
        public float DockedEnergy=>dockedEnergy;
        public float PassingEnergy=>passingEnergy;
        public float Warmth=>warmth;
        public float PassingHint=>Mathf.Max(proximityHint[0],Mathf.Max(proximityHint[1],proximityHint[2]));
        public bool Charged=>TotalEnergy>=.99999f;
        public bool Falling=>fallTime>=0;
        public bool Stunned {get;private set;}
        public float NicheBloomWeight(Transform eye)
        {
            var toDoor=transform.TransformPoint(new Vector3(0,height*.5f,LeafZ))-eye.position;
            float near=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(6,14,toDoor.magnitude));
            float facing=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.1f,.75f,Vector3.Dot(eye.forward,toDoor.normalized)));
            return Mathf.Lerp(1,.12f,facing*Mathf.Lerp(.85f,1,near));
        }
        float doorEnergy,dockedEnergy,passingEnergy,warmth,fullLimit;
        readonly float[] proximityHint=new float[3];
        readonly float[] nextHint=new float[3];
        readonly Transform[] fills=new Transform[3];
        readonly Transform[] grooves=new Transform[3];
        Renderer frame,passageFloor;
        Mesh frameMesh;
        Material glow,inscription,doorInk;
        Light doorLight;
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
        float RailX=>(MouthWidth+RailWidth)*.5f+railGap;
        float RailHeight=>MouthHeight+railGap;
        public bool Owns(Collider hit)=>hit && (hit.transform==Leaf || hit.transform==frame.transform);
        public bool OnStairSide(Vector3 local)=>local.z>=journey.RoomStart+journey.roomSize.z && local.z<journey.RoomStart+journey.roomSize.z+(journey.Ascent?journey.Ascent.TopZ+4:4) && Mathf.Abs(local.x)<=width;
        public bool Paused=>!journey.enabled || !journey.entry.room.Opened || journey.entry.ConfirmingExit ||
            journey.entry.portableClosure&&journey.entry.portableClosure.Isolated;
        public bool LessonAcquired=>lesson && journey.entry.board.State.HasAcquired(lesson.acquisitionId);
        public bool LightHeld=>!LessonAcquired || journey.entry.sentenceRuntime.HasEffect(CognitionSignal.IlluminateDoor,PassageJourney.LightSpace.FarRoom);
        public bool TryCollectLesson(){
            if(!lesson || LessonAcquired || journey.entry.chamberLight.Collecting)return false;
            journey.entry.chamberLight.CollectObservation(lesson,showWord:false);return true;
        }

        public void Build(PassageJourney owner)
        {
            journey=owner;ink=new MaterialPropertyBlock();
            if(!lesson)lesson=Resources.Load<ObservationLesson>("V5/ChargeDoorLesson");
            doorEnergy=Mathf.Clamp(initialEnergy-baseEnergy,0,doorCapacity);TotalEnergy=baseEnergy+doorEnergy;
            fullLimit=incompleteLimit;
            transform.SetParent(owner.Root,false);
            transform.localPosition=Vector3.forward*(owner.RoomStart+owner.roomSize.z);
            glow=new Material(owner.lampMaterial);
            doorInk=new Material(owner.lampMaterial);
            // Emission lights the slab uniformly; its point light only lights the surroundings.
            doorInk.SetFloat("_SpecularHighlights",0);doorInk.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            doorInk.SetFloat("_EnvironmentReflections",0);doorInk.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            var emitter=new GameObject("Door soft light",typeof(Light));emitter.transform.SetParent(transform,false);
            emitter.transform.localPosition=new Vector3(0,height*.5f,-.7f);doorLight=emitter.GetComponent<Light>();
            doorLight.type=LightType.Point;doorLight.range=owner.LightStyle.LampRange(owner.LightStyle.LampIntensity(.25f));
            doorLight.shadows=LightShadows.Soft;doorLight.color=Color.white;
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
            frame.gameObject.layer=wall.gameObject.layer;Leaf.gameObject.layer=wall.gameObject.layer;
            frame.sharedMaterial=doorInk;Leaf.GetComponent<Renderer>().sharedMaterial=doorInk;
            frame.SetPropertyBlock(null);Leaf.GetComponent<Renderer>().SetPropertyBlock(null);
            Bind(passageFloor,floor);
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
        float DistanceToRail(Vector3 p,int rail){
            var closest=rail==1?new Vector3(Mathf.Clamp(p.x,-RailX,RailX),RailHeight,-WallHalfDepth):
                new Vector3(rail==0?-RailX:RailX,Mathf.Clamp(p.y,0,RailHeight),-WallHalfDepth);
            return Vector3.Distance(closest,p);
        }
        void Charge(float dt){
            var runtime=journey.entry.sentenceRuntime;
            const PassageJourney.LightSpace room=PassageJourney.LightSpace.FarRoom;
            bool held=LightHeld;
            // Hold the first reveal at its authored energy while the entrance is still locked.
            if((ProgressLock.Accessible(PassageJourney.LightSpace.All)&room)==0)return;
            float gain=chargeRate*(runtime.HasEffect(CognitionSignal.IlluminateLight,room)?bonus:1);
            int count=journey.Lamps.Count,docked=0;float passing=0;
            System.Array.Clear(nextHint,0,nextHint.Length);
            foreach(var lamp in journey.Lamps){
                if(!lamp)continue;
                if(lamp.Docked){docked++;continue;}
                var p=transform.InverseTransformPoint(lamp.transform.position);
                int nearest=0;float distance=DistanceToRail(p,0);
                for(int rail=1;rail<3;rail++){float next=DistanceToRail(p,rail);if(next<distance){distance=next;nearest=rail;}}
                float influence=Mathf.Clamp01(1-distance/passingRadius)*journey.Brightness;
                passing+=influence;nextHint[nearest]=Mathf.Max(nextHint[nearest],influence);
            }
            float dockTarget=count>0?dockedCapacity*docked/count*journey.Brightness:0;
            float passTarget=passingCapacity*Mathf.Clamp01(passing);
            float Approach(float current,float target)=>Mathf.MoveTowards(current,target,dt*(target>current?gain:decayRate));
            doorEnergy=Approach(doorEnergy,held?doorCapacity:0);
            dockedEnergy=Approach(dockedEnergy,dockTarget);passingEnergy=Approach(passingEnergy,passTarget);
            for(int i=0;i<3;i++)proximityHint[i]=Mathf.MoveTowards(proximityHint[i],nextHint[i],dt/.8f);
            bool full=held && count>0 && docked==count && journey.Brightness>=.999f;
            // Lifting the incomplete cap must not reveal accumulated energy in a single frame.
            fullLimit=full?Mathf.MoveTowards(fullLimit,1,dt*gain):incompleteLimit;
            TotalEnergy=Mathf.Clamp(baseEnergy+doorEnergy+dockedEnergy+passingEnergy,baseEnergy,fullLimit);
        }
        void Update(){
            if(!Leaf || Paused)return;
            DoorGlow=Mathf.MoveTowards(DoorGlow,LightHeld?1:0,Time.unscaledDeltaTime/.8f);
            if(Time.deltaTime<=0){Draw();return;}
            float dt=Time.deltaTime;
            var runtime=journey.entry.sentenceRuntime;
            const PassageJourney.LightSpace room=PassageJourney.LightSpace.FarRoom;
            Charge(dt);
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
            bool warm=Charged && rt && rt.HasEffect(CognitionSignal.IlluminateLight,PassageJourney.LightSpace.FarRoom);
            warmth=Mathf.MoveTowards(warmth,warm?1:0,Time.unscaledDeltaTime/colorFadeSeconds);
            Color color=Color.Lerp(Color.white,fullWarmColor,warmth);
            glow.SetColor("_EmissionColor",color*(flash>0?1.6f:1.2f));glow.SetColor("_BaseColor",color);
            doorInk.SetColor("_BaseColor",Color.Lerp(new Color(.015f,.015f,.015f),Color.black,DoorGlow));
            doorInk.SetColor("_EmissionColor",Color.white*(doorEmission*DoorGlow));
            doorLight.intensity=journey.LightStyle.LampIntensity(.25f)*DoorGlow;
            ink.Clear();ink.SetColor("_EmissionColor",Color.white*(doorEmission*DoorGlow+.25f*PassingHint));frame.SetPropertyBlock(ink);
            glyphGlow=Mathf.MoveTowards(glyphGlow,Charged?1:0,Time.unscaledDeltaTime/.8f);
            inscription.SetColor("_GlyphEmission",color*(.3f*glyphGlow));
            float topWidth=2*RailX+RailWidth;
            float remaining=Mathf.Max(flash>0?.015f:0,TotalEnergy)*(2*RailHeight+topWidth);
            for(int i=0;i<fills.Length;i++){
                float length=i==1?topWidth:RailHeight;
                float t=Mathf.Clamp01(remaining/length);
                remaining-=length;
                DrawRail(fills[i],i,0,t);
                DrawRail(grooves[i],i,t,1);
                ink.Clear();ink.SetColor("_BaseColor",Color.Lerp(Color.black,Color.white,proximityHint[i]*.35f));
                grooves[i].GetComponent<Renderer>().SetPropertyBlock(ink);
            }
        }
        void DrawRail(Transform strip,int index,float from,float to)
        {
            bool vertical=index!=1;
            float length=vertical?RailHeight:2*RailX+RailWidth;
            float span=(to-from)*length;
            strip.gameObject.SetActive(span>.00001f);
            if(span<=.00001f)return;
            float mid=(from+to)*.5f;
            float x=vertical?(index==0?-RailX:RailX):(mid-.5f)*length;
            float y=vertical?(index==0?mid:1-mid)*length:RailHeight+RailWidth*.5f;
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
        public void RecoverFromFall(){if(!Stunned)StartCoroutine(Stun(false,false));}
        IEnumerator Stun(bool stairs,bool impact=true){
            Stunned=true;
            var e=journey.entry;var p=e.player;var panel=e.board.Panel;
            bool move=p.MovementLocked,look=p.CinematicLocked,input=panel.InputBlocked;
            var output=e.chamberLight.GetComponent<ReticleBackground>();
            p.MovementLocked=p.CinematicLocked=panel.InputBlocked=true;
            var start=p.view.rotation;var up=Quaternion.AngleAxis(-35,p.view.right)*start;
            try{
                for(float t=0;t<.38f;t+=Time.unscaledDeltaTime){
                    if(impact)p.view.rotation=Quaternion.Slerp(start,up,Mathf.Clamp01(t/.3f));
                    output.WorldVisibility=1-Mathf.SmoothStep(0,1,t/.38f);yield return null;
                }
                output.WorldVisibility=0;
                p.Teleport(stairs?StairLanding:RoomLanding,transform.rotation);
                yield return null;
                for(float t=0;t<.5f;t+=Time.unscaledDeltaTime){output.WorldVisibility=Mathf.SmoothStep(0,1,t/.5f);yield return null;}
            }finally{output.WorldVisibility=1;p.MovementLocked=move;p.CinematicLocked=look;panel.InputBlocked=input;p.SuppressInteractionThisFrame();Stunned=false;}
        }
        void OnDestroy(){if(frameMesh)Destroy(frameMesh);if(glow)Destroy(glow);if(inscription)Destroy(inscription);if(doorInk)Destroy(doorInk);}
    }
}
