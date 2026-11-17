using UnityEngine;
using UnityEngine.Rendering;

namespace PuzzleApple.V5
{
    public sealed class AscentStars : MonoBehaviour
    {
        struct Star
        {
            public MeshRenderer renderer;
            public Vector3 anchor;
            public float height,phase,frequency;
        }
        Star[] stars;
        Material ink;
        bool visible,collectible;

        public void Build(MeshFilter diamond,int count,float stairWidth,float start,float top,float rise)
        {
            ink=new Material(diamond.GetComponent<Renderer>().sharedMaterial);
            ink.SetVector("_CorridorAxis",Vector4.zero);
            ink.SetFloat("_FacetStrength",0);
            ink.SetFloat("_FogInfluence",0);
            // The white veil is behind these stars when the player looks down the stair.
            ink.renderQueue=(int)RenderQueue.Transparent+55;
            var random=new System.Random(7181);
            float Range(float min,float max)=>Mathf.Lerp(min,max,(float)random.NextDouble());
            stars=new Star[count];
            for(int i=0;i<count;i++){
                float u=(i+Range(.05f,.95f))/count;
                float sign=(i%2==0)?-1:1;
                float lateral=stairWidth*.5f+Range(.7f,i%4<2?3.4f:10.5f);
                var anchor=new Vector3(sign*lateral,rise*u+Range(-1.4f,5.4f),Mathf.Lerp(start,top+4,u));
                var go=new GameObject("Floating star",typeof(MeshFilter),typeof(MeshRenderer));
                go.transform.SetParent(transform,false);
                go.transform.localPosition=anchor;
                go.GetComponent<MeshFilter>().sharedMesh=diamond.sharedMesh;
                var renderer=go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial=ink;renderer.shadowCastingMode=ShadowCastingMode.Off;
                renderer.receiveShadows=false;renderer.enabled=false;
                stars[i]=new Star{renderer=renderer,anchor=anchor,height=Range(.19f,.66f),phase=Range(0,2*Mathf.PI),frequency=Range(.65f,1.4f)};
            }
        }

        public void Draw(Transform eye,float white,float time,bool show)
        {
            if(!show && !visible)return;
            visible=show;
            float contrast=Mathf.Abs(1-2*white);
            collectible=show && white<.5f && contrast>.15f;
            ink.SetColor("_EmissionColor",white<.5f?Color.white*1.6f:Color.black);
            ink.SetColor("_BaseColor",new Color(0,0,0,Mathf.SmoothStep(0,1,contrast)));
            foreach(var star in stars){
                star.renderer.enabled=show;
                if(!show)continue;
                var t=star.renderer.transform;
                float beat=time*star.frequency+star.phase;
                t.localPosition=star.anchor+Vector3.up*(.12f*Mathf.Sin(beat));
                t.localScale=Vector3.one*(star.height*(.91f+.09f*Mathf.Sin(beat*1.3f)));
                t.rotation=Quaternion.LookRotation(t.position-eye.position,transform.up);
            }
        }

        public Renderer Pick(Ray ray,float reach)
        {
            if(!visible || !collectible || !gameObject.activeInHierarchy)return null;
            Renderer nearest=null;
            foreach(var star in stars){
                var r=star.renderer;
                if(!r.enabled || r.forceRenderingOff)continue;
                var t=r.transform;
                var plane=new Plane(t.forward,t.position);
                if(!plane.Raycast(ray,out float distance) || distance>=reach)continue;
                var p=t.InverseTransformPoint(ray.GetPoint(distance));
                var bounds=r.localBounds;
                if(Mathf.Abs(p.x)/bounds.extents.x+Mathf.Abs(p.y)/bounds.extents.y>1)continue;
                nearest=r;reach=distance;
            }
            return nearest;
        }
        void OnDestroy(){if(ink)Destroy(ink);}
    }
}
