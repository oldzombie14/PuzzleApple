using UnityEngine;
using UnityEngine.Rendering;
namespace PuzzleApple.V5.Studies
{
    public sealed class StudyRain : MonoBehaviour
    {
        public Material material;
        [Range(8,320)] public int density=96;
        [Range(.08f,1.4f)] public float length=.45f;
        [Range(.003f,.04f)] public float width=.009f;
        [Range(.2f,14)] public float fallSpeed=5;
        [Range(0,1)] public float speedVariation=.35f;
        [Range(-45,45)] public float angle=8;
        [Range(0,2)] public float gust=.3f;
        public Vector3 area=new Vector3(12,7,10);
        LineRenderer[] lines;
        public int ActiveLines {get;private set;}
        static float Seed(int i,float factor)=>Mathf.Repeat(Mathf.Sin(i*factor+1.37f)*43758.5453f,1);
        void Awake()
        {
            lines=new LineRenderer[320];
            for(int i=0;i<lines.Length;i++)
            {
                var go=new GameObject("Rain streak "+i);go.transform.SetParent(transform,false);
                var line=go.AddComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=2;
                line.sharedMaterial=material;line.numCapVertices=0;line.numCornerVertices=0;
                line.alignment=LineAlignment.View;line.shadowCastingMode=ShadowCastingMode.Off;
                line.receiveShadows=false;line.enabled=false;lines[i]=line;
            }
        }
        public void Tick(float time)
        {
            if(lines==null)return;
            ActiveLines=Mathf.Clamp(density,8,lines.Length);
            for(int i=0;i<lines.Length;i++)
            {
                var line=lines[i];line.enabled=i<ActiveLines;if(!line.enabled)continue;
                float seed=Seed(i,12.9898f),speed=fallSpeed*Mathf.Lerp(1-speedVariation*.75f,1+speedVariation,seed);
                float phase=Mathf.Repeat(time*speed/area.y+Seed(i,78.233f),1);
                float age=phase*area.y/speed;
                float horizontal=speed*Mathf.Tan(angle*Mathf.Deg2Rad);
                float wind=gust*Mathf.Sin(time*.8f+seed*6.28f);
                float x=(Seed(i,43.71f)-.5f)*area.x+horizontal*age+wind;
                float z=(Seed(i,93.11f)-.5f)*area.z;
                var head=new Vector3(x,Mathf.Lerp(area.y,.02f,phase),z);
                var velocity=new Vector3(horizontal+gust*.8f*Mathf.Cos(time*.8f+seed*6.28f),-speed,0);
                var tail=head-velocity.normalized*length*Mathf.Lerp(.65f,1.35f,seed);
                line.SetPosition(0,head);line.SetPosition(1,tail);line.startWidth=line.endWidth=width*Mathf.Lerp(.7f,1.2f,seed);
                float alpha=Mathf.SmoothStep(0,1,Mathf.Clamp01(phase/.04f))*Mathf.SmoothStep(0,1,Mathf.Clamp01((1-phase)/.06f));
                line.startColor=new Color(1,1,1,alpha);line.endColor=new Color(1,1,1,alpha*.5f);
            }
        }
    }
}
