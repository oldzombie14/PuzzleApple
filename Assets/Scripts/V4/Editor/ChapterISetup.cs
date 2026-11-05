using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using PuzzleApple.V4.Cognition;

namespace PuzzleApple.V4.EditorTools
{
    // One-time import/assembly. The resulting scenes contain editable native objects.
    public static class ChapterISetup
    {
        const string Art = "Assets/ArtAssets-3D/V4/ChapterI/";
        const string Mats = "Assets/Materials/V4/ChapterI/";
        const string Chapter = "Assets/Scenes/V4/ChapterI.unity";
        const string Garden = "Assets/Scenes/V4/ChapterI_Garden.unity";
        [Serializable] class Manifest { public MaterialData[] materials; }
        [Serializable] class MaterialData { public string name, texture; public float[] color; public float metallic, roughness; }
        static Dictionary<string, Material> materials;
        static Material wall, floor, portalMaterial;
        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before assembly.");
            if (File.Exists(Chapter) || File.Exists(Garden)) throw new InvalidOperationException("Chapter scenes already exist; edit them in place.");
            for (int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Unsaved scene: " + SceneManager.GetSceneAt(i).path);
            Directory.CreateDirectory(Mats);
            Directory.CreateDirectory("Temp/ChapterI");
            File.Copy("Assets/Scenes/V4/P4_Transition.unity", "Temp/ChapterI/TransitionBeforeIntegration.unity", true);
            var error = AssetDatabase.MoveAsset("Assets/Scenes/V4/P4_Transition.unity", Chapter);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            CreateMaterials(); AddWord();
            BuildChapter(); BuildGarden();
            var scenes=EditorBuildSettings.scenes.Select(s => new EditorBuildSettingsScene(s.path == "Assets/Scenes/V4/P4_Transition.unity" ? Chapter : s.path,s.enabled)).ToList();
            if(!scenes.Any(s=>s.path==Chapter))scenes.Add(new EditorBuildSettingsScene(Chapter,true));
            scenes.Add(new EditorBuildSettingsScene(Garden,true)); EditorBuildSettings.scenes=scenes.ToArray();
            var intro=EditorSceneManager.OpenScene("Assets/Scenes/V4/P4.unity",OpenSceneMode.Single);
            foreach(var passage in UnityEngine.Object.FindObjectsByType<DoorPassage>(FindObjectsInactive.Include,FindObjectsSortMode.None))passage.destinationScene="ChapterI";
            EditorSceneManager.SaveScene(intro);
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(Chapter,OpenSceneMode.Single);
            return "Created ChapterI and ChapterI_Garden, preserved transition scene GUID, connected Intro and mirror vocabulary.";
        }
        static Material SaveMaterial(string name,Color color)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name=name };
            mat.SetColor("_BaseColor", color);mat.SetFloat("_Smoothness",.2f);
            AssetDatabase.CreateAsset(mat,Mats+name+".mat");return mat;
        }
        static void CreateMaterials()
        {
            materials=new Dictionary<string,Material>();
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Art+"Materials.json"));
            for(int i=0;i<manifest.materials.Length;i++)
            {
                var d=manifest.materials[i];
                var c=new Color(d.color[0],d.color[1],d.color[2],1);
                var mat=SaveMaterial("Blender_"+i.ToString("D2"),c.gamma);
                mat.name=d.name;
                mat.SetFloat("_Metallic",d.metallic);mat.SetFloat("_Smoothness",1-d.roughness);
                mat.SetFloat("_Cull",0); // Thin petals and leaves are visible from either side.
                if(!string.IsNullOrEmpty(d.texture))
                {
                    mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Textures/"+d.texture));
                    mat.SetColor("_BaseColor",Color.white);
                }
                if(d.name=="Architectural_Glass")
                {
                    mat.SetColor("_BaseColor",new Color(.72f,.82f,.88f,.12f));mat.SetFloat("_Surface",1);
                    mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);mat.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
                    mat.SetFloat("_ZWrite",0);mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.renderQueue=3000;
                }
                if(d.name.Contains("PictureFrame")) {mat.SetColor("_BaseColor",new Color(.72f,.58f,.36f));mat.SetFloat("_Metallic",.45f);}
                materials[d.name]=mat;
            }
            wall=SaveMaterial("GalleryWall",Color.white);
            floor=SaveMaterial("GalleryFloor",new Color(.89f,.89f,.89f));
            floor.SetFloat("_Smoothness",.08f);
            var neutral=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/V4/WorldPicture.mat")){name="NeutralPicture"};
            neutral.SetColor("_ShadowTint",Color.white);neutral.SetColor("_LightTint",Color.white);
            neutral.SetFloat("_Saturation",1);neutral.SetFloat("_Strength",0);
            AssetDatabase.CreateAsset(neutral,Mats+"NeutralPicture.mat");
            portalMaterial=new Material(Shader.Find("PuzzleApple/Planar Mirror")){name="PortalSurface"};
            portalMaterial.SetColor("_Tint",Color.white);AssetDatabase.CreateAsset(portalMaterial,Mats+"PortalSurface.mat");
        }
        static void AddWord()
        {
            AssetDatabase.CopyAsset("Assets/Sprites/V3/Mirror.png","Assets/Sprites/V4/Mirror.png");
            var word=ScriptableObject.CreateInstance<WordDefinition>();
            var so=new SerializedObject(word);
            so.FindProperty("id").stringValue="mirror";so.FindProperty("displayText").stringValue="mirror";
            so.FindProperty("symbol").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/V4/Mirror.png");
            so.FindProperty("senses").arraySize=1;
            so.FindProperty("senses").GetArrayElementAtIndex(0).FindPropertyRelative("meaning").stringValue="mirror";
            so.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.CreateAsset(word,"Assets/GameData/V4/Opening/Words/mirror.asset");
            var catalog=AssetDatabase.LoadAssetAtPath<CognitionCatalog>("Assets/GameData/V4/Opening/OpeningCatalog.asset");
            so=new SerializedObject(catalog);var words=so.FindProperty("words");words.InsertArrayElementAtIndex(words.arraySize);words.GetArrayElementAtIndex(words.arraySize-1).objectReferenceValue=word;so.ApplyModifiedPropertiesWithoutUndo();
        }
        static GameObject Node(string name,Transform parent,Vector3 position)
        {var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;return go;}
        static GameObject Box(string name,Transform parent,Vector3 position,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
        static GameObject Import(string name,Transform parent,Vector3 position,float scale=1)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Art+name+".fbx"));
            go.transform.SetParent(parent,false);go.transform.localPosition=position+go.transform.localPosition*scale;go.transform.localScale*=scale;
            foreach(var r in go.GetComponentsInChildren<Renderer>())
                r.sharedMaterials=r.sharedMaterials.Select(m=>m&&materials.TryGetValue(m.name,out var replacement)?replacement:wall).ToArray();
            return go;
        }
        static Transform Find(GameObject go,string name)=>go.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
        static void Environment(Transform parent,Vector3 offset, bool garden)
        {
            RenderSettings.skybox=null;RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.32f,.32f,.32f);RenderSettings.reflectionIntensity=.3f;
            var sun=Node("Soft directional fill",parent,offset+Vector3.up*10).AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=0;sun.transform.rotation=Quaternion.Euler(65,25,0);sun.shadows=LightShadows.Hard;
            // Local point lights allow both independently placed scenes to remain loaded.
            var positions=garden?new[]{new Vector3(-4,7,-3),new Vector3(4,7,3),new Vector3(0,6,0)}:new[]{new Vector3(29,9,0),new Vector3(14,9,0),new Vector3(0,9,0),new Vector3(-12,7,0)};
            foreach(var p in positions)
            {
                var light=Node("Ceiling light",parent,offset+p).AddComponent<Light>();light.type=LightType.Point;light.range=garden?15:17;light.intensity=garden?12:18;
                light.color=Color.white;light.shadows=LightShadows.Soft;light.shadowStrength=.9f;
            }
        }
        static SceneEntry Entry(string name,Vector3 spawn,Quaternion rotation,out GameObject content)
        {
            var entry=new GameObject(name+" entry").AddComponent<SceneEntry>();content=new GameObject(name+" content");
            entry.eyeHeight=2;entry.content=content;entry.arrival=Node("Arrival",entry.transform,spawn).transform;entry.arrival.rotation=rotation;
            entry.pictureMaterialOverride=AssetDatabase.LoadAssetAtPath<Material>(Mats+"NeutralPicture.mat");
            content.AddComponent<OpeningFlow>().explorationOnly=true;
            return entry;
        }
        static ScenePortal Portal(Transform parent,Vector3 center,float yaw,string destination)
        {
            var go=Node("Living frame to "+destination,parent,center);go.transform.localRotation=Quaternion.Euler(0,yaw,0);
            var portal=go.AddComponent<ScenePortal>();portal.destinationScene=destination;portal.aperture=new Vector2(5.73f,8.14f);
            var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.name="Live view";quad.transform.SetParent(go.transform,false);quad.transform.localRotation=Quaternion.Euler(0,180,0);quad.transform.localScale=new Vector3(5.73f,8.14f,1);
            UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
            portal.surface=quad.GetComponent<Renderer>();portal.surface.sharedMaterial=portalMaterial;
            var barrier=go.AddComponent<BoxCollider>();barrier.size=new Vector3(3,3.4f,.12f);portal.loadingBarrier=barrier;
            return portal;
        }
        static void BuildChapter()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            Entry("ChapterI",new Vector3(29,1.12f,0),Quaternion.Euler(0,-90,0),out var content);
            var corridor=Import("Corridor",content.transform,Vector3.zero,1.5f);
            // Replace the combined wall mesh with native walls, including the missing end and a real frame aperture.
            Find(corridor,"Cube.001").gameObject.SetActive(false);
            foreach(var name in new[]{"Cube","Cube.002","Corridor_Ceiling"})
            {var t=Find(corridor,name);t.gameObject.AddComponent<MeshCollider>();t.GetComponent<Renderer>().sharedMaterial=name=="Cube"?floor:wall;}
            Box("Missing end wall",content.transform,new Vector3(-18.075f,6.4f,0),new Vector3(.15f,12.8f,15),wall);
            Box("Right gallery wall",content.transform,new Vector3(9,6.4f,7.575f),new Vector3(54,12.8f,.15f),wall);
            float center=20, left=center-2.865f,right=center+2.865f;
            Box("Left wall before frame",content.transform,new Vector3((-18+left)/2,6.4f,-7.575f),new Vector3(left+18,12.8f,.15f),wall);
            Box("Left wall after frame",content.transform,new Vector3((36+right)/2,6.4f,-7.575f),new Vector3(36-right,12.8f,.15f),wall);
            Box("Left frame lintel",content.transform,new Vector3(center,(12.8f+8.63f)/2,-7.575f),new Vector3(5.73f,12.8f-8.63f,.15f),wall);
            var frame=Find(corridor,"frame.001");
            frame.SetParent(content.transform,true);
            // Match the right frame; the garden opening follows this frame.
            var renderer=frame.GetComponent<Renderer>();
            frame.localScale=Find(corridor,"frame").localScale;
            frame.position+=new Vector3(center,4.56f,-7.5f)-renderer.bounds.center;
            frame.gameObject.AddComponent<MeshCollider>();
            Find(corridor,"frame.002").gameObject.SetActive(false);
            Find(corridor,"frame").gameObject.AddComponent<MeshCollider>();
            var rightFrame=Find(corridor,"frame");rightFrame.position+=Vector3.right*(center-rightFrame.GetComponent<Renderer>().bounds.center.x);
            // Preserve the other painting as an ordinary display.
            var painting=GameObject.CreatePrimitive(PrimitiveType.Quad);painting.name="Right painting";painting.transform.SetParent(content.transform,false);
            painting.transform.position=new Vector3(center,4.56f,7.39f);painting.transform.localScale=new Vector3(5.73f,8.14f,1);painting.GetComponent<Renderer>().sharedMaterial=materials["TmpPaint_Canvas"];
            Portal(content.transform,new Vector3(center,4.56f,-7.48f),0,"ChapterI_Garden");
            var glass=GameObject.CreatePrimitive(PrimitiveType.Quad);glass.name="Mirror reflection and vocabulary";glass.transform.SetParent(content.transform,false);
            glass.GetComponent<Renderer>().sharedMaterial=portalMaterial;glass.AddComponent<ChapterVocabulary>();
            var reflection=glass.AddComponent<PlanarMirror>();var so=new SerializedObject(reflection);
            so.FindProperty("localSurfacePoint").vector3Value=Vector3.zero;so.FindProperty("localSurfaceNormal").vector3Value=Vector3.back;so.FindProperty("maxReflectionDepth").intValue=1;so.ApplyModifiedPropertiesWithoutUndo();
            var display=Find(corridor,"Showcase_Wood_Plinth");display.gameObject.AddComponent<BoxCollider>();
            foreach(var t in corridor.GetComponentsInChildren<Transform>())
                if(t.name.StartsWith("Showcase_")||t.name.StartsWith("Balance_")||t.name.StartsWith("Feather_"))t.position+=new Vector3(-47.4f,0,0);
            var bounds=display.GetComponent<Renderer>().bounds;
            var pivot=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            float displayScale=1f/bounds.size.y;
            foreach(var t in corridor.GetComponentsInChildren<Transform>())
                if(t.name.StartsWith("Showcase_")||t.name.StartsWith("Balance_")||t.name.StartsWith("Feather_"))
                {t.position=pivot+(t.position-pivot)*displayScale;t.localScale*=displayScale;}
            Fold(content.transform);
            Environment(content.transform,Vector3.zero,false);
            var key=Node("Unity showcase light",content.transform,new Vector3(-13.8f,4.5f,-1.4f)).AddComponent<Light>();
            key.type=LightType.Spot;key.intensity=45;key.range=9;key.spotAngle=42;key.innerSpotAngle=24;key.shadows=LightShadows.Soft;
            key.transform.LookAt(new Vector3(-15,1.4f,0));
            PlaceArrivalMirror();
            EditorSceneManager.SaveScene(scene,Chapter);
        }
        public static string PlaceArrivalMirror()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Edit mode required.");
            var entry=UnityEngine.Object.FindObjectsByType<SceneEntry>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(e=>e.content.name=="ChapterI content");
            var glass=entry.content.GetComponentInChildren<ChapterVocabulary>(true).gameObject;
            var arrival=entry.arrival;
            Physics.SyncTransforms();
            var hits=Physics.RaycastAll(arrival.position+Vector3.up*.5f,-arrival.forward,20,~0,QueryTriggerInteraction.Ignore)
                .Where(h=>!h.collider.GetComponentInParent<ChapterVocabulary>() && !h.collider.GetComponentInParent<PlanarMirror>()).OrderBy(h=>h.distance).ToArray();
            if(hits.Length==0)throw new InvalidOperationException("No rear wall behind arrival.");
            var wallPoint=hits[0].point;
            float ground=Physics.Raycast(arrival.position,Vector3.down,out var floorHit,5)?floorHit.point.y:0;
            var old=entry.content.transform.Find("Mirror");
            if(old && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(old.gameObject)==Art+"Mirror.fbx")UnityEngine.Object.DestroyImmediate(old.gameObject);
            var mount=entry.content.transform.Find("Arrival mirror");
            if(!mount)mount=Node("Arrival mirror",entry.content.transform,Vector3.zero).transform;
            mount.position=new Vector3(wallPoint.x,ground+1.8f,wallPoint.z)+arrival.forward*.12f;
            mount.rotation=Quaternion.LookRotation(-arrival.forward,Vector3.up);
            glass.transform.SetParent(mount,false);glass.transform.localPosition=Vector3.zero;glass.transform.localRotation=Quaternion.identity;glass.transform.localScale=new Vector3(1.9f,2.9f,1);
            var frameMat=AssetDatabase.LoadAssetAtPath<Material>(Mats+"ArrivalMirrorFrame.mat");
            if(!frameMat)frameMat=SaveMaterial("ArrivalMirrorFrame",new Color(.15f,.16f,.16f));
            if(!mount.Find("Frame left"))
            {
                Box("Frame left",mount,new Vector3(-1.01f,0,0),new Vector3(.12f,3.14f,.12f),frameMat);
                Box("Frame right",mount,new Vector3(1.01f,0,0),new Vector3(.12f,3.14f,.12f),frameMat);
                Box("Frame top",mount,new Vector3(0,1.51f,0),new Vector3(1.9f,.12f,.12f),frameMat);
                Box("Frame bottom",mount,new Vector3(0,-1.51f,0),new Vector3(1.9f,.12f,.12f),frameMat);
            }
            EditorSceneManager.MarkSceneDirty(entry.gameObject.scene);
            return "Mirror placed from arrival backward ray against rear wall: "+mount.position;
        }
        static void Fold(Transform parent)
        {
            var root=Node("Unfolding arrival box",parent,new Vector3(0,-.25f,0));
            var fold=root.AddComponent<FoldingRoom>();var panels=new List<FoldingRoom.Panel>();var colliders=new List<Collider>();
            var front=Node("Front hinge",root.transform,new Vector3(23.8f,.075f,0));
            colliders.Add(Box("Front panel",front.transform,new Vector3(0,6,0),new Vector3(.12f,12,12),wall).GetComponent<Collider>());
            panels.Add(new FoldingRoom.Panel{hinge=front.transform,openEuler=new Vector3(0,0,90)});
            foreach(int side in new[]{-1,1})
            {
                var hinge=Node("Side hinge "+side,root.transform,new Vector3(29.8f,.075f,side*6));
                colliders.Add(Box("Side panel",hinge.transform,new Vector3(0,6,0),new Vector3(12,12,.12f),wall).GetComponent<Collider>());
                panels.Add(new FoldingRoom.Panel{hinge=hinge.transform,openEuler=new Vector3(side*90,0,0)});
            }
            var roof=Node("Roof hinge",front.transform,new Vector3(0,12,0));
            colliders.Add(Box("Roof panel",roof.transform,new Vector3(6,0,0),new Vector3(12,.12f,12),wall).GetComponent<Collider>());
            panels.Add(new FoldingRoom.Panel{hinge=roof.transform,openEuler=new Vector3(0,0,90)});
            fold.panels=panels.ToArray();fold.movingColliders=colliders.ToArray();
        }
        static void BuildGarden()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var offset=new Vector3(1000,0,0);
            Entry("Garden",offset+new Vector3(0,1.05f,6.8f),Quaternion.Euler(0,180,0),out var content);
            var garden=Import("FloatingGarden",content.transform,offset);
            var flowers=new List<Transform>();
            foreach(var r in garden.GetComponentsInChildren<Renderer>())
            {
                if(r.name.StartsWith("Floor")||r.name.StartsWith("Wall")||r.name.StartsWith("Ceiling")||r.name.StartsWith("Entry"))
                {r.gameObject.AddComponent<MeshCollider>();r.sharedMaterial=r.name.StartsWith("Floor")?floor:wall;}
                else flowers.Add(r.transform);
            }
            var drift=garden.AddComponent<FloatingFlowers>();drift.flowers=flowers.ToArray();
            var lintel=Find(garden,"Entry • Lintel");var lintelRenderer=lintel.GetComponent<Renderer>();
            var openingScale=lintel.localScale;openingScale.x*=5.73f/lintelRenderer.bounds.size.x;lintel.localScale=openingScale;
            foreach(var sideName in new[]{"Entry • Left","Entry • Right"}){var side=Find(garden,sideName);var sr=side.GetComponent<Renderer>();var ss=side.localScale;ss.x*=5.135f/sr.bounds.size.x;side.localScale=ss;side.position+=offset+new Vector3((sideName.EndsWith("Left")?1:-1)*5.4325f,5,8.15f)-sr.bounds.center;}
            var lintelScale=lintel.localScale;lintelScale.z*=(10-8.555f)/lintelRenderer.bounds.size.y;lintel.localScale=lintelScale;
            lintel.position+=offset+new Vector3(0,(10+8.555f)/2,8.15f)-lintelRenderer.bounds.center;
            Box("Entry threshold",content.transform,offset+new Vector3(0,.2075f,8.15f),new Vector3(5.73f,.415f,.3f),wall);
            Portal(content.transform,offset+new Vector3(0,4.485f,8.00f),180,"ChapterI");
            Environment(content.transform,offset,true);
            EditorSceneManager.SaveScene(scene,Garden);
        }
    }
}
