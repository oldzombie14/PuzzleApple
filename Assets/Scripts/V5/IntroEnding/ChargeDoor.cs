using System.Collections;
using System.Collections.Generic;
using PuzzleApple.V5.Cognition;
using UnityEngine;

namespace PuzzleApple.V5
{
    public sealed class ChargeDoor : MonoBehaviour, IProgressBoundary, ISentenceOutputState<ChargeDoor.LiftState>
    {
        [Min(.1f)] public float width=3.6f,height=3.8f;
        [Min(.01f)] public float chargeRate=.19f,decayRate=.12f,bonus=1.5f;
        [Min(.1f)] public float riseSeconds=2.2f,lowerSeconds=1.6f,warningSeconds=.4f,fallSeconds=.5f;
        public AudioClip strainClip,slamClip;
        public ObservationLesson lesson;
        [Header("Energy sources")]
        [Range(0,1)] public float baseEnergy=.005f,doorCapacity=.4f,dockedCapacity=.595f,passingCapacity=.1f;
        [Range(0,1)] public float initialEnergy=.15f,incompleteLimit=.95f;
        [Min(.1f)] public float passingRadius=4,doorEmission=1.5f,colorFadeSeconds=1.5f;
        [Min(0)] public float railGap=.18f;
        public Color fullWarmColor=new Color(1,.90f,.85f);
        public Transform Leaf {get;private set;}
        LiftState travel = new LiftState {fallTime = -1};
        public float Lift
        {
            get => travel.lift;
            private set { if (travel.lift == value) return; travel.lift = value; BoundaryChanged?.Invoke(); }
        }
        public float BoundaryProgress => Lift;
        public bool AllowsConnection => Lift > 0;
        public event System.Action BoundaryChanged;
        public float TotalEnergy {get;private set;}
        public float DoorGlow {get;private set;}=1;
        public float DoorEnergy=>doorEnergy;
        public float DockedEnergy=>dockedEnergy;
        public float PassingEnergy=>passingEnergy;
        public float Warmth=>warmth;
        public float PassingHint=>Mathf.Max(proximityHint[0],Mathf.Max(proximityHint[1],proximityHint[2]));
        public bool Charged=>TotalEnergy>=.99999f;
        public bool Falling=>travel.fallTime>=0;
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
        Renderer frame,passageFloor,slabSurface;
        Mesh frameMesh;
        Material glow,inscription;
        Light doorLight;
        MaterialPropertyBlock ink;
        PassageJourney journey;
        AudioSource audioSource;
        [System.Serializable]
        public struct LiftState
        {
            public float lift, fallTime, fallFrom;
        }
        public LiftState CaptureState() => travel;
        public bool TryRestoreState(LiftState state)
        {
            if (!Leaf || Stunned || !(state.lift >= 0 && state.lift <= 1) ||
                !(state.fallFrom >= 0 && state.fallFrom <= 1)) return false;
            bool falling = state.fallTime >= 0;
            if (falling ? !(state.fallTime < warningSeconds + fallSeconds && state.lift > 0 && state.lift <= state.fallFrom)
                        : state.fallTime != -1 || state.lift > 0 && !Charged) return false;
            float next = falling ? state.lift : Mathf.SmoothStep(0, 1, state.lift);
            if (next < SlabFraction && PlayerInDescent(SlabFraction, next)) return false;
            if (falling || Falling) flash = falling ? Mathf.Max(0, warningSeconds - state.fallTime) : 0;
            travel.fallFrom = state.fallFrom;
            travel.fallTime = state.fallTime;
            Lift = state.lift;
            Leaf.localPosition = new Vector3(0, height * (.5f + next), LeafZ);
            Physics.SyncTransforms();
            return true;
        }
        float flash,glyphGlow;
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
        public PassageJourney.LightSpace ControlSpaces=>ProgressLock.DoorBoundary(this);
        public bool LightHeld=>!LessonAcquired || journey.entry.sentenceRuntime.HasIntent(SentenceAction.Illuminate,SentenceCapabilityCatalog.Default.door,ControlSpaces);
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
            void Bind(Renderer piece,Renderer source){
                piece.gameObject.layer=source.gameObject.layer;piece.sharedMaterial=source.sharedMaterial;
                ink.Clear();
                ink.SetVector("_NicheFillAnchor",new Vector4(sample.x,sample.y,sample.z,1));
                // The bevel is one piece; the floor continues level through the open bottom.
                ink.SetFloat("_EdgeWidth",0);
                piece.SetPropertyBlock(ink);
            }
            slabSurface=Leaf.GetComponent<Renderer>();
            Bind(frame,wall);
            Bind(slabSurface,wall);
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
            float gain=chargeRate*(runtime.HasIntent(SentenceAction.Illuminate,SentenceCapabilityCatalog.Default.light,room)?bonus:1);
            float dockedPower=0,passing=0;
            System.Array.Clear(nextHint,0,nextHint.Length);
            foreach(var lamp in journey.Lamps){
                if(!lamp)continue;
                if(lamp.Docked){if(lamp.AtChargeDoor)dockedPower+=lamp.ChargePower;continue;}
                var p=transform.InverseTransformPoint(lamp.transform.position);
                int nearest=0;float distance=DistanceToRail(p,0);
                for(int rail=1;rail<3;rail++){float next=DistanceToRail(p,rail);if(next<distance){distance=next;nearest=rail;}}
                float influence=Mathf.Clamp01(1-distance/passingRadius)*journey.Brightness;
                passing+=influence;nextHint[nearest]=Mathf.Max(nextHint[nearest],influence);
            }
            float fraction=Mathf.Clamp01(dockedPower/Mathf.Max(.0001f,journey.ChargePowerRequired));
            float dockTarget=dockedCapacity*fraction*journey.Brightness;
            float passTarget=passingCapacity*Mathf.Clamp01(passing);
            float Approach(float current,float target)=>Mathf.MoveTowards(current,target,dt*(target>current?gain:decayRate));
            doorEnergy=Approach(doorEnergy,held?doorCapacity:0);
            dockedEnergy=Approach(dockedEnergy,dockTarget);passingEnergy=Approach(passingEnergy,passTarget);
            for(int i=0;i<3;i++)proximityHint[i]=Mathf.MoveTowards(proximityHint[i],nextHint[i],dt/.8f);
            bool full=held && fraction>=.99999f && journey.Brightness>=.999f;
            // Lifting the incomplete cap must not reveal accumulated energy in a single frame.
            fullLimit=full?Mathf.MoveTowards(fullLimit,1,dt*gain):incompleteLimit;
            TotalEnergy=Mathf.Clamp(baseEnergy+doorEnergy+dockedEnergy+passingEnergy,baseEnergy,fullLimit);
        }
        void Update(){
            if(!Leaf || Paused)return;
            DoorGlow=Mathf.MoveTowards(DoorGlow,LightHeld?1:0,Time.unscaledDeltaTime/.8f);
            if(Time.deltaTime<=0){Draw();return;}
            float dt=Time.deltaTime;
            Charge(dt);
            flash=Mathf.Max(0,flash-dt);
            float before=SlabFraction;
            if(!Charged && Lift>0 && !Falling){
                travel.fallTime=0;travel.fallFrom=before;
                flash=warningSeconds;
                if(strainClip)audioSource.PlayOneShot(strainClip);
            }
            if(Falling){
                travel.fallTime+=dt;
                float t=Mathf.Clamp01((travel.fallTime-warningSeconds)/fallSeconds);
                Lift=Mathf.Max(0,travel.fallFrom*(1-t*t)-Mathf.Min(travel.fallFrom,.008f)*Mathf.Clamp01(travel.fallTime/warningSeconds));
                if(t>=1 || Lift<=0){Lift=0;travel.fallTime=-1;}
            }else if(Charged){
                var feet=transform.InverseTransformPoint(journey.entry.player.transform.position);
                feet.z-=LeafZ;
                bool near=PassageEntranceDoor.WithinSensor(feet,width,height,journey.sensorRange,journey.sensorRange);
                Lift=Mathf.MoveTowards(Lift,near?1:0,dt/(near?riseSeconds:lowerSeconds));
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
            bool warm=Charged && rt && rt.HasIntent(SentenceAction.Illuminate,SentenceCapabilityCatalog.Default.light,PassageJourney.LightSpace.FarRoom);
            warmth=Mathf.MoveTowards(warmth,warm?1:0,Time.unscaledDeltaTime/colorFadeSeconds);
            Color color=Color.Lerp(Color.white,fullWarmColor,warmth);
            glow.SetColor("_EmissionColor",color*(flash>0?1.6f:1.2f));glow.SetColor("_BaseColor",color);
            float surfaceLevel=DoorGlow*(rt ? 1+.25f*(rt.LampBoost(PassageJourney.LightSpace.FarRoom)-1):1);
            if(slabSurface){
                slabSurface.GetPropertyBlock(ink);ink.SetFloat("_ChargeDoorEmission",doorEmission*surfaceLevel);
                slabSurface.SetPropertyBlock(ink);
            }
            doorLight.intensity=journey.LightStyle.LampIntensity(.25f)*DoorGlow;
            frame.GetPropertyBlock(ink);ink.SetFloat("_ChargeDoorEmission",doorEmission*surfaceLevel+.25f*PassingHint);frame.SetPropertyBlock(ink);
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
            if (Stunned || !PlayerInDescent(before, SlabFraction)) return;
            bool stairs = transform.InverseTransformPoint(journey.entry.player.GetComponent<CharacterController>().bounds.center).z > LeafZ;
            StartCoroutine(Stun(stairs));
        }
        bool PlayerInDescent(float before, float next)
        {
            var player=journey.entry.player;
            var capsule=player.GetComponent<CharacterController>();
            var centre=transform.InverseTransformPoint(capsule.bounds.center);
            float head=centre.y+capsule.bounds.extents.y;
            return Mathf.Abs(centre.x)<=width*.5f+capsule.radius && Mathf.Abs(centre.z-LeafZ)<=.2f+capsule.radius &&
                head>=next*height && centre.y-capsule.bounds.extents.y<=before*height;
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
        void OnDestroy(){if(frameMesh)Destroy(frameMesh);if(glow)Destroy(glow);if(inscription)Destroy(inscription);}
    }
}
