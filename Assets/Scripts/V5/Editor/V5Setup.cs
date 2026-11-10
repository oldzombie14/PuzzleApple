using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using PuzzleApple.V5.Cognition;

namespace PuzzleApple.V5.Editor
{
    public static class V5Setup
    {
        const string ScenePath="Assets/Scenes/V5/P5.unity";
        [MenuItem("PuzzleApple/V5/Open")]
        public static void Open()
        {
            if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }
        // One-time authoring only; edits thereafter belong to native scene objects.
        public static string Create()
        {
            if(System.IO.File.Exists(ScenePath))return "V5 already exists; did not rebuild.";
            for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
                if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new System.InvalidOperationException("An open scene has unsaved changes.");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var white=Material("White plaster",Color.white);
            var outer=white;
            RenderSettings.skybox=null;RenderSettings.fog=false;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.003f,.003f,.003f);
            var box=Node("Outer box",null,Vector3.zero);
            Cube("Floor",box.transform,new Vector3(0,-.2f,0),new Vector3(28,.4f,28),outer);
            Cube("Ceiling",box.transform,new Vector3(0,28.2f,0),new Vector3(28,.4f,28),outer);
            foreach(int side in new[]{-1,1})
            {
                Cube("Wall X "+side,box.transform,new Vector3(side*14.2f,14,0),new Vector3(.4f,28,28.8f),outer);
                Cube("Wall Z "+side,box.transform,new Vector3(0,14,side*14.2f),new Vector3(28,28,.4f),outer);
            }
            var room=Node("Small folding room",null,new Vector3(0,-.08f,0)).AddComponent<FoldingRoom>();
            var panels=new List<FoldingRoom.Panel>();var colliders=new List<Collider>();
            Transform front=null;
            foreach(int side in new[]{-1,1})
            {
                var hinge=Node("X hinge "+side,room.transform,new Vector3(side*3,0,0)).transform;
                colliders.Add(Cube("White wall",hinge,new Vector3(0,3,0),new Vector3(.12f,5.88f,6.12f),white).GetComponent<Collider>());
                panels.Add(new FoldingRoom.Panel{hinge=hinge,openEuler=new Vector3(0,0,-side*90)});
                var z=Node("Z hinge "+side,room.transform,new Vector3(0,0,side*3)).transform;
                colliders.Add(Cube("White wall",z,new Vector3(0,3,0),new Vector3(5.88f,5.88f,.12f),white).GetComponent<Collider>());
                panels.Add(new FoldingRoom.Panel{hinge=z,openEuler=new Vector3(side*90,0,0)});
                if(side==1)front=z;
            }
            var roof=Node("Roof hinge",front,new Vector3(0,6,0)).transform;
            colliders.Add(Cube("White ceiling",roof,new Vector3(0,0,-3),new Vector3(6.12f,.12f,6.12f),white).GetComponent<Collider>());
            panels.Add(new FoldingRoom.Panel{hinge=roof,openEuler=new Vector3(90,0,0)});
            room.panels=panels.ToArray();room.movingColliders=colliders.ToArray();
            room.centreMin=new Vector2(-11,-11);room.centreMax=new Vector2(11,11);
            var rig=Node("Outer spot lighting",null,Vector3.zero).AddComponent<RoomLighting>();rig.room=room;
            var spots=new List<Light>();
            for(int i=0;i<5;i++)
            {
                float angle=i*Mathf.PI*.5f;
                var light=Node(i==4?"Ceiling wash":"Wall wash "+i,rig.transform,
                    i==4?new Vector3(0,25,0):new Vector3(Mathf.Cos(angle)*9,18,Mathf.Sin(angle)*9)).AddComponent<Light>();
                light.transform.LookAt(i==4?Vector3.zero:new Vector3(-Mathf.Cos(angle)*14,13,-Mathf.Sin(angle)*14));
                light.type=LightType.Spot;light.color=Color.white;light.range=48;light.spotAngle=120;light.innerSpotAngle=100;
                light.intensity=rig.closedIntensity;light.shadows=LightShadows.Soft;light.shadowBias=.02f;light.shadowNormalBias=.03f;
                spots.Add(light);
            }
            rig.spotlights=spots.ToArray();
            var player=Node("Player",null,new Vector3(0,.02f,0));
            var motor=player.AddComponent<CharacterController>();motor.height=1.8f;motor.radius=.28f;motor.center=new Vector3(0,.9f,0);motor.stepOffset=.2f;
            var camera=Node("Main Camera",player.transform,new Vector3(0,1.65f,0)).AddComponent<Camera>();
            camera.tag="MainCamera";camera.nearClipPlane=.05f;camera.farClipPlane=80;camera.fieldOfView=65;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=~(1<<31);
            camera.gameObject.AddComponent<AudioListener>();
            var controller=player.AddComponent<FirstPersonController>();controller.view=camera.transform;
            var ui=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/V5/UI/V5Interface.prefab"));
            var board=ui.GetComponentInChildren<CognitionBoard>(true);
            var panel=ui.GetComponentInChildren<GameplayPanel>(true);
            var serialized=new SerializedObject(panel);serialized.FindProperty("player").objectReferenceValue=controller;serialized.ApplyModifiedPropertiesWithoutUndo();
            var library=ui.GetComponentInChildren<WordLibrary>(true);
            library.vocabularyImages=new[]{new WordLibrary.VocabularyImage{wordId="positive",image=Load<Texture2D>("Textures","EntryMemory.png")},
                new WordLibrary.VocabularyImage{wordId="negative",image=Load<Texture2D>("Textures","EntryMemory.png")},
                new WordLibrary.VocabularyImage{wordId="world",image=Load<Texture2D>("Textures","WorldMemory.png")}};
            var effects=Node("Sentence feedback",null,Vector3.zero).AddComponent<SentenceFeedback>();effects.board=board;
            effects.sparklePrefab=Load<GameObject>("Prefabs","UI/SentenceSparkle.prefab").GetComponent<ParticleSystem>();
            effects.rippleSound=Load<AudioClip>("Audio","SFX/SentenceRipple_E5.wav");
            effects.rippleMaterial=new Material(Shader.Find("PuzzleApple/V5/SentenceParticle"));
            AssetDatabase.CreateAsset(effects.rippleMaterial,"Assets/Materials/V5/SentenceRipple.mat");
            BuildMenu(controller,board,room,effects);
            var events=Node("EventSystem",null,Vector3.zero);events.AddComponent<EventSystem>();events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            return ScenePath;
        }
        static T Load<T>(string category,string path)where T:Object => AssetDatabase.LoadAssetAtPath<T>("Assets/"+category+"/V5/"+path);
        static Material Material(string name,Color color)
        {
            var value=new Material(Shader.Find("Universal Render Pipeline/Lit"));value.SetColor("_BaseColor",color);value.SetFloat("_Smoothness",0);
            AssetDatabase.CreateAsset(value,"Assets/Materials/V5/"+name+".mat");return value;
        }
        static GameObject Node(string name,Transform parent,Vector3 position)
        {var obj=new GameObject(name);obj.transform.SetParent(parent,false);obj.transform.localPosition=position;return obj;}
        static GameObject Cube(string name,Transform parent,Vector3 position,Vector3 scale,Material mat)
        {var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(parent,false);obj.transform.localPosition=position;obj.transform.localScale=scale;obj.GetComponent<Renderer>().sharedMaterial=mat;return obj;}
        static Image Image(string name,Transform parent,Vector2 size,Vector2 position,Color color)
        {var obj=new GameObject(name,typeof(RectTransform),typeof(Image));obj.transform.SetParent(parent,false);var image=obj.GetComponent<Image>();image.rectTransform.sizeDelta=size;image.rectTransform.anchoredPosition=position;image.color=color;return image;}
        static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
        static Button Symbols(string name,Transform parent,Vector2 pos,float size,params string[] words)
        {
            var hit=Image(name,parent,new Vector2(size*words.Length+80,size+60),pos,Color.clear);
            var button=hit.gameObject.AddComponent<SymbolButton>();button.targetGraphic=hit;button.navigation=new Navigation{mode=Navigation.Mode.None};
            var colors=button.colors;colors.normalColor=new Color(.65f,.65f,.65f);colors.highlightedColor=colors.selectedColor=Color.white;colors.pressedColor=new Color(.45f,.45f,.45f);button.colors=colors;
            for(int i=0;i<words.Length;i++){var image=Image(words[i],hit.transform,Vector2.one*size,new Vector2((i-(words.Length-1)*.5f)*(size+28),0),Color.white);image.sprite=Load<Sprite>("Sprites",words[i]+".png");image.preserveAspect=true;image.raycastTarget=false;}
            return button;
        }
        static void BuildMenu(FirstPersonController player,CognitionBoard board,FoldingRoom room,SentenceFeedback feedback)
        {
            var root=new GameObject("World Entry",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(WorldEntry));
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var entry=root.GetComponent<WorldEntry>();entry.player=player;entry.board=board;entry.room=room;entry.feedback=feedback;
            entry.blackBackground=Image("Black background",root.transform,Vector2.zero,Vector2.zero,Color.black);Stretch(entry.blackBackground.rectTransform);
            entry.enterButton=Symbols("Enter world",root.transform,Vector2.zero,104,"Positive","World");
            entry.exitButton=Symbols("Exit world",root.transform,new Vector2(-110,-76),54,"Negative","World");
            var exitRect=(RectTransform)entry.exitButton.transform;exitRect.anchorMin=exitRect.anchorMax=Vector2.one;
            var dialog=Image("Exit confirmation",root.transform,Vector2.zero,Vector2.zero,Color.clear);Stretch(dialog.rectTransform);entry.dialog=dialog.gameObject;
            var fill=Image("Black panel",dialog.transform,new Vector2(520,260),Vector2.zero,Color.black);
            entry.confirmButton=Symbols("ON",fill.transform,new Vector2(-100,0),64,"Positive");
            entry.cancelButton=Symbols("OFF",fill.transform,new Vector2(100,0),64,"Negative");
            entry.dialog.SetActive(false);
        }
    }
}
