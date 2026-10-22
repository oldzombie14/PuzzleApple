using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace PuzzleApple.V4.Editor
{
    /// <summary>Run in a fresh P4 Play session. Does not inject or replace input devices.</summary>
    public static class LightFieldChecks
    {
        public static string Result { get; private set; }
        static void Require(bool value, string message)
        {
            if (value) return;
            Result = "FAIL: " + message;
            throw new InvalidOperationException(Result);
        }
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Requires P4 Play mode.");
            Result = "Running";
            var flow = UnityEngine.Object.FindFirstObjectByType<OpeningFlow>();
            flow.StartCoroutine(Check(flow));
        }
        static OpeningLight[] Lamps(OpeningLightField field) => field.GetComponentsInChildren<OpeningLight>();
        static void Teleport(FirstPersonController player, Vector3 point)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false; player.transform.position = point; controller.enabled = true;
        }
        static IEnumerator Check(OpeningFlow flow)
        {
            bool background = Application.runInBackground;
            Application.runInBackground = true;
            try
            {
                UnityEngine.Object.FindFirstObjectByType<WorldEntry>().EnterWorld();
                yield return new WaitForSecondsRealtime(3);
                var field = flow.lightField;
                var start = flow.player.transform.position;
                var lamps = Lamps(field);
                Require(flow.Ready && lamps.Length > 20 && lamps.Length <= field.maxActive, "entry and active budget");
                Require(lamps.All(l => l.transform.localScale.x >= field.scaleRange.x && l.transform.localScale.x <= field.scaleRange.y), "size range");
                Require(lamps.All(l => l.surface.bounds.min.y >= field.clearanceRange.x - .01f
                    && l.surface.bounds.min.y <= field.clearanceRange.y + .01f), "initial ground clearance");
                Require(lamps.Select(l => l.surface.sharedMaterial.GetInstanceID()).Distinct().Count() == 1, "shared material");
                Require(lamps.Count(l => l.glow.enabled) <= field.maxPointLights, "point light budget");
                var target = lamps.OrderBy(l => (l.transform.position - start).sqrMagnitude).First();
                Cursor.lockState = CursorLockMode.Locked;
                flow.player.view.LookAt(target.transform);
                Require(flow.TryCollectLight(), "actual pickup ray hits small lamp");
                yield return new WaitForSecondsRealtime(2);
                Require(flow.board.State.Knows("light"), "pickup flight grants light");
                var controller = flow.player.GetComponent<CharacterController>();
                Require(lamps.All(l => Physics.GetIgnoreCollision(l.GetComponent<Collider>(), controller)), "all lamps ignore player");
                var point = target.transform.position;
                Teleport(flow.player, new Vector3(point.x - 1, start.y, point.z));
                controller.Move(Vector3.right * 2);
                Require(flow.player.transform.position.x > point.x + .8f, "CharacterController passes through cube");
                Teleport(flow.player, start);
                var state = flow.board.State;
                state.TryAcquireWord("field.check.move.word", flow.board.Catalog.Word("move"));
                Require(state.TryAcquireSentence("field.check.move", "light move"), "create movement sentence");
                var motion = target.GetComponent<LightMotion>();
                foreach (bool open in new[] { true, false, true, false })
                {
                    flow.board.Panel.SetOpen(open);
                    Vector3 before = target.transform.position;
                    yield return new WaitForSecondsRealtime(.4f);
                    Require(field.Moving && motion.Moving && Vector3.Distance(before, target.transform.position) > .05f, "movement survives panel open/close");
                }
                state.TryAcquireSentence("field.check.off", "negative light");
                yield return new WaitForSecondsRealtime(1.5f);
                Require(!flow.lamp.IsOn && field.Moving && flow.door.Revealed, "off coexists with movement and reveals door");
                Require(Lamps(field).All(l => !l.IsOn && !l.glow.enabled), "all active lamps turn off");
                state.ClearWorkspace();
                yield return new WaitForSecondsRealtime(.1f);
                Vector3 stopped = target.transform.position;
                yield return new WaitForSecondsRealtime(.3f);
                Require(!field.Moving && Vector3.Distance(stopped, target.transform.position) < .01f, "dismantling freezes lamps");
                var snapshot = Lamps(field).Where(l => Vector3.Distance(l.transform.position, start) < field.spawnRadius - 3)
                    .Select(l => new { position = l.transform.position, scale = l.transform.localScale, direction = l.GetComponent<LightMotion>().Direction }).ToArray();
                state.TryAcquireSentence("field.check.off2", "negative light");
                Teleport(flow.player, start + Vector3.right * 160);
                yield return new WaitForSecondsRealtime(3);
                Require(field.ActiveCount <= field.maxActive && field.ActiveCount + field.PooledCount <= field.maxActive, "travel respects object budget");
                Require(Lamps(field).All(l => !l.IsOn && !l.glow.enabled), "newly loaded lamps inherit off state");
                Require(Lamps(field).All(l => Vector3.Distance(l.transform.position, start) > field.spawnRadius), "old area releases its objects");
                Teleport(flow.player, start);
                yield return new WaitForSecondsRealtime(3);
                lamps = Lamps(field);
                Require(snapshot.All(saved => lamps.Any(l => Vector3.Distance(l.transform.position, saved.position) < .001f
                    && l.transform.localScale == saved.scale && Vector3.Distance(l.GetComponent<LightMotion>().Direction, saved.direction) < .001f)), "return restores positions sizes and directions");
                state.TryAcquireSentence("field.check.move2", "light move");
                Teleport(flow.player, start + Vector3.right * 160);
                yield return new WaitForSecondsRealtime(2);
                Require(Lamps(field).All(l => l.GetComponent<LightMotion>().Moving && !l.IsOn), "reloaded lamps inherit simultaneous move/off");
                state.ClearWorkspace();
                Teleport(flow.player, start);
                yield return new WaitForSecondsRealtime(3);
                Require(flow.lamp.IsOn && !field.Moving && Lamps(field).All(l => !l.GetComponent<LightMotion>().Moving), "return after dismantling stays still and restores on");
                Result = "PASS: pickup, player passage, initial distribution, shared material/light budgets, panel movement, off/move coexistence, dismantling, streaming restoration and inherited rules.";
                Debug.Log(Result);
            }
            finally { Application.runInBackground = background; }
        }
    }
}
