#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PuzzleApple.V5.Cognition;
using O=PuzzleApple.V5.Outdoor;

namespace PuzzleApple.V5.Editor
{
    public static class OutdoorSetup
    {
        const string PrefabPath="Assets/Prefabs/V5/Resources/V5/Outdoor/OutdoorWorld.prefab";
        const string ScenePath="Assets/Scenes/V5/Outdoor.unity";
        static readonly Dictionary<UnityEngine.Object,UnityEngine.Object> copies=new Dictionary<UnityEngine.Object,UnityEngine.Object>();
        static readonly Dictionary<string,string> paths=new Dictionary<string,string>();
        static void Folder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            var parent=path.Substring(0,path.LastIndexOf('/'));Folder(parent);AssetDatabase.CreateFolder(parent,path.Substring(path.LastIndexOf('/')+1));
        }
        static T Copy<T>(T source) where T:UnityEngine.Object
        {
            if(!source)return null;if(copies.TryGetValue(source,out var existing))return (T)existing;
            var path=AssetDatabase.GetAssetPath(source);
            if(string.IsNullOrEmpty(path)||!path.StartsWith("Assets/"))return source;
            if(!paths.TryGetValue(path,out var target))
            {
                if(!path.Contains("/V4/"))return source;
                target=path.Replace("/V4/","/V5/Outdoor/");paths[path]=target;
                Folder(target.Substring(0,target.LastIndexOf('/')));
                if(!AssetDatabase.LoadMainAssetAtPath(target)&&!AssetDatabase.CopyAsset(path,target))throw new Exception("Cannot copy "+path);
                var asset=AssetDatabase.LoadMainAssetAtPath(target);
                if(asset is Shader)
                {
                    var shaderText=System.IO.File.ReadAllText(target).Replace("PuzzleApple/V4/","PuzzleApple/V5/Outdoor/");
                    System.IO.File.WriteAllText(target,shaderText);AssetDatabase.ImportAsset(target);
                }
            }
            var result=AssetDatabase.LoadAllAssetsAtPath(target).OfType<T>().FirstOrDefault(a=>a.name==source.name)??AssetDatabase.LoadAssetAtPath<T>(target);
            copies[source]=result;
            if(result is Material||result is UnityEngine.Rendering.VolumeProfile)
            {
                var serialized=new SerializedObject(result);var it=serialized.GetIterator();
                while(it.Next(true))if(it.propertyType==SerializedPropertyType.ObjectReference&&it.objectReferenceValue&&it.objectReferenceValue!=result)
                    it.objectReferenceValue=Copy(it.objectReferenceValue);
                serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(result);AssetDatabase.SaveAssetIfDirty(result);
                if(result is UnityEngine.Rendering.VolumeProfile profile)
                    foreach(var component in profile.components){var so=new SerializedObject(component);var p=so.GetIterator();while(p.Next(true))if(p.propertyType==SerializedPropertyType.ObjectReference&&p.objectReferenceValue&&AssetDatabase.GetAssetPath(p.objectReferenceValue)!=target)p.objectReferenceValue=Copy(p.objectReferenceValue);so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(component);AssetDatabase.SaveAssetIfDirty(component);}
            }
            return result;
        }
        // Copy only native presentation components. No V4 gameplay/session scripts enter the scene.
        static GameObject Visual(GameObject source,Transform parent)
        {
            var go=new GameObject(source.name);go.transform.SetParent(parent,false);
            go.transform.localPosition=source.transform.localPosition;go.transform.localRotation=source.transform.localRotation;go.transform.localScale=source.transform.localScale;
            foreach(var component in source.GetComponents<Component>())
            {
                if(component is Transform)continue;
                if(!(component is MeshFilter||component is Renderer||component is Collider||component is Light||
                    component is UnityEngine.Rendering.Volume||component is UnityEngine.Rendering.Universal.UniversalAdditionalLightData))continue;
                var clone=go.AddComponent(component.GetType());EditorUtility.CopySerialized(component,clone);
                var so=new SerializedObject(clone);var p=so.GetIterator();while(p.Next(true))
                    if(p.propertyType==SerializedPropertyType.ObjectReference&&p.objectReferenceValue&&AssetDatabase.Contains(p.objectReferenceValue))p.objectReferenceValue=Copy(p.objectReferenceValue);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach(Transform child in source.transform)Visual(child.gameObject,go.transform);
            go.SetActive(source.activeSelf);return go;
        }
        [MenuItem("PuzzleApple/V5/Outdoor/Create missing assets")]
        public static string Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Leave Play before authoring outdoor assets.");
            MigrateGrammar();
            if(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))return "Existing outdoor assets preserved; grammar checked.";
            copies.Clear();paths.Clear();
            var original=SceneManager.GetActiveScene();var source=EditorSceneManager.OpenPreviewScene("Assets/Scenes/V4/P4.unity");
            var authored=EditorSceneManager.NewPreviewScene();
            GameObject root=null;
            try
            {
                var all=source.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
                Transform Find(string name)=>all.First(t=>t.name==name);
                root=new GameObject("V5 Outdoor");root.SetActive(false);SceneManager.MoveGameObjectToScene(root,authored);
                var ground=Visual(Find("Ground").gameObject,root.transform).GetComponent<Collider>();
                var sun=Visual(Find("Directional Light").gameObject,root.transform).GetComponent<Light>();
                var cloud=Visual(Find("Cloud Layer").gameObject,root.transform).GetComponent<Renderer>();
                var volume=Visual(Find("World Glow").gameObject,root.transform);
                var oldEnvironment=all.Select(t=>t.GetComponent<PuzzleApple.V4.NightEnvironment>()).First(x=>x);
                var oldClouds=all.Select(t=>t.GetComponent<PuzzleApple.V4.CloudLightCookie>()).First(x=>x);
                var environment=root.AddComponent<O.NightEnvironment>();EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(oldEnvironment),environment);
                var clouds=root.AddComponent<O.CloudLightCookie>();EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(oldClouds),clouds);
                environment.sun=sun;environment.daySky=Copy(oldEnvironment.daySky);environment.nightSky=Copy(oldEnvironment.nightSky);environment.clouds=clouds;
                clouds.sun=sun;clouds.cloudPlane=cloud;clouds.cloudMask=Copy(oldClouds.cloudMask);clouds.sky=environment.daySky;clouds.player=null;
                var oldLamp=all.Select(t=>t.GetComponent<PuzzleApple.V4.OpeningLight>()).First(l=>l&&!l.appearanceSource);
                var sourceObject=new GameObject("Outdoor light state",typeof(MeshRenderer));sourceObject.transform.SetParent(root.transform,false);
                var sourceRenderer=sourceObject.GetComponent<MeshRenderer>();sourceRenderer.sharedMaterial=Copy(oldLamp.surface.sharedMaterial);sourceRenderer.enabled=false;
                var state=sourceObject.AddComponent<O.OpeningLight>();state.surface=sourceRenderer;state.transitionDuration=oldLamp.transitionDuration;
                environment.lamp=state;
                var field=root.AddComponent<O.OpeningLightField>();field.stateSource=state;field.ground=ground;
                var oldField=all.Select(t=>t.GetComponent<PuzzleApple.V4.OpeningLightField>()).First(x=>x);
                field.scaleRange=oldField.scaleRange;field.clearanceRange=oldField.clearanceRange;field.revealDuration=oldField.revealDuration;field.moveSpeed=oldField.moveSpeed;
                var frame=new GameObject("Independent exit frame");frame.transform.SetParent(root.transform,false);
                frame.transform.localPosition=new Vector3(0,ground.bounds.max.y+.02f,0);
                var oldDoor=all.Select(t=>t.GetComponent<PuzzleApple.V4.ThresholdDoor>()).First(x=>x);
                foreach(var name in new[]{"Left pier","Right pier","Lintel","Sill"})
                {
                    var part=oldDoor.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
                    var copy=Visual(part.gameObject,frame.transform);copy.transform.localPosition=oldDoor.transform.InverseTransformPoint(part.position);
                    copy.transform.localRotation=Quaternion.Inverse(oldDoor.transform.rotation)*part.rotation;
                    foreach(var renderer in copy.GetComponentsInChildren<Renderer>(true))renderer.enabled=true;
                    foreach(var collider in copy.GetComponentsInChildren<Collider>(true))collider.enabled=true;
                }
                var door=frame.AddComponent<O.ThresholdDoor>();door.groundHeight=ground.bounds.max.y+.02f;
                frame.AddComponent<O.LightMotion>().speed=oldDoor.GetComponent<PuzzleApple.V4.LightMotion>().speed;
                door.collectionDistance=oldDoor.collectionDistance;door.loweringSpeed=oldDoor.loweringSpeed;door.loweringAcceleration=oldDoor.loweringAcceleration;
                frame.GetComponent<Rigidbody>().isKinematic=true;frame.GetComponent<Rigidbody>().useGravity=false;
                var appearance=frame.AddComponent<O.DoorStateEffects>();
                var lightEffects=root.AddComponent<O.LightSentenceEffects>();lightEffects.field=field;lightEffects.stateSource=state;lightEffects.ground=ground;lightEffects.door=door;
                var doorEffects=root.AddComponent<O.DoorSentenceEffects>();doorEffects.door=door;doorEffects.appearance=appearance;doorEffects.lights=lightEffects;doorEffects.ground=ground;
                var worldEffects=root.AddComponent<O.WorldSentenceEffects>();worldEffects.environment=environment;worldEffects.ground=ground;worldEffects.door=door;worldEffects.lights=lightEffects;
                var exit=new GameObject("Horizontal exit").transform;exit.SetParent(frame.transform,false);exit.localPosition=new Vector3(0,1.2f,.6f);
                var region=new GameObject("Outdoor closure region",typeof(BoxCollider),typeof(ClosureRegion));region.transform.SetParent(root.transform,false);
                region.GetComponent<BoxCollider>().isTrigger=true;region.GetComponent<BoxCollider>().center=new Vector3(0,100,0);region.GetComponent<BoxCollider>().size=new Vector3(2000,400,2000);region.GetComponent<ClosureRegion>().priority=10;
                var outdoor=root.AddComponent<O.OutdoorWorld>();outdoor.ground=ground;outdoor.sun=sun;outdoor.environment=environment;outdoor.clouds=clouds;outdoor.stateSource=state;outdoor.field=field;outdoor.door=door;outdoor.lights=lightEffects;outdoor.doors=doorEffects;outdoor.world=worldEffects;outdoor.exit=exit;
                var oldMusic=AssetDatabase.LoadAssetAtPath<PuzzleApple.V4.MusicTrack>("Assets/Audio/V4/Music/IntroMusic.asset");
                const string musicPath="Assets/Audio/V5/Outdoor/IntroMusic.asset";Folder("Assets/Audio/V5/Outdoor");
                var music=ScriptableObject.CreateInstance<O.MusicTrack>();EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(oldMusic),music);music.clip=Copy(oldMusic.clip);AssetDatabase.CreateAsset(music,musicPath);outdoor.music=music;
                root.SetActive(true);Folder(PrefabPath.Substring(0,PrefabPath.LastIndexOf('/')));PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
                // A separate editable scene, backed by the same prefab used in player builds.
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
                try
                {
                    // RenderSettings belong to the active scene, not the prefab's scene.
                    SceneManager.SetActiveScene(scene);
                    PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),scene);
                    var camera=new GameObject("Outdoor preview camera",typeof(Camera),typeof(AudioListener));SceneManager.MoveGameObjectToScene(camera,scene);
                    camera.transform.position=new Vector3(0,2,12);camera.transform.rotation=Quaternion.Euler(0,180,0);camera.GetComponent<Camera>().clearFlags=CameraClearFlags.Skybox;
                    RenderSettings.skybox=environment.daySky;RenderSettings.ambientMode=environment.dayAmbientMode;RenderSettings.ambientSkyColor=environment.dayAmbientSky;RenderSettings.ambientEquatorColor=environment.dayAmbientEquator;RenderSettings.ambientGroundColor=environment.dayAmbientGround;
                    EditorSceneManager.SaveScene(scene,ScenePath);
                }
                finally{EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(original);}
                return "Created V5 Outdoor scene, prefab, independent visuals, V4 BGM and full grammar. P5 untouched.";
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(authored);EditorSceneManager.ClosePreviewScene(source);
                if(original.IsValid()&&original.isLoaded)SceneManager.SetActiveScene(original);
            }
        }
        internal static string[] V5Words(PuzzleApple.V4.Cognition.CognitionRule rule)
        {
            if(rule.Id=="light.ground.move")return new[]{"light","move","floor"};
            return rule.Words.Select(w=>w.Id=="ground"?"floor":w.Id).ToArray();
        }

        static string V5RuleId(PuzzleApple.V4.Cognition.CognitionRule rule)=>
            rule.Id=="light.ground.move"?"light.move.floor":rule.Id.Replace("ground","floor");

        public static void MigrateGrammar()
        {
            var target=AssetDatabase.LoadAssetAtPath<CognitionCatalog>("Assets/GameData/V5/Opening/OpeningCatalog.asset");
            var source=AssetDatabase.LoadAssetAtPath<PuzzleApple.V4.Cognition.CognitionCatalog>("Assets/GameData/V4/Opening/OpeningCatalog.asset");
            var rules=target.Rules.ToList();
            foreach(var old in source.Rules)
            {
                var ids=V5Words(old);
                if(rules.Any(r=>r.Words.Select(w=>w.Id).SequenceEqual(ids)))continue;
                var rule=ScriptableObject.CreateInstance<CognitionRule>();var so=new SerializedObject(rule);
                so.FindProperty("id").stringValue=V5RuleId(old);
                var words=so.FindProperty("words");words.arraySize=ids.Length;for(int i=0;i<ids.Length;i++)words.GetArrayElementAtIndex(i).objectReferenceValue=target.Word(ids[i]);
                var effect=(CognitionSignal)Enum.Parse(typeof(CognitionSignal),old.Effect.ToString());so.FindProperty("effect").intValue=(int)effect;
                so.FindProperty("exclusionKey").stringValue=old.ExclusionKey;so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(rule,"Assets/GameData/V5/Opening/Rules/"+old.name+".asset");rules.Add(rule);
            }
            var catalog=new SerializedObject(target);var array=catalog.FindProperty("rules");array.arraySize=rules.Count;for(int i=0;i<rules.Count;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=rules[i];catalog.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(target);target.ValidateOrThrow();
            var settings=AssetDatabase.LoadAssetAtPath<SentencePresentationSettings>("Assets/GameData/V5/Resources/V5/SentencePresentation.asset");
            var shots=settings.shots.ToList();
            foreach(var old in AssetDatabase.FindAssets("t:IntroShotProfile",new[]{"Assets/GameData/V4/Opening/IntroShots"})
                .Select(g=>AssetDatabase.LoadAssetAtPath<PuzzleApple.V4.IntroShotProfile>(AssetDatabase.GUIDToAssetPath(g))))
            {
                if(!old||!old.rule)continue;var id=V5RuleId(old.rule);
                if(shots.Any(s=>s.rule==id))continue;
                var focus=id=="floor.move"?SentencePresentation.Focus.Sky:id.StartsWith("door.")||id=="light.door.move"?SentencePresentation.Focus.Door:
                    id.StartsWith("floor.")?SentencePresentation.Focus.Floor:SentencePresentation.Focus.LightGroup;
                shots.Add(new SentencePresentation.Shot(id,focus,old.followTarget){enter=old.enterDuration,hold=old.holdDuration,exit=old.exitDuration,
                    letterbox=old.letterboxHeight,followSpeed=old.maxFollowDegreesPerSecond,offset=old.targetOffset});
            }
            settings.shots=shots.ToArray();EditorUtility.SetDirty(settings);AssetDatabase.SaveAssetIfDirty(settings);
        }
    }
}
#endif
