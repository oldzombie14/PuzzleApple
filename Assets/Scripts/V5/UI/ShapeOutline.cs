using UnityEngine;
using UnityEngine.UI;

namespace PuzzleApple.V5
{
    public sealed class ShapeOutline : MaskableGraphic
    {
        public InstanceShape shape;
        public bool returnArrow;
        void Line(VertexHelper mesh,Vector2 a,Vector2 b,float width){
            var e=(b-a).normalized;var n=new Vector2(-e.y,e.x)*width*.5f;
            int first=mesh.currentVertCount;
            mesh.AddVert(a-n,color,Vector2.zero);mesh.AddVert(a+n,color,Vector2.zero);mesh.AddVert(b+n,color,Vector2.zero);mesh.AddVert(b-n,color,Vector2.zero);
            mesh.AddTriangle(first,first+1,first+2);mesh.AddTriangle(first,first+2,first+3);
        }
        protected override void OnPopulateMesh(VertexHelper mesh){
            mesh.Clear();
            float r=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.3f;
            int sides=returnArrow?32:shape==InstanceShape.Sphere?40:4;
            float offset=shape==InstanceShape.Cube?Mathf.PI*.25f:0;
            float sweep=returnArrow?Mathf.PI*1.6f:Mathf.PI*2;
            Vector2 Point(int i){float a=i*sweep/sides+offset;return new Vector2(Mathf.Sin(a),Mathf.Cos(a))*r;}
            for(int i=0;i<sides;i++)Line(mesh,Point(i),Point(i+1),1.8f);
            if(returnArrow){var p=Point(0);Line(mesh,p,p+new Vector2(4,3),1.8f);Line(mesh,p,p+new Vector2(4,-3),1.8f);}
        }
    }
}
