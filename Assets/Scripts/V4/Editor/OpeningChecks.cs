using System;
using System.Collections;
using System.Linq;
using PuzzleApple.V4.Cognition;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace PuzzleApple.V4.Editor
{
    public static class OpeningChecks
    {
        public static string Result { get; private set; }
        static int checks;
        static void Require(bool condition, string message)
        {
            if (!condition) { Result = "FAIL: " + message; throw new InvalidOperationException(Result); }
            checks++;
        }
        public static void Run(bool lightFirst)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Start a fresh P4 Play session.");
            var flow = UnityEngine.Object.FindFirstObjectByType<OpeningFlow>();
            Result = "Running"; checks = 0;
            EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
            flow.StartCoroutine(Check(flow, lightFirst));
        }
        static void Keys(params Key[] keys)
        {
            Cursor.lockState = CursorLockMode.Locked;
            if (!Keyboard.current.enabled) InputSystem.EnableDevice(Keyboard.current);
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
        }
        static IEnumerator Check(OpeningFlow flow, bool lightFirst)
        {
            var original = InputSystem.settings;
            var originalEditorInput = original.editorInputBehaviorInPlayMode;
            var originalBackground = original.backgroundBehavior;
            // Keep the Input System's settings object alive across Play mode/domain reloads.
            // A temporary clone can be destroyed before the package restores its saved state.
            original.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            original.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            bool background = Application.runInBackground; Application.runInBackground = true;
            try
            {
                Require(!flow.Ready && !flow.player.enabled && flow.board.Panel.InputBlocked, "entry prevents movement and Tab");
                Require(!flow.door.Revealed && !flow.door.gameObject.activeSelf, "door hidden before first blackout");
                UnityEngine.Object.FindFirstObjectByType<WorldEntry>().EnterWorld();
                float end = Time.unscaledTime + 5;
                while (!flow.Ready && Time.unscaledTime < end) yield return null;
                Require(flow.Ready && flow.player.enabled, "entry releases opening");
                Require(flow.board.Catalog.Words.Where(w => flow.board.State.Knows(w.Id)).Select(w => w.Id).SequenceEqual(new[] { "positive", "negative", "world" }), "exact initial vocabulary");
                var start = flow.player.transform.position;
                Keys(Key.Tab); yield return null; Keys(); yield return null;
                Require(flow.board.Panel.IsOpen && !flow.CanInteract, "Tab opens panel and blocks world interaction");
                for (int i = 0; i < 10; i++) { Keys(Key.W); yield return null; }
                Require(Vector3.ProjectOnPlane(flow.player.transform.position - start, Vector3.up).magnitude < .001f, "panel stops walking");
                Keys(Key.Tab); yield return null; Keys(); yield return null; yield return null;
                Require(!flow.board.Panel.IsOpen, "Tab closes panel");
                Aim(flow);
                yield return null;
                if (lightFirst)
                {
                    Require(flow.TryCollectLight() && flow.player.MovementLocked, "light first reserves collection and freezes translation");
                    Require(!flow.board.State.Knows("light") && !flow.TryCollectLight(), "light is not granted early or queued twice");
                    var still = flow.player.transform.position;
                    while (flow.Collecting) { Keys(Key.W); yield return null; }
                    Keys();
                    Require(Vector3.ProjectOnPlane(flow.player.transform.position - still, Vector3.up).magnitude < .01f, "light collection prevents first steps");
                    Require(flow.board.State.Knows("light") && !flow.player.MovementLocked, "light flight commits vocabulary and restores walking");
                }
                end = Time.unscaledTime + 3;
                while (flow.player.WalkedDistance <= .02f && Time.unscaledTime < end) { Keys(Key.W); yield return null; }
                Keys(); yield return null;
                Require(flow.MovementDiscoveryPending && !flow.CanInteract && !flow.TryCollectLight(), "first actual step blocks interaction before learning threshold");
                for (int i = 0; i < 10; i++) { Keys(); yield return null; }
                Require(flow.MovementDiscoveryPending && !flow.CanInteract, "stopping mid discovery does not bypass interaction lock");
                end = Time.unscaledTime + 15;
                while (!flow.Collecting && !flow.board.State.Knows("move") && Time.unscaledTime < end) { Keys(Key.W); yield return null; }
                Keys();
                Require(flow.Collecting && !flow.board.State.Knows("move") && !flow.CanInteract, "move stays pending throughout flight");
                while (flow.Collecting && Time.unscaledTime < end) { Keys(); yield return null; }
                Keys(); yield return null;
                Require(flow.board.State.Knows("move") && !flow.MovementDiscoveryPending && flow.CanInteract, "move completion restores interaction");
                if (!lightFirst)
                {
                    Aim(flow); yield return null; Keys();
                    Require(flow.TryCollectLight(), "light can be collected after move");
                    while (flow.Collecting) { Keys(); yield return null; }
                }
                Require(flow.board.State.Knows("light") && !flow.TryCollectLight(), "light is acquired once");
                var s = flow.board.State;
                s.TryAcquireSentence("check.off", "negative light");
                var spawnForward = Vector3.ProjectOnPlane(flow.player.view.forward, Vector3.up).normalized;
                var expectedSpawn = flow.player.view.position + spawnForward * flow.door.spawnDistance;
                expectedSpawn.y = flow.door.groundHeight + flow.door.suspendedHeight;
                var off = s.Groups.Last();
                Require(flow.lamp.surface.material.GetColor("_BaseColor").r > .99f, "off command does not snap appearance to black");
                yield return new WaitForSecondsRealtime(flow.lamp.transitionDuration * .5f);
                float middle = flow.lamp.surface.material.GetColor("_BaseColor").r;
                Require(middle > .05f && middle < .95f && VisibleLamp(flow).glow.enabled, "cube and light fade through an intermediate state");
                yield return new WaitForSecondsRealtime(flow.lamp.transitionDuration * .5f + .1f);
                Require(off.Effective && !flow.lamp.IsOn && !VisibleLamp(flow).glow.enabled, "off light disables emission and local light");
                Require(flow.door.Revealed && Vector3.Distance(flow.door.transform.position, expectedSpawn)<.02f, "blackout reveals door ahead of current player");
                s.TryAcquireSentence("check.on", "positive light");
                var on = s.Groups.Last();
                Require(on.Conflicted && !flow.lamp.IsOn, "earliest opposing state wins");
                s.ReturnToLibrary(off.Id, off.Words[0].Id, true);
                yield return new WaitForSecondsRealtime(flow.lamp.transitionDuration + .1f);
                Require(on.Effective && flow.lamp.IsOn && VisibleLamp(flow).glow.enabled, "removing off hands control to on");
                Require(flow.door.gameObject.activeSelf && Vector3.Distance(flow.door.transform.position, expectedSpawn)<.02f, "relighting preserves door progress and position");
                s.TryAcquireSentence("check.invalid", "move world");
                Require(!s.Groups.Last().Recognized && flow.board.Surface.GetComponentsInChildren<CognitionChalkStroke>(true).Length > 0, "unsupported sentence is struck through");
                s.ClearWorkspace(); Require(flow.lamp.IsOn, "dismantling returns initial light state");
                s.TryAcquireSentence("check.off2", "negative light");
                Require(!flow.lamp.IsOn, "off can be reapplied");
                s.ClearWorkspace(); Require(flow.lamp.IsOn, "removing off restores light");
                Require(flow.hoverEye.rectTransform.sizeDelta == new Vector2(140, 140) && flow.hoverEye.rectTransform.anchoredPosition == Vector2.zero, "eye matches original geometry");
                Require(flow.hoverEye.material.shader == AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/General/UI/ReticleContrast.mat").shader, "eye retains original contrast shader with V4 world image binding");
                Require(!flow.gameObject.GetComponentsInChildren<MonoBehaviour>(true).Any(c => c && c.GetType().Namespace.StartsWith("PuzzleApple.V3")), "V4 UI has no V3 behavior components");
                s.TryAcquireSentence("check.light.move", "light move");
                var movingLamp = VisibleLamp(flow);
                var motion = movingLamp.GetComponent<LightMotion>();
                var before = movingLamp.transform.position;
                yield return new WaitForSecondsRealtime(.5f);
                Require(motion.Moving && Vector3.Distance(before, movingLamp.transform.position) > .1f, "light move travels");
                // Panel ownership must never pause a sentence's world effect.
                foreach (bool open in new[] { false, true, false })
                {
                    flow.board.Panel.SetOpen(open);
                    yield return new WaitForFixedUpdate();
                    before = movingLamp.transform.position;
                    yield return new WaitForSecondsRealtime(.5f);
                    Require(s.HasEffect(CognitionSignal.LightMove) && motion.Moving &&
                        Vector3.Distance(before, movingLamp.transform.position) > .1f,
                        "light continues moving with Tab " + (open ? "open" : "closed"));
                }
                s.TryAcquireSentence("check.world.move", "world move");
                var night = UnityEngine.Object.FindFirstObjectByType<NightEnvironment>();
                Require(night.WorldMoving, "world move active alongside light move");
                s.TryAcquireSentence("check.move.off", "negative light");
                Require(!flow.lamp.IsOn && motion.Moving && night.WorldMoving, "off and both moves coexist");
                s.ClearWorkspace();
                before = movingLamp.transform.position;
                yield return new WaitForSecondsRealtime(.2f);
                Require(!motion.Moving && !night.WorldMoving && Vector3.Distance(before, movingLamp.transform.position) < .01f, "dismantling stops at current position");
                flow.board.Panel.SetOpen(false);
                AimPoint(flow, flow.door.transform.position + Vector3.up * 3);
                yield return null;
                Require(flow.TryCollectDoor(), "floating door can be clicked to learn");
                yield return new WaitForSecondsRealtime(flow.wordDisplayDuration + .2f);
                Require(s.Knows("door") && !flow.TryCollectDoor(), "door word acquired once");
                s.ClearWorkspace();
                s.TryAcquireSentence("check.door.free", "door move");
                var doorMotion = flow.door.GetComponent<LightMotion>();
                var doorBody = flow.door.GetComponent<Rigidbody>();
                var doorStart = flow.door.transform.position;
                doorBody.position = new Vector3(doorStart.x, flow.door.groundHeight+.15f, doorStart.z);
                typeof(LightMotion).GetField("direction", System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(doorMotion, Vector3.down);
                yield return new WaitForSecondsRealtime(.5f);
                Require(doorBody.linearVelocity.y > 0 && !flow.door.LandingCompleted && !GameObject.Find("Door landing ripple"), "free move bounces off real ground without progress ripple");
                doorBody.position=doorStart;
                typeof(LightMotion).GetField("direction", System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(doorMotion, Vector3.right);
                yield return new WaitForSecondsRealtime(.4f);
                Require(doorMotion.Moving && Vector3.Distance(doorStart,flow.door.transform.position)>.1f, "door free move carries entire frame");
                s.TryAcquireSentence("check.door.conflict", "door world move");
                Require(s.Groups.Last().Conflicted && !flow.door.Descending, "earlier free motion wins over lowering");
                var freeGroup = s.Groups.First(g=>g.Signal==CognitionSignal.DoorMove);
                s.ReturnToLibrary(freeGroup.Id,freeGroup.Words[0].Id,true);
                Require(flow.door.Descending && !doorMotion.Moving && flow.door.GetComponent<Rigidbody>().isKinematic, "lowering takes over safely after free move removed");
                s.ClearWorkspace();
                yield return new WaitForSecondsRealtime(.1f);
                doorStart=flow.door.transform.position;
                yield return new WaitForSecondsRealtime(.2f);
                Require(Vector3.Distance(doorStart,flow.door.transform.position)<.01f && !flow.door.LandingCompleted, "free movement stops without landing progress");
                s.TryAcquireSentence("check.door.old", "door move world");
                Require(!s.Groups.Last().Recognized && !flow.door.Descending, "old A move B order invalid");
                s.ClearWorkspace();
                float doorHeight = flow.door.transform.position.y;
                s.TryAcquireSentence("check.door.lower", "door world move");
                var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blocker.name="Check door obstruction";
                blocker.transform.position=flow.door.transform.position+Vector3.down*.3f;
                blocker.transform.localScale=new Vector3(8,.2f,8);
                Physics.SyncTransforms();
                yield return new WaitForSecondsRealtime(1);
                float blockedHeight=flow.door.transform.position.y;
                yield return new WaitForSecondsRealtime(.3f);
                Require(Mathf.Abs(blockedHeight-flow.door.transform.position.y)<.01f && !flow.door.LandingCompleted, "solid obstruction pauses descent without completing progress");
                UnityEngine.Object.Destroy(blocker);
                yield return new WaitForSecondsRealtime(.8f);
                Require(flow.door.Descending && flow.door.transform.position.y < doorHeight - .1f, "A B move lowers frame");
                s.ClearWorkspace();
                yield return new WaitForSecondsRealtime(.1f);
                doorHeight = flow.door.transform.position.y;
                yield return new WaitForSecondsRealtime(.3f);
                Require(Mathf.Abs(flow.door.transform.position.y - doorHeight) < .01f, "removing door move pauses in place");
                s.TryAcquireSentence("check.door.resume", "door world move");
                for (float timeout=0; !flow.door.Landed && timeout<8; timeout+=Time.unscaledDeltaTime) yield return null;
                Require(flow.door.Landed, "rejoining resumes until grounded");
                yield return new WaitForSecondsRealtime(.1f);
                Require(flow.door.LandingCompleted && GameObject.Find("Door landing ripple"), "directed landing commits progress and emits ripple");
                s.ClearWorkspace();
                yield return new WaitForSecondsRealtime(.2f);
                Require(flow.door.Landed, "grounded door stays after removing rule");
                yield return new WaitForSecondsRealtime(flow.door.rippleDuration+.1f);
                Require(!GameObject.Find("Door landing ripple"), "landing ripple fades and is released");
                s.TryAcquireSentence("check.door.repeat", "door world move");
                yield return new WaitForSecondsRealtime(.15f);
                Require(!GameObject.Find("Door landing ripple"), "completed progress does not emit a second ripple");
                s.ClearWorkspace();
                var passage = new Ray(flow.door.transform.position + new Vector3(0,1,-2), Vector3.forward);
                Require(!Physics.Raycast(passage,4,~0,QueryTriggerInteraction.Ignore), "grounded opening has no solid blocker");
                s.TryAcquireSentence("check.preview.invalid", "door move world");
                s.TryAcquireSentence("check.preview.on", "positive light");
                flow.board.Panel.SetOpen(true);
                yield return new WaitForSecondsRealtime(.5f);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot("Temp/V4Checks/" + (lightFirst ? "light-first-panel.png" : "move-first-panel.png"));
                Result = "PASS: " + checks + " assertions (" + (lightFirst ? "light first" : "move first") + ")";
                Debug.Log(Result);
            }
            finally
            {
                if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
                original.editorInputBehaviorInPlayMode = originalEditorInput;
                original.backgroundBehavior = originalBackground;
                Application.runInBackground = background;
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            }
        }
        static void Aim(OpeningFlow flow)
        {
            AimPoint(flow, VisibleLamp(flow).transform.position);
        }
        static OpeningLight VisibleLamp(OpeningFlow flow) => flow.lightField
            ? flow.lightField.GetComponentsInChildren<OpeningLight>().OrderBy(l => (l.transform.position - flow.player.transform.position).sqrMagnitude).First()
            : flow.lamp;
        static void AimPoint(OpeningFlow flow, Vector3 point)
        {
            var direction = point - flow.player.view.position;
            flow.player.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(direction, Vector3.up));
            float pitch = -Mathf.Atan2(direction.y, Vector3.ProjectOnPlane(direction, Vector3.up).magnitude) * Mathf.Rad2Deg;
            typeof(FirstPersonController).GetField("pitch", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(flow.player, pitch);
            flow.player.view.localRotation = Quaternion.Euler(pitch, 0, 0);
            Physics.SyncTransforms();
        }
    }
}
