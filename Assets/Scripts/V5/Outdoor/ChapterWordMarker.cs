using UnityEngine;

namespace PuzzleApple.V5.Outdoor
{
    public sealed class ChapterWordMarker : MonoBehaviour
    {
        public string word;
        [Min(.1f)] public float completionSeconds=1.5f;
        [Min(0)] public float floatHeight=.12f,floatSpeed=1;
        Light glow;
        MaterialPropertyBlock tint;
        MeshRenderer surface;
        OpeningLight source;
        public bool Completed {get;private set;}
        public float Glow {get;private set;}
        Vector3 origin;
        public void Build(string id,float width,Vector3 frameCenter,OpeningLight lamp){
            word=id;
            var prefab=Resources.Load<GameObject>(id=="light"?"V5/AscentLight":"V5/ChapterMirror");
            var mesh=prefab.GetComponentInChildren<MeshFilter>().sharedMesh;
            gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            source=lamp;
            surface=gameObject.AddComponent<MeshRenderer>();
            surface.sharedMaterial=source.AppearanceMaterial;
            surface.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            tint=new MaterialPropertyBlock();
            glow=new GameObject("Marker glow",typeof(Light)).GetComponent<Light>();
            glow.transform.SetParent(transform,false);
            glow.transform.localPosition=mesh.bounds.center;
            glow.type=LightType.Point;
            glow.range=OpeningLight.OutdoorPointRange;
            ApplyGlow();
            transform.localScale=Vector3.one*(width/Mathf.Max(.001f,mesh.bounds.size.x));
            // Centre the visible glyph, including any offset in the source mesh's pivot.
            origin=frameCenter-Vector3.Scale(mesh.bounds.center,transform.localScale);
            transform.localPosition=origin;
        }
        public void SetCompleted(bool completed=true){Completed=completed;}
        void Update(){
            Glow=Mathf.MoveTowards(Glow,Completed?1:0,Time.unscaledDeltaTime/Mathf.Max(.1f,completionSeconds));
            ApplyGlow();
            transform.localPosition=origin+Vector3.up*(floatHeight*Mathf.Sin(Time.time*floatSpeed));
        }
        void ApplyGlow(){
            float amount=Mathf.SmoothStep(0,1,Glow);
            tint.SetColor("_BaseColor",Color.Lerp(Color.black,source.LitBaseColor,amount));
            tint.SetColor("_EmissionColor",source.LitEmission*amount);
            tint.SetColor("_SpecColor",new Color(.04f,.04f,.04f)*amount);
            surface.SetPropertyBlock(tint);
            glow.intensity=OpeningLight.OutdoorPointIntensity*amount;
            glow.enabled=amount>0;
        }
    }
}
