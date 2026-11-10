using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace PuzzleApple.V5.Studies
{
    public sealed class ShaderStudyController : MonoBehaviour
    {
        [Serializable] public class Parameter
        {
            public string label,property;
            public float min,max;
        }
        [Serializable] public class Preset
        {
            public string label;
            [TextArea] public string description;
            public Material material;
            public Parameter[] parameters;
        }
        [Header("Stage / editable scene objects")]
        public Camera stageCamera;
        public Transform focus;
        public Renderer[] subjects;
        public Renderer floor;
        public ParticleSystem squares;
        public StudyRain rain;
        public StudyRainCurtain rainCurtain;
        public Renderer[] clockTargets;
        public bool[] presetRain;
        public Preset[] presets;
        public Shader colorMapShader;
        [Header("Native UI")]
        public Canvas canvas;
        public RectTransform preview;
        public RawImage sourceImage,mappedImage;
        public GameObject splitMarker;
        public Text caption,status,description;
        public Button[] presetButtons;
        public Button targetButton,resetButton;
        public Slider[] parameterSliders;
        public Text[] parameterLabels;
        public Toggle applyToggle,postToggle,compareToggle,particleToggle,pauseToggle,invertToggle,orthoToggle;
        public Slider postAmount,postBlack,postWhite,postGamma,postSteps,particleCount,particleSize;
        public Text[] postLabels;
        public Toggle rainToggle;
        public Slider[] rainSliders;
        public Text[] rainLabels;
        public Slider[] rainDetailSliders;
        public Text[] rainDetailLabels;
        public Button copyButton;
        [Header("Initial view")]
        public float yaw=25,pitch=19,distance=12;
        public int selectedPreset,targetMode=1;
        public float Clock {get;private set;}
        public RenderTexture SourceFrame=>source;
        public RenderTexture MappedFrame=>mapped;
        public Material CurrentMaterial=>materials!=null?materials[selectedPreset]:null;
        Material[] materials;
        Material mapMaterial;
        Material[][] originals;
        Renderer[] all;
        Texture2D ramp;
        RenderTexture source,mapped,oldTarget;
        int lastSteps=-1;
        ParticleSystem.Particle[] particles=new ParticleSystem.Particle[128];
        bool ready;
        Vector3 initialView;
        MaterialPropertyBlock clockProperties;

        void Start()
        {
            clockProperties=new MaterialPropertyBlock();
            all=subjects.Concat(new[]{floor}).ToArray();
            originals=all.Select(x=>x.sharedMaterials).ToArray();
            materials=presets.Select(x=>new Material(x.material){hideFlags=HideFlags.DontSave}).ToArray();
            oldTarget=stageCamera.targetTexture;
            initialView=new Vector3(yaw,pitch,distance);
            for(int i=0;i<presetButtons.Length;i++){int index=i;presetButtons[i].onClick.AddListener(()=>SelectPreset(index));}
            applyToggle.onValueChanged.AddListener(_=>Apply());
            postToggle.onValueChanged.AddListener(_=>UpdateImages());
            compareToggle.onValueChanged.AddListener(_=>UpdateImages());
            particleToggle.onValueChanged.AddListener(value=>squares.gameObject.SetActive(value));
            invertToggle.onValueChanged.AddListener(value=>{foreach(var m in materials)if(m.HasProperty("_Invert"))m.SetFloat("_Invert",value?1:0);});
            targetButton.onClick.AddListener(()=>{targetMode=(targetMode+1)%4;Apply();});
            resetButton.onClick.AddListener(()=>{yaw=initialView.x;pitch=initialView.y;distance=initialView.z;});
            rainToggle.onValueChanged.AddListener(value=>{if(rain)rain.gameObject.SetActive(value);if(rainCurtain)rainCurtain.gameObject.SetActive(value);});
            copyButton.onClick.AddListener(CopyRecipe);
            ready=true;
            SelectPreset(Mathf.Clamp(selectedPreset,0,presets.Length-1));
            squares.gameObject.SetActive(particleToggle.isOn);
            if(rain)rain.gameObject.SetActive(rainToggle.isOn);
            if(rainCurtain)rainCurtain.gameObject.SetActive(rainToggle.isOn);
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            RenderPipelineManager.endCameraRendering+=AfterCamera;
            Resize();UpdateImages();
        }
        public void SelectPreset(int index)
        {
            if(!ready)return;
            selectedPreset=Mathf.Clamp(index,0,presets.Length-1);
            if(presetRain!=null&&selectedPreset<presetRain.Length)rainToggle.isOn=presetRain[selectedPreset];
            var preset=presets[selectedPreset];
            description.text=preset.description;
            caption.text=preset.label.ToUpperInvariant();
            for(int i=0;i<presetButtons.Length;i++)
            {
                var colors=presetButtons[i].colors;
                colors.normalColor=i==selectedPreset?new Color(.84f,.84f,.80f):new Color(.17f,.18f,.18f);
                presetButtons[i].colors=colors;
                presetButtons[i].GetComponentInChildren<Text>().color=i==selectedPreset?Color.black:new Color(.8f,.82f,.8f);
            }
            for(int i=0;i<parameterSliders.Length;i++)
            {
                var slider=parameterSliders[i];slider.onValueChanged.RemoveAllListeners();
                bool used=i<preset.parameters.Length;slider.transform.parent.gameObject.SetActive(used);
                if(!used)continue;
                int slot=i;var p=preset.parameters[i];slider.minValue=p.min;slider.maxValue=p.max;
                slider.SetValueWithoutNotify(CurrentMaterial.GetFloat(p.property));
                parameterLabels[i].text=p.label+"   "+slider.value.ToString("0.00");
                slider.onValueChanged.AddListener(value=>{CurrentMaterial.SetFloat(p.property,value);parameterLabels[slot].text=p.label+"   "+value.ToString("0.00");});
            }
            Apply();
        }
        public void Apply()
        {
            if(!ready)return;
            for(int i=0;i<all.Length;i++)
            {
                bool target=targetMode==3||(targetMode==0&&i==0)||(targetMode==1&&i<subjects.Length)||(targetMode==2&&i==subjects.Length);
                all[i].sharedMaterials=target&&applyToggle.isOn?new[]{CurrentMaterial}:originals[i];
            }
            targetButton.GetComponentInChildren<Text>().text="TARGET   /   "+new[]{"CUBE","ALL SOLIDS","FLOOR","ALL SURFACES"}[targetMode];
        }
        public void Orbit(Vector2 delta){yaw+=delta.x*.25f;pitch=Mathf.Clamp(pitch-delta.y*.2f,-10,75);}
        public void Zoom(float delta){distance=Mathf.Clamp(distance-delta*.6f,5,24);}
        void Update()
        {
            if(!ready)return;
            if(!pauseToggle.isOn)Clock+=Time.unscaledDeltaTime;
            foreach(var m in materials)if(m.HasProperty("_StudyTime"))m.SetFloat("_StudyTime",Clock);
            if(clockTargets!=null)foreach(var r in clockTargets)if(r){r.GetPropertyBlock(clockProperties);clockProperties.SetFloat("_StudyTime",Clock);r.SetPropertyBlock(clockProperties);}
            stageCamera.transform.position=focus.position+Quaternion.Euler(pitch,yaw,0)*Vector3.back*distance;
            stageCamera.transform.LookAt(focus);
            stageCamera.orthographic=orthoToggle.isOn;stageCamera.orthographicSize=distance*.36f;
            Resize();UpdateImages();UpdateParticles();
            if(rainToggle.isOn&&rain)
            {
                rain.density=Mathf.RoundToInt(rainSliders[0].value);rain.length=rainSliders[1].value;rain.width=rainSliders[2].value;
                rain.fallSpeed=rainSliders[3].value;rain.speedVariation=rainSliders[4].value;rain.angle=rainSliders[5].value;rain.gust=rainSliders[6].value;
                rain.Tick(Clock);
            }
            if(rainToggle.isOn&&rainCurtain)
            {
                rainCurtain.density=rainSliders[0].value;rainCurtain.length=rainSliders[1].value;rainCurtain.widthPixels=rainSliders[2].value;
                rainCurtain.fallSpeed=rainSliders[3].value;rainCurtain.speedVariation=rainSliders[4].value;rainCurtain.tailFade=rainSliders[5].value;rainCurtain.headFade=rainSliders[6].value;
                if(rainDetailSliders!=null&&rainDetailSliders.Length==1)
                {
                    rainCurtain.pulseSpacing=rainDetailSliders[0].value;
                }
                rainCurtain.Tick(Clock);
            }
            string[] names=rainCurtain?new[]{"Density / all three layers","Streak length (world)","Stroke width (pixels)","Fall speed","Speed variation","Upper fade-out fraction","Lower fade-in fraction"}:new[]{"Density","Length","Width","Fall speed","Speed variation","Slant angle","Gust amplitude"};
            for(int i=0;i<rainSliders.Length;i++)rainLabels[i].text=names[i]+"   "+rainSliders[i].value.ToString("0.000");
            if(rainDetailSliders!=null&&rainDetailSliders.Length==1)
            {
                rainDetailLabels[0].text="Pulse spacing   "+rainDetailSliders[0].value.ToString("0.00");
            }
            postLabels[0].text="Amount   "+postAmount.value.ToString("0.00");
            postLabels[1].text="Black point   "+postBlack.value.ToString("0.00");
            postLabels[2].text="White point   "+postWhite.value.ToString("0.00");
            postLabels[3].text="Midtone   "+postGamma.value.ToString("0.00");
            postLabels[4].text="Gray steps   "+(postSteps.value<2?"continuous":Mathf.RoundToInt(postSteps.value).ToString());
            status.text=(pauseToggle.isOn?"PAUSED":"LIVE")+"   /   "+(postToggle.isOn?(compareToggle.isOn?"RAW | COLOR MAP":"COLOR MAP ON"):"COLOR MAP OFF")+"   /   DRAG TO ORBIT  -  SCROLL TO ZOOM";
        }
        public void CopyRecipe()
        {
            var text=new System.Text.StringBuilder("Shader Study: "+presets[selectedPreset].label+"\nTarget: "+targetMode+"\n");
            foreach(var p in presets[selectedPreset].parameters)text.AppendLine(p.property+"="+CurrentMaterial.GetFloat(p.property).ToString("0.000"));
            text.AppendLine("Invert="+invertToggle.isOn+"; Apply="+applyToggle.isOn);
            text.AppendLine("ColorMap="+postToggle.isOn+"; Amount="+postAmount.value+"; Black="+postBlack.value+"; White="+postWhite.value+"; Gamma="+postGamma.value+"; Steps="+postSteps.value);
            text.AppendLine("Squares="+particleToggle.isOn+"; Count="+particleCount.value+"; Size="+particleSize.value);
            text.AppendLine("Rain="+rainToggle.isOn);
            for(int i=0;i<rainSliders.Length;i++)text.AppendLine(rainLabels[i].text);
            if(rainDetailLabels!=null)foreach(var detail in rainDetailLabels)if(detail)text.AppendLine(detail.text);
            GUIUtility.systemCopyBuffer=text.ToString();
        }
        void Resize()
        {
            int width=Mathf.Clamp(Mathf.RoundToInt(preview.rect.width*canvas.scaleFactor),64,4096);
            int height=Mathf.Clamp(Mathf.RoundToInt(preview.rect.height*canvas.scaleFactor),64,2160);
            if(source&&source.width==width&&source.height==height)return;
            stageCamera.targetTexture=oldTarget;
            FreeFrame(ref source);FreeFrame(ref mapped);
            source=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){name="Shader Study source",hideFlags=HideFlags.DontSave};source.Create();
            mapped=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32){name="Shader Study mapped",hideFlags=HideFlags.DontSave};mapped.Create();
            stageCamera.targetTexture=source;stageCamera.aspect=(float)width/height;
            sourceImage.texture=source;mappedImage.texture=mapped;
        }
        void UpdateImages()
        {
            bool active=postToggle.isOn,split=active&&compareToggle.isOn;
            sourceImage.gameObject.SetActive(!active||split);mappedImage.gameObject.SetActive(active);splitMarker.SetActive(split);
            sourceImage.rectTransform.anchorMin=Vector2.zero;sourceImage.rectTransform.anchorMax=new Vector2(split?.5f:1,1);
            sourceImage.rectTransform.offsetMin=sourceImage.rectTransform.offsetMax=Vector2.zero;
            sourceImage.uvRect=new Rect(0,0,split?.5f:1,1);
            mappedImage.rectTransform.anchorMin=new Vector2(split?.5f:0,0);mappedImage.rectTransform.anchorMax=Vector2.one;
            mappedImage.rectTransform.offsetMin=mappedImage.rectTransform.offsetMax=Vector2.zero;
            mappedImage.uvRect=new Rect(split?.5f:0,0,split?.5f:1,1);
        }
        void AfterCamera(ScriptableRenderContext context,Camera camera)
        {
            if(!ready||camera!=stageCamera||!postToggle.isOn||!source||!mapped||!colorMapShader)return;
            if(!mapMaterial)mapMaterial=new Material(colorMapShader){hideFlags=HideFlags.DontSave};
            if(!ramp)ramp=new Texture2D(256,1,TextureFormat.RGBA32,false,true){name="Shader Study ramp",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Point,hideFlags=HideFlags.DontSave};
            int steps=Mathf.RoundToInt(postSteps.value);
            if(lastSteps!=steps)
            {
                var colors=new Color[256];for(int i=0;i<256;i++){float v=i/255f;if(steps>=2)v=Mathf.Round(v*(steps-1))/(steps-1);colors[i]=new Color(v,v,v,1);}
                ramp.SetPixels(colors);ramp.Apply();lastSteps=steps;
            }
            mapMaterial.SetTexture("_GradientTex",ramp);mapMaterial.SetFloat("_Intensity",postAmount.value);
            mapMaterial.SetFloat("_BlackPoint",postBlack.value);mapMaterial.SetFloat("_WhitePoint",Mathf.Max(postBlack.value+.01f,postWhite.value));
            mapMaterial.SetFloat("_Gamma",postGamma.value);
            var prior=RenderTexture.active;bool srgb=GL.sRGBWrite;
            try{GL.sRGBWrite=mapped.sRGB;Graphics.Blit(source,mapped,mapMaterial,0);}
            finally{GL.sRGBWrite=srgb;RenderTexture.active=prior;}
        }
        void UpdateParticles()
        {
            if(!particleToggle.isOn)return;
            int count=Mathf.RoundToInt(particleCount.value);
            for(int i=0;i<count;i++)
            {
                float seed=Mathf.Repeat(i*.6180339f,1),p=Mathf.Repeat(Clock*(.09f+seed*.04f)+i*.137f,1)*4;
                float r=1.8f+seed*1.6f;
                Vector3 v=p<1?new Vector3(Mathf.Lerp(-r,r,p),0,-r):p<2?new Vector3(r,0,Mathf.Lerp(-r,r,p-1)):p<3?new Vector3(Mathf.Lerp(r,-r,p-2),0,r):new Vector3(-r,0,Mathf.Lerp(r,-r,p-3));
                v.y=.4f+Mathf.Repeat(i*.371f,1)*3.6f;
                particles[i].position=v;particles[i].velocity=Vector3.zero;
                particles[i].startSize=particleSize.value*Mathf.Lerp(.45f,1.8f,seed);
                particles[i].startColor=Color.white;particles[i].rotation3D=Vector3.zero;
                particles[i].startLifetime=1000;particles[i].remainingLifetime=1000;
            }
            squares.SetParticles(particles,count);
        }
        void OnDestroy()
        {
            RenderPipelineManager.endCameraRendering-=AfterCamera;
            if(stageCamera)stageCamera.targetTexture=oldTarget;
            if(all!=null)for(int i=0;i<all.Length;i++)if(all[i])all[i].sharedMaterials=originals[i];
            if(materials!=null)foreach(var m in materials)if(m)Destroy(m);
            if(mapMaterial)Destroy(mapMaterial);if(ramp)Destroy(ramp);
            FreeFrame(ref source);FreeFrame(ref mapped);
        }
        static void FreeFrame(ref RenderTexture frame){if(frame){frame.Release();Destroy(frame);}frame=null;}
    }
}
