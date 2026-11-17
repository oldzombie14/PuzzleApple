using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PuzzleApple.V5.Editor
{
    // Records the sphere-sourced "move" memory from the real move tutorial room, once lit and once dark.
    // The floating spheres cross the portrait on staggered paths, wrapping outside the view.
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
            public Vector3 start, from, to;
            public float phase;
            public Vector3 At(float t) => Vector3.Lerp(from, to, Mathf.Repeat(t + phase, 1));
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
            var backgroundSpheres = new List<Renderer>();
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
                // Look across the left side of the room. The exit and its blocking sphere
                // sit behind the right edge of this portrait view.
                var focus = new Vector3(-room.HalfWidth * .65f, height * .5f, journey.pathLength + room.HalfWidth);
                float halfHeight = Mathf.Min(height * .39f, room.HalfWidth * .6f);
                float halfWidth = halfHeight * Width / Height;
                const float fieldOfView = 42;
                var eye = focus + Vector3.right * (halfHeight / Mathf.Tan(fieldOfView * .5f * Mathf.Deg2Rad));
                shot = new Shot {
                    eye = journey.Root.TransformPoint(eye),
                    look = Quaternion.LookRotation(-journey.Root.right, journey.Root.up),
                    fieldOfView = fieldOfView
                };

                // Portrait coordinates, measured in half-frame widths and heights. Leave room
                // beyond each edge for the entire sphere and its bloom before wrapping.
                Vector3 InFrame(float x, float y) => focus + new Vector3(0, y * halfHeight, x * halfWidth);
                var crossings = new[] {
                    (from: InFrame(-1.55f, .72f), to: InFrame(1.55f, .29f), phase: .24f),
                    (from: InFrame(1.8f, -.54f), to: InFrame(-1.8f, .43f), phase: .47f),
                    (from: InFrame(-.84f, -1.4f), to: InFrame(.69f, 1.4f), phase: .81f)
                };
                for (int i = 0; i < crossings.Length; i++)
                {
                    var sphere = journey.Root.Find("Move tutorial small sphere " + (i + 1));
                    if (!sphere) throw new InvalidOperationException("Missing tutorial sphere " + (i + 1));
                    paths.Add(new Path {
                        sphere = sphere, start = sphere.localPosition,
                        from = crossings[i].from, to = crossings[i].to, phase = crossings[i].phase
                    });
                }
                // Unposed lamps spawn at random and can otherwise sit motionless in the shot.
                for (int i = crossings.Length + 1; ; i++)
                {
                    var other = journey.Root.Find("Move tutorial small sphere " + i);
                    if (!other) break;
                    var surface = other.GetComponent<Renderer>();
                    if (!surface.enabled) continue;
                    backgroundSpheres.Add(surface);
                    surface.enabled = false;
                }
                var camera = player.view.GetComponent<Camera>();
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
                foreach (var surface in backgroundSpheres) if (surface) surface.enabled = true;
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
