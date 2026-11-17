using UnityEngine;
using UnityEngine.Events;
using PuzzleApple.V5.Cognition;
using PuzzleApple.V5.Outdoor;

namespace PuzzleApple.V5
{
    [DefaultExecutionOrder(85)]
    public sealed class VitruvianPuzzle : MonoBehaviour,ISwitchableLight
    {
        public ChapterRoom room;
        public OpeningLight source,ball;
        public OpeningLightField field;
        public Transform rod,pivot;
        public Material lineMaterial;
        public Transform upper,lower;
        public float height=10,navelRatio=.607f;
        [Header("Square")]
        public float amplitude=30,halfPeriod=7,snapTolerance=3,snapSeconds=.35f;
        public float initialOffset=1,bottomPhaseOffset=0,bottomPeriodScale=1;
        public float mirrorLength=1.2f,beamWidth=.03f,meetingTolerance=.09f;
        [Range(2,16)] public int maxReflections=8;
        [Header("Circle")]
        public float orbitSeconds=8,startAngle=-30,trailWidth=.04f,trailFadeSeconds=1.5f;
        public UnityEvent<bool> SquareChanged=new UnityEvent<bool>(),CircleChanged=new UnityEvent<bool>();
        public bool SquareSolved {get;private set;}
        public bool CircleSolved {get;private set;}
        LightSentenceEffects createdLightMotion;
        CognitionSignal previousMotion;
        public bool CanMove(CognitionSignal signal)=>
            signal==CognitionSignal.LightMove && ball.Brightness>0 ||
            createdLightMotion && (signal==CognitionSignal.LightMove || signal==CognitionSignal.LightFloorMove ||
                signal==CognitionSignal.LightLightMove || signal==CognitionSignal.LightDoorMove) && createdLightMotion.CanMove(signal);
        public float UpperOffset {get;private set;}
        public float LowerOffset {get;private set;}
        public float OrbitAngle {get;private set;}
        float upperPhase,lowerPhase,snapTime=-1,fromUpper,fromLower;
        float arc,trailAlpha;
        bool swinging,orbiting;
        LineRenderer stem,trail;
        LineRenderer[] branches;
        LineRenderer[][] spots;
        LineRenderer[] celebration,sourceSpot;
        float rippleAge=2;
        MaterialPropertyBlock ink;
        readonly RaycastHit[] hits=new RaycastHit[32];
        Vector3 Foot=>transform.position;
        float Radius=>height*navelRatio;
        public bool DropOnBall(Ray ray)
        {
            if(!Physics.Raycast(ray,out var hit,100,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) ||
                hit.collider.GetComponentInParent<OpeningLight>()!=ball)return false;
            ball.SetOn(true);return true;
        }
        public bool WordAt(Collider c,out Renderer glow)
        {
            var light=c.GetComponentInParent<OpeningLight>();
            glow=light && light.Brightness>.001f?light.surface:null;
            return glow;
        }
        public bool WouldChange(bool on)=>on?source.Brightness<1 || ball.Brightness<1:source.Brightness>0 || ball.Brightness>0;
        public bool Settling(bool on)=>WouldChange(on);
        public void SetOn(bool on){source.SetOn(on);ball.SetOn(on);}
        public void Hold(){source.HoldCurrentState();ball.HoldCurrentState();}

