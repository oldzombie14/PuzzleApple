using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PuzzleApple.V5.Editor
{
    // Records the sphere-sourced "move" memory from the real move tutorial room, once lit and once dark.
    // The floating spheres are posed on bouncing straight-line paths whose bounce counts divide the clip,
    // so the last frame leads straight into the first. The blocking sphere stays where the room seats it.
    // Requires a fresh P5 Play session.
    public static class SphereMoveMemoryRecorder
    {
        const string LitPath = "Assets/Textures/V5/SphereMoveMemory.png";
        const string DarkPath = "Assets/Textures/V5/SphereMoveMemoryDark.png";
        const string SequencePath = "Assets/GameData/V5/Opening/SphereMoveMemory.asset";
        const string InterfacePath = "Assets/Prefabs/V5/UI/V5Interface.prefab";
        const int Width = 160, Height = 240, Columns = 8, Rows = 8, Frames = 64, Supersample = 3;
        const float FramesPerSecond = 18;
        public static string Result { get; private set; }

        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Fresh P5 Play required.");
            Result = "Running";
            var entry = UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            entry.StartCoroutine(Record(entry));
        }

        sealed class Path
        {
            public Transform sphere;
            public Vector3 start, centre, amplitude, phase, cycles;
            public Vector3 At(float t)
            {
                var p = centre;
                for (int axis = 0; axis < 3; axis++)
                    p[axis] += amplitude[axis] * Triangle(phase[axis] + cycles[axis] * t);
                return p;
            }
            // Linear travel with a reflection at each extreme, period 1.
            static float Triangle(float x) => 1 - 4 * Mathf.Abs(Mathf.Repeat(x + .25f, 1) - .5f);
        }

        struct Shot { public Vector3 eye; public Quaternion look; public float fieldOfView; }
        static Shot shot;

        static IEnumerator Until(Func<bool> done, float timeout = 20)
        {
            for (float t = 0; !done(); t += Time.unscaledDeltaTime)
            {
                if (t > timeout) { Result = "FAIL: timed out"; throw new TimeoutException(Result); }
                yield return null;
            }
        }

        static IEnumerator Record(WorldEntry entry)
        {
            var presentation = entry.sentenceRuntime.presentation;
            bool shots = presentation.enabledShots;
            presentation.enabledShots = false;
            bool background = Application.runInBackground;
            Application.runInBackground = true;
            var paths = new List<Path>();
            try
            {
                entry.EnterWorld();
                yield return Until(() => entry.Entered);
                var state = entry.board.State;
                state.TryAcquireSentence("record.sphere.world", "positive world");
                yield return Until(() => entry.room.Opened && !entry.Busy);
                state.TryAcquireSentence("record.sphere.door", "positive door");
                var journey = entry.journey;
                yield return Until(() => journey.DoorProgress >= 1);
                entry.board.Panel.SetOpen(false);
                var room = journey.Tutorial;
                var player = entry.player;
                player.Teleport(journey.Root.TransformPoint(new Vector3(0, .08f, journey.pathLength + .5f)), journey.Root.rotation);
                yield return Until(() => journey.InTutorial);
                yield return new WaitForSecondsRealtime(.5f);

                float height = journey.Root.Find("Move tutorial Ceiling").localPosition.y - .15f;
                float seconds = Frames / FramesPerSecond;
                var random = new System.Random(5);
                for (int i = 1; ; i++)
                {
                    var sphere = journey.Root.Find("Move tutorial small sphere " + i);
                    if (!sphere) break;
                    Vector3 ext = sphere.localScale * .5f;
                    Vector3 lo = new Vector3(-room.HalfWidth, 0, journey.pathLength + .15f) + ext;
                    Vector3 hi = new Vector3(room.HalfWidth, height, room.ExitZ - .15f) - ext;
                    // Keep clear of the camera standing at the entrance.
                    lo.z = Mathf.Max(lo.z, journey.pathLength + 2.2f);
                    var direction = new Vector3((float)random.NextDouble() * 2 - 1, (float)random.NextDouble() * 2 - 1,
                        (float)random.NextDouble() * 2 - 1).normalized;
                    var path = new Path { sphere = sphere, start = sphere.localPosition };
                    float travel = entry.chamberLight.moveSpeed * seconds;
                    for (int axis = 0; axis < 3; axis++)
                    {
                        // One bounce cycle covers 4 amplitudes; add bounces until the path fits the room.
                        float span = Mathf.Max(0, (hi[axis] - lo[axis]) * .5f);
                        float distance = travel * Mathf.Abs(direction[axis]);
                        int cycles = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Max(.01f, 4 * span * .8f)));
                        float amplitude = Mathf.Min(distance / (4 * cycles), span);
                        path.amplitude[axis] = amplitude;
                        path.cycles[axis] = cycles * Mathf.Sign(direction[axis] == 0 ? 1 : direction[axis]);
                        path.centre[axis] = Mathf.Clamp(path.start[axis], lo[axis] + amplitude, hi[axis] - amplitude);
                        path.phase[axis] = (float)random.NextDouble();
                    }
                    paths.Add(path);
                }
                if (paths.Count == 0) { Result = "FAIL: no tutorial spheres"; yield break; }

                var camera = player.view.GetComponent<Camera>();
                // Frame the whole room from the entrance, wide enough that every floating sphere stays in view.
                float depth = room.ExitZ - journey.pathLength;
                var eye = journey.Root.TransformPoint(new Vector3(0, height * .55f, journey.pathLength + .2f));
                var focus = journey.Root.TransformPoint(new Vector3(0, height * .45f, journey.pathLength + depth * .55f));
                float horizontal = 2 * Mathf.Atan(room.HalfWidth / (depth * .5f));
                float vertical = 2 * Mathf.Atan(Mathf.Tan(horizontal * .5f) / ((float)Width / Height)) * Mathf.Rad2Deg;
                shot = new Shot { eye = eye, look = Quaternion.LookRotation(focus - eye, journey.Root.up), fieldOfView = Mathf.Min(100, vertical) };
                room.SetOn(true);
                yield return Until(() => !room.Changing && room.SphereBrightness >= .999f);
                var lit = Capture(camera, paths, "lit");
                room.SetOn(false);
                yield return Until(() => !room.Changing && room.SphereBrightness <= .001f);
                var dark = Capture(camera, paths, "dark");
                room.SetOn(true);

                Save(lit, LitPath);
                Save(dark, DarkPath);
                UnityEngine.Object.Destroy(lit);
                UnityEngine.Object.Destroy(dark);
                var clip = AssetDatabase.LoadAssetAtPath<SnapshotSequence>(SequencePath);
                if (!clip) { clip = ScriptableObject.CreateInstance<SnapshotSequence>(); AssetDatabase.CreateAsset(clip, SequencePath); }
                clip.atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(LitPath);
                clip.unlitAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(DarkPath);
                clip.columns = Columns; clip.rows = Rows; clip.frames = Frames;
                clip.framesPerSecond = FramesPerSecond; clip.aspect = (float)Width / Height;
                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssetIfDirty(clip);
                AssignToInterface(clip);
                Result = "PASS: recorded " + paths.Count + " looping spheres";
            }
            finally
            {
                foreach (var path in paths) if (path.sphere) path.sphere.localPosition = path.start;
                presentation.enabledShots = shots;
                Application.runInBackground = background;
                if (Result == "Running") Result = "FAIL: interrupted";
            }
        }

        static Texture2D Capture(Camera camera, List<Path> paths, string label)
        {
            var original = camera.targetTexture;
            var descriptor = original ? original.descriptor : new RenderTextureDescriptor(Width, Height, RenderTextureFormat.ARGB32, 24);
            descriptor.width = Width * Supersample;
            descriptor.height = Height * Supersample;
            descriptor.msaaSamples = 1;
            var large = RenderTexture.GetTemporary(descriptor);
            var small = RenderTexture.GetTemporary(Width, Height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var atlas = new Texture2D(Width * Columns, Height * Rows, TextureFormat.RGB24, false) { name = "Sphere move " + label };
            var frame = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var active = RenderTexture.active;
            var view = camera.transform;
            Vector3 position = view.position;
            Quaternion rotation = view.rotation;
            float fieldOfView = camera.fieldOfView;
            try
            {
                view.SetPositionAndRotation(shot.eye, shot.look);
                camera.fieldOfView = shot.fieldOfView;
                camera.targetTexture = large;
                for (int f = 0; f < Frames; f++)
                {
                    float t = f / (float)Frames;
                    foreach (var path in paths) path.sphere.localPosition = path.At(t);
                    camera.Render();
                    Graphics.Blit(large, small);
                    RenderTexture.active = small;
                    frame.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                    frame.Apply(false);
                    atlas.SetPixels(f % Columns * Width, (Rows - 1 - f / Columns) * Height, Width, Height, frame.GetPixels());
                }
                atlas.Apply(false);
                return atlas;
            }
            finally
            {
                camera.targetTexture = original;
                camera.fieldOfView = fieldOfView;
                view.SetPositionAndRotation(position, rotation);
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(large);
                RenderTexture.ReleaseTemporary(small);
                UnityEngine.Object.Destroy(frame);
            }
        }

        static void Save(Texture2D texture, string path)
        {
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        static void AssignToInterface(SnapshotSequence clip)
        {
            var root = PrefabUtility.LoadPrefabContents(InterfacePath);
            try
            {
                var library = root.GetComponentInChildren<WordLibrary>(true);
                if (!library || library.sphereMoveMemory == clip) return;
                library.sphereMoveMemory = clip;
                PrefabUtility.SaveAsPrefabAsset(root, InterfacePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
