using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

namespace PuzzleApple.V4.Editor
{
    public static class ThresholdSetup
    {
        const string Folder = "Assets/ArtAssets-3D/V4/Threshold";
        static Material Lit(string name, Color color, float emission=0)
        {
            string path="Assets/Materials/V4/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m) { m=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,path); }
            m.SetColor("_BaseColor",color); m.SetFloat("_Smoothness",.15f);
            m.SetFloat("_EnvironmentReflections",0); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            if(emission>0) { m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive; m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*emission); }
            return m;
        }
        static GameObject Cube(Transform parent,string name,Vector3 position,Vector3 scale,Material mat)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);
            g.transform.localPosition=position;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=mat;return g;
        }
        static Mesh Shape(string name,Vector3[] vertices,int[] indices)
        {
            string path=Folder+"/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh)return mesh;
            var points=new List<Vector3>();var tris=new List<int>();
            foreach(int i in indices){tris.Add(points.Count);points.Add(vertices[i]);}
            mesh=new Mesh {name=name};mesh.SetVertices(points);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static Transform Part(Transform parent,string name,Mesh mesh,Material mat,Vector3 position)
        {
            var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(parent,false);g.transform.localPosition=position;
            g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=mat;return g.transform;
        }
        public static void Apply()
        {
            if(GameObject.Find("Fish Threshold"))throw new System.InvalidOperationException("Threshold already exists; edit it in place.");
            System.IO.Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var stone=Lit("ThresholdStone",new Color(.18f,.19f,.19f));
            var edge=Lit("ThresholdEdge",new Color(.34f,.36f,.35f));
            var silver=Lit("ThresholdSilver",new Color(.72f,.72f,.72f),.18f);


            var black=Lit("ThresholdEyes",new Color(.008f,.009f,.008f));
            var root=new GameObject("Fish Threshold");root.transform.position=new Vector3(12,3.5f,18);
            Cube(root.transform,"Left pier",new Vector3(-2.15f,3,0),new Vector3(.55f,6,1.15f),stone);
            Cube(root.transform,"Right pier",new Vector3(2.15f,3,0),new Vector3(.55f,6,1.15f),stone);
            Cube(root.transform,"Lintel",new Vector3(0,6,0),new Vector3(4.85f,.45f,1.15f),stone);

            Cube(root.transform,"Left inner edge",new Vector3(-1.86f,3,-.48f),new Vector3(.06f,5.7f,.08f),edge);
            Cube(root.transform,"Right inner edge",new Vector3(1.86f,3,-.48f),new Vector3(.06f,5.7f,.08f),edge);
            var body=Shape("Fish body",new[]{new Vector3(0,0,.48f),new Vector3(0,.17f,0),new Vector3(.115f,0,0),new Vector3(0,-.13f,0),new Vector3(-.115f,0,0),new Vector3(0,0,-.38f)},new[]{0,2,1,0,3,2,0,4,3,0,1,4,5,1,2,5,2,3,5,3,4,5,4,1});
            var fin=Shape("Fish tail",new[]{Vector3.zero,new Vector3(0,.24f,-.28f),new Vector3(0,0,-.18f),new Vector3(0,-.24f,-.28f)},new[]{0,1,2,0,2,3,2,1,0,3,2,0});
            var dorsal=Shape("Fish fin",new[]{new Vector3(0,.10f,.12f),new Vector3(0,.29f,-.12f),new Vector3(0,.07f,-.25f)},new[]{0,1,2,2,1,0});
            for(int i=0;i<13;i++)
            {
                var fish=new GameObject("Threshold fish "+(i+1));fish.transform.SetParent(root.transform,false);
                var mat=silver;
                Part(fish.transform,"Body",body,mat,Vector3.zero);Part(fish.transform,"Dorsal fin",dorsal,mat,Vector3.zero);
                var tail=Part(fish.transform,"Tail",fin,mat,new Vector3(0,0,-.36f));
                for(int side=-1;side<=1;side+=2)
                {
                    var eye=GameObject.CreatePrimitive(PrimitiveType.Sphere);eye.name="Eye";Object.DestroyImmediate(eye.GetComponent<Collider>());eye.transform.SetParent(fish.transform,false);
                    eye.transform.localPosition=new Vector3(side*.067f,.035f,.22f);eye.transform.localScale=Vector3.one*.037f;eye.GetComponent<Renderer>().sharedMaterial=black;
                }
                float p=i*2.39996f;var swim=fish.AddComponent<ThresholdFish>();swim.tail=tail;swim.phase=p;
                swim.center=new Vector3(0,1.0f+i*.32f,0);swim.radii=new Vector3(.9f+(i%3)*.23f,.2f+(i%4)*.06f,.45f+(i%3)*.16f);
                swim.orbitSpeed=.22f+(i%5)*.025f;swim.tailSpeed=4.2f+(i%4)*.6f;
                fish.transform.localScale=Vector3.one*(.56f+(i%4)*.10f);
                fish.transform.localPosition=swim.center+new Vector3(Mathf.Sin(p)*swim.radii.x,Mathf.Sin(p*3)*swim.radii.y,Mathf.Cos(p)*swim.radii.z);
                fish.transform.localRotation=Quaternion.LookRotation(new Vector3(Mathf.Cos(p),.1f,-Mathf.Sin(p)));
            }
            var lightGO=new GameObject("Threshold skylight",typeof(Light));lightGO.transform.SetParent(root.transform,false);lightGO.transform.localPosition=new Vector3(0,5.6f,-.1f);lightGO.transform.localRotation=Quaternion.Euler(87,0,0);
            var light=lightGO.GetComponent<Light>();light.type=LightType.Spot;light.color=Color.white;light.intensity=18;light.range=9;light.spotAngle=49;light.innerSpotAngle=25;light.shadows=LightShadows.Soft;
            root.AddComponent<ThresholdDoor>();
            ConfigureVisuals(root);
            var target=root.AddComponent<BoxCollider>();target.isTrigger=true;target.center=new Vector3(0,3,0);target.size=new Vector3(4.85f,6.4f,1.15f);
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/V4/FishThreshold.prefab");
            EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);AssetDatabase.SaveAssets();Selection.activeGameObject=root;
        }
        public static void ConfigureVisuals(GameObject root)
        {
            var stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/V4/ThresholdStone.mat");
            if (!root.transform.Find("Sill"))
                Cube(root.transform, "Sill", new Vector3(0,.11f,0), new Vector3(4.85f,.22f,1.15f), stone);
            var fish = root.GetComponentsInChildren<ThresholdFish>(true);
            for (int i=0; i<fish.Length; i++)
                fish[i].transform.localScale = Vector3.one * Mathf.Lerp(.44f, 1.02f, Mathf.Repeat(i*.618034f+.17f, 1));
            const string path = "Assets/Materials/V4/DoorRipple.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("PuzzleApple/V4/DoorRipple"));
                AssetDatabase.CreateAsset(material, path);
            }
            root.GetComponent<ThresholdDoor>().rippleMaterial = material;
            EditorUtility.SetDirty(root);
        }
    }
}
