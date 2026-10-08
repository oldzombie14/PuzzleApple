using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PuzzleApple.V3.Cognition;
using Object=UnityEngine.Object;

namespace PuzzleApple.V3.Editor
{
    public static class AppleCoreSetup
    {
        const string Model="Assets/ArtAssets-3D/V3/AppleCore/AppleCore.fbx";
        static Material Material(string name,Color color,float smoothness)
        {
            string path="Assets/Materials/V3/Opening/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smoothness);EditorUtility.SetDirty(m);return m;
        }
        public static string Apply()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Edit mode only.");
            var room=Object.FindFirstObjectByType<OpeningRoom>();
            if(!room||room.gameObject.scene.path!="Assets/Scenes/V3/P3.unity")throw new InvalidOperationException("Open P3 first.");
            var skin=Material("AppleCoreSkin",new Color(.65f,.1f,.12f),.32f);
            var original=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/General/Apple.mat");
            if(original){skin.CopyPropertiesFromMaterial(original);EditorUtility.SetDirty(skin);}
            // The original is an atlas, not a repeating texture. Sample only a red
            // skin patch; its authored normal map does not match this new topology.
            skin.SetTextureScale("_BaseMap",new Vector2(.3f,.055f));skin.SetTextureOffset("_BaseMap",new Vector2(.1f,.82f));
            skin.SetTexture("_BumpMap",null);skin.SetTexture("_MetallicGlossMap",null);
            skin.DisableKeyword("_NORMALMAP");skin.DisableKeyword("_METALLICSPECGLOSSMAP");skin.SetFloat("_Smoothness",.3f);skin.SetFloat("_Metallic",0);
            var flesh=Material("AppleCoreFlesh",new Color(.89f,.83f,.64f),.17f);
            var stem=Material("AppleCoreStem",new Color(.23f,.11f,.045f),.1f);
            var prefab=PrefabUtility.LoadPrefabContents(OpeningSetup.PrefabPath);
            try{Configure(prefab.GetComponent<OpeningRoom>(),skin,flesh,stem);PrefabUtility.SaveAsPrefabAsset(prefab,OpeningSetup.PrefabPath);}
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
            Configure(room,skin,flesh,stem);
            HiddenRouteSetup.Rule(room.board.Catalog,"negative apple",CognitionSignal.AppleNegative,"state:apple");
            room.board.Catalog.ValidateOrThrow();AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(room.gameObject.scene);
            return "Apple core configured on both physical apples; exit corridor removed; gameplay cameras clear black.";
        }
        static void Configure(OpeningRoom room,Material skin,Material flesh,Material stem)
        {
            foreach(var apple in room.objects.Where(o=>o.wordId=="apple"))
            {
                if(apple.GetComponent<AppleState>())continue;
                var bounds=apple.Bounds;
                var state=apple.gameObject.AddComponent<AppleState>();
                state.wholeSurfaces=apple.GetComponentsInChildren<Renderer>();state.wholeColliders=apple.GetComponentsInChildren<Collider>();
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model),apple.transform);
                model.name="Eaten apple core";
                model.transform.rotation=Quaternion.identity;
                model.transform.localScale=Vector3.one/apple.transform.lossyScale.x;
                model.transform.position=Vector3.zero;
                var rs=model.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
                model.transform.position=new Vector3(bounds.center.x-b.center.x,bounds.min.y-b.min.y,bounds.center.z-b.center.z);
                var ambient=state.wholeSurfaces.Select(r=>r.GetComponent<LocalAmbientProbe>()).FirstOrDefault(a=>a);
                foreach(var r in rs)
                {
                    r.sharedMaterials=r.sharedMaterials.Select(m=>m.name.Contains("Skin")?skin:m.name.Contains("Stem")?stem:flesh).ToArray();
                    if(ambient){var a=r.gameObject.AddComponent<LocalAmbientProbe>();a.roomVolume=ambient.roomVolume;a.ambient=ambient.ambient;a.transition=ambient.transition;a.followRoomLighting=ambient.followRoomLighting;}
                }
                // A compound collider preserves the narrow edible middle. A single convex
                // hull around the whole core would fill its concavity like an intact apple.
                var middle=new GameObject("Core collision");middle.transform.SetParent(model.transform,false);
                var capsule=middle.AddComponent<CapsuleCollider>();capsule.radius=.088f;capsule.height=.43f;capsule.center=new Vector3(0,.273f,0);
                Cap(model.transform,"Upper skin collision",new Vector3(0,.475f,0),new Vector3(.416f,.115f,.416f));
                Cap(model.transform,"Lower skin collision",new Vector3(0,.065f,0),new Vector3(.368f,.12f,.368f));
                state.core=model;state.SetCore(false);EditorUtility.SetDirty(apple);
            }
            var passage=room.mainRoute.passageSeal.parent;
            foreach(var name in new[]{"Passage floor","Passage ceiling","Passage side A","Passage side B","Passage end"})
            {var part=passage.Find(name);if(part)Object.DestroyImmediate(part.gameObject);}
            var exit=room.mainRoute.exitVolume;exit.transform.localPosition=new Vector3(-18.6f,1,0);exit.size=new Vector3(1.5f,4,2.4f);
            foreach(var camera in new[]{room.presentation.view,room.mirrorView.reflectedCamera})
                if(camera){camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;EditorUtility.SetDirty(camera);}
            EditorUtility.SetDirty(room.mainRoute);EditorUtility.SetDirty(room);
        }
        static void Cap(Transform parent,string name,Vector3 position,Vector3 scale)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Sphere);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=position;g.transform.localScale=scale;
            var mesh=g.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(g.GetComponent<Collider>());Object.DestroyImmediate(g.GetComponent<MeshRenderer>());Object.DestroyImmediate(g.GetComponent<MeshFilter>());
            var collider=g.AddComponent<MeshCollider>();collider.sharedMesh=mesh;collider.convex=true;
        }
        public static string Preview()
        {
            var room=Object.FindFirstObjectByType<OpeningRoom>();var apple=room.mainRoute.apple;
            var state=apple.GetComponent<AppleState>();
            var scene=EditorSceneManager.NewPreviewScene();RenderTexture rt=null;Texture2D image=null;
            var previous=RenderTexture.active;
            try
            {
                int layer=LayerMask.NameToLayer("V3 Snapshot");
                for(int side=0;side<2;side++)
                {
                    var sources=side==0?state.wholeSurfaces:state.core.GetComponentsInChildren<Renderer>(true);
                    foreach(var source in sources)
                    {
                        var filter=source.GetComponent<MeshFilter>();if(!filter)continue;
                        var g=new GameObject(source.name){layer=layer};UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(g,scene);
                        g.transform.SetPositionAndRotation(source.transform.position-apple.transform.position+Vector3.right*(side==0?-.38f:.38f),source.transform.rotation);
                        g.transform.localScale=source.transform.lossyScale;g.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
                        var r=g.AddComponent<MeshRenderer>();r.sharedMaterials=source.sharedMaterials;
                        r.lightProbeUsage=UnityEngine.Rendering.LightProbeUsage.CustomProvided;r.reflectionProbeUsage=UnityEngine.Rendering.ReflectionProbeUsage.Off;
                        var sh=new UnityEngine.Rendering.SphericalHarmonicsL2();sh.AddAmbientLight(new Color(.23f,.23f,.23f));
                        var block=new MaterialPropertyBlock();block.CopySHCoefficientArraysFrom(new[]{sh});r.SetPropertyBlock(block);
                    }
                }
                var node=new GameObject("Preview camera");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(node,scene);
                var camera=node.AddComponent<Camera>();camera.enabled=false;camera.scene=scene;camera.cullingMask=1<<layer;
                camera.orthographic=true;camera.orthographicSize=.55f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.105f,.115f,.13f);
                camera.transform.position=new Vector3(.15f,.78f,-2);camera.transform.LookAt(new Vector3(0,.30f,0));
                var data=node.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();data.renderPostProcessing=false;
                for(int i=0;i<2;i++)
                {
                    var g=new GameObject("Preview light");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(g,scene);
                    var light=g.AddComponent<Light>();light.type=LightType.Directional;light.intensity=i==0?1.1f:.45f;light.cullingMask=1<<layer;
                    g.transform.rotation=Quaternion.Euler(i==0?40:15,i==0?-30:130,0);
                }
                rt=RenderTexture.GetTemporary(1000,650,24);camera.targetTexture=rt;
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                RenderTexture.active=rt;image=new Texture2D(1000,650,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1000,650),0,0);image.Apply();
                string path=System.IO.Path.GetFullPath("Temp/AppleCore-Unity.png");System.IO.File.WriteAllBytes(path,image.EncodeToPNG());return path;
            }
            finally{RenderTexture.active=previous;if(image)Object.DestroyImmediate(image);if(rt)RenderTexture.ReleaseTemporary(rt);EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
