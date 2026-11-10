using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace PuzzleApple.V5.Editor
{
    // No saved scene/component/material/profile. Closing this window restores output.
    // Only this editor adapter and the trial shader need removing after a decision.
    public sealed class ColorMapTrialWindow : EditorWindow
    {
        const string ShaderPath = "Assets/Shaders/V5/Studies/ColorMapTrial.shader";
        [SerializeField] bool trialEnabled = true;
        [SerializeField] float intensity = 1, blackPoint = .08f, whitePoint = .85f, gamma = 1.35f;
        [SerializeField] int steps = 4;
        ReticleBackground background;
        PassageJourney journey;
        RawImage display;
        Material material;
        Texture2D gradient;
        RenderTexture processed;
        int lastSteps = -1;
        bool visitRequested;
        double visitDeadline;
        public bool Applied { get; private set; }
        public RenderTexture Processed => processed;
        public bool TrialEnabled { get => trialEnabled; set { trialEnabled=value; if(!value)Restore(); Repaint(); } }

        [MenuItem("PuzzleApple/V5/Color Map Trial")]
        public static void Open() => GetWindow<ColorMapTrialWindow>("通道黑白试验");
        void OnEnable()
        {
            minSize=new Vector2(340,330);
            RenderPipelineManager.endCameraRendering+=AfterCamera;
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=PlayChanged;
        }
        void OnDisable()
        {
            RenderPipelineManager.endCameraRendering-=AfterCamera;
            EditorApplication.update-=Tick;
            EditorApplication.playModeStateChanged-=PlayChanged;
            Release();
        }
        void PlayChanged(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.ExitingPlayMode||state==PlayModeStateChange.EnteredEditMode)
            {visitRequested=false;Release();}
        }
        void OnGUI()
        {
            EditorGUILayout.HelpBox("仅 P5 的 Play 通道生效；关闭窗口即恢复原画面。无需保存场景。",MessageType.Info);
            TrialEnabled=EditorGUILayout.Toggle("启用黑白映射",trialEnabled);
            intensity=EditorGUILayout.Slider("混合强度",intensity,0,1);
            blackPoint=EditorGUILayout.Slider("黑位",blackPoint,0,.8f);
            whitePoint=EditorGUILayout.Slider("白位",whitePoint,blackPoint+.01f,1);
            gamma=EditorGUILayout.Slider("中间调（大于1压暗）",gamma,.2f,3);
            steps=EditorGUILayout.IntSlider("灰阶数量（0为连续）",steps,0,8);
            if(steps==1)steps=2;
            if(GUILayout.Button("恢复试验默认值")) {intensity=1;blackPoint=.08f;whitePoint=.85f;gamma=1.35f;steps=4;}
            using(new EditorGUI.DisabledScope(!EditorApplication.isPlaying||!journey))
                if(GUILayout.Button("前往通道预览（本次 Play）"))VisitCorridor();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(Applied?"当前：通道映射生效":"当前：原画面／等待进入通道");
            EditorGUILayout.HelpBox("基于 NullTale ColorMap；仅本地试用，正式发布前核对原包授权。试验不改变通道后期开关、材质或玩法。",MessageType.None);
        }
        public void VisitCorridor()
        {visitRequested=true;visitDeadline=EditorApplication.timeSinceStartup+20;}
        void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            if(!background)
            {
                background=Object.FindObjectsByType<ReticleBackground>(FindObjectsSortMode.None)
                    .FirstOrDefault(x=>x.gameObject.scene.path=="Assets/Scenes/V5/P5.unity");
                if(background)journey=Object.FindObjectsByType<PassageJourney>(FindObjectsSortMode.None)
                    .FirstOrDefault(x=>x.gameObject.scene==background.gameObject.scene);
            }
            if(!visitRequested||!journey)return;
            if(EditorApplication.timeSinceStartup>visitDeadline){visitRequested=false;return;}
            var entry=journey.entry;
            if(!entry||entry.Busy)return;
            if(!entry.Entered){entry.EnterWorld();return;}
            if(!entry.room.Opened){entry.OpenWorld(-1);return;}
            if(!journey.Root)return;
            entry.board.Panel.SetOpen(false);
            entry.player.Teleport(journey.Root.TransformPoint(new Vector3(0,.08f,4)),journey.Root.rotation);
            entry.player.view.LookAt(journey.Door.position);
            entry.player.AdoptCurrentView();
            visitRequested=false;
        }
        void AfterCamera(ScriptableRenderContext context,Camera camera)
        {
            if(!background||!journey||camera!=background.worldCamera)return;
            var source=camera.targetTexture;
            if(!display)display=background.GetComponentsInChildren<RawImage>(true)
                .FirstOrDefault(x=>x.name=="World image"&&x.texture==source);
            if(!EditorApplication.isPlaying||!trialEnabled||intensity<=0||!display||!source||
               !journey.isActiveAndEnabled||!journey.InCorridor||!journey.entry||
               (journey.entry.portableClosure&&journey.entry.portableClosure.Isolated))
            {Restore();return;}
            if(!material)
            {
                var shader=AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
                if(!shader||!shader.isSupported){Restore();return;}
                material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
            }
            if(!gradient)
                gradient=new Texture2D(256,1,TextureFormat.RGBA32,false,true)
                    {name="ColorMap trial ramp",hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Point};
            if(lastSteps!=steps)
            {
                var colors=new Color[256];
                for(int i=0;i<256;i++)
                {
                    float v=i/255f;
                    if(steps>=2)v=Mathf.Round(v*(steps-1))/(steps-1);
                    colors[i]=new Color(v,v,v,1);
                }
                gradient.SetPixels(colors);gradient.Apply();lastSteps=steps;
            }
            if(!processed||processed.width!=source.width||processed.height!=source.height)
            {
                Restore();
                if(processed){processed.Release();DestroyImmediate(processed);}
                var desc=source.descriptor;desc.depthBufferBits=0;desc.msaaSamples=1;
                processed=new RenderTexture(desc){name="ColorMap trial output",hideFlags=HideFlags.HideAndDontSave};processed.Create();
            }
            material.SetTexture("_GradientTex",gradient);
            material.SetFloat("_Intensity",intensity);material.SetFloat("_BlackPoint",blackPoint);
            material.SetFloat("_WhitePoint",whitePoint);material.SetFloat("_Gamma",gamma);
            var previous=RenderTexture.active;
            bool srgb=GL.sRGBWrite;
            try {GL.sRGBWrite=processed.sRGB;Graphics.Blit(source,processed,material,0);}
            finally {GL.sRGBWrite=srgb;RenderTexture.active=previous;}
            display.texture=processed;
            if(background.Contrast)background.Contrast.SetTexture("_ReticleSceneColor",processed);
            if(!Applied){Applied=true;Repaint();}
        }
        void Restore()
        {
            if(background&&background.worldCamera)
            {
                var source=background.worldCamera.targetTexture;
                if(display&&display.texture==processed)display.texture=source;
                if(background.Contrast&&background.Contrast.GetTexture("_ReticleSceneColor")==processed)
                    background.Contrast.SetTexture("_ReticleSceneColor",source);
            }
            if(Applied){Applied=false;Repaint();}
        }
        void Release()
        {
            Restore();
            if(processed){processed.Release();DestroyImmediate(processed);}
            if(material)DestroyImmediate(material);
            if(gradient)DestroyImmediate(gradient);
            processed=null;material=null;gradient=null;lastSteps=-1;
            background=null;journey=null;display=null;
        }
    }
}
