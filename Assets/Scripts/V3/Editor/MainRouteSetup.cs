using System;
using System.Linq;
using PuzzleApple.V3.Cognition;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace PuzzleApple.V3.Editor
{
    // One-time migration: authored native components remain editable in OpeningPrototype.
    public static class MainRouteSetup
    {
        static GameObject Child(string name,Transform parent,Vector3 p)
        {var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.localPosition=p;return g;}
        static GameObject Clone(Transform source,Transform parent)
        {
            var g=Object.Instantiate(source.gameObject);g.name=source.name;
            g.transform.SetParent(parent,false);g.transform.localPosition=source.position-Vector3.right*100;
            g.transform.localRotation=source.rotation;g.transform.localScale=source.lossyScale;
            foreach(var old in g.GetComponentsInChildren<V3Object>(true))Object.DestroyImmediate(old);
            return g;
        }
        static GameObject Box(string name,Transform parent,Vector3 p,Vector3 size,Material material)
        {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=material;return g;}
        static BoxCollider Volume(string name,Transform parent,Vector3 p,Vector3 size)
        {var c=Child(name,parent,p).AddComponent<BoxCollider>();c.isTrigger=true;c.size=size;return c;}
        static void Word(GameObject g,params string[] words)=>g.AddComponent<OpeningCollectible>().words=words;
        static void Snapshot(OpeningRoom room,Transform parent,string word,Transform subject,Vector3 position,Vector3 look)
        {
            var g=Child("Snapshot — "+word,parent,position);var c=g.AddComponent<Camera>();c.CopyFrom(room.presentation.view);
            c.enabled=false;c.fieldOfView=40;c.transform.LookAt(parent.TransformPoint(look));
            var data=c.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();data.renderPostProcessing=true;
            var s=g.AddComponent<OpeningSnapshotCamera>();s.wordId=word;s.subject=subject;
        }
        public static string Build()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Exit Play first.");
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!="Assets/Scenes/V3/P3.unity")throw new InvalidOperationException("Open P3.");
            var opening=GameObject.Find("Opening Prototype");var room=opening.GetComponent<OpeningRoom>();
            if(room.mainRoute)throw new InvalidOperationException("Main route already exists; edit its native components instead.");
            var reserve=scene.GetRootGameObjects().Single(g=>g.name.StartsWith("Legacy"));
            var legacy=reserve.GetComponentInChildren<V3World>(true);
            var root=Child("Main route",opening.transform,new Vector3(0,0,14));root.transform.localRotation=Quaternion.Euler(0,90,0);
            var route=root.AddComponent<MainRoute>();route.room=room;room.mainRoute=route;
            var geometry=Child("Gallery and corridor",root.transform,Vector3.zero).transform;
            var oldSpace=reserve.transform.Find("PuzzleApple-SpaceStudyV2");
            foreach(var name in new[]{"主房-入口","主房-左右","主房-顶底","走廊-左右","走廊-顶底"})Clone(oldSpace.Find(name),geometry);
            var passage=Clone(reserve.transform.Find("V3 balance passage"),root.transform);
            route.passageSeal=passage.transform.Find("Balance stone seal");
            route.exitVolume=Volume("Exit reached",root.transform,new Vector3(-22,1.5f,0),new Vector3(3,3,2.4f));
            var gate=Clone(reserve.transform.Find("Corridor gate - V3"),root.transform);route.gateFrame=gate.transform;
            var door=gate.transform.Find("Corridor door");Word(door.gameObject,"door");
            route.doorLeaves=door.GetComponentsInChildren<MeshFilter>().OrderByDescending(m=>m.transform.localPosition.z).Select(m=>m.transform).ToArray();
            var barrier=Child("Passage barrier",gate.transform,new Vector3(0,1.75f,0));
            var barrierCollider=barrier.AddComponent<BoxCollider>();barrierCollider.size=new Vector3(.14f,3.5f,3.2f);
            Word(barrier,"door");route.gateBarrier=barrier.AddComponent<PassageBarrier>();
            var lamp=gate.transform.Find("Door indicator");Word(lamp.gameObject,"positive","negative");route.indicator=lamp.GetComponentInChildren<Renderer>();
            var lampMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Main route indicator"};lampMaterial.SetColor("_BaseColor",Color.white);
            AssetDatabase.CreateAsset(lampMaterial,"Assets/Materials/V3/Opening/Indicator.mat");route.indicator.sharedMaterial=lampMaterial;
            var baseMaterial=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/V3/Opening/Floor.mat")){name="Pressure plate"};
            baseMaterial.SetColor("_BaseColor",new Color(.28f,.30f,.32f));baseMaterial.SetFloat("_Smoothness",.2f);
            AssetDatabase.CreateAsset(baseMaterial,"Assets/Materials/V3/Opening/PressurePlate.mat");
            var plate=Box("Pressure plate",root.transform,new Vector3(-8,.1f,0),new Vector3(1.5f,.12f,2.65f),baseMaterial);route.pressurePlate=plate.transform;
            Box("Pressure plate recess",root.transform,new Vector3(-8,.04f,0),new Vector3(1.58f,.02f,2.73f),baseMaterial);
            var balance=Clone(reserve.transform.Find("Balance - V3"),root.transform);balance.transform.localPosition=new Vector3(-8,.16f,0);
            route.balance=balance.transform;route.beam=balance.transform.Find("Beam pivot");
            route.trays=new[]{balance.transform.Find("Tray slot 0"),balance.transform.Find("Tray slot 1")};
            route.trayPlaces=root.AddComponent<DestinationSlots>();route.trayPlaces.places=route.trays;
            // Authored model pans differ slightly in height; a level beam must have level support surfaces.
            var pan1=route.trays[1].localPosition;pan1.y=route.trays[0].localPosition.y;route.trays[1].localPosition=pan1;
            Word(balance,"equal");
            var apple=Clone(legacy.originalApple.transform,root.transform);apple.name="Apple";
            var body=apple.GetComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;body.constraints=RigidbodyConstraints.FreezeRotation;
            route.apple=apple.AddComponent<OpeningObject>();route.apple.wordId="apple";
            route.apple.reflectionPlane=Child("Apple reflection plane",root.transform,new Vector3(-8,0,0)).transform;
            route.apple.movementBounds=Volume("Gallery movement bounds",root.transform,new Vector3(-8,3.3f,0),new Vector3(19,6.4f,17));
            room.objects=room.objects.Concat(new[]{route.apple}).ToArray();
            var spotlight=Child("Balance spotlight",root.transform,new Vector3(-5,6,-1)).AddComponent<Light>();
            spotlight.type=LightType.Spot;spotlight.transform.LookAt(root.transform.TransformPoint(new Vector3(-8,1,0)));spotlight.color=Color.white;
            spotlight.intensity=180;spotlight.range=14;spotlight.spotAngle=48;spotlight.innerSpotAngle=32;spotlight.shadows=LightShadows.Soft;
            var profile=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/Settings/V3/MainHallProfile.asset");
            if(!profile)
            {
                profile=ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
                AssetDatabase.CreateAsset(profile,"Assets/Settings/V3/MainHallProfile.asset");
                var grading=profile.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);grading.postExposure.Override(-1.8f);
                AssetDatabase.AddObjectToAsset(grading,profile);EditorUtility.SetDirty(profile);
            }
            var exposure=Volume("Hall exposure volume",root.transform,new Vector3(-8,6,0),new Vector3(20,12,18));
            var volume=exposure.gameObject.AddComponent<UnityEngine.Rendering.Volume>();volume.isGlobal=false;volume.priority=10;volume.blendDistance=1;volume.sharedProfile=profile;
            var fill=Child("Gallery dim fill",root.transform,new Vector3(-8,4,0)).AddComponent<Light>();fill.type=LightType.Point;fill.intensity=.6f;fill.range=15;
            var corridor=Child("Corridor light",root.transform,new Vector3(5.4f,2.9f,0)).AddComponent<Light>();corridor.type=LightType.Point;corridor.intensity=.75f;corridor.range=7;corridor.shadows=LightShadows.Soft;
            var rooms=opening.transform.Find("Rooms");
            foreach(var name in new[]{"Floor","Ceiling","West wall","East wall"})
            {var t=rooms.Find(name);var p=t.localPosition;p.z=-4;t.localPosition=p;var s=t.localScale;s.z=16.4f;t.localScale=s;}
            rooms.Find("Main room end").gameObject.SetActive(false);
            rooms.Find("Ceiling light 8").gameObject.SetActive(false);
            var marker=rooms.Find("Main next room");if(marker)marker.gameObject.SetActive(false);
            Snapshot(room,root.transform,"apple",apple.transform,new Vector3(-4.6f,1.25f,-.75f),new Vector3(-6.3f,.35f,-.75f));
            Snapshot(room,root.transform,"equal",balance.transform,new Vector3(-3.6f,2.35f,0),new Vector3(-8,1.2f,0));
            Snapshot(room,root.transform,"door",door,new Vector3(7.6f,1.8f,0),new Vector3(2,1.8f,0));
            Snapshot(room,root.transform,"positive",lamp,new Vector3(3.1f,3.78f,0),new Vector3(2.1f,3.78f,0));
            Snapshot(room,root.transform,"negative",lamp,new Vector3(3.1f,3.78f,0),new Vector3(2.1f,3.78f,0));

            BuildRules(room.board.Catalog);
            OpeningMaterials.Assign(room);
            PrefabUtility.ApplyPrefabInstance(opening,InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            MainHallLook.Apply();
            return "Main route migrated; opening and hidden room retained.";
        }
        static void BuildRules(CognitionCatalog catalog)
        {
            var equal=new SerializedObject(catalog.Word("equal"));var senses=equal.FindProperty("senses");
            if(!catalog.Word("equal").Senses.Any(s=>s.role==WordRole.Reference))
            {int i=senses.arraySize++;senses.GetArrayElementAtIndex(i).FindPropertyRelative("meaning").stringValue="balance";senses.GetArrayElementAtIndex(i).FindPropertyRelative("role").enumValueIndex=(int)WordRole.Reference;equal.ApplyModifiedPropertiesWithoutUndo();}
            var rules=catalog.Rules.ToList();
            void Rule(string sentence,CognitionSignal signal,string channel)
            {
                var path="Assets/GameData/V3/Opening/Rules/"+sentence.Replace(' ','-')+".asset";
                var r=AssetDatabase.LoadAssetAtPath<CognitionRule>(path);if(!r){r=ScriptableObject.CreateInstance<CognitionRule>();AssetDatabase.CreateAsset(r,path);}
                var so=new SerializedObject(r);so.FindProperty("id").stringValue="opening-"+sentence.Replace(' ','-');
                so.FindProperty("effect").enumValueIndex=(int)signal;so.FindProperty("exclusionKey").stringValue=channel;
                var words=sentence.Split(' ');var p=so.FindProperty("words");p.arraySize=words.Length;
                for(int i=0;i<words.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=catalog.Word(words[i]);
                so.ApplyModifiedPropertiesWithoutUndo();if(!rules.Contains(r))rules.Add(r);
            }
            Rule("apple move equal",CognitionSignal.OpeningMove,"motion:apple");
            Rule("i move equal",CognitionSignal.OpeningMove,"motion:i");
            Rule("i equal apple",CognitionSignal.EqualApple,"form:i");
            Rule("positive door",CognitionSignal.DoorPositive,"gate");Rule("negative door",CognitionSignal.DoorNegative,"gate");
            var data=new SerializedObject(catalog);var list=data.FindProperty("rules");list.arraySize=rules.Count;
            for(int i=0;i<rules.Count;i++)list.GetArrayElementAtIndex(i).objectReferenceValue=rules[i];data.ApplyModifiedPropertiesWithoutUndo();catalog.ValidateOrThrow();
        }
    }
}
