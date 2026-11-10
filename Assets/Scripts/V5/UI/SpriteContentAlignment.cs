using UnityEngine;
using UnityEngine.UI;

namespace PuzzleApple.V5
{
    // Align visible artwork, leaving the original sprite, Image and layout rect intact.
    [RequireComponent(typeof(Image))]
    public sealed class SpriteContentAlignment : BaseMeshEffect
    {
        [Tooltip("Centre of the visible alpha bounds within the sprite rect, measured from bottom-left.")]
        public Vector2 contentCentre=new Vector2(.5f,.5f);
        public Vector2 opticalOffset;
        public override void ModifyMesh(VertexHelper vh)
        {
            var image=graphic as Image;
            if(!IsActive()||!image||!image.sprite||vh.currentVertCount==0)return;
            var sprite=image.sprite;
            Vector2 uvMin=Vector2.positiveInfinity,uvMax=Vector2.negativeInfinity;
            Vector2 min=Vector2.positiveInfinity,max=Vector2.negativeInfinity;
            var vertex=new UIVertex();
            for(int i=0;i<vh.currentVertCount;i++)
            {
                vh.PopulateUIVertex(ref vertex,i);min=Vector2.Min(min,vertex.position);max=Vector2.Max(max,vertex.position);
                uvMin=Vector2.Min(uvMin,vertex.uv0);uvMax=Vector2.Max(uvMax,vertex.uv0);
            }
            Vector2 uv=new Vector2((sprite.rect.x+sprite.rect.width*contentCentre.x)/sprite.texture.width,
                (sprite.rect.y+sprite.rect.height*contentCentre.y)/sprite.texture.height);
            var point=new Vector2(Mathf.LerpUnclamped(min.x,max.x,(uv.x-uvMin.x)/Mathf.Max(.00001f,uvMax.x-uvMin.x)),
                Mathf.LerpUnclamped(min.y,max.y,(uv.y-uvMin.y)/Mathf.Max(.00001f,uvMax.y-uvMin.y)));
            Vector2 offset=image.GetPixelAdjustedRect().center+opticalOffset-point;
            for(int i=0;i<vh.currentVertCount;i++){vh.PopulateUIVertex(ref vertex,i);vertex.position+=(Vector3)offset;vh.SetUIVertex(vertex,i);}
        }
    }
}
