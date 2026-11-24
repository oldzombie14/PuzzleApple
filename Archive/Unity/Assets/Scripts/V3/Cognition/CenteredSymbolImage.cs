using UnityEngine;
using UnityEngine.UI;

namespace PuzzleApple.V3.Cognition
{
    // Image normally aligns aspect-preserving geometry to the RectTransform pivot.
    // The board uses a top-left pivot for layout, but symbols must stay centered in their row.
    public sealed class CenteredSymbolImage : Image
    {
        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            base.OnPopulateMesh(vertices);
            if (!sprite || !preserveAspect || vertices.currentVertCount == 0) return;
            var rect = GetPixelAdjustedRect();
            var vertex = new UIVertex();
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            for (int i = 0; i < vertices.currentVertCount; i++)
            {
                vertices.PopulateUIVertex(ref vertex, i);
                min = Vector2.Min(min, vertex.position);
                max = Vector2.Max(max, vertex.position);
            }
            // Imported sprites can also carry unequal trim padding. Center the generated geometry.
            var offset = rect.center - (min + max) * .5f;
            for (int i = 0; i < vertices.currentVertCount; i++)
            {
                vertices.PopulateUIVertex(ref vertex, i);
                vertex.position += (Vector3)offset;
                vertices.SetUIVertex(vertex, i);
            }
        }
    }
}
