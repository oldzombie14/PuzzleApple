using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PuzzleApple.Editor
{
    public static class SampleSceneSetup
    {



        [MenuItem("PuzzleApple/Configure Sample Scene")]
        public static void Configure()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/SampleScene.unity") throw new System.InvalidOperationException("Open SampleScene first.");
            var root = GameObject.Find("PuzzleApple-场景-0");
            if (!root) throw new System.InvalidOperationException("Scene model not found.");

            System.IO.Directory.CreateDirectory("Assets/Materials");
            AssetDatabase.Refresh();
            var wall = Material("FlatWhite", "PuzzleApple/Two Tone", Color.white, new Color(.88f,.88f,.88f));
            var floor = Material("Floor", "PuzzleApple/Two Tone", new Color(.68f,.105f,.035f), new Color(.36f,.045f,.018f));
            var ink = Material("Outline", "PuzzleApple/Screen Outline", Color.black, Color.black);
            var sourceRoot = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ArtAssets-3D/PuzzleApple-场景-0.fbx");
            var filters = root.GetComponentsInChildren<MeshFilter>();
            foreach (var f in filters)
            {
                var source = sourceRoot.GetComponentsInChildren<MeshFilter>().FirstOrDefault(m => m.name == f.name);
                if (!source) continue;
                f.sharedMesh = source.sharedMesh;
                var mc = f.GetComponent<MeshCollider>();
                if (!mc) mc = Undo.AddComponent<MeshCollider>(f.gameObject);
                mc.sharedMesh = f.sharedMesh;
                mc.convex = false;
                f.GetComponent<MeshRenderer>().sharedMaterials = Enumerable.Repeat(f.name == "红地毯" ? floor : wall, f.sharedMesh.subMeshCount).ToArray();
                // Lift the carpet only enough to clear the underlying floor, without accumulating offsets.
                if (f.name == "红地毯")
                {
                    var carpetBounds = f.GetComponent<Renderer>().bounds;
                    float top = root.GetComponentsInChildren<Renderer>().Where(r => r.name.Contains("顶底")).Select(r => r.bounds.min.y + .05f).Max();
                    if (carpetBounds.max.y < top + .005f)
                        f.transform.position += Vector3.up * (top + .005f - carpetBounds.max.y);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(f.transform);
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(mc);
                PrefabUtility.RecordPrefabInstancePropertyModifications(f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(f.GetComponent<MeshRenderer>());
            }
            ConfigureOutline(ink);
            var player=GameObject.Find("Player");
            if (!player) { player=new GameObject("Player"); Undo.RegisterCreatedObjectUndo(player,"Create player"); }
            player.transform.SetPositionAndRotation(new Vector3(3.05f,.08f,0),Quaternion.Euler(0,-90,0));
            var controller=player.GetComponent<CharacterController>();
            if (!controller) controller=player.AddComponent<CharacterController>();
            controller.height=1.8f; controller.radius=.28f; controller.center=new Vector3(0,.9f,0);
            controller.skinWidth=.025f; controller.stepOffset=.22f; controller.minMoveDistance=0;
            var camera=Camera.main;
            camera.transform.SetParent(player.transform,false);
            camera.transform.localPosition=new Vector3(0,1.65f,0); camera.transform.localRotation=Quaternion.identity;
            camera.nearClipPlane=.05f; camera.farClipPlane=100; camera.fieldOfView=70;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            if (!player.GetComponent<FirstPersonController>()) player.AddComponent<FirstPersonController>();
            var light=GameObject.Find("Directional Light").GetComponent<Light>();
            light.shadows=LightShadows.Hard;
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        static Material Material(string name,string shader,Color lit,Color shadow)
        {
            string path="Assets/Materials/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m) return m;
            m=new Material(Shader.Find(shader)); m.name=name;
            if(m.HasProperty("_LitColor")) { m.SetColor("_LitColor",lit); m.SetColor("_ShadowColor",shadow); }
            AssetDatabase.CreateAsset(m,path); return m;
        }
        static void ConfigureOutline(Material ink)
        {
            ink.shader=Shader.Find("PuzzleApple/Screen Outline");
            foreach(var path in new[]{"Assets/Settings/PC_Renderer.asset","Assets/Settings/Mobile_Renderer.asset"})
            {
                var data=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                var feature=data.rendererFeatures.OfType<FullScreenPassRendererFeature>().FirstOrDefault(f=>f.name=="PuzzleApple Outline");
                if(!feature)
                {
                    feature=ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                    feature.name="PuzzleApple Outline";
                    AssetDatabase.AddObjectToAsset(feature,data);
                    data.rendererFeatures.Add(feature);
                }
                feature.passMaterial=ink;
                feature.injectionPoint=FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
                feature.requirements=ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal;
                feature.fetchColorBuffer=true;
                feature.Create();
                EditorUtility.SetDirty(feature);
                data.SetDirty();
                EditorUtility.SetDirty(data);
            }
        }
    }
}

