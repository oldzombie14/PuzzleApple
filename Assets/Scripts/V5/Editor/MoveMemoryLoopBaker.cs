using UnityEditor;
using UnityEngine;

namespace PuzzleApple.V5.Editor
{
    // Bakes the star "move" memory as a forward-only loop. Each corridor star flies out along its own
    // direction, fades, and reappears at its start, exactly once per clip, so the last frame leads
    // straight into the first. Layout, sizes and flight directions come from the original corridor
    // recording; the flat white diamonds match the CorridorDiamond silhouette (width 0.58 of height).
    public static class MoveMemoryLoopBaker
    {
        const string LitPath = "Assets/Textures/V5/MoveMemory.png";
        const string DarkPath = "Assets/Textures/V5/MoveMemoryDark.png";
        const string SequencePath = "Assets/GameData/V5/Opening/MoveMemory.asset";
        const int Width = 160, Height = 240, Columns = 8, Rows = 8, Frames = 64;
        const float DarkLevel = 105f / 255f, DiamondAspect = .58f;

        // Position on frame 0 (pixels, origin bottom-left), flight per loop (pixels), half height, loop phase on frame 0.
        static readonly (Vector2 at, Vector2 flight, float halfHeight, float phase)[] Stars =
        {
            (new Vector2(47.9f, 32.4f), new Vector2(-8, 44), 7.9f, .30f),
            (new Vector2(49.1f, 84.5f), new Vector2(-36, 25), 4.8f, .62f),
            (new Vector2(118.4f, 114.5f), new Vector2(-40, 43), 23.8f, .22f),
            (new Vector2(53.0f, 146.4f), new Vector2(-6, -47), 12.9f, .48f),
            (new Vector2(117.0f, 137.0f), new Vector2(42, 36), 12.0f, .08f),
            (new Vector2(70.9f, 193.8f), new Vector2(-69, 3), 7.6f, .80f),
        };

        [MenuItem("PuzzleApple/V5/Bake Move Memory Loop")]
        public static string Bake()
        {
            var lit = new Texture2D(Width * Columns, Height * Rows, TextureFormat.RGB24, false);
            var dark = new Texture2D(lit.width, lit.height, TextureFormat.RGB24, false);
            var litPixels = new Color32[lit.width * lit.height];
            var darkPixels = new Color32[litPixels.Length];
            var frame = new float[Width * Height];
            for (int f = 0; f < Frames; f++)
            {
                System.Array.Clear(frame, 0, frame.Length);
                foreach (var star in Stars) Draw(frame, star, f / (float)Frames);
                int ox = f % Columns * Width, oy = (Rows - 1 - f / Columns) * Height;
                for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    float v = Mathf.Clamp01(frame[y * Width + x]);
                    byte l = (byte)Mathf.RoundToInt(v * 255), d = (byte)Mathf.RoundToInt(v * DarkLevel * 255);
                    int i = (oy + y) * lit.width + ox + x;
                    litPixels[i] = new Color32(l, l, l, 255);
                    darkPixels[i] = new Color32(d, d, d, 255);
                }
            }
            lit.SetPixels32(litPixels);
            dark.SetPixels32(darkPixels);
            Save(lit, LitPath);
            Save(dark, DarkPath);
            Object.DestroyImmediate(lit);
            Object.DestroyImmediate(dark);
            var clip = AssetDatabase.LoadAssetAtPath<SnapshotSequence>(SequencePath);
            if (clip)
            {
                clip.columns = Columns; clip.rows = Rows; clip.frames = Frames; clip.aspect = (float)Width / Height;
                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssetIfDirty(clip);
            }
            return "Baked " + Frames + " looping frames";
        }

        static void Draw(float[] frame, (Vector2 at, Vector2 flight, float halfHeight, float phase) star, float time)
        {
            float phase = Mathf.Repeat(star.phase + time, 1);
            // Fade in from the start point, fly, then fade before restarting, like CorridorStars flights.
            float reveal = Mathf.SmoothStep(0, 1, phase / .18f) * (1 - Mathf.SmoothStep(0, 1, (phase - .68f) / .32f));
            if (reveal <= .001f) return;
            Vector2 centre = star.at + star.flight * (phase - star.phase);
            float h = star.halfHeight * Mathf.Lerp(.4f, 1, reveal), w = h * DiamondAspect;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(centre.x - w)), x1 = Mathf.Min(Width - 1, Mathf.CeilToInt(centre.x + w));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(centre.y - h)), y1 = Mathf.Min(Height - 1, Mathf.CeilToInt(centre.y + h));
            const int samples = 4;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int inside = 0;
                for (int sy = 0; sy < samples; sy++)
                for (int sx = 0; sx < samples; sx++)
                {
                    float px = x + (sx + .5f) / samples - centre.x, py = y + (sy + .5f) / samples - centre.y;
                    if (Mathf.Abs(px) / w + Mathf.Abs(py) / h <= 1) inside++;
                }
                if (inside == 0) continue;
                int i = y * Width + x;
                frame[i] = Mathf.Max(frame[i], reveal * inside / (float)(samples * samples));
            }
        }

        static void Save(Texture2D texture, string path)
        {
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
        }
    }
}
