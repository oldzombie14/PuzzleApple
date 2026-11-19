using UnityEngine;

namespace PuzzleApple.V5.Outdoor
{
    public sealed class ChapterWordMarker : MonoBehaviour
    {
        public string word;
        [Min(.1f)] public float completionSeconds=1.5f;
        [Min(0)] public float floatHeight=.12f,floatSpeed=1;
        [Min(0)] public float emission=1;
        public bool Completed {get;private set;}
        public float Glow {get;private set;}
        Material ink;
        Vector3 origin;
        public void Build(string id,float width,Vector3 frameCenter){
            word=id;
            var prefab=Resources.Load<GameObject>(id=="light"?"V5/AscentLight":"V5/ChapterMirror");
            var mesh=prefab.GetComponentInChildren<MeshFilter>().sharedMesh;
            gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=gameObject.AddComponent<MeshRenderer>();
            ink=new Material(Resources.Load<Shader>("V5/AscentInk"));
            ink.SetColor("_Face",Color.black);ink.SetColor("_Edge",Color.black);
            renderer.sharedMaterial=ink;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            transform.localScale=Vector3.one*(width/Mathf.Max(.001f,mesh.bounds.size.x));
            // Centre the visible glyph, including any offset in the source mesh's pivot.
            origin=frameCenter-Vector3.Scale(mesh.bounds.center,transform.localScale);
            transform.localPosition=origin;
        }
        public void SetCompleted(bool completed=true){Completed=completed;}
        void Update(){
            Glow=Mathf.MoveTowards(Glow,Completed?1:0,Time.unscaledDeltaTime/Mathf.Max(.1f,completionSeconds));
            float light=Mathf.SmoothStep(0,1,Glow);
            ink.SetColor("_Face",Color.white*(emission*light));ink.SetColor("_Edge",Color.white*(emission*light));
            transform.localPosition=origin+Vector3.up*(floatHeight*Mathf.Sin(Time.time*floatSpeed));
        }
        void OnDestroy(){if(ink)Destroy(ink);}
    }
}
