using System;
using System.Linq;
using PuzzleApple.V3.Cognition;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace PuzzleApple.V3.Editor
{
    // One-time authoring migration; the resulting model, slots and screen remain editable.
    public static class HiddenRouteSetup
    {
        const string Data="Assets/GameData/V3/Opening";
        static GameObject Child(string name,Transform parent,Vector3 position)
        {var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.localPosition=position;return g;}
        static void Refs(Object owner,string field,System.Collections.Generic.IEnumerable<Object> values)
        {var so=new SerializedObject(owner);var p=so.FindProperty(field);var a=values.ToArray();p.arraySize=a.Length;for(int i=0;i<a.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=a[i];so.ApplyModifiedPropertiesWithoutUndo();}
        static WordDefinition Word(string id,string display,string sprite,WordRole role)
        {
            var w=AssetDatabase.LoadAssetAtPath<WordDefinition>(Data+"/Words/"+id+".asset");
            if(!w){w=ScriptableObject.CreateInstance<WordDefinition>();AssetDatabase.CreateAsset(w,Data+"/Words/"+id+".asset");}
            var so=new SerializedObject(w);so.FindProperty("id").stringValue=id;so.FindProperty("displayText").stringValue=display;
            so.FindProperty("symbol").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/V3/"+sprite+".png");
            var sense=so.FindProperty("senses");sense.arraySize=1;sense.GetArrayElementAtIndex(0).FindPropertyRelative("meaning").stringValue=id;
            sense.GetArrayElementAtIndex(0).FindPropertyRelative("role").enumValueIndex=(int)role;so.ApplyModifiedPropertiesWithoutUndo();return w;
        }
        public static void Rule(CognitionCatalog catalog,string sentence,CognitionSignal signal,string channel)
        {
            var path=Data+"/Rules/"+sentence.Replace(' ','-')+".asset";
            var rule=AssetDatabase.LoadAssetAtPath<CognitionRule>(path);
            if(!rule){rule=ScriptableObject.CreateInstance<CognitionRule>();AssetDatabase.CreateAsset(rule,path);}
            var so=new SerializedObject(rule);so.FindProperty("id").stringValue="opening-"+sentence.Replace(' ','-');
            so.FindProperty("effect").enumValueIndex=(int)signal;so.FindProperty("exclusionKey").stringValue=channel;so.ApplyModifiedPropertiesWithoutUndo();
            Refs(rule,"words",sentence.Split(' ').Select(catalog.Word));Refs(catalog,"rules",catalog.Rules.Concat(new[]{rule}).Distinct());
        }
        static Image Image(string name,Transform parent,Vector2 position,Vector2 size)
        {
            var g=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));g.transform.SetParent(parent,false);
            var image=g.GetComponent<Image>();image.raycastTarget=false;image.rectTransform.anchoredPosition=position;image.rectTransform.sizeDelta=size;return image;
        }
        static void Snapshot(OpeningRoom room,Transform parent,string id,Transform subject,Vector3 position,Vector3 look)
        {
            var g=Child("Snapshot — "+id,parent,position);var c=g.AddComponent<Camera>();c.CopyFrom(room.presentation.view);
            c.enabled=false;c.targetTexture=null;c.tag="Untagged";c.fieldOfView=38;c.transform.LookAt(look);
            var data=g.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();data.renderPostProcessing=true;
            var s=g.AddComponent<OpeningSnapshotCamera>();s.wordId=id;s.subject=subject;
        }
        public static string Build()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Exit Play first.");
            var scene=EditorSceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/V3/P3.unity")throw new InvalidOperationException("Open P3 first.");
            var room=Object.FindFirstObjectByType<OpeningRoom>();if(room.analyzer)throw new InvalidOperationException("Hidden route already authored.");
            if(!AssetDatabase.IsValidFolder(Data+"/Words"))AssetDatabase.CreateFolder(Data,"Words");
            var catalog=room.board.Catalog;
            var apple=Word("apple","Apple","apple - red round",WordRole.Reference);
            var machine=Word("analyzer","Analyzer","Apple",WordRole.Reference);
            var red=Word("red","Red","red",WordRole.Transform);var round=Word("round","Round","round",WordRole.Transform);
            Refs(catalog,"words",catalog.Words.Where(w=>w.Id!="apple").Concat(new[]{apple,machine,red,round}));
            foreach(var rule in catalog.Rules)Refs(rule,"words",rule.Words.Select(w=>w.Id=="apple"?apple:w).ToArray());
            Rule(catalog,"apple move analyzer",CognitionSignal.OpeningMove,"motion:apple");
            var recipe=ScriptableObject.CreateInstance<AnalysisRecipe>();recipe.input=apple;recipe.outputs=new[]{red,round};
            AssetDatabase.CreateAsset(recipe,Data+"/AppleAnalysis.asset");

            var root=Child("Hidden room route",room.transform,Vector3.zero);
            var analyzer=root.AddComponent<WordAnalyzer>();room.analyzer=analyzer;analyzer.room=room;analyzer.recipes=new[]{recipe};
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ArtAssets-3D/V3/Analyzer.fbx"),root.transform);
            model.name="Analyzer";model.transform.localScale*=1.15f/1.8008f;model.transform.position=new Vector3(0,.575f,-10);
            foreach(var filter in model.GetComponentsInChildren<MeshFilter>())
            {var collider=filter.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;}
            model.AddComponent<OpeningCollectible>().words=new[]{"analyzer"};
            analyzer.intake=Child("Intake — one place",root.transform,new Vector3(0,1.15f,-10)).transform;
            var screen=model.transform.Find("Screen");screen.gameObject.AddComponent<AnalyzerScreen>().analyzer=analyzer;
            var screenRenderer=screen.GetComponent<Renderer>();
            var dark=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Analyzer screen"};dark.SetColor("_BaseColor",new Color(.025f,.03f,.032f));
            AssetDatabase.CreateAsset(dark,"Assets/Materials/V3/Opening/AnalyzerScreen.mat");screenRenderer.sharedMaterial=dark;
            var bounds=screenRenderer.bounds;
            var canvasGo=new GameObject("Analysis screen",typeof(RectTransform),typeof(Canvas));canvasGo.transform.SetParent(root.transform,false);
            canvasGo.transform.position=new Vector3(bounds.center.x,bounds.center.y,bounds.max.z+.004f);canvasGo.transform.rotation=Quaternion.Euler(0,180,0);
            canvasGo.transform.localScale=Vector3.one*.001f;var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta=new Vector2(900,380);
            var row=new GameObject("Apple decomposition",typeof(RectTransform));row.transform.SetParent(canvas.transform,false);analyzer.resultRow=row;
            analyzer.outputSymbols=new[]{Image("Red",row.transform,new Vector2(-140,0),new Vector2(160,180)),Image("Round",row.transform,new Vector2(140,0),new Vector2(160,180))};
            foreach(var img in analyzer.outputSymbols)img.preserveAspect=true;
            analyzer.outputSymbols[0].sprite=red.Symbol;analyzer.outputSymbols[1].sprite=round.Symbol;
            Image("Plus horizontal",row.transform,Vector2.zero,new Vector2(38,5));Image("Plus vertical",row.transform,Vector2.zero,new Vector2(5,38));
            row.SetActive(false);
            analyzer.progress=Image("Analysis progress",canvas.transform,Vector2.zero,new Vector2(350,4));
            analyzer.progress.type=UnityEngine.UI.Image.Type.Filled;analyzer.progress.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;analyzer.progress.fillAmount=0;analyzer.progress.gameObject.SetActive(false);

            var fruit=Object.Instantiate(room.mainRoute.apple,root.transform);fruit.name="Hidden room apple";fruit.transform.position=new Vector3(-1.2f,.04f,-7.5f);
            fruit.reflectionPlane=Child("Hidden apple reflection plane",root.transform,new Vector3(0,0,-8)).transform;fruit.reflectionPlane.rotation=Quaternion.Euler(0,90,0);
            fruit.movementBounds=room.secretRoomVolume;room.objects=room.objects.Concat(new[]{fruit}).ToArray();
            Snapshot(room,root.transform,"apple",fruit.transform,new Vector3(-1.2f,1.05f,-5.7f),fruit.transform.position+Vector3.up*.3f);
            Snapshot(room,root.transform,"analyzer",model.transform,new Vector3(0,1.55f,-6.9f),new Vector3(0,.7f,-10));
            foreach(var id in new[]{"red","round"})Snapshot(room,root.transform,id,screen,new Vector3(0,.6f,-7.9f),bounds.center);

            var placeholder=room.transform.Find("Rooms/Optional next room — empty");if(placeholder)placeholder.gameObject.SetActive(false);
            ConfigureTransforms(room);
            catalog.ValidateOrThrow();PrefabUtility.ApplyPrefabInstance(room.gameObject,InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            return "Hidden room model, single-place intake, recipe screen, apple and vocabulary authored.";
        }
        public static void ConfigureTransforms(OpeningRoom room)
        {
            var target=room.mainRoute.balance.GetComponent<WordTransformTarget>();
            if(!target)target=room.mainRoute.balance.gameObject.AddComponent<WordTransformTarget>();
            target.wordId="equal";target.surfaces=room.mainRoute.balance.GetComponentsInChildren<Renderer>();
            var mirror=room.doorwayMirror.GetComponent<WordTransformTarget>();
            if(!mirror)mirror=room.doorwayMirror.gameObject.AddComponent<WordTransformTarget>();
            mirror.wordId="mirror";mirror.surfaces=room.doorwayMirror.GetComponentsInChildren<Renderer>().Where(r=>r.sharedMaterials.All(m=>m&&m.shader.name!="PuzzleApple/Planar Mirror")).ToArray();
            room.transformTargets=new[]{target,mirror};
            Rule(room.board.Catalog,"red mirror",CognitionSignal.OpeningTransform,"color:mirror");
            Rule(room.board.Catalog,"red equal",CognitionSignal.OpeningTransform,"color:equal");
            Rule(room.board.Catalog,"round equal",CognitionSignal.OpeningTransform,"shape:equal");
            Rule(room.board.Catalog,"i equal",CognitionSignal.OpeningReset,"form:equal");
        }
    }
}
