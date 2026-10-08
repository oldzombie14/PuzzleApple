using System;
using System.Collections.Generic;
using System.Linq;
using PuzzleApple.V3.Cognition;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PuzzleApple.V3.Editor
{
    // Explicit migration only. Runtime never rebuilds authored scene geometry or UI.
    public static class OpeningSetup
    {
        const string Data="Assets/GameData/V3/Opening";
        public const string PrefabPath="Assets/Prefabs/V3/OpeningPrototype.prefab";
        static void Folder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            var parent=System.IO.Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);
            AssetDatabase.CreateFolder(parent,System.IO.Path.GetFileName(path));
        }
        static void Ref(UnityEngine.Object obj,string field,UnityEngine.Object value)
        {var so=new SerializedObject(obj);so.FindProperty(field).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
        static void Refs(UnityEngine.Object obj,string field,IEnumerable<UnityEngine.Object> values)
        {var so=new SerializedObject(obj);var p=so.FindProperty(field);var a=values.ToArray();p.arraySize=a.Length;for(int i=0;i<a.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=a[i];so.ApplyModifiedPropertiesWithoutUndo();}
        public static CognitionCatalog BuildCatalog()
        {
            Folder(Data+"/Rules");
            var legacy=AssetDatabase.LoadAssetAtPath<CognitionCatalog>("Assets/GameData/V3/Cognition/V3Catalog.asset");
            var rules=new List<CognitionRule>();
            void Rule(string sentence,CognitionSignal effect,string channel)
            {
                var id=sentence.Replace(' ','-');string path=Data+"/Rules/"+id+".asset";
                var r=AssetDatabase.LoadAssetAtPath<CognitionRule>(path);
                if(!r){r=ScriptableObject.CreateInstance<CognitionRule>();AssetDatabase.CreateAsset(r,path);}
                var so=new SerializedObject(r);so.FindProperty("id").stringValue="opening-"+id;
                so.FindProperty("effect").enumValueIndex=(int)effect;so.FindProperty("trigger").enumValueIndex=0;
                so.FindProperty("exclusionKey").stringValue=channel;so.ApplyModifiedPropertiesWithoutUndo();
                Refs(r,"words",sentence.Split(' ').Select(legacy.Word));rules.Add(r);
            }
            // Known physical nouns share the three movement forms. Discovery stays room-specific.
            var nouns=new[]{"i","mirror","apple","door"};
            foreach(var subject in nouns)
            {
                Rule(subject+" move",subject=="i"?CognitionSignal.PlayerMove:CognitionSignal.OpeningMove,"motion:"+subject);
                Rule(subject+" positive move",CognitionSignal.OpeningMove,"motion:"+subject);
                Rule(subject+" negative move",CognitionSignal.OpeningMove,"motion:"+subject);
                foreach(var destination in nouns)Rule(subject+" move "+destination,CognitionSignal.OpeningMove,"motion:"+subject);
                Rule("mirror "+subject,CognitionSignal.OpeningMirror,"form:"+subject);
                Rule("i "+subject,CognitionSignal.OpeningReset,"form:"+subject);
            }
            var catalog=AssetDatabase.LoadAssetAtPath<CognitionCatalog>(Data+"/OpeningCatalog.asset");
            if(!catalog){catalog=ScriptableObject.CreateInstance<CognitionCatalog>();AssetDatabase.CreateAsset(catalog,Data+"/OpeningCatalog.asset");}
            Refs(catalog,"words",legacy.Words);Refs(catalog,"rules",rules);Refs(catalog,"conflicts",new UnityEngine.Object[0]);
            catalog.ValidateOrThrow();AssetDatabase.SaveAssets();return catalog;
        }
        static GameObject Child(string name,Transform parent,Vector3 position)
        {var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.localPosition=position;return g;}
        static Material Material(string name,Color color)
        {
            Folder("Assets/Materials/V3/Opening");string path="Assets/Materials/V3/Opening/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m)return m;
            m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.12f);AssetDatabase.CreateAsset(m,path);return m;
        }
        static GameObject Box(string name,Transform parent,Vector3 center,Vector3 size,Material material)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=center;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=material;return g;
        }
        static BoxCollider Volume(string name,Transform parent,Vector3 center,Vector3 size)
        {var c=Child(name,parent,center).AddComponent<BoxCollider>();c.size=size;c.isTrigger=true;return c;}
        public static string Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            var scene=EditorSceneManager.GetActiveScene();
            if(scene.path!="Assets/Scenes/V3/P3.unity"||UnityEngine.Object.FindFirstObjectByType<OpeningRoom>())throw new InvalidOperationException("Migration requires the original P3 scene; never overwrite an authored opening.");
            var legacy=UnityEngine.Object.FindFirstObjectByType<V3World>();if(!legacy)throw new InvalidOperationException("Legacy room missing.");
            var roots=scene.GetRootGameObjects();
            var catalog=BuildCatalog();
            var root=new GameObject("Opening Prototype");var room=root.AddComponent<OpeningRoom>();
            var environment=Child("Rooms",root.transform,Vector3.zero).transform;
            var wall=Material("Plaster",new Color(.94f,.94f,.94f));var floor=Material("Floor",new Color(.89f,.89f,.89f));
            var ceiling=Material("Ceiling",new Color(.93f,.93f,.93f));
            Box("Floor",environment,new Vector3(0,-.15f,0),new Vector3(7.6f,.3f,24.4f),floor);
            Box("Ceiling",environment,new Vector3(0,3.95f,0),new Vector3(7.6f,.3f,24.4f),ceiling);
            Box("West wall",environment,new Vector3(-3.65f,1.9f,0),new Vector3(.3f,3.8f,24.4f),wall);
            Box("East wall",environment,new Vector3(3.65f,1.9f,0),new Vector3(.3f,3.8f,24.4f),wall);
            Box("Main room end",environment,new Vector3(0,1.9f,12),new Vector3(7,3.8f,.3f),wall);
            Box("Secret room end",environment,new Vector3(0,1.9f,-12),new Vector3(7,3.8f,.3f),wall);
            foreach(float z in new[]{-4f,4f})
            {
                var partition=Child(z>0?"Main passage":"Optional passage",environment,new Vector3(0,0,z)).transform;
                Box("Left wall",partition,new Vector3(-2.175f,1.9f,0),new Vector3(2.65f,3.8f,.3f),wall);
                Box("Right wall",partition,new Vector3(2.175f,1.9f,0),new Vector3(2.65f,3.8f,.3f),wall);
                Box("Lintel",partition,new Vector3(0,3.125f,0),new Vector3(1.7f,1.35f,.3f),wall);
                if(z<0)room.secretSeal=Box("Undiscovered wall",partition,new Vector3(0,1.225f,0),new Vector3(1.7f,2.45f,.3f),wall);
            }
            Child("Main next room — empty",environment,new Vector3(0,0,8));Child("Optional next room — empty",environment,new Vector3(0,0,-8));
            foreach(float z in new[]{-8f,-1.5f,2f,8f})
            {
                var light=Child("Ceiling light "+z,environment,new Vector3(0,2.85f,z)).AddComponent<Light>();
                light.type=LightType.Point;light.color=Color.white;light.intensity=.85f;light.range=9;light.shadows=LightShadows.Soft;
            }
            room.movementVolume=Volume("Movement bounds",root.transform,new Vector3(0,1.9f,0),new Vector3(7,3.8f,7.65f));
            room.secretRoomVolume=Volume("Optional room bounds",root.transform,new Vector3(0,1.9f,-8.2f),new Vector3(7,3.8f,8.4f));
            room.playerSpawn=Child("Player spawn",root.transform,new Vector3(0,.04f,-1.8f)).transform;
            var pg=UnityEngine.Object.Instantiate(legacy.player.gameObject,root.transform);pg.name="Player";pg.transform.SetPositionAndRotation(room.playerSpawn.position,room.playerSpawn.rotation);
            var player=pg.GetComponent<FirstPersonController>();player.view=pg.GetComponentInChildren<Camera>().transform;player.view.localRotation=Quaternion.identity;
            var ui=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/V3/UI/V3Interface.prefab"),root.transform);
            var board=ui.GetComponent<CognitionBoard>();Ref(board,"catalog",catalog);player.cognition=board;
            Ref(ui.GetComponent<GameplayPanel>(),"player",player);
            var presentation=ui.GetComponent<V3Presentation>();presentation.view=player.view.GetComponent<Camera>();presentation.selfBeforeEyes=true;
            presentation.selfFadeInDuration=1.5f;presentation.selfBlackoutDuration=1;presentation.selfHoldDuration=.3f;room.interactionDistance=6.5f;
            presentation.view.nearClipPlane=.12f;presentation.view.farClipPlane=70;
            var library=ui.GetComponent<WordLibrary>();
            // Old preset photographs belong to the preserved gallery; opening captures its own learning moments.
            library.vocabularyImages=new WordLibrary.VocabularyImage[0];
            var mirrorGo=UnityEngine.Object.Instantiate(legacy.originalMirror.gameObject,root.transform);mirrorGo.name="Doorway mirror";
            UnityEngine.Object.DestroyImmediate(mirrorGo.GetComponent<V3Object>());
            mirrorGo.transform.SetPositionAndRotation(new Vector3(0,1.35f,3.75f),Quaternion.Euler(0,180,0));mirrorGo.transform.localScale=Vector3.one*1.42f;
            var rb=mirrorGo.GetComponent<Rigidbody>();if(!rb)rb=mirrorGo.AddComponent<Rigidbody>();rb.isKinematic=true;rb.useGravity=false;rb.mass=8;
            rb.constraints=RigidbodyConstraints.FreezeRotation;rb.interpolation=RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            var actor=mirrorGo.AddComponent<OpeningObject>();
            foreach(var mirror in mirrorGo.GetComponentsInChildren<PlanarMirror>())Ref(mirror,"sourceCamera",presentation.view);
            room.objects=new[]{actor};room.doorwayMirror=actor;
            var view=root.AddComponent<OpeningMirrorView>();view.player=player;view.presentation=presentation;
            view.plane=Child("Reflection plane — local Z normal",root.transform,Vector3.zero).transform;
            room.board=board;room.library=library;room.player=player;room.presentation=presentation;room.mirrorView=view;
            OpeningMaterials.Assign(room);
            ConfigureViews(root);
            AddMainRoomMarker(root,legacy.originalApple.gameObject);
            // Store old geometry, props, rig and logic together; avoid duplicate active cameras or gameplay.
            var reserve=new GameObject("Legacy V3 — Reserved (inactive)");reserve.SetActive(false);
            foreach(var old in roots)if(old.name!="EventSystem"&&old.name!="Global Volume"&&old.name!="Directional Light")old.transform.SetParent(reserve.transform,true);
            reserve.transform.position=new Vector3(100,0,0);
            foreach(var component in ui.GetComponents<MonoBehaviour>())PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            Folder("Assets/Prefabs/V3");PrefabUtility.SaveAsPrefabAssetAndConnect(root,PrefabPath,InteractionMode.AutomatedAction);
            EditorUtility.SetDirty(root);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject=root;
            return "Opening prefab and scene saved; previous setup preserved at X=100, inactive.";
        }
        // Explicit editor migration; all poses and Camera settings remain authored in the prefab.
        public static void ConfigureViews(GameObject root)
        {
            var room=root.GetComponent<OpeningRoom>();var main=room.presentation.view;
            ConfigureSelfPlane(room);
            Camera CameraNode(string name)
            {
                var existing=root.transform.Find(name);var go=existing?existing.gameObject:Child(name,root.transform,Vector3.zero);
                var c=go.GetComponent<Camera>();if(!c)c=go.AddComponent<Camera>();
                c.CopyFrom(main);c.enabled=false;c.targetTexture=null;c.rect=new Rect(0,0,1,1);c.tag="Untagged";
                var source=main.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                var data=go.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                if(!data)data=go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                if(source)EditorUtility.CopySerialized(source,data);
                return c;
            }
            var reflected=CameraNode("Reflected view camera");reflected.depth=main.depth+1;
            reflected.transform.SetPositionAndRotation(room.mirrorView.ReflectSelfPoint(main.transform.position),room.mirrorView.ReflectSelfRotation(main.transform.rotation));
            room.mirrorView.reflectedCamera=reflected;room.presentation.secondaryView=reflected;
            var shots=new List<OpeningSnapshotCamera>();
            foreach(string id in new[]{"move","mirror"})
            {
                var camera=CameraNode(id=="move"?"Snapshot — Floor":"Snapshot — Mirror");camera.fieldOfView=50;camera.aspect=.8f;
                var shot=camera.GetComponent<OpeningSnapshotCamera>();if(!shot)shot=camera.gameObject.AddComponent<OpeningSnapshotCamera>();
                shot.wordId=id;shot.resolution=new Vector2Int(512,640);
                if(id=="mirror")
                {
                    shot.subject=room.doorwayMirror.transform;
                    camera.transform.SetPositionAndRotation(shot.subject.position+shot.subject.forward*3.5f,Quaternion.LookRotation(-shot.subject.forward,Vector3.up));
                }
                else{shot.subject=null;camera.transform.SetPositionAndRotation(room.playerSpawn.position+Vector3.up*2,Quaternion.Euler(90,0,0));}
                shots.Add(shot);
            }
            room.library.vocabularyImages=new WordLibrary.VocabularyImage[0];
            room.presentation.selfBlackoutDuration=1;room.presentation.selfFadeInDuration=1.5f;room.presentation.selfHoldDuration=.3f;
            room.presentation.wordFadeInDuration=.3f;room.presentation.wordFlyDuration=.6f;room.presentation.wordDisplayDuration=1.6f;
            foreach(var path in new[]{"Player/Original body","Reflected body"})
            {var body=root.transform.Find(path);if(body)UnityEngine.Object.DestroyImmediate(body.gameObject);}
            foreach(var mirror in root.GetComponentsInChildren<PlanarMirror>())
            {var so=new SerializedObject(mirror);so.FindProperty("resolution").intValue=512;so.ApplyModifiedPropertiesWithoutUndo();}
            PrefabUtility.RecordPrefabInstancePropertyModifications(room.presentation);PrefabUtility.RecordPrefabInstancePropertyModifications(room.library);
        }
        public static void ConfigureSelfPlane(OpeningRoom room)
        {
            var plane=room.mirrorView.selfPlane;
            if(!plane)plane=room.transform.Find("Player view reflection plane");
            if(!plane)plane=room.doorwayMirror.transform.Find("Player view reflection plane");
            if(!plane)
            {
                plane=Child("Player view reflection plane",room.transform,Vector3.zero).transform;
            }
            // Room-local X=0 is fixed, independently of the movable mirror.
            plane.SetParent(room.transform,false);
            plane.localPosition=Vector3.zero;
            plane.localRotation=Quaternion.Euler(0,90,0);
            plane.localScale=Vector3.one;
            room.mirrorView.selfPlane=plane;
        }
        static void AddMainRoomMarker(GameObject root,GameObject source)
        {
            var parent=root.transform.Find("Rooms/Main next room — empty");
            if(!parent)parent=root.transform.Find("Rooms/Main next room");
            parent.name="Main next room";
            if(parent.Find("Main route apple marker"))return;
            var apple=UnityEngine.Object.Instantiate(source,parent);apple.name="Main route apple marker";
            var logic=apple.GetComponent<V3Object>();if(logic)UnityEngine.Object.DestroyImmediate(logic);
            var body=apple.GetComponent<Rigidbody>();if(body)UnityEngine.Object.DestroyImmediate(body);
            apple.SetActive(true);apple.transform.localPosition=Vector3.zero;
            apple.transform.position+=Vector3.up*(.02f-apple.GetComponent<Renderer>().bounds.min.y);
        }
    }
}
