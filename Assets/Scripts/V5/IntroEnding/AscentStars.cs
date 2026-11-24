using UnityEngine;
using UnityEngine.Rendering;

namespace PuzzleApple.V5
{
    public sealed class AscentStars : MonoBehaviour,ISwitchableLight,IBrightenableLight, ISentenceOutputState<EmissionState>,
        ISentenceOutputState<AscentStars.MotionState>
    {
        internal SentenceStateOutput LightOutput;
        public SentenceMotionOutput MotionOutput {get;internal set;}
        struct Star
        {
            public MeshRenderer renderer;
            public Renderer halo;
            public Vector3 anchor;
            public float height,phase,frequency;
        }
        Star[] stars;
        Material ink;
        Material haloInk;
        MaterialPropertyBlock glow;
        public StarGlowStyle glowStyle;
        bool visible,collectible;
        PassageJourney journey;
        float brightness=1,target=1,motionTime;
        public float Brightness=>brightness;
        public bool Lit=>brightness>0 && journey && journey.Ascent && journey.Ascent.WhiteAmount<.5f;
        public bool WouldChange(bool on)=>on?brightness<1:brightness>0;
        public bool Settling(bool on)=>target==(on?1:0)&&brightness!=target;
        [System.Serializable]
        public struct MotionState { public float time; }
        MotionState ISentenceOutputState<MotionState>.CaptureState() => new MotionState { time = motionTime };
        public bool TryRestoreState(MotionState state)
        {
            if (!journey || !float.IsFinite(state.time) || state.time < 0) return false;
            motionTime = state.time;
            RefreshAppearance();
            return true;
        }
        void RefreshAppearance()
        {
            if (visible) DrawAt(journey.entry.player.view, journey.Ascent.WhiteAmount, sceneryTime, true);
        }
        float sceneryTime;
        bool driving;
        public void SetOn(bool on){driving=true;target=on?1:0;}
        public void Hold(){driving=false;target=brightness;}
        public EmissionState CaptureState() => new EmissionState(brightness);
        public bool TryRestoreState(EmissionState state)
        {
            if (!journey || !state.Valid) return false;
            brightness = state.brightness;
            if (!driving) target = brightness;
            RefreshAppearance();
            return true;
        }
        public bool CanMove(SentenceMotion signal)=>signal==SentenceMotion.Light && brightness>0;
        void Update(){if(journey)brightness=SentenceStateOutput.Advance(brightness,target,Time.unscaledDeltaTime/Mathf.Max(.01f,journey.lightFadeDuration),LightOutput);}

        public void Build(MeshFilter diamond,int count,float stairWidth,float start,float top,float rise,PassageJourney owner)
        {
            journey=owner;
            ink=new Material(diamond.GetComponent<Renderer>().sharedMaterial);
            if(!glowStyle)glowStyle=StarGlowStyle.Load();
            haloInk=new Material(Resources.Load<Shader>("V5/StarHalo"));
            haloInk.renderQueue=(int)RenderQueue.Transparent+54;glow=new MaterialPropertyBlock();
            ink.SetVector("_CorridorAxis",Vector4.zero);
            ink.SetFloat("_FacetStrength",0);
            ink.SetFloat("_FogInfluence",0);
            // The white veil is behind these stars when the player looks down the stair.
            ink.renderQueue=(int)RenderQueue.Transparent+55;
            var random=new System.Random(7181);
            float Range(float min,float max)=>Mathf.Lerp(min,max,(float)random.NextDouble());
            stars=new Star[count];
            for(int i=0;i<count;i++){
                float u=(i+Range(.05f,.95f))/count;
                float sign=(i%2==0)?-1:1;
                float lateral=stairWidth*.5f+Range(.7f,i%4<2?3.4f:10.5f);
                var anchor=new Vector3(sign*lateral,rise*u+Range(-1.4f,5.4f),Mathf.Lerp(start,top+4,u));
                var go=new GameObject("Floating star",typeof(MeshFilter),typeof(MeshRenderer));
                go.transform.SetParent(transform,false);
                go.transform.localPosition=anchor;
                go.GetComponent<MeshFilter>().sharedMesh=diamond.sharedMesh;
                var renderer=go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial=ink;renderer.shadowCastingMode=ShadowCastingMode.Off;
                renderer.receiveShadows=false;renderer.enabled=false;
                stars[i]=new Star{renderer=renderer,halo=StarGlowStyle.CreateHalo(go.transform,haloInk),anchor=anchor,height=Range(.19f,.66f),phase=Range(0,2*Mathf.PI),frequency=Range(.65f,1.4f)};
            }
        }

        public void Draw(Transform eye,float white,float time,bool show)
        {
            sceneryTime = time;
            if(brightness>0 && (MotionOutput?.ControlsLamps!=true||(MotionOutput?.HasMotion(SentenceMotion.Light)==true)))motionTime+=Time.deltaTime;
            DrawAt(eye, white, time, show);
        }
        void DrawAt(Transform eye,float white,float time,bool show)
        {
            var runtime=journey.entry.sentenceRuntime;
            const PassageJourney.LightSpace space=PassageJourney.LightSpace.Ascent;
            if(white<.5f)time=motionTime;
            if(!show && !visible)return;
            visible=show;
            float contrast=Mathf.Abs(1-2*white);
            collectible=show && brightness>0 && white<.5f && contrast>.15f;
            var emission=white<.5f?Color.white*(glowStyle?glowStyle.emission:1f)*brightness*runtime.LampBoost(space):Color.black;
            ink.SetColor("_EmissionColor",emission);
            ink.SetColor("_BaseColor",new Color(0,0,0,Mathf.SmoothStep(0,1,contrast)));
            glow.SetColor("_EmissionColor",emission);glow.SetColor("_BaseColor",ink.GetColor("_BaseColor"));
            foreach(var star in stars){
                star.renderer.enabled=show && (white>=.5f || brightness>0);
                if(glowStyle)glowStyle.DrawHalo(star.halo,show && white<.5f && brightness>0,glow);
                if(!show)continue;
                var t=star.renderer.transform;
                float beat=time*star.frequency+star.phase;
                t.localPosition=star.anchor+Vector3.up*(.12f*Mathf.Sin(beat));
                t.localScale=Vector3.one*(star.height*(.91f+.09f*Mathf.Sin(beat*1.3f)));
                t.rotation=Quaternion.LookRotation(t.position-eye.position,transform.up);
            }
        }

        public Renderer Pick(Ray ray,float reach)
        {
            if(!visible || !collectible || !gameObject.activeInHierarchy)return null;
            Renderer nearest=null;
            foreach(var star in stars){
                var r=star.renderer;
                if(!r.enabled || r.forceRenderingOff)continue;
                var t=r.transform;
                var plane=new Plane(t.forward,t.position);
                if(!plane.Raycast(ray,out float distance) || distance>=reach)continue;
                var p=t.InverseTransformPoint(ray.GetPoint(distance));
                var bounds=r.localBounds;
                if(Mathf.Abs(p.x)/bounds.extents.x+Mathf.Abs(p.y)/bounds.extents.y>1)continue;
                nearest=r;reach=distance;
            }
            return nearest;
        }
        void OnDestroy(){if(ink)Destroy(ink);if(haloInk)Destroy(haloInk);}
    }
}
