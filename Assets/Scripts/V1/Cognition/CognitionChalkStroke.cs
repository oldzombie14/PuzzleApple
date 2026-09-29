using UnityEngine;
using UnityEngine.UI;

namespace PuzzleApple.Cognition
{
    // Stable, broken chalk grains rather than a uniformly filled UI rectangle.
    // Image keeps the usual UI masking/material behaviour; the grain is geometry, not a new bitmap.
    public sealed class CognitionChalkStroke : Image
    {
        [SerializeField] int seed;
        public int Seed { get => seed; set { seed = value; SetVerticesDirty(); } }

        static float Noise(int sample, int seed)
        {
            uint n = (uint)(sample * 374761393 + seed * 668265263);
            n = (n ^ (n >> 13)) * 1274126177u;
            return (n & 65535) / 65535f;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = rectTransform.rect;
            const float step = .85f;
            int index = 0;
            for (float x = 0; x < rect.width; x += step, index++)
            {
                float wave = Mathf.Sin(x * .065f + Seed) * .35f + Mathf.Sin(x * .21f + Seed) * .16f;
                for (int track = 0; track < 3; track++)
                {
                    int sample = index * 7 + track;
                    float grain = Noise(sample, Seed + 1);
                    if (grain < (track == 1 ? .08f : .27f)) continue;
                    float y = rect.center.y + wave + (track - 1) * .83f;
                    float height = .45f + Noise(sample, Seed + 9) * .65f;
                    float width = Mathf.Min(step * (.7f + grain * .4f), rect.width - x);
                    float jitter = (Noise(sample, Seed + 17) - .5f) * .55f;
                    Color tint = color;
                    tint.a *= .3f + grain * .7f;
                    int first = mesh.currentVertCount;
                    mesh.AddVert(new Vector3(rect.xMin + x, y + jitter - height * .5f), tint, Vector2.zero);
                    mesh.AddVert(new Vector3(rect.xMin + x, y + jitter + height * .5f), tint, Vector2.zero);
                    mesh.AddVert(new Vector3(rect.xMin + x + width, y + height * .5f), tint, Vector2.zero);
                    mesh.AddVert(new Vector3(rect.xMin + x + width, y - height * .5f), tint, Vector2.zero);
                    mesh.AddTriangle(first,first + 1,first + 2);
                    mesh.AddTriangle(first,first + 2,first + 3);
                }
            }
        }
    }
}
