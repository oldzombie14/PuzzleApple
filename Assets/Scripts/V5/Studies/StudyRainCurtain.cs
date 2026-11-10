using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace PuzzleApple.V5.Studies
{
    // Isolated preparation: not imported into Assets until editor handoff.
    // Three mesh batches, actual world depth, no textures and no per-streak GameObjects.
    public sealed class StudyRainCurtain : MonoBehaviour
    {
        [Serializable] public sealed class Layer
        {
            public string name;
            public Vector3 center, size;
            [Range(1,4000)] public int capacity = 1000;
            [Range(0,1)] public float opacity = .6f;
            public float speedScale = 1, lengthScale = 1, widthScale = 1;
        }
        public Material material;
        public Layer[] layers = {
            new Layer {name="Far", center=new Vector3(0,5,24), size=new Vector3(26,12,15), capacity=2400, opacity=.28f, speedScale=.75f, lengthScale=.8f, widthScale=.75f},
            new Layer {name="Middle", center=new Vector3(0,5,11), size=new Vector3(23,12,10), capacity=1400, opacity=.48f, speedScale=.9f, lengthScale=1, widthScale=.9f},
            new Layer {name="Near", center=new Vector3(0,5,1), size=new Vector3(19,12,8), capacity=700, opacity=.8f, speedScale=1.1f, lengthScale=1.2f, widthScale=1}
        };
        [Range(0,1)] public float density = .6f;
        [Range(.08f,2)] public float length = 1.2f;
        [Range(.3f,2)] public float widthPixels = .8f;
        [Range(.1f,20)] public float fallSpeed = 3;
        [Range(0,.9f)] public float speedVariation = .25f;
        [Range(.05f,.95f)] public float tailFade = .6f;
        [Range(.05f,.65f)] public float headFade = .3f;
        [Range(.5f,2)] public float pulseSpacing = 1;
        public Color color = new Color(.94f,.94f,.94f,1);
        public bool runOwnClock = true;
        public int ActiveStreaks {get;private set;}
        Mesh[] meshes;
        MeshRenderer[] renderers;
        MaterialPropertyBlock properties;
        int[] counts;
        float clock;

        void OnEnable() { Build(); }
        void Update() { if(runOwnClock) { clock+=Time.unscaledDeltaTime; Tick(clock); } }
        void Build()
        {
            if(material==null || layers==null)return;
            properties=new MaterialPropertyBlock();
            meshes=new Mesh[layers.Length];renderers=new MeshRenderer[layers.Length];counts=new int[layers.Length];
            for(int k=0;k<layers.Length;k++)
            {
                var layer=layers[k];int count=Mathf.Clamp(layer.capacity,1,4000);
                var vertices=new Vector3[count*4];var corners=new Vector2[count*4];var ids=new Vector2[count*4];var indices=new int[count*6];
                for(int i=0;i<count;i++)
                {
                    int v=i*4;corners[v]=new Vector2(-1,0);corners[v+1]=new Vector2(1,0);corners[v+2]=new Vector2(-1,1);corners[v+3]=new Vector2(1,1);
                    for(int j=0;j<4;j++)ids[v+j]=new Vector2(i+17+k*4099,0);
                    int t=i*6;indices[t]=v;indices[t+1]=v+2;indices[t+2]=v+1;indices[t+3]=v+1;indices[t+4]=v+2;indices[t+5]=v+3;
                }
                var mesh=new Mesh {name="Rain curtain / "+layer.name,hideFlags=HideFlags.DontSave};
                mesh.vertices=vertices;mesh.uv=corners;mesh.uv2=ids;mesh.triangles=indices;
                var go=new GameObject(layer.name+" rain batch"){hideFlags=HideFlags.DontSave};go.transform.SetParent(transform,false);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                renderers[k]=renderer;meshes[k]=mesh;counts[k]=-1;
            }
            Tick(clock);
        }
        public void Tick(float time)
        {
            if(meshes==null)return;
            ActiveStreaks=0;
            for(int k=0;k<meshes.Length;k++)
            {
                var layer=layers[k];int capacity=meshes[k].vertexCount/4;
                int count=Mathf.Clamp(Mathf.RoundToInt(density*capacity),0,capacity);ActiveStreaks+=count;
                if(counts[k]!=count)
                {
                    meshes[k].SetSubMesh(0,new SubMeshDescriptor(0,count*6,MeshTopology.Triangles),MeshUpdateFlags.DontRecalculateBounds);
                    counts[k]=count;
                }
                Vector3 size=new Vector3(Mathf.Max(1,layer.size.x),Mathf.Max(1,layer.size.y),Mathf.Max(1,layer.size.z));
                meshes[k].bounds=new Bounds(layer.center,size+Vector3.one*(length*layer.lengthScale+2));
                properties.Clear();properties.SetFloat("_StudyTime",Mathf.Max(0,time));properties.SetVector("_Center",layer.center);properties.SetVector("_Area",size);
                properties.SetColor("_Color",new Color(color.r,color.g,color.b,color.a*layer.opacity));
                properties.SetFloat("_FallSpeed",Mathf.Max(.01f,fallSpeed*layer.speedScale));properties.SetFloat("_SpeedVariation",speedVariation);
                properties.SetFloat("_Length",Mathf.Max(.01f,length*layer.lengthScale));properties.SetFloat("_Width",widthPixels*layer.widthScale);
                properties.SetFloat("_TailFade",tailFade);
                properties.SetFloat("_HeadFade",headFade);properties.SetFloat("_Spacing",pulseSpacing);
                renderers[k].SetPropertyBlock(properties);
            }
        }
        void OnDisable()
        {
            if(meshes==null)return;
            for(int k=0;k<meshes.Length;k++)
            {
                if(renderers[k]!=null)Destroy(renderers[k].gameObject);
                if(meshes[k]!=null)Destroy(meshes[k]);
            }
            meshes=null;renderers=null;counts=null;ActiveStreaks=0;
        }
    }
}
