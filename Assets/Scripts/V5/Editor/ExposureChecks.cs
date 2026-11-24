using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PuzzleApple.V5.Editor
{
    public static class ExposureChecks
    {
        public static string Result { get; private set; }
        static readonly List<string> samples = new List<string>();
        static bool enforce;
        static string folder;
        public static void RunBaseline() => Begin(false);
        public static void Run() => Begin(true);
        public static void RunChapter() => Begin(true, true);
        static void Begin(bool check, bool chapterOnly = false)
        {
            enforce = check; samples.Clear(); Result = "Running";
            folder = Path.GetFullPath("Temp/ExposureChecks/" + (chapterOnly ? "chapter-balanced" : check ? "balanced" : "baseline"));
            Directory.CreateDirectory(folder);
            var entry = UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            Check(entry, "P5 world entry exists");
            entry.StartCoroutine(chapterOnly ? Chapter(entry) : Survey(entry));
        }
        static void Check(bool value, string label)
        {
            if (value) return;
            Result = "FAIL: " + label; throw new InvalidOperationException(Result);
        }
        static IEnumerator View(WorldEntry e, Vector3 feet, Vector3 target, string name)
        {
            e.board.Panel.SetOpen(false);
            e.player.Teleport(feet, Quaternion.identity);
            e.player.view.LookAt(target); e.player.AdoptCurrentView();
            e.player.MovementLocked = true;
            yield return new WaitForSecondsRealtime(1.5f);
            yield return new WaitForEndOfFrame();
            var camera = e.player.view.GetComponent<Camera>();
            var frame = camera.targetTexture;
            Check(frame, "HDR world render target exists");
            var pixels = new Texture2D(frame.width, frame.height, TextureFormat.RGBAFloat, false, true);
            var png = new Texture2D(frame.width, frame.height, TextureFormat.RGB24, false);
            var display = RenderTexture.GetTemporary(frame.width, frame.height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = frame; pixels.ReadPixels(new Rect(0, 0, frame.width, frame.height), 0, 0); pixels.Apply();
                int over = 0; float peak = 0;
                foreach (var color in pixels.GetPixels())
                {
                    float value = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
                    if (value > 1.01f) over++;
                    peak = Mathf.Max(peak, value);
                }
                float fraction = (float)over / (frame.width * frame.height);
                Graphics.Blit(frame, display); RenderTexture.active = display;
                png.ReadPixels(new Rect(0, 0, frame.width, frame.height), 0, 0); png.Apply();
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), png.EncodeToPNG());
                var manager = VolumeManager.instance; var stack = manager.CreateStack();
                try
                {
                    manager.Update(stack, e.player.view, camera.GetUniversalAdditionalCameraData().volumeLayerMask);
                    var bloom = stack.GetComponent<Bloom>(); var tone = stack.GetComponent<Tonemapping>();
                    samples.Add(name + ": over-range=" + fraction.ToString("P2") + ", peak=" + peak.ToString("F3") +
                        ", bloom=" + bloom.intensity.value.ToString("F2") + ", scatter=" + bloom.scatter.value.ToString("F2") +
                        ", clamp=" + bloom.clamp.value.ToString("F2") + ", tone=" + tone.mode.value);
                    File.WriteAllLines(Path.Combine(folder, "measurements.txt"), samples);
                    if (enforce)
                    {
                        Check(tone.mode.value == TonemappingMode.Neutral, name + " uses the shared highlight response");
                        Check(bloom.intensity.value <= 1.6f && bloom.scatter.value <= .65f && bloom.clamp.value <= 4, name + " has bounded bloom");
                        Check(fraction < .001f && peak <= 1.05f, name + " retains display-range highlights");
                    }
                }
                finally { manager.DestroyStack(stack); }
            }
            finally
            {
                RenderTexture.active = previous; RenderTexture.ReleaseTemporary(display);
                UnityEngine.Object.Destroy(pixels); UnityEngine.Object.Destroy(png);
            }
        }
        static void Illuminate(WorldEntry e, bool boosted)
        {
            e.board.State.ClearWorkspace();
            if (boosted) foreach (var noun in new[] { "world", "light", "door", "floor" }) VitruvianChecks.Say(e.board, "light", noun);
            e.board.Panel.SetOpen(false);
        }
        static IEnumerator Survey(WorldEntry e)
        {
            e.OpenWorld(-1);
            yield return ChargeDoorChecks.Wait(() => e.room.Opened && !e.Busy, "open exposure fixture");
            e.sentenceRuntime.presentation.enabledShots = false;
            var j = e.journey;
            foreach (var gate in UnityEngine.Object.FindObjectsByType<ProgressLock>(FindObjectsSortMode.None)) gate.Unlock();
            j.SetDoor(true); j.EntranceDoor.SetOpen(true); j.SetLights(true, PassageJourney.LightSpace.All);
            yield return ChargeDoorChecks.Wait(() => j.DoorProgress == 1 && j.EntranceDoor.Progress == 1 && j.Brightness == 1 && e.chamberLight.Brightness == 1, "fully lit fixture");
            foreach (bool boost in new[] { false, true })
            {
                Illuminate(e, boost);
                string suffix = boost ? "-boosted" : "-normal";
                yield return View(e, e.chamberLight.transform.position + new Vector3(0, .08f, -5), e.chamberLight.transform.position, "chamber" + suffix);
                yield return View(e, j.Root.TransformPoint(new Vector3(0, .08f, j.pathLength + 1)), e.chamberLight.transform.position, "corridor-back" + suffix);
                yield return View(e, j.Root.TransformPoint(new Vector3(0, .08f, 2)), j.Tutorial.Sphere.position, "corridor-forward" + suffix);
                yield return View(e, j.Root.TransformPoint(new Vector3(0, .08f, j.pathLength + 4)), j.Tutorial.Sphere.position, "sphere-room" + suffix);
                yield return View(e, j.Root.TransformPoint(new Vector3(0, .08f, j.RoomStart + 3)), j.EndingDoor.Leaf.position + Vector3.up * 2, "far-room" + suffix);
                yield return View(e, j.EndingDoor.StairLanding + j.Root.forward * 3, j.Ascent.Landing.position, "black-stairs" + suffix);
                yield return View(e, j.Ascent.Landing.position + Vector3.up * .36f - j.Ascent.transform.forward * .75f, j.Ascent.Symbol.position, "white-stairs" + suffix);
            }
            e.board.State.ClearWorkspace(); e.player.MovementLocked = false;
            yield return WhiteAscentChecks.Leave(e);
            var world = e.outdoor.World;
            foreach (bool boost in new[] { false, true })
            {
                Illuminate(e, boost);
                yield return View(e, world.exit.position + Vector3.forward * 10, world.door.transform.position + Vector3.up * 3, "outdoor-" + (boost ? "boosted" : "normal"));
            }
            e.board.State.ClearWorkspace(); e.player.MovementLocked = false;
            yield return Chapter(e);
        }
        static IEnumerator Chapter(WorldEntry e)
        {
            yield return VitruvianChecks.Gallery(e);
            e.sentenceRuntime.presentation.enabledShots = false;
            VitruvianChecks.Say(e.board, "positive", "world");
            yield return ChargeDoorChecks.Wait(() => e.outdoor.Chapter.box.Opened && !e.Busy, "open chapter");
            foreach (var room in new[] { e.outdoor.Chapter, e.outdoor.Garden, e.outdoor.Vitruvian })
            {
                room.SetLight(true); if (room.puzzle) room.puzzle.SetOn(true);
                var feet = room.floor.bounds.center; feet.y = room.floor.bounds.max.y + .08f;
                var target = room.puzzle ? room.puzzle.source.transform.position : room.lamp ? room.lamp.position : room.paintingLens.bounds.center;
                foreach (bool boost in new[] { false, true })
                {
                    Illuminate(e, boost);
                    yield return View(e, feet, target, room.Space + "-" + (boost ? "boosted" : "normal"));
                }
            }
            e.player.MovementLocked = false;
            Result = "PASS: cross-region normal/stacked-light rendering captured at " + folder;
        }
    }
}
