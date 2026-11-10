using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PuzzleApple.V5.Studies.Editor
{
    [InitializeOnLoad]
    public static class RefinedShaderStudySetup
    {
        public const string ScenePath="Assets/Scenes/V5/Studies/ShaderStudyRefined.unity";
        const string Key="PuzzleApple.RefinedStudy.PendingPlay";
        static RefinedShaderStudySetup(){EditorApplication.playModeStateChanged+=Changed;}
        [MenuItem("PuzzleApple/V5/Shader Study Refined/Play (preserve open scenes)")]
        public static void Preview()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop current Play before previewing.");
            if(!AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))throw new InvalidOperationException("Create refined study first.");
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Changed(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))
            {SessionState.SetBool(Key,false);EditorSceneManager.LoadSceneInPlayMode(ScenePath,new LoadSceneParameters(LoadSceneMode.Single));}
            if(state==PlayModeStateChange.EnteredEditMode)SessionState.SetBool(Key,false);
        }
        [MenuItem("PuzzleApple/V5/Shader Study Refined/Open additively")]
        public static void Open()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(!SceneManager.GetSceneByPath(ScenePath).isLoaded)EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        }
        [MenuItem("PuzzleApple/V5/Shader Study Refined/Create if missing")]
        public static void Create()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Create outside Play.");
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))return;
            var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);RenderSettings.skybox=null;RenderSettings.fog=false;
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.gray*.25f;
                var root=Node("Refined shader study / independent architecture",null);root.transform.position=new Vector3(4000,0,4000);
                var study=root.AddComponent<ShaderStudyController>();study.yaw=-12;study.pitch=0;study.distance=18;study.targetMode=0;study.selectedPreset=1;
                var focus=Node("Composition focus",root.transform);focus.transform.localPosition=new Vector3(0,3.3f,7);study.focus=focus.transform;
                var ink=Mat("RefinedInk","PuzzleApple/V5/Studies/Architectural Ink");
                var pale=Mat("RefinedPale","PuzzleApple/V5/Studies/Architectural Ink");pale.SetFloat("_FaceValue",.64f);pale.SetFloat("_SideValue",.1f);pale.SetFloat("_TopValue",.95f);pale.SetFloat("_EdgeStrength",.2f);
                var ground=Mat("RefinedGround","PuzzleApple/V5/Studies/Architectural Ink");ground.SetFloat("_TopValue",.07f);ground.SetFloat("_EdgeStrength",.3f);
                var etched=Mat("RefinedCurrent","PuzzleApple/V5/Studies/Etched Current");
                var rainMat=Mat("RefinedRain","PuzzleApple/V5/Studies/Rain Curtain");
                foreach(var material in new[]{ink,pale,ground,etched,rainMat}){EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);}
                var architecture=Node("Architecture / editable original primitives",root.transform);
                var clocks=new List<Renderer>();
                Action<string,Vector3,Vector3,Material> block=(name,pos,scale,mat)=>clocks.Add(Box(name,architecture.transform,pos,scale,mat));
                block("Left solid wall",new Vector3(-7,5,13),new Vector3(2,10,38),ink);
                block("Right solid wall",new Vector3(7,5,16),new Vector3(2,10,33),ink);
                block("End wall",new Vector3(0,5,33),new Vector3(14,10,.5f),ink);
                for(int i=0;i<7;i++)
                {
                    float z=5+i*4.1f;
                    block("Portico "+i+" / left",new Vector3(-4.6f,4.4f,z),new Vector3(.24f,8.8f,.35f),ink);
                    block("Portico "+i+" / right",new Vector3(4.6f,4.4f,z),new Vector3(.24f,8.8f,.35f),ink);
                    block("Portico "+i+" / lintel",new Vector3(0,8.7f,z),new Vector3(9.4f,.24f,.35f),ink);
                    if(i%2==0)block("Offset screen "+i,new Vector3(-3.2f,2.1f,z+2),new Vector3(.5f,4.2f,1.8f),ink);
                }
                block("Left overhead slab",new Vector3(-4.4f,8.25f,14),new Vector3(3.1f,.2f,31),ink);
                block("Raised pale path",new Vector3(.6f,.06f,15),new Vector3(4.2f,.12f,35),pale);
                block("Floating vertical monolith",new Vector3(2.3f,3.8f,18),new Vector3(1.7f,5.6f,.6f),pale);
                block("Subject plinth",new Vector3(-.8f,.25f,2.6f),new Vector3(3.5f,.5f,3.5f),ink);
                var cube=Box("Cube / primary material target",root.transform,new Vector3(-.8f,1.95f,2.6f),Vector3.one*2.9f,ink);cube.transform.localRotation=Quaternion.Euler(0,12,0);
                var panel=Box("Narrow screen / secondary material target",root.transform,new Vector3(3.8f,3.1f,8.8f),new Vector3(1.6f,5.5f,.18f),ink);
                study.subjects=new[]{cube,panel};
                study.floor=Box("Dark continuous floor",root.transform,new Vector3(0,-.2f,13),new Vector3(18,.4f,44),ground);
                study.clockTargets=clocks.ToArray();
                var camera=Node("Study world camera",root.transform).AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.075f,.075f,.075f);camera.nearClipPlane=.15f;camera.farClipPlane=90;camera.fieldOfView=52;camera.allowHDR=false;camera.allowMSAA=false;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;study.stageCamera=camera;
                var light=Node("Neutral main light / existing material comparison",root.transform).AddComponent<Light>();light.type=LightType.Directional;light.color=Color.white;light.intensity=1.25f;light.transform.rotation=Quaternion.Euler(45,-25,0);light.shadows=LightShadows.None;
                var rain=Node("Rain curtain / three depth layers",root.transform).AddComponent<StudyRainCurtain>();rain.material=rainMat;rain.runOwnClock=false;rain.density=.45f;study.rainCurtain=rain;
                var ps=Node("Optional square particles",root.transform).AddComponent<ParticleSystem>();var main=ps.main;main.playOnAwake=false;main.maxParticles=128;main.simulationSpace=ParticleSystemSimulationSpace.Local;main.startSpeed=0;main.startLifetime=1000;var emission=ps.emission;emission.enabled=false;var shape=ps.shape;shape.enabled=false;
                var pr=ps.GetComponent<ParticleSystemRenderer>();pr.renderMode=ParticleSystemRenderMode.Mesh;pr.mesh=cube.GetComponent<MeshFilter>().sharedMesh;pr.sharedMaterial=pale;pr.shadowCastingMode=ShadowCastingMode.Off;study.squares=ps;
                study.presets=new[]{
                    Preset("01  PORTICO / fine edges","Thin, pixel-scaled edges and quiet solid faces. Small pulses travel on the perimeter; architecture remains sparse.",ink,new[]{P("Edge width (pixels)","_EdgeWidth",.25f,1.6f),P("Line strength","_EdgeStrength",.1f,1),P("Pulse speed","_PulseSpeed",-.5f,.5f),P("Pulse amount","_PulseAmount",0,1)}),
                    Preset("02  RAIN / layered depth","Light grows down each fixed rain path: the upper part fades away as the lower part appears. Three depth layers; no moving image.",ink,new[]{P("Edge width (pixels)","_EdgeWidth",.25f,1.6f),P("Front value","_FaceValue",0,.8f),P("Side value","_SideValue",0,.4f),P("Line strength","_EdgeStrength",.1f,1)}),
                    Preset("03  CURRENT / etched flow","Evolving analytic gradients bend a local current of fine contours. Broad unmarked borders keep the pattern inside each target.",etched,new[]{P("Contour count","_Frequency",3,24),P("Curvature","_Curvature",0,.9f),P("Evolution speed","_Speed",-.5f,.5f),P("Stroke width (pixels)","_LineWidth",.3f,1.6f)}),
                    Preset("04  EXISTING / matte baseline","Current V5 matte material on the same targets, camera and Color Map. Use Apply and split comparison to check compatibility.",AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/V5/SmallRoomWhite.mat"),new[]{P("Smoothness","_Smoothness",0,1)})};
                study.presetRain=new[]{false,true,false,false};
                study.colorMapShader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/V5/Studies/ColorMapTrial.shader");
                ShaderStudySetup.BuildUI(study,root.transform);
                study.orthoToggle.isOn=false;study.invertToggle.gameObject.SetActive(false);
                study.rainSliders[0].wholeNumbers=false;
                float[] mins={0,.08f,.3f,.2f,0,.05f,.05f},maxs={1,2,2,18,.9f,.95f,.65f},values={.45f,1.2f,.8f,3,.25f,.6f,.3f};
                for(int i=0;i<7;i++){study.rainSliders[i].minValue=mins[i];study.rainSliders[i].maxValue=maxs[i];study.rainSliders[i].SetValueWithoutNotify(values[i]);}
                study.postBlack.value=0;study.postWhite.value=1;study.postGamma.value=1;study.postSteps.value=0;
                foreach(var label in study.canvas.GetComponentsInChildren<Text>(true))
                {
                    if(label.name=="Title")label.text="MONO / STUDIES";
                    if(label.name=="Subtitle")label.text="SPACE   /   RAIN   /   CURRENT";
                    if(label.name=="Scope")label.text="LOCAL STUDY / NO GAMEPLAY CHANGES\nDrag to orbit; scroll to zoom.\nLayer details are editable on Rain curtain.";
                }
                var events=Node("Study EventSystem",root.transform);events.AddComponent<EventSystem>();events.AddComponent<InputSystemUIInputModule>();
                if(!EditorSceneManager.SaveScene(scene,ScenePath))throw new InvalidOperationException("Could not save refined scene.");
            }
            finally
            {
                if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene,true);
            }
        }
        static ShaderStudyController.Parameter P(string label,string property,float min,float max)=>new ShaderStudyController.Parameter {label=label,property=property,min=min,max=max};
        static ShaderStudyController.Preset Preset(string label,string description,Material material,ShaderStudyController.Parameter[] parameters)=>new ShaderStudyController.Preset {label=label,description=description,material=material,parameters=parameters};
        static Material Mat(string name,string shaderName)
        {
            string path="Assets/Materials/V5/Studies/"+name+".mat";var result=AssetDatabase.LoadAssetAtPath<Material>(path);if(result)return result;
            var shader=Shader.Find(shaderName);if(!shader)throw new InvalidOperationException("Missing shader "+shaderName);
            result=new Material(shader);AssetDatabase.CreateAsset(result,path);return result;
        }
        static GameObject Node(string name,Transform parent){var go=new GameObject(name);if(parent)go.transform.SetParent(parent,false);return go;}
        static Renderer Box(string name,Transform parent,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
            var r=go.GetComponent<Renderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;return r;
        }
    }
}
