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
    public static class ShaderStudySetup
    {
        public const string ScenePath="Assets/Scenes/V5/Studies/ShaderStudy.unity";
        const string Materials="Assets/Materials/V5/Studies/";
        const string PreviewKey="PuzzleApple.ShaderStudy.PendingPlay";
        static Font font;
        static readonly Color Paper=new Color(.84f,.86f,.83f),Muted=new Color(.51f,.55f,.53f),Panel=new Color(.075f,.083f,.083f);
        static ShaderStudySetup(){EditorApplication.playModeStateChanged+=PlayChanged;}
        [MenuItem("PuzzleApple/V5/Shader Study/Play (preserve open scenes)")]
        public static void Preview()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop the current Play session before previewing Shader Study.");
            if(!AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))throw new InvalidOperationException("Create Shader Study first.");
            SessionState.SetBool(PreviewKey,true);
            EditorApplication.EnterPlaymode();
        }
        static void PlayChanged(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(PreviewKey,false))
            {
                SessionState.SetBool(PreviewKey,false);
                // Load only the runtime copy. Unity restores the original unsaved edit scenes on Stop.
                EditorSceneManager.LoadSceneInPlayMode(ScenePath,new LoadSceneParameters(LoadSceneMode.Single));
            }
            if(state==PlayModeStateChange.EnteredEditMode)SessionState.SetBool(PreviewKey,false);
        }
        [MenuItem("PuzzleApple/V5/Shader Study/Open additively")]
        public static void Open()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var existing=SceneManager.GetSceneByPath(ScenePath);
            if(!existing.isLoaded)EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        }
        [MenuItem("PuzzleApple/V5/Shader Study/Create if missing")]
        public static void Create()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Create the study only outside Play mode.");
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))return;
            Folder("Assets/Scenes/V5/Studies");Folder("Assets/Materials/V5/Studies");
            var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                RenderSettings.skybox=null;RenderSettings.fog=false;
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.15f,.15f,.15f);
                font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                var root=Node("Shader Study - isolated test stage",null);root.transform.position=new Vector3(2000,0,2000);
                var study=root.AddComponent<ShaderStudyController>();
                var focus=Node("Orbit focus",root.transform);focus.transform.localPosition=new Vector3(0,1.4f,0);study.focus=focus.transform;
                var baseline=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/V5/PathWhite.mat");
                var matte=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/V5/SmallRoomWhite.mat");
                var floorMat=MakeMaterial("StudyFloor","Universal Render Pipeline/Unlit");floorMat.SetColor("_BaseColor",new Color(.12f,.12f,.12f));EditorUtility.SetDirty(floorMat);AssetDatabase.SaveAssetIfDirty(floorMat);
                var objects=new List<Renderer>();
                objects.Add(Shape("Cube - primary target",PrimitiveType.Cube,root.transform,new Vector3(0,1.45f,0),Vector3.one*2.5f,baseline));
                objects.Add(Shape("Sphere - curved projection",PrimitiveType.Sphere,root.transform,new Vector3(-3.3f,1.05f,.2f),Vector3.one*2,baseline));
                objects.Add(Shape("Column - curved sides",PrimitiveType.Cylinder,root.transform,new Vector3(3.3f,1.5f,.2f),new Vector3(1.7f,1.5f,1.7f),baseline));
                objects.Add(Shape("Frame left",PrimitiveType.Cube,root.transform,new Vector3(-1.8f,2.3f,3),new Vector3(.24f,4.6f,.4f),baseline));
                objects.Add(Shape("Frame right",PrimitiveType.Cube,root.transform,new Vector3(1.8f,2.3f,3),new Vector3(.24f,4.6f,.4f),baseline));
                objects.Add(Shape("Frame top",PrimitiveType.Cube,root.transform,new Vector3(0,4.5f,3),new Vector3(3.8f,.24f,.4f),baseline));
                study.subjects=objects.ToArray();
                study.floor=Shape("Floor - optional target",PrimitiveType.Cube,root.transform,new Vector3(0,-.16f,1),new Vector3(14,.3f,12),floorMat);
                var camera=Node("Study world camera",root.transform).AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.028f,.028f);camera.nearClipPlane=.1f;camera.farClipPlane=70;camera.fieldOfView=45;camera.allowHDR=false;camera.allowMSAA=false;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;study.stageCamera=camera;
                var light=Node("Neutral directional light",root.transform).AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.color=Color.white;light.transform.rotation=Quaternion.Euler(48,-35,0);light.shadows=LightShadows.None;
                var ps=Node("Square orbit - native mesh particles",root.transform).AddComponent<ParticleSystem>();
                var main=ps.main;main.playOnAwake=false;main.maxParticles=128;main.simulationSpace=ParticleSystemSimulationSpace.Local;main.startSpeed=0;main.startLifetime=1000;
                var emission=ps.emission;emission.enabled=false;var shape=ps.shape;shape.enabled=false;
                var pr=ps.GetComponent<ParticleSystemRenderer>();pr.renderMode=ParticleSystemRenderMode.Mesh;pr.mesh=objects[0].GetComponent<MeshFilter>().sharedMesh;
                var squareMat=MakeMaterial("SquareParticle","Universal Render Pipeline/Unlit");squareMat.SetColor("_BaseColor",Color.white);EditorUtility.SetDirty(squareMat);AssetDatabase.SaveAssetIfDirty(squareMat);pr.sharedMaterial=squareMat;pr.shadowCastingMode=ShadowCastingMode.Off;study.squares=ps;
                var rain=Node("Rain field - procedural line geometry",root.transform).AddComponent<StudyRain>();rain.material=MakeMaterial("RainStreak","PuzzleApple/V5/Studies/RainStreak");study.rain=rain;
                var a=MakeMaterial("CircuitStreams","PuzzleApple/V5/Studies/CircuitStreams");
                var b=MakeMaterial("ExpandingFrames","PuzzleApple/V5/Studies/ExpandingFrames");
                var c=MakeMaterial("TileCascade","PuzzleApple/V5/Studies/TileCascade");
                study.presets=new[]{
                    Preset("01  Circuit streams","Fixed right-angle paths. Packets move along each path and turn at the junctions; no scrolling image.",a,Common()),
                    Preset("02  Expanding frames","Square fronts grow outwards while small pulses travel around their perimeter. Cell timing is staggered.",b,Common()),
                    Preset("03  Tile cascade","Cells activate in a diagonal relay. Filled squares and outlines grow, hold and retract at different phases.",c,Common()),
                    Preset("04  Existing toon passage","The current V5 flat, three-value surface. Static baseline for comparison; original asset stays unchanged.",baseline,new[]{P("Edge width (pixels)","_EdgeWidth",0,3),P("White front faces","_FrontWhite",0,1)}),
                    Preset("05  Existing filaments","Current V5 straight moving lines. Originally authored for narrow ribbons; mesh UVs change their layout. Uses Unity time, not Pause studies.",AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/V5/PassageFilaments.mat"),new[]{P("Flow speed","_FlowSpeed",-2,2),P("Repeat length","_RepeatLength",.1f,3),P("Visibility","_Visibility",0,1)}),
                    Preset("06  Existing URP matte","Native URP material under a neutral directional light. Tests compatibility with the same Color Map stage.",matte,new[]{P("Smoothness","_Smoothness",0,1)})};
                study.colorMapShader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/V5/Studies/ColorMapTrial.shader");
                BuildUI(study,root.transform);
                var eventSystem=Node("Study EventSystem",root.transform);eventSystem.AddComponent<EventSystem>();eventSystem.AddComponent<InputSystemUIInputModule>();
                if(!EditorSceneManager.SaveScene(scene,ScenePath))throw new InvalidOperationException("Could not save the new study scene.");
            }
            finally
            {
                if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene,true);
            }
        }
        static ShaderStudyController.Parameter P(string label,string property,float min,float max)=>new ShaderStudyController.Parameter{label=label,property=property,min=min,max=max};
        static ShaderStudyController.Parameter[] Common()=>new[]{P("Pattern scale","_Scale",1,12),P("Stroke width","_LineWidth",.005f,.15f),P("Flow speed / direction","_Speed",-2,2),P("Activity","_Density",.1f,1)};
        static ShaderStudyController.Preset Preset(string label,string description,Material material,ShaderStudyController.Parameter[] parameters)=>new ShaderStudyController.Preset{label=label,description=description,material=material,parameters=parameters};
        static Material MakeMaterial(string name,string shaderName)
        {
            var path=Materials+name+".mat";var existing=AssetDatabase.LoadAssetAtPath<Material>(path);if(existing)return existing;
            var shader=Shader.Find(shaderName);if(!shader)throw new InvalidOperationException("Missing shader: "+shaderName);
            var material=new Material(shader);AssetDatabase.CreateAsset(material,path);return material;
        }
        static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;var slash=path.LastIndexOf('/');Folder(path.Substring(0,slash));AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));}
        static GameObject Node(string name,Transform parent){var go=new GameObject(name);if(parent)go.transform.SetParent(parent,false);return go;}
        static Renderer Shape(string name,PrimitiveType type,Transform parent,Vector3 position,Vector3 scale,Material material)
        {var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=material;return renderer;}
        static RectTransform Rect(string name,Transform parent)
        {var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return (RectTransform)go.transform;}
        static void Stretch(RectTransform rt){rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;}
        static void Place(RectTransform rt,float x,float top,float width,float height)
        {rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(x,-top);rt.sizeDelta=new Vector2(width,height);}
        static Text Label(Transform parent,string name,string text,int size,Color color)
        {var rt=Rect(name,parent);var t=rt.gameObject.AddComponent<Text>();t.font=font;t.fontSize=size;t.text=text;t.color=color;t.raycastTarget=false;t.verticalOverflow=VerticalWrapMode.Truncate;return t;}
        static Button Button(Transform parent,string text,float y,float height=40)
        {
            var rt=Rect(text,parent);Place(rt,0,y,336,height);var image=rt.gameObject.AddComponent<Image>();image.color=Color.white;
            var b=rt.gameObject.AddComponent<Button>();b.targetGraphic=image;var colors=b.colors;colors.normalColor=new Color(.17f,.18f,.18f);colors.highlightedColor=new Color(.35f,.38f,.36f);colors.pressedColor=new Color(.5f,.53f,.5f);b.colors=colors;
            var t=Label(rt,"Label",text,17,Paper);Stretch(t.rectTransform);t.rectTransform.offsetMin=new Vector2(12,0);t.alignment=TextAnchor.MiddleLeft;return b;
        }
        static Toggle Toggle(Transform parent,string text,float y,bool value)
        {
            var rt=Rect(text,parent);Place(rt,0,y,336,32);var image=rt.gameObject.AddComponent<Image>();image.color=new Color(1,1,1,0);
            var box=Rect("Box",rt);Place(box,0,6,18,18);var bg=box.gameObject.AddComponent<Image>();bg.color=new Color(.24f,.27f,.25f);
            var mark=Rect("Check",box);Stretch(mark);mark.offsetMin=new Vector2(4,4);mark.offsetMax=new Vector2(-4,-4);var ink=mark.gameObject.AddComponent<Image>();ink.color=Paper;
            var t=rt.gameObject.AddComponent<Toggle>();t.targetGraphic=bg;t.graphic=ink;t.isOn=value;
            var label=Label(rt,"Label",text,16,Paper);Place(label.rectTransform,30,0,300,32);label.alignment=TextAnchor.MiddleLeft;return t;
        }
        static Slider Slider(Transform parent,string text,float y,float min,float max,float value,out Text label)
        {
            var row=Rect(text,parent);Place(row,0,y,336,56);label=Label(row,"Value",text,15,Muted);Place(label.rectTransform,0,0,336,25);
            var rt=Rect("Slider",row);Place(rt,0,28,336,20);var slider=rt.gameObject.AddComponent<Slider>();slider.minValue=min;slider.maxValue=max;
            var track=Rect("Track",rt);Stretch(track);track.offsetMin=new Vector2(0,8);track.offsetMax=new Vector2(0,-8);var bg=track.gameObject.AddComponent<Image>();bg.color=new Color(.22f,.25f,.24f);
            var fill=Rect("Fill",track);Stretch(fill);var ink=fill.gameObject.AddComponent<Image>();ink.color=new Color(.65f,.7f,.65f);slider.fillRect=fill;
            var area=Rect("Handle area",rt);Stretch(area);area.offsetMin=new Vector2(7,0);area.offsetMax=new Vector2(-7,0);
            var handle=Rect("Handle",area);handle.sizeDelta=new Vector2(14,20);var hi=handle.gameObject.AddComponent<Image>();hi.color=Paper;slider.handleRect=handle;slider.targetGraphic=hi;slider.SetValueWithoutNotify(value);return slider;
        }
        static void Heading(Transform parent,string text,ref float y)
        {y+=18;var label=Label(parent,text,text,14,Muted);Place(label.rectTransform,0,y,336,24);y+=30;}
        public static void BuildUI(ShaderStudyController s,Transform root)
        {
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas=Node("Shader Study UI - editable native controls",root).AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;s.canvas=canvas;
            var scaler=canvas.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,1000);scaler.matchWidthOrHeight=.5f;canvas.gameObject.AddComponent<GraphicRaycaster>();
            var screenCamera=Node("Study display camera",root).AddComponent<Camera>();screenCamera.depth=100;screenCamera.cullingMask=0;screenCamera.clearFlags=CameraClearFlags.SolidColor;screenCamera.backgroundColor=Panel;screenCamera.allowHDR=false;screenCamera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var sidebar=Rect("Left control panel",canvas.transform);sidebar.anchorMin=Vector2.zero;sidebar.anchorMax=new Vector2(0,1);sidebar.pivot=Vector2.zero;sidebar.sizeDelta=new Vector2(384,0);sidebar.gameObject.AddComponent<Image>().color=Panel;
            var title=Label(sidebar,"Title","SHADER / STUDY",27,Paper);Place(title.rectTransform,24,24,336,36);
            var subtitle=Label(sidebar,"Subtitle","MONOCHROME   /   PROCEDURAL MOTION",12,Muted);Place(subtitle.rectTransform,24,67,336,25);
            var viewport=Rect("Controls viewport",sidebar);Stretch(viewport);viewport.offsetMin=new Vector2(24,20);viewport.offsetMax=new Vector2(-24,-110);viewport.gameObject.AddComponent<Image>().color=Panel;viewport.gameObject.AddComponent<RectMask2D>();
            var content=Rect("Controls content",viewport);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=new Vector2(0,1700);
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.scrollSensitivity=35;scroll.movementType=ScrollRect.MovementType.Clamped;
            float y=0;s.presetButtons=new Button[s.presets.Length];for(int i=0;i<s.presets.Length;i++){s.presetButtons[i]=Button(content,s.presets[i].label,y);y+=46;}
            s.description=Label(content,"Effect explanation","",15,Muted);Place(s.description.rectTransform,0,y+4,336,88);y+=96;
            s.targetButton=Button(content,"TARGET / ALL SOLIDS",y);y+=46;
            s.applyToggle=Toggle(content,"Apply selected material",y,true);y+=32;
            s.invertToggle=Toggle(content,"Invert new study patterns",y,false);y+=32;
            Heading(content,"SURFACE PARAMETERS",ref y);
            s.parameterSliders=new Slider[4];s.parameterLabels=new Text[4];
            for(int i=0;i<4;i++){s.parameterSliders[i]=Slider(content,"Parameter "+i,y,0,1,0,out s.parameterLabels[i]);y+=56;}
            Heading(content,"SQUARE PARTICLES",ref y);
            s.particleToggle=Toggle(content,"Geometric orbit field",y,false);y+=36;
            Text ignored;s.particleCount=Slider(content,"Count",y,8,128,48,out ignored);s.particleCount.wholeNumbers=true;y+=56;
            s.particleSize=Slider(content,"Particle size",y,.025f,.35f,.12f,out ignored);y+=56;
            Heading(content,"RAIN / THIN LINE GEOMETRY",ref y);
            s.rainToggle=Toggle(content,"Enable rain streaks",y,false);y+=36;
            s.rainSliders=new Slider[7];s.rainLabels=new Text[7];
            string[] rainNames={"Density","Length","Width","Fall speed","Speed variation","Slant angle","Gust amplitude"};
            float[] mins={8,.08f,.003f,.2f,0,-45,0},maxs={320,1.4f,.04f,14,1,45,2},values={96,.45f,.009f,5,.35f,8,.3f};
            for(int i=0;i<7;i++){s.rainSliders[i]=Slider(content,rainNames[i],y,mins[i],maxs[i],values[i],out s.rainLabels[i]);y+=56;}s.rainSliders[0].wholeNumbers=true;
            if(s.rainCurtain)
            {
                s.rainDetailSliders=new Slider[1];s.rainDetailLabels=new Text[1];
                s.rainDetailSliders[0]=Slider(content,"Pulse spacing",y,.5f,2,1,out s.rainDetailLabels[0]);y+=56;
            }
            Heading(content,"COLOR MAP / POST PROCESS",ref y);
            s.postToggle=Toggle(content,"Enable Color Map",y,false);y+=32;s.compareToggle=Toggle(content,"Split: raw left / mapped right",y,true);y+=36;
            s.postLabels=new Text[5];s.postAmount=Slider(content,"Amount",y,0,1,1,out s.postLabels[0]);y+=56;
            s.postBlack=Slider(content,"Black point",y,0,.4f,.08f,out s.postLabels[1]);y+=56;
            s.postWhite=Slider(content,"White point",y,.41f,1,.85f,out s.postLabels[2]);y+=56;
            s.postGamma=Slider(content,"Midtone",y,.2f,3,1.35f,out s.postLabels[3]);y+=56;
            s.postSteps=Slider(content,"Gray steps",y,0,8,4,out s.postLabels[4]);s.postSteps.wholeNumbers=true;y+=56;
            Heading(content,"VIEW / TIME",ref y);
            s.pauseToggle=Toggle(content,"Pause new studies + particles",y,false);y+=32;s.orthoToggle=Toggle(content,"Orthographic camera",y,true);y+=40;
            s.resetButton=Button(content,"RESET VIEW",y);y+=56;
            s.copyButton=Button(content,"COPY CURRENT RECIPE",y);y+=56;
            var note=Label(content,"Scope","LOCAL STUDY ONLY\nOriginal materials are never overwritten.\nDrag in the view to orbit. Scroll to zoom.",13,Muted);Place(note.rectTransform,0,y,336,72);y+=90;
            content.sizeDelta=new Vector2(0,y);
            var preview=Rect("Live stage viewport",canvas.transform);Stretch(preview);preview.offsetMin=new Vector2(384,64);preview.offsetMax=new Vector2(0,-90);preview.gameObject.AddComponent<Image>().color=Color.black;s.preview=preview;
            var raw=Rect("Raw world image",preview);Stretch(raw);s.sourceImage=raw.gameObject.AddComponent<RawImage>();s.sourceImage.raycastTarget=false;
            var mapped=Rect("Mapped world image",preview);Stretch(mapped);s.mappedImage=mapped.gameObject.AddComponent<RawImage>();s.mappedImage.raycastTarget=false;
            var divider=Rect("Comparison divider",preview);divider.anchorMin=new Vector2(.5f,0);divider.anchorMax=new Vector2(.5f,1);divider.sizeDelta=new Vector2(2,0);divider.gameObject.AddComponent<Image>().color=Muted;s.splitMarker=divider.gameObject;
            var orbit=preview.gameObject.AddComponent<ShaderStudyOrbit>();orbit.study=s;
            s.caption=Label(canvas.transform,"Current effect","",24,Paper);s.caption.rectTransform.anchorMin=new Vector2(0,1);s.caption.rectTransform.anchorMax=Vector2.one;s.caption.rectTransform.pivot=new Vector2(0,1);s.caption.rectTransform.offsetMin=new Vector2(414,-70);s.caption.rectTransform.offsetMax=new Vector2(-24,-25);
            s.status=Label(canvas.transform,"Live status","",12,Muted);s.status.rectTransform.anchorMin=Vector2.zero;s.status.rectTransform.anchorMax=new Vector2(1,0);s.status.rectTransform.pivot=Vector2.zero;s.status.rectTransform.offsetMin=new Vector2(414,18);s.status.rectTransform.offsetMax=new Vector2(-20,44);
        }
    }
}
