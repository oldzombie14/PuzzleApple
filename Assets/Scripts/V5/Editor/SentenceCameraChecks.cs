using System;
using System.Collections;
using System.Linq;
using PuzzleApple.V5.Cognition;
using UnityEngine;
using Space = PuzzleApple.V5.PassageJourney.LightSpace;

namespace PuzzleApple.V5.Editor
{
    public static class SentenceCameraChecks
    {
        public static string Result { get; private set; }
        static void Check(bool ok, string label) { if (ok) return; Result = "FAIL: " + label; throw new InvalidOperationException(Result); }
        static IEnumerator Wait(Func<bool> done, string label)
        {
            float end = Time.realtimeSinceStartup + 35;
            while (!done() && Time.realtimeSinceStartup < end) yield return null;
            Check(done(), label);
        }
        public static void Run()
        {
            Result = "Running";
            var e = UnityEngine.Object.FindFirstObjectByType<WorldEntry>(); e.StartCoroutine(Exercise(e));
        }
        static CognitionState.Group Probe(WorldEntry e, string words)
        {
            var state = new CognitionState(e.board.Catalog);
            state.TryAcquireSentence("camera.probe", words, out var group);
            return group;
        }
        static SentenceSpaceSet Region(Space space) => SentenceSpaceCatalog.Default.FromLegacy(space);
        static void Point(Vector3? actual, Vector3 expected, string label) => Check(actual.HasValue && Vector3.Distance(actual.Value, expected) < .001f, label);

        static IEnumerator Registration(WorldEntry e)
        {
            var registry = new SentenceEntityRegistry(); var group = Probe(e, "light world");
            var a = new GameObject("Camera registration probe A"); var b = new GameObject("Camera registration probe B");
            bool changes = true; var space = Region(Space.Chamber);
            try
            {
                var first = SentenceFocusSource.Bind(registry, a.transform, () => space, g => changes ? Vector3.right : (Vector3?)null);
                var second = SentenceFocusSource.Bind(registry, b.transform, () => Region(Space.Chamber), g => Vector3.up);
                Func<Vector3?> resolve = () => SentenceCameraDirector.Resolve(registry, group, Region(Space.Chamber), Region(Space.All));
                Point(resolve(), Vector3.right, "stable first eligible request");
                var again = SentenceFocusSource.Bind(registry, a.transform, () => space, g => changes ? Vector3.right : (Vector3?)null);
                Check(again == first && registry.Count == 2, "rebinding does not duplicate source");
                // Rebinding may reorder registrations, so test eligibility independently of ties.
                second.enabled = false;
                Point(resolve(), Vector3.right, "enabled registration participates");
                changes = false; Check(!resolve().HasValue, "no actual change keeps view"); changes = true;
                space = Region(Space.FarRoom); Check(!resolve().HasValue, "remote request cannot turn camera");
                space = Region(Space.Chamber);
                Check(!SentenceCameraDirector.Resolve(registry, group, space, Region(Space.FarRoom)).HasValue, "effect scope excludes local hint");
                a.SetActive(false); Check(!resolve().HasValue, "inactive mechanism unregisters"); a.SetActive(true);
                UnityEngine.Object.Destroy(a); yield return null;
                Check(!resolve().HasValue, "destroyed mechanism cannot leave a stale request");
                second.enabled = true; Point(resolve(), Vector3.up, "remaining request takes over");
            }
            finally { if (a) UnityEngine.Object.Destroy(a); UnityEngine.Object.Destroy(b); }
        }

