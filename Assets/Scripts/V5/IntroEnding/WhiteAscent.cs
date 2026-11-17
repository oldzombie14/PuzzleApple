using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace PuzzleApple.V5
{
    [DefaultExecutionOrder(250)]
    public sealed class WhiteAscent : MonoBehaviour
    {
        [Min(.1f)] public float stepRise=.25f,stepRun=.45f,totalRise=16,whiteBlendHeight=3.2f;
        [Min(0)] public float whiteStartHeight;
        internal const float StairStart=2f;
        public bool showHorizon=true;
        [Range(.5f,4)] public float outlinePixels=1.8f;
        [Range(0,120)] public int starCount=64;
        public AudioClip igniteClip,swellClip,flashClip;
        public bool Completed {get;private set;}
        public bool Playing {get;private set;}
        public float WhiteAmount {get;private set;}
        public Transform Symbol {get;private set;}
        public Transform Landing {get;private set;}
        public float TopZ {get;private set;}
        ChargeDoor door;
        PassageJourney journey;
        Material steps,mark,haze,rippleInk,veilMaterial;
        public AscentStars Stars {get;private set;}
        Renderer veil;
        Transform horizon,plinth;
        readonly LineRenderer[] symbolRipples=new LineRenderer[2];
        AltarParticles particles;
        Image flash;
        Volume volume,daylight;
        Bloom stairBloom;
        LiftGammaGain stairGain;
        VolumeProfile profile,daylightProfile;
        ColorAdjustments exposure;
        Camera cam;
        float time,baseSymbolY,baseY;
        public void Build(PassageJourney j,ChargeDoor d)
        {
            journey=j;door=d;cam=j.entry.player.view.GetComponent<Camera>();
            transform.SetParent(door.transform,false);
            steps=new Material(Resources.Load<Shader>("V5/AscentTreads"));
            steps.SetFloat("_InkPixels",outlinePixels);
            int count=Mathf.CeilToInt(totalRise/stepRise);
            float stairWidth=d.MouthWidth; // carry the width of the approach through the entire flight
            // Stair tops use a separate continuous ramp collider for the character capsule.
            var neighbours=new MaterialPropertyBlock();
            for(int i=0;i<count;i++){
                var block=door.Block("White tread",new Vector3(0,(i+.5f)*stepRise,StairStart+(i+.5f)*stepRun),new Vector3(stairWidth,stepRise,stepRun),steps,false);
                neighbours.SetVector("_TreadNeighbours",new Vector4(i>0?1:0,i<count-1?1:0,0,0));
                block.GetComponent<Renderer>().SetPropertyBlock(neighbours);
                block.SetParent(transform,true);
            }
            TopZ=StairStart+count*stepRun;baseY=count*stepRise;
            var ramp=new GameObject("Walking slope",typeof(BoxCollider)).transform;
            ramp.SetParent(transform,false);
            var along=new Vector3(0,baseY,count*stepRun);
            ramp.localRotation=Quaternion.LookRotation(along);
            ramp.localPosition=new Vector3(0,baseY*.5f,StairStart+count*stepRun*.5f)-ramp.localRotation*Vector3.up*.1f;
            ramp.GetComponent<BoxCollider>().size=new Vector3(stairWidth,.2f,along.magnitude);
            Landing=door.Block("Upper landing",new Vector3(0,baseY-.3f,TopZ+1.8f),new Vector3(stairWidth,.6f,3.65f),steps,true);
            Landing.SetParent(transform,true);
            plinth=door.Block("Symbol plinth",new Vector3(0,baseY+.24f,TopZ+2.1f),new Vector3(.55f,.48f,.55f),steps,true);
            plinth.SetParent(transform,true);
            mark=new Material(Resources.Load<Shader>("V5/AscentInk"));
            mark.SetTexture("_Mask",j.entry.board.Catalog.Word("light").Symbol.texture);
            mark.SetFloat("_MaskScale",1/.9f);
            Symbol=new GameObject("Sculpted light",typeof(MeshFilter),typeof(MeshRenderer),typeof(BoxCollider)).transform;
            Symbol.SetParent(transform,false);baseSymbolY=baseY+1.3f;
            Symbol.localPosition=new Vector3(0,baseSymbolY,TopZ+2.1f);
            var model=Resources.Load<GameObject>("V5/AscentLight");
            var mesh=model.GetComponentInChildren<MeshFilter>().sharedMesh;
            Symbol.GetComponent<MeshFilter>().sharedMesh=mesh;Symbol.GetComponent<Renderer>().sharedMaterial=mark;
            Symbol.localScale=Vector3.one*(.9f/Mathf.Max(.001f,mesh.bounds.size.y));
            Symbol.GetComponent<BoxCollider>().center=mesh.bounds.center;
            Symbol.GetComponent<BoxCollider>().size=mesh.bounds.size+Vector3.one*(.05f/Symbol.localScale.x);
            haze=new Material(Resources.Load<Shader>("V5/AscentInk"));haze.SetColor("_Face",new Color(.94f,.93f,.92f));haze.SetColor("_Edge",new Color(.8f,.79f,.78f));
            // Mask the black room exteriors behind the first step as the stair enters daylight.
            var backdrop=GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdrop.name="Fading lower world";backdrop.transform.SetParent(transform,false);
            backdrop.transform.localPosition=Vector3.forward*(d.LeafZ+.32f);
            backdrop.transform.localScale=Vector3.one*(cam.farClipPlane*3);
            var backCollider=backdrop.GetComponent<Collider>();backCollider.enabled=false;Destroy(backCollider);
            veilMaterial=new Material(Resources.Load<Shader>("V5/AscentVeil"));
            veil=backdrop.GetComponent<Renderer>();veil.sharedMaterial=veilMaterial;veil.enabled=false;
            veil.shadowCastingMode=ShadowCastingMode.Off;
            horizon=new GameObject("White horizon",typeof(LineRenderer)).transform;
            horizon.SetParent(transform,false);horizon.localPosition=new Vector3(0,baseY-4,TopZ);
            var line=horizon.GetComponent<LineRenderer>();line.useWorldSpace=false;line.loop=true;line.positionCount=128;line.widthMultiplier=.08f;line.sharedMaterial=haze;
            for(int i=0;i<line.positionCount;i++){
                float angle=i*Mathf.PI*2/line.positionCount;
                line.SetPosition(i,new Vector3(Mathf.Cos(angle)*110,0,Mathf.Sin(angle)*110));
            }
            horizon.gameObject.SetActive(false);
            var starMesh=j.Stars.GetComponentInChildren<MeshFilter>(true);
            Stars=new GameObject("Ascent stars").AddComponent<AscentStars>();
            Stars.transform.SetParent(transform,false);
            Stars.Build(starMesh,starCount,stairWidth,StairStart,TopZ,baseY);
            rippleInk=new Material(starMesh.GetComponent<Renderer>().sharedMaterial);
            rippleInk.SetVector("_CorridorAxis",Vector4.zero);
            rippleInk.SetFloat("_FogInfluence",0);
            rippleInk.SetColor("_EmissionColor",Color.black);
            rippleInk.SetColor("_BaseColor",new Color(.08f,.08f,.08f,.72f));
            for(int i=0;i<symbolRipples.Length;i++){
                var ring=new GameObject("Altar ripple",typeof(LineRenderer)).GetComponent<LineRenderer>();
                ring.transform.SetParent(transform,false);ring.transform.localPosition=Symbol.localPosition;
                ring.useWorldSpace=false;ring.loop=true;ring.positionCount=64;ring.widthMultiplier=.018f;ring.sharedMaterial=rippleInk;
                for(int k=0;k<64;k++){float angle=Mathf.PI*k/32;ring.SetPosition(k,new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0));}
                ring.enabled=false;symbolRipples[i]=ring;
            }
            particles=Symbol.gameObject.AddComponent<AltarParticles>();
            particles.Build(starMesh.sharedMesh,j.lampMaterial);
            flash=Instantiate(j.entry.blackBackground,j.entry.blackBackground.transform.parent);flash.name="Ascent white flash";flash.color=Color.clear;flash.raycastTarget=false;flash.gameObject.SetActive(false);
            // The room's wide bloom would spill across the black tread edges and stars on white.
            var upperLight=new GameObject("White stair daylight");upperLight.transform.SetParent(transform,false);
            daylight=upperLight.AddComponent<Volume>();daylight.isGlobal=true;daylight.priority=110;daylight.weight=0;
            daylightProfile=ScriptableObject.CreateInstance<VolumeProfile>();daylight.sharedProfile=daylightProfile;
            stairBloom=daylightProfile.Add<Bloom>();stairBloom.intensity.Override(2.1f);stairBloom.scatter.Override(.65f);stairBloom.threshold.Override(1.1f);
            stairGain=daylightProfile.Add<LiftGammaGain>();stairGain.gain.Override(new Vector4(1,1,1,0));
            volume=gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=120;profile=ScriptableObject.CreateInstance<VolumeProfile>();volume.sharedProfile=profile;
            exposure=profile.Add<ColorAdjustments>();exposure.postExposure.Override(0);
            var bloom=profile.Add<Bloom>();bloom.intensity.Override(2);bloom.threshold.Override(1.1f);volume.weight=0;
        }
        public bool LookingAtSymbol(){
            if(Completed || Playing || !journey.enabled || door.Paused || !journey.entry.player.CanInteractWithWorld)return false;
            var eye=journey.entry.player.view;
            return Physics.Raycast(eye.position,eye.forward,out var hit,5,~0,QueryTriggerInteraction.Ignore) && hit.collider.transform==Symbol;
        }
        public bool Ignite(){
            if(!LookingAtSymbol())return false;
            StartCoroutine(Depart(true));return true;
        }
        void LateUpdate(){
            if(!door || !journey.enabled){if(daylight)daylight.weight=0;return;}
            time+=Time.deltaTime;
            if(!Completed && !Playing){
                Symbol.localRotation=Quaternion.Euler(0,20*Mathf.Sin(time*.6f),0);
                Symbol.localPosition=new Vector3(0,baseSymbolY+.1f*Mathf.Sin(time*1.4f),TopZ+2.1f);
            }
            if(door.Paused){daylight.weight=0;return;}
            var eye=journey.entry.player.view;
            var local=transform.InverseTransformPoint(eye.position);
            bool upstairs=local.z>door.LeafZ && Mathf.Abs(local.x)<door.width;
            float heightAlongFlight=baseY*Mathf.InverseLerp(StairStart,TopZ,local.z);
            var threshold=whiteStartHeight>0?whiteStartHeight:(baseY-whiteBlendHeight)*.5f;
            WhiteAmount=upstairs && !Completed?Mathf.SmoothStep(0,1,Mathf.InverseLerp(threshold,threshold+whiteBlendHeight,heightAlongFlight)):0;
            if(!Playing){
                daylight.weight=upstairs?Mathf.SmoothStep(0,1,Mathf.InverseLerp(door.LeafZ,2.5f,local.z)):0;
                stairBloom.threshold.value=Mathf.Lerp(1.1f,1.8f,WhiteAmount);
                stairGain.gain.value=new Vector4(1,1,1,.7f*WhiteAmount);
            }
            Stars.Draw(eye,WhiteAmount,time,upstairs || (journey.InSecondRoom && door.Lift>.02f));
            veil.enabled=WhiteAmount>0;veilMaterial.SetFloat("_Amount",WhiteAmount);
            if(WhiteAmount>0){
                cam.backgroundColor=Color.Lerp(Color.black,Color.white,WhiteAmount);
                RenderSettings.fogColor=Color.Lerp(Color.black,Color.white,WhiteAmount);
                RenderSettings.fogDensity=Mathf.Lerp(RenderSettings.fogDensity,.065f,WhiteAmount);
                RenderSettings.ambientLight=Color.Lerp(RenderSettings.ambientLight,Color.white,WhiteAmount);
            }
            horizon.gameObject.SetActive(showHorizon && WhiteAmount>.98f);
            if(Completed && !Playing && upstairs && local.y>baseY && local.z>=TopZ+.6f)StartCoroutine(Depart(false));
        }
        IEnumerator Fade(float from,float to,float duration,bool white){
            for(float t=0;t<duration;t+=Time.unscaledDeltaTime){
                float k=Mathf.SmoothStep(0,1,t/duration),a=Mathf.Lerp(from,to,k);
                flash.color=new Color(white?1:0,white?1:0,white?1:0,a);
                if(white && to==1){
                    // Ignition has already settled the exposure so the diamonds stay visible.
                    exposure.postExposure.value=4*a;volume.weight=1;
                }else volume.weight=white?a:0;
                yield return null;
            }
            flash.color=new Color(white?1:0,white?1:0,white?1:0,to);volume.weight=white?to:0;
            if(white && to==1)exposure.postExposure.value=4;
        }
        IEnumerator Depart(bool first){
            Playing=true;
            var entry=journey.entry;var p=entry.player;var panel=entry.board.Panel;
            bool move=p.MovementLocked,look=p.CinematicLocked,input=panel.InputBlocked;
            entry.sentenceRuntime.presentation.Cancel();panel.SetOpen(false);
            p.MovementLocked=p.CinematicLocked=panel.InputBlocked=true;
            var voice=door.GetComponent<AudioSource>();
            try{
                flash.transform.SetAsLastSibling();flash.color=Color.clear;flash.gameObject.SetActive(true);
                if(first){
                    if(igniteClip)voice.PlayOneShot(igniteClip);
                    if(swellClip)voice.PlayOneShot(swellClip);
                    var facing=Quaternion.LookRotation(Vector3.ProjectOnPlane(Symbol.position-p.view.position,Vector3.up));
                    particles.Ignite();
                    foreach(var ring in symbolRipples){
                        ring.transform.position=Symbol.position;
                        ring.transform.rotation=Quaternion.LookRotation(Symbol.position-p.view.position,p.view.up);
                    }

                    for(float t=0;t<2.8f;t+=Time.unscaledDeltaTime){
                        float k=Mathf.SmoothStep(0,1,t/2.8f);
                        for(int i=0;i<symbolRipples.Length;i++){
                            float u=(t-.24f*i)/1.35f;
                            var ripple=symbolRipples[i];bool active=u>=0 && u<1;
                            if(active && !ripple.enabled)particles.Scatter(p.view,i);
                            ripple.enabled=active;
                            ripple.transform.localScale=Vector3.one*Mathf.Lerp(.06f,1.35f,u);
                            ripple.widthMultiplier=.028f*(1-Mathf.Clamp01(u));
                        }
                        volume.weight=Mathf.SmoothStep(0,1,t/.55f);
                        daylight.weight=1-volume.weight;
                        flash.color=new Color(0,0,0,.13f*Mathf.Sin(Mathf.PI*Mathf.Clamp01(t/.55f)));
                        mark.SetColor("_Face",Color.white*(1+5*k));mark.SetColor("_Edge",Color.Lerp(Color.black,Color.white,k));
                        Symbol.rotation=Quaternion.Slerp(Symbol.rotation,facing,Time.unscaledDeltaTime*3);
                        yield return null;
                    }
                    if(flashClip)voice.PlayOneShot(flashClip);
                }
                yield return Fade(0,1,first?.8f:.2f,first);
                if(first)yield return new WaitForSecondsRealtime(.6f);
                while(!entry.outdoor.Prepared)yield return null;
                entry.outdoor.ArriveFromAscent();
                Completed=true;WhiteAmount=0;veil.enabled=false;plinth.gameObject.SetActive(false);Symbol.gameObject.SetActive(false);horizon.gameObject.SetActive(false);
                yield return Fade(1,0,first?3:.2f,first);
            }finally{
                volume.weight=0;flash.gameObject.SetActive(false);p.AdoptCurrentView();
                p.MovementLocked=move;p.CinematicLocked=look;panel.InputBlocked=input;p.RestoreGameplayCursor();p.SuppressInteractionThisFrame();Playing=false;
            }
        }
        void OnDestroy(){
            if(flash)Destroy(flash.gameObject);
            if(profile){foreach(var part in profile.components)Destroy(part);Destroy(profile);}
            if(daylightProfile){foreach(var part in daylightProfile.components)Destroy(part);Destroy(daylightProfile);}
            if(steps)Destroy(steps);if(mark)Destroy(mark);if(haze)Destroy(haze);if(rippleInk)Destroy(rippleInk);if(veilMaterial)Destroy(veilMaterial);
        }
    }
}
