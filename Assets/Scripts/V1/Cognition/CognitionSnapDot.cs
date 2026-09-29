using UnityEngine;
using UnityEngine.UI;

namespace PuzzleApple.Cognition
{
    // A small geometry-only round hint, independent of font glyphs and icon assets.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CognitionSnapDot : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = rectTransform.rect;
            Vector2 center = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * .5f;
            const int segments = 24;
            mesh.AddVert(center, color, Vector2.zero);
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
                mesh.AddTriangle(0, i + 1, (i + 1) % segments + 1);
            }
        }
    }
}
