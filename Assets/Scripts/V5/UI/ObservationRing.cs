using UnityEngine;
using UnityEngine.UI;

namespace PuzzleApple.V5
{
    // Native UI mesh: rounded arc ends, with the same background contrast as the eye.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ObservationRing : MaskableGraphic
    {
        [Min(1)] public float radius=34;
        [Range(.5f,12)] public float thickness=3;
        [Range(.5f,12)] public float progressThickness=5;
        [Range(0,180)] public float topGap=32;
        [Range(0,1)] public float idleAlpha=.7f;
        [Range(0,1)] public float loadingTrackAlpha=.15f;
        public Color idleTint=Color.white,progressTint=Color.white;
        [Range(0,1)] public float progress;
        [Range(12,160)] public int segments=96;
        float stateAlpha=1;
        public void SetState(float value,float alpha){progress=Mathf.Clamp01(value);stateAlpha=alpha;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();Arc(vh,1,thickness,idleTint*new Color(1,1,1,(progress>0?loadingTrackAlpha:idleAlpha)*stateAlpha));
            if(progress>0)Arc(vh,progress,progressThickness,progressTint*new Color(1,1,1,stateAlpha));
        }
        Vector2 Point(float angle,float r)=>new Vector2(Mathf.Sin(angle),Mathf.Cos(angle))*r;
        void Arc(VertexHelper vh,float amount,float width,Color ink)
        {
            float start=topGap*.5f*Mathf.Deg2Rad,sweep=(360-topGap)*Mathf.Deg2Rad*amount;
            int count=Mathf.Max(2,Mathf.CeilToInt(segments*amount));float half=width*.5f;
            for(int i=0;i<count;i++)
            {
                float a=start+sweep*i/count,b=start+sweep*(i+1)/count;int n=vh.currentVertCount;
                vh.AddVert(Point(a,radius-half),ink,Vector2.zero);vh.AddVert(Point(a,radius+half),ink,Vector2.zero);
                vh.AddVert(Point(b,radius+half),ink,Vector2.zero);vh.AddVert(Point(b,radius-half),ink,Vector2.zero);
                vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
            }
            Cap(vh,Point(start,radius),half,ink);Cap(vh,Point(start+sweep,radius),half,ink);
        }
        void Cap(VertexHelper vh,Vector2 centre,float r,Color ink)
        {
            int n=vh.currentVertCount;vh.AddVert(centre,ink,Vector2.zero);
            for(int i=0;i<=12;i++)vh.AddVert(centre+Point(i*Mathf.PI/6,r),ink,Vector2.zero);
            for(int i=0;i<12;i++)vh.AddTriangle(n,n+i+1,n+i+2);
        }
    }
}