        static IEnumerator Exercise(WorldEntry e)
        {
            Check(e.board.State.RequirePeriod, "formal period design");
            e.OpenWorld(-1); yield return Wait(() => e.room.Opened && !e.Busy, "open Intro");
            yield return Registration(e);
            var b = e.board; var show = e.sentenceRuntime.presentation; var j = e.journey; var p = e.player;
            p.MovementLocked = true;
            var registration = e.chamberLight.GetComponents<SentenceEntity>().Single(x => x.Endpoint is SentenceFocusSource);
            try
            {
                e.chamberLight.SetOn(true); yield return Wait(() => e.chamberLight.Brightness == 1, "initial light");
                b.Panel.SetOpen(true); yield return Wait(() => b.Panel.FullyOpen, "open editor");
                var command = PeriodChecks.Draft(b, "negative", "light");
                Check(!show.Playing, "unspoken draft does not play");
                p.view.rotation = Quaternion.Euler(8, p.view.eulerAngles.y + 65, 3); p.AdoptCurrentView();
                b.State.Speak(command.Id); yield return Wait(() => show.Playing, "period starts presentation");
                var start = p.view.rotation; var position = p.view.position;
                Point(show.FocusTarget(command), e.chamberLight.surface.bounds.center, "real changing chamber lamp requests focus");
                yield return Wait(() => show.CanInterrupt, "apply after camera enter");
                var angles = start.eulerAngles; var now = p.view.eulerAngles;
                Check(Quaternion.Angle(start, p.view.rotation) > .1f, "key mechanism nudges camera");
                Check(Mathf.Abs(Mathf.DeltaAngle(angles.x, now.x)) <= 10.01f && Mathf.Abs(Mathf.DeltaAngle(angles.y, now.y)) <= 15.01f, "bounded pitch and yaw");
                Check(Mathf.Abs(Mathf.DeltaAngle(angles.z, now.z)) < .01f && Vector3.Distance(position, p.view.position) < .001f, "focus preserves roll and position");
                yield return Wait(() => !show.Playing && e.chamberLight.Brightness == 0, "normal completion");
                Check(!p.CinematicLocked && !b.Panel.InputBlocked && !b.Panel.IsOpen, "normal completion restores gameplay");

                b.Panel.SetOpen(true); yield return Wait(() => b.Panel.FullyOpen, "reopen editor");
                command = VitruvianChecks.Say(b, "positive", "light");
                yield return Wait(() => show.Playing, "start light-on shot");
                Point(show.FocusTarget(command), e.chamberLight.surface.bounds.center, "candidate exists during feedback");
                registration.enabled = false; // Disappears during feedback, before the director resolves.
                start = p.view.rotation; position = p.view.position;
                yield return Wait(() => show.CanInterrupt, "no focus still applies sentence");
                Check(Quaternion.Angle(start, p.view.rotation) < .01f && Vector3.Distance(position, p.view.position) < .001f, "missing source keeps view");
                Check(e.sentenceRuntime.HasEffect(CognitionSignal.LightOn), "camera registration does not narrow execution");
                Check(show.InterruptForEditing() && b.Panel.IsOpen && !p.CinematicLocked && !b.Panel.InputBlocked, "interrupt restores editor");
                registration.enabled = true;
                yield return Wait(() => e.chamberLight.Brightness == 1, "effect continues after interruption");

                var all = Region(Space.All | Space.Outdoor | Space.Gallery | Space.Garden | Space.Vitruvian);
                foreach (var words in new[] { "negative door", "light move door", "light world", "light light", "light door", "light floor" })
                    foreach (Space room in new[] { Space.Chamber, Space.FirstPath, Space.SphereRoom, Space.ReturnPath, Space.FarRoom, Space.Ascent, Space.Outdoor, Space.Gallery })
                        Check(!SentenceCameraDirector.Resolve(e.Entities, Probe(e, words), Region(room), all).HasValue, "current-view policy: " + words + " / " + room);
                var onDoor = Probe(e, "positive door");
                Point(SentenceCameraDirector.Resolve(e.Entities, onDoor, Region(Space.FirstPath), all), j.Door.position, "white-door hint");
                Check(!SentenceCameraDirector.Resolve(e.Entities,onDoor,Region(Space.ApproachPath),all).HasValue,"sensor lift gives no manual open hint");
                Check(!SentenceCameraDirector.Resolve(e.Entities, onDoor, Region(Space.FarRoom), all).HasValue, "unpowered charge door offers no hint");
                Point(SentenceCameraDirector.Resolve(e.Entities, Probe(e, "floor move"), Region(Space.ReturnPath), all), j.Platform.Deck.bounds.center, "platform hint");
                yield return ChargeDoorChecks.UnlockFirstPassage(e);
                j.SetDoor(true);
                yield return Wait(()=>j.DoorProgress==1,"unlock platform");
                b.Panel.SetOpen(true);
                yield return Wait(()=>b.Panel.FullyOpen,"visible panel for remote-only movement");
                int strong=e.feedback.StrongCount;
                var remote=VitruvianChecks.Say(b,"floor","move");
                yield return Wait(()=>j.Platform.MotionOutput.HasMotion(SentenceMotion.Floor),"remote platform starts moving");
                var word=b.GroupRect(remote.Id).GetComponentInChildren<CenteredSymbolImage>();
                yield return Wait(()=>Mathf.Abs(word.canvasRenderer.GetAlpha()-.62f)<.01f,"remote words use middle opacity");
                Check(b.Panel.IsOpen && !show.Playing && e.feedback.StrongCount==strong+1 &&
                    e.sentenceRuntime.SustainsFeedback(remote),"remote platform gives stars without a cutscene");
                p.Teleport(j.Platform.Deck.bounds.center+Vector3.up*.4f,j.Root.rotation*Quaternion.Euler(0,65,0));
                yield return Wait(()=>j.PlayerSpace==Space.ReturnPath && word.canvasRenderer.GetAlpha()>.99f,"enter platform space and brighten the running sentence");
                Check(!show.Playing && remote.Spoken,"crossing refreshes feedback without another presentation");
                b.State.Unsay(remote.Id);
                yield return null;
                p.view.rotation=j.Root.rotation*Quaternion.Euler(-5,65,2);
                p.AdoptCurrentView();
                VitruvianChecks.Say(b,"floor","move");
                yield return Wait(()=>show.Playing,"platform presentation starts");
                angles=p.view.eulerAngles;
                yield return Wait(()=>show.CanInterrupt,"platform presentation aims");
                now=p.view.eulerAngles;
                Check(Mathf.Abs(Mathf.DeltaAngle(angles.y,now.y))<.01f && Mathf.Abs(Mathf.DeltaAngle(angles.z,now.z))<.01f &&
                    Mathf.Abs(Mathf.DeltaAngle(angles.x,now.x)-14)<.05f,"platform lowers pitch by 14 degrees while holding yaw and roll");
                Result = "PASS: registered local focus, live eligibility and lifetime, bounded real period-triggered camera, scope-independent execution, current-view fallback, completion and interruption";
            }
            finally { registration.enabled = true; show.Cancel(); p.MovementLocked = false; }
        }
    }
}
