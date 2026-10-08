using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PuzzleApple.V3.Cognition;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PuzzleApple.V3.Editor
{
    public static class V3Setup
    {
        const string Root="Assets/GameData/V3/Cognition";
        [MenuItem("PuzzleApple/V3/Open playable scene")]
        public static void Open()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene("Assets/Scenes/V3/P3.unity");
        }
        static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;var parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
        static T Asset<T>(string path) where T:ScriptableObject {var a=AssetDatabase.LoadAssetAtPath<T>(path);if(a)return a;Folder(Path.GetDirectoryName(path).Replace('\\','/'));a=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(a,path);return a;}
        static void Field(UnityEngine.Object obj,string field,UnityEngine.Object value){var so=new SerializedObject(obj);so.FindProperty(field).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
        static void TextField(UnityEngine.Object obj,string field,string value){var so=new SerializedObject(obj);so.FindProperty(field).stringValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
        static void ArrayField(UnityEngine.Object obj,string field,IEnumerable<UnityEngine.Object> values){var so=new SerializedObject(obj);var p=so.FindProperty(field);var items=values.ToArray();p.arraySize=items.Length;for(int i=0;i<items.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=items[i];so.ApplyModifiedPropertiesWithoutUndo();}
        static void EnumField(UnityEngine.Object obj,string field,int value){var so=new SerializedObject(obj);so.FindProperty(field).enumValueIndex=value;so.ApplyModifiedPropertiesWithoutUndo();}
        static T Add<T>(GameObject g) where T:Component {var existing=g.GetComponent<T>();return existing?existing:g.AddComponent<T>();}
        static void Sense(WordDefinition word,params (string,WordRole)[] senses){var so=new SerializedObject(word);var p=so.FindProperty("senses");p.arraySize=senses.Length;for(int i=0;i<senses.Length;i++){var e=p.GetArrayElementAtIndex(i);e.FindPropertyRelative("meaning").stringValue=senses[i].Item1;e.FindPropertyRelative("role").enumValueIndex=(int)senses[i].Item2;}so.ApplyModifiedPropertiesWithoutUndo();}
        [MenuItem("PuzzleApple/V3/Build playable prototype")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/V3/P3.unity")throw new InvalidOperationException("Open P3 first.");
            if(GameObject.Find("V3 Runtime"))throw new InvalidOperationException("V3 already configured. Do not rebuild over authored scene changes.");
            Folder(Root);Folder("Assets/Sprites/V3");Folder("Assets/Materials/V3");
            var ids=new[]{"i","mirror","move","door","positive","negative","equal","apple"};
            var images=new[]{"I","Mirror","Move","Door","Positive","Negative","Equal","Apple"};
            var words=new Dictionary<string,WordDefinition>();
            for(int i=0;i<ids.Length;i++)
            {
                string path="Assets/Sprites/V3/"+images[i]+".png";
                if(!File.Exists(path))File.Copy("Assets/Sprites/v2_260929/"+images[i]+".png",path);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.SaveAndReimport();
                var word=Asset<WordDefinition>(Root+"/Words/"+ids[i]+".asset");TextField(word,"id",ids[i]);TextField(word,"displayText",ids[i]);Field(word,"symbol",AssetDatabase.LoadAssetAtPath<Sprite>(path));words.Add(ids[i],word);
                Sense(word,(ids[i],ids[i]=="move"?WordRole.Predicate:ids[i]=="equal"?WordRole.Relation:ids[i]=="positive"||ids[i]=="negative"?WordRole.Modifier:WordRole.Reference));
            }
            Sense(words["i"],("self",WordRole.Reference),("essence",WordRole.Transform));Sense(words["mirror"],("mirror",WordRole.Reference),("reflect",WordRole.Transform));
            var rules=new List<CognitionRule>();
            void Rule(string sentence,CognitionSignal effect,TriggerMode trigger=TriggerMode.Direct)
            {var r=Asset<CognitionRule>(Root+"/Rules/"+effect+".asset");TextField(r,"id",effect.ToString().ToLowerInvariant());ArrayField(r,"words",sentence.Split(' ').Select(w=>words[w]));EnumField(r,"effect",(int)effect);EnumField(r,"trigger",(int)trigger);rules.Add(r);}
            Rule("apple move",CognitionSignal.AppleMove);Rule("apple negative move",CognitionSignal.AppleStop);
            Rule("i move",CognitionSignal.PlayerMove);Rule("i negative move",CognitionSignal.PlayerNoMove);
            Rule("mirror move",CognitionSignal.MirrorMove);Rule("mirror negative move",CognitionSignal.MirrorStop);
            Rule("positive door",CognitionSignal.DoorPositive);Rule("negative door",CognitionSignal.DoorNegative);
            Rule("mirror apple",CognitionSignal.MirrorApple,TriggerMode.Interaction);Rule("mirror mirror",CognitionSignal.MirrorMirror,TriggerMode.Interaction);Rule("mirror i",CognitionSignal.MirrorSelf);
            Rule("i apple",CognitionSignal.EssenceApple,TriggerMode.Interaction);Rule("i mirror",CognitionSignal.EssenceMirror,TriggerMode.Interaction);Rule("i i",CognitionSignal.EssenceSelf);Rule("i equal apple",CognitionSignal.EqualApple);
            var conflicts=new List<CognitionConflict>();
            void Conflict(CognitionSignal a,CognitionSignal b,bool earliest=false){var c=Asset<CognitionConflict>(Root+"/Conflicts/"+a+"-"+b+".asset");TextField(c,"id",("conflict-"+a+"-"+b).ToLowerInvariant());EnumField(c,"first",(int)a);EnumField(c,"second",(int)b);var so=new SerializedObject(c);so.FindProperty("earliestWins").boolValue=earliest;so.ApplyModifiedPropertiesWithoutUndo();conflicts.Add(c);}
            Conflict(CognitionSignal.EqualApple,CognitionSignal.EssenceSelf,true);Conflict(CognitionSignal.AppleMove,CognitionSignal.AppleStop);Conflict(CognitionSignal.PlayerMove,CognitionSignal.PlayerNoMove);Conflict(CognitionSignal.MirrorMove,CognitionSignal.MirrorStop);Conflict(CognitionSignal.DoorPositive,CognitionSignal.DoorNegative);Conflict(CognitionSignal.MirrorApple,CognitionSignal.EssenceApple);Conflict(CognitionSignal.MirrorMirror,CognitionSignal.EssenceMirror);Conflict(CognitionSignal.MirrorSelf,CognitionSignal.EssenceSelf);
            var catalog=Asset<CognitionCatalog>(Root+"/V3Catalog.asset");ArrayField(catalog,"words",words.Values);ArrayField(catalog,"rules",rules);ArrayField(catalog,"conflicts",conflicts);catalog.ValidateOrThrow();
            var interfaceAsset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/V3/UI/V3Interface.prefab");
            if(!interfaceAsset)throw new InvalidOperationException("V3Interface prefab is missing.");
            var ui=(GameObject)PrefabUtility.InstantiatePrefab(interfaceAsset);
            var board=ui.GetComponent<CognitionBoard>();Field(board,"catalog",catalog);
            var panel=ui.GetComponent<GameplayPanel>();var library=ui.GetComponent<WordLibrary>();
            var presentation=ui.GetComponent<V3Presentation>();
            var pg=GameObject.Find("Player");var basic=pg.GetComponent<PuzzleApple.General.BasicFirstPersonController>();if(basic)UnityEngine.Object.DestroyImmediate(basic);
            var player=Add<FirstPersonController>(pg);player.view=pg.GetComponentInChildren<Camera>().transform;player.cognition=board;Field(panel,"player",player);
            presentation.view=player.view.GetComponent<Camera>();
            var world=new GameObject("V3 Runtime").AddComponent<V3World>();world.board=board;world.library=library;world.player=player;world.presentation=presentation;
            V3Object Object(GameObject g,ObjectKind kind){var o=Add<V3Object>(g);o.kind=kind;return o;}
            world.originalApple=Object(GameObject.Find("AppleLowPoly"),ObjectKind.Apple);var rb=Add<Rigidbody>(world.originalApple.gameObject);rb.mass=1;rb.isKinematic=false;rb.useGravity=true;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;rb.interpolation=RigidbodyInterpolation.Interpolate;rb.constraints=RigidbodyConstraints.FreezeRotation;
            world.originalMirror=Object(GameObject.Find("Mirror"),ObjectKind.Mirror);var mirrorRenderer=world.originalMirror.GetComponentsInChildren<MeshFilter>().First();var collider=Add<BoxCollider>(mirrorRenderer.gameObject);collider.center=mirrorRenderer.sharedMesh.bounds.center;collider.size=mirrorRenderer.sharedMesh.bounds.size;
            var door=GameObject.Find("Corridor gate - V3/Corridor door");Object(door,ObjectKind.Door);world.doorLeaves=door.GetComponentsInChildren<MeshFilter>().OrderByDescending(m=>m.GetComponent<Renderer>().bounds.center.z).Select(m=>m.transform).ToArray();
            var indicator=GameObject.Find("Corridor gate - V3/Door indicator");Object(indicator,ObjectKind.Indicator);world.indicator=indicator.GetComponentInChildren<Renderer>();
            var balance=GameObject.Find("Balance - V3");Object(balance,ObjectKind.Balance);PrepareBalance(balance,world);
            var references=new GameObject("V3 world references");
            Transform Reference(string name,Vector3 position){var g=new GameObject(name);g.transform.SetParent(references.transform);g.transform.position=position;g.transform.rotation=Quaternion.Euler(0,90,0);return g.transform;}
            world.appleReference=Reference("Apple mirror reference",new Vector3(-8,1.7f,0));world.mirrorReference=Reference("Corridor mirror reference",new Vector3(6,2,0));
            PreparePassage(world);
            if(!UnityEngine.Object.FindFirstObjectByType<EventSystem>()){var es=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();}
            EditorUtility.SetDirty(world);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            Debug.Log("V3 setup complete: 8 words, 15 rules, isolated world and UI.");
        }
        static Mesh Part(Mesh source,int[] triangles,string name)
        {
            var map=new Dictionary<int,int>();var old=new List<int>();int[] mapped=triangles.Select(i=>{if(!map.TryGetValue(i,out int n)){n=map.Count;map.Add(i,n);old.Add(i);}return n;}).ToArray();
            var mesh=new Mesh{name=name};mesh.vertices=old.Select(i=>source.vertices[i]).ToArray();mesh.normals=old.Select(i=>source.normals[i]).ToArray();if(source.uv.Length>0)mesh.uv=old.Select(i=>source.uv[i]).ToArray();mesh.triangles=mapped;mesh.RecalculateBounds();
            string path="Assets/ArtAssets-3D/V3/Generated/"+name+".asset";AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static void PrepareBalance(GameObject balance,V3World world)
        {
            if(PrefabUtility.IsPartOfPrefabInstance(balance))PrefabUtility.UnpackPrefabInstance(balance,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            var mixed=balance.GetComponentsInChildren<MeshFilter>().First(m=>m.name.StartsWith("Object_7"));var source=mixed.sharedMesh;var triangles=source.triangles;var vertices=source.vertices;var sets=new[]{new List<int>(),new List<int>(),new List<int>()};
            for(int t=0;t<triangles.Length;t+=3){var p=mixed.transform.TransformPoint((vertices[triangles[t]]+vertices[triangles[t+1]]+vertices[triangles[t+2]])/3);int part=p.y>1.54f&&p.y<1.83f&&Mathf.Abs(p.z)>.36f?(p.z>0?1:2):0;sets[part].AddRange(new[]{triangles[t],triangles[t+1],triangles[t+2]});}
            if(sets[1].Count==0||sets[2].Count==0)throw new Exception("Could not isolate authored balance bowls.");
            var white=mixed.GetComponent<Renderer>().sharedMaterial;
            for(int part=0;part<3;part++)
            {
                var g=new GameObject(part==0?"Balance stand":"Pan "+part,typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));g.transform.SetParent(balance.transform,false);g.transform.SetPositionAndRotation(mixed.transform.position,mixed.transform.rotation);g.transform.localScale=mixed.transform.localScale;
                var mesh=Part(source,sets[part].ToArray(),"Balance-"+part);g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=white;g.GetComponent<MeshCollider>().sharedMesh=mesh;
            }
            mixed.gameObject.SetActive(false);
            world.trays=new Transform[2];world.trayHangers=new Transform[2];
            for(int i=0;i<2;i++)
            {
                var pan=balance.transform.Find("Pan "+(i+1));var b=pan.GetComponent<Renderer>().bounds;var root=new GameObject("Tray slot "+i).transform;root.SetParent(balance.transform);root.position=new Vector3(b.center.x,b.center.y,b.center.z);pan.SetParent(root,true);
                string[] chainNames=i==0?new[]{"Object_3","Object_4"}:new[]{"Object_5","Object_6"};
                foreach(var n in chainNames){var chain=balance.GetComponentsInChildren<Transform>().First(t=>t.name.StartsWith(n));chain.SetParent(root,true);}
                world.trays[i]=root;world.trayHangers[i]=root;
            }
            var beam=balance.GetComponentsInChildren<Transform>().First(t=>t.name.StartsWith("Object_2"));var pivot=new GameObject("Beam pivot").transform;pivot.SetParent(balance.transform);pivot.position=new Vector3(-8,beam.GetComponent<Renderer>().bounds.center.y,0);beam.SetParent(pivot,true);world.balanceBeam=pivot;
            Physics.SyncTransforms();foreach(var slot in world.trays){if(Physics.Raycast(new Vector3(slot.position.x,2.1f,slot.position.z),Vector3.down,out var hit,.8f)&&hit.collider.transform.IsChildOf(slot)){var children=slot.Cast<Transform>().ToArray();foreach(var c in children)c.SetParent(balance.transform,true);slot.position=hit.point;foreach(var c in children)c.SetParent(slot,true);}}
        }
        static void PreparePassage(V3World world)
        {
            GameObject.Find("Exit door").SetActive(false);GameObject.Find("主房-待改墙").SetActive(false);
            var root=new GameObject("V3 balance passage");var white=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/General/GalleryWall.mat");
            GameObject Box(string name,Vector3 p,Vector3 size){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(root.transform);g.transform.position=p;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=white;return g;}
            Box("Rear wall left",new Vector3(-18,5.98f,-5.15f),new Vector3(.05f,12.04f,7.7f));Box("Rear wall right",new Vector3(-18,5.98f,5.15f),new Vector3(.05f,12.04f,7.7f));Box("Rear wall above passage",new Vector3(-18,7.6f,0),new Vector3(.05f,8.8f,2.6f));
            world.passageSeal=Box("Balance stone seal",new Vector3(-18,1.62f,0),new Vector3(.18f,3.16f,2.6f)).transform;
            Box("Passage floor",new Vector3(-21,-.04f,0),new Vector3(6,.12f,2.6f));Box("Passage ceiling",new Vector3(-21,3.24f,0),new Vector3(6,.1f,2.6f));
            Box("Passage side A",new Vector3(-21,1.6f,-1.35f),new Vector3(6,3.2f,.1f));Box("Passage side B",new Vector3(-21,1.6f,1.35f),new Vector3(6,3.2f,.1f));Box("Passage end",new Vector3(-24,1.6f,0),new Vector3(.1f,3.2f,2.7f));
        }
    }
}