        LineRenderer Line(string name,float width,bool loop=false)
        {
            var line=new GameObject(name,typeof(LineRenderer)).GetComponent<LineRenderer>();
            line.transform.SetParent(transform,false);line.sharedMaterial=lineMaterial;line.useWorldSpace=true;
            line.loop=loop;line.widthMultiplier=width;line.numCapVertices=2;
            line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;
            return line;
        }
        void Tint(LineRenderer line,float brightness,float opacity)
        {
            ink.Clear();ink.SetVector("_CorridorAxis",Vector4.zero);
            ink.SetColor("_BaseColor",new Color(1,1,1,opacity));ink.SetColor("_EmissionColor",Color.white*brightness);
            ink.SetFloat("_FogInfluence",0);line.SetPropertyBlock(ink);
        }
        LineRenderer[] Rings(string name)
        {
            var result=new[]{Line(name,beamWidth*.65f,true),Line(name,beamWidth*.45f,true)};
            foreach(var line in result){line.positionCount=40;line.enabled=false;}
            return result;
        }
        void Ring(LineRenderer line,Vector3 centre,float radius){
            for(int i=0;i<line.positionCount;i++){
                float a=2*Mathf.PI*i/line.positionCount;
                line.SetPosition(i,centre+radius*(transform.right*Mathf.Cos(a)+transform.up*Mathf.Sin(a)));
            }
        }
        void Start(){
            createdLightMotion=field.gameObject.AddComponent<LightSentenceEffects>();
            createdLightMotion.field=field;createdLightMotion.ground=field.ground;createdLightMotion.stateSource=source;
            ink=new MaterialPropertyBlock();
            upperPhase=Mathf.Asin(Mathf.Clamp(initialOffset/Mathf.Max(.01f,amplitude),-1,1));
            lowerPhase=upperPhase+bottomPhaseOffset*Mathf.Deg2Rad;
            UpperOffset=amplitude*Mathf.Sin(upperPhase);LowerOffset=amplitude*Mathf.Sin(lowerPhase);
            PoseMirrors();
            OrbitAngle=startAngle;PoseOrbit();
            stem=Line("Light to splitter",beamWidth);
            branches=new[]{Line("Left reflected ray",beamWidth),Line("Right reflected ray",beamWidth)};
            spots=new[]{Rings("Left beam spot"),Rings("Right beam spot")};
            celebration=Rings("Completion ripple");sourceSpot=Rings("Blocked source spot");
            trail=Line("Orbital light trail",trailWidth);
        }
        void PoseMirrors(){
            upper.localRotation=Quaternion.Euler(0,0,45+UpperOffset);
            lower.localRotation=Quaternion.Euler(0,0,-45+LowerOffset);
        }
        void AdvanceMirrors(bool moving)
        {
            if(moving){
                snapTime=-1;
                float step=Mathf.PI*Time.deltaTime/Mathf.Max(.05f,halfPeriod);
                upperPhase+=step;lowerPhase+=step/Mathf.Max(.01f,bottomPeriodScale);
                UpperOffset=amplitude*Mathf.Sin(upperPhase);LowerOffset=amplitude*Mathf.Sin(lowerPhase);
            }else{
                if(swinging && Mathf.Max(Mathf.Abs(UpperOffset),Mathf.Abs(LowerOffset))<=snapTolerance){
                    snapTime=0;fromUpper=UpperOffset;fromLower=LowerOffset;
                }
                if(snapTime>=0){
                    snapTime+=Time.unscaledDeltaTime;
                    float u=Mathf.Clamp01(snapTime/Mathf.Max(.01f,snapSeconds));
                    float k=(1-u)*(1-u)*Mathf.Cos(1.35f*Mathf.PI*u);
                    UpperOffset=fromUpper*k;LowerOffset=fromLower*k;
                    // Preserve the branch of the sine wave, including travel direction.
                    upperPhase=NearestPhase(upperPhase,UpperOffset);lowerPhase=NearestPhase(lowerPhase,LowerOffset);
                    if(u>=1)snapTime=-1;
                }
            }
            swinging=moving;PoseMirrors();
        }
        float NearestPhase(float phase,float offset){
            float baseAngle=Mathf.Asin(Mathf.Clamp(offset/Mathf.Max(.001f,amplitude),-1,1));
            return Mathf.Cos(phase)>=0?baseAngle:Mathf.PI-baseAngle;
        }
        Vector3 OrbitPoint(float degrees){
            float a=degrees*Mathf.Deg2Rad;
            return pivot.position+Radius*(transform.right*Mathf.Cos(a)+transform.up*Mathf.Sin(a));
        }
        void PoseOrbit(){
            var end=OrbitPoint(OrbitAngle);ball.transform.position=end;
            rod.position=(pivot.position+end)*.5f;rod.up=(end-pivot.position).normalized;
            var s=rod.localScale;s.y=Radius;rod.localScale=s;
        }
        void Update(){
            if(!room.Entry)return;
            var runtime=room.Entry.sentenceRuntime;
            field.observer=room.Entry.player.transform;
            AdvanceMirrors(runtime.HasEffect(CognitionSignal.MirrorMove,room.Space) && !runtime.MotionFrozen);
            bool move=runtime.HasEffect(CognitionSignal.LightMove,room.Space);
            var motion=move?CognitionSignal.LightMove:
                runtime.HasEffect(CognitionSignal.LightFloorMove,room.Space)?CognitionSignal.LightFloorMove:
                runtime.HasEffect(CognitionSignal.LightLightMove,room.Space)?CognitionSignal.LightLightMove:
                runtime.HasEffect(CognitionSignal.LightDoorMove,room.Space)?CognitionSignal.LightDoorMove:CognitionSignal.Empty;
            if(previousMotion!=motion){previousMotion=motion;createdLightMotion.Apply(signal=>signal==motion);}
            float boost=runtime.LampBoost(room.Space);source.SetLampMultiplier(boost);ball.SetLampMultiplier(boost);
            bool running=move && ball.TargetOn && ball.Brightness>.001f && !runtime.MotionFrozen;
            if(running){
                if(!orbiting)arc=0;
                float step=360*Time.deltaTime/Mathf.Max(.1f,orbitSeconds);
                OrbitAngle=Mathf.Repeat(OrbitAngle+step,360);
                arc=Mathf.Min(360,arc+step);trailAlpha=1;PoseOrbit();
            }else trailAlpha=Mathf.MoveTowards(trailAlpha,0,Time.unscaledDeltaTime/Mathf.Max(.01f,trailFadeSeconds));
            orbiting=running;
            bool circle=running && arc>=360;
            if(circle!=CircleSolved){CircleSolved=circle;CircleChanged.Invoke(circle);if(circle)Pulse(pivot.position);}
            trail.enabled=trailAlpha>0 && arc>0;
            if(trail.enabled){
                trail.positionCount=Mathf.Max(2,Mathf.CeilToInt(arc/3)+1);
                for(int i=0;i<trail.positionCount;i++)trail.SetPosition(i,OrbitPoint(OrbitAngle-arc*i/(trail.positionCount-1)));
                Tint(trail,circle?4:2,trailAlpha);
            }
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        Vector2 Local(Vector3 p)=>transform.InverseTransformPoint(p);
        Vector3 World(Vector2 p)=>transform.TransformPoint(new Vector3(p.x,p.y,0));
        float DistanceToObstacle(Vector3 origin,Vector3 direction,float maximum)
        {
            int count=Physics.RaycastNonAlloc(origin,direction,hits,maximum,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            float distance=maximum;
            for(int i=0;i<count;i++){
                var collider=hits[i].collider;
                if(collider.transform.IsChildOf(transform) || room.mirrors.IsMirror(collider))continue;
                distance=Mathf.Min(distance,hits[i].distance);
            }
            return distance;
        }
        bool Trace(int side)
        {
            Vector2 p=Local(room.mirrors.splitter.position);
            Vector2 d=side==0?Vector2.left:Vector2.right;
            var line=branches[side];line.positionCount=maxReflections+2;line.SetPosition(0,World(p));int count=1;
            bool met=false;
            for(int bounce=0;bounce<=maxReflections;bounce++){
                float distance=DistanceToObstacle(World(p),transform.TransformDirection(d),height*4);
                Vector2 normal=Vector2.zero;
                for(int i=0;i<(room.mirrors.CopiesActive?4:2);i++){
                    var mirror=i<2?room.mirrors.originals[i]:room.mirrors.Copy(i-2);
                    if(!mirror.gameObject.activeInHierarchy)continue;
                    Vector2 tangent=transform.InverseTransformDirection(mirror.right);
                    Vector2 delta=Local(mirror.position)-p;
                    float den=Cross(d,tangent);if(Mathf.Abs(den)<.0001f)continue;
                    float t=Cross(delta,tangent)/den,s=Cross(delta,d)/den;
                    if(t<.007f || t>=distance || Mathf.Abs(s)>mirrorLength*.5f)continue;
                    distance=t;normal=new Vector2(-tangent.y,tangent.x);
                }
                float along=Vector2.Dot(-p,d);
                if(bounce>=2 && along>=0 && along<distance && (p+d*along).magnitude<=meetingTolerance){
                    distance=along;met=true;
                }
                p+=d*distance;line.SetPosition(count++,World(p));
                if(normal==Vector2.zero || met)break;
                d=Vector2.Reflect(d,normal).normalized;p+=d*.01f;
            }
            line.positionCount=count;
            for(int i=0;i<2;i++){var ring=spots[side][i];ring.enabled=true;Ring(ring,World(p),beamWidth*(2+i*1.8f));Tint(ring,2,1);}
            return met;
        }
        void Pulse(Vector3 centre){rippleAge=0;pulseCentre=centre;}
        Vector3 pulseCentre;
        void LateUpdate(){
            if(!room.Entry || !trail)return;
            bool lit=source.Brightness>.001f;
            stem.enabled=lit;
            var origin=source.transform.position;
            var toSplitter=room.mirrors.splitter.position-origin;
            float length=toSplitter.magnitude;
            float travel=lit?DistanceToObstacle(origin,toSplitter.normalized,length):0;
            bool reachesSplitter=lit && travel>=length-.001f;
            var end=origin+toSplitter.normalized*travel;
            if(lit){stem.positionCount=2;stem.SetPosition(0,origin);stem.SetPosition(1,end);}
            for(int i=0;i<sourceSpot.Length;i++){
                var ring=sourceSpot[i];ring.enabled=lit && !reachesSplitter;
                if(ring.enabled){Ring(ring,end,beamWidth*(2+i*1.8f));Tint(ring,2,source.Brightness);}
            }
            foreach(var line in branches)line.enabled=reachesSplitter;
            bool left=reachesSplitter && Trace(0),right=reachesSplitter && Trace(1);
            if(!reachesSplitter)foreach(var pair in spots)foreach(var ring in pair)ring.enabled=false;
            bool square=left && right && room.mirrors.CopiesActive && !swinging && snapTime<0 &&
                Mathf.Max(Mathf.Abs(UpperOffset),Mathf.Abs(LowerOffset))<=.5f;
            if(square!=SquareSolved){SquareSolved=square;SquareChanged.Invoke(square);if(square)Pulse(Foot);}
            if(lit){Tint(stem,2,source.Brightness);foreach(var line in branches)Tint(line,square?4:2,source.Brightness);}
            rippleAge+=Time.unscaledDeltaTime;
            for(int i=0;i<2;i++){float t=rippleAge-.18f*i;var line=celebration[i];line.enabled=t>=0 && t<1;
                if(line.enabled){Ring(line,pulseCentre,.12f+t*1.05f);Tint(line,3,1-t);}}
        }
        void OnDrawGizmosSelected(){
            Gizmos.matrix=transform.localToWorldMatrix;Gizmos.color=Color.white;
            Gizmos.DrawWireCube(Vector3.up*height*.5f,new Vector3(height,height,.01f));
            Vector3 last=Vector3.up*Radius+Vector3.right*Radius;
            for(int i=1;i<=64;i++){float a=i*Mathf.PI/32;var p=Vector3.up*Radius+new Vector3(Mathf.Cos(a),Mathf.Sin(a))*Radius;Gizmos.DrawLine(last,p);last=p;}
        }
    }
}
