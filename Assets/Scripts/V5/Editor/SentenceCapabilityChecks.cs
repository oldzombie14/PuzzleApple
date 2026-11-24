using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Space = PuzzleApple.V5.PassageJourney.LightSpace;

namespace PuzzleApple.V5.Editor
{
    public static class SentenceCapabilityChecks
    {
        public static string Result { get; private set; }
        static void Check(bool condition, string label)
        {
            if (condition) return;
            Result = "FAIL: " + label; throw new InvalidOperationException(Result);
        }
        static IEnumerator Wait(Func<bool> ready, string label)
        {
            float end = Time.realtimeSinceStartup + 35;
            while (!ready() && Time.realtimeSinceStartup < end) yield return null;
            Check(ready(), label);
        }
        public static void Run()
        {
            Result = "Running";
            var entry = UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            entry.StartCoroutine(Exercise(entry));
        }
        // Deliberately implements neither legacy light nor door interface.
        sealed class StateProbe
        {
            public int commands, holds;
            public bool running, requested;
            public float value;
            public SentenceStateOutput Output(Component lifetime, SentenceOutputDefinition definition) =>
                new SentenceStateOutput(lifetime, this, definition, () => value, on => value != (on ? 1 : 0),
                    on => running && value != (on ? 1 : 0), on => { requested = on; running = true; commands++; },
                    () => { running = false; holds++; });
            public void Complete() { value = requested ? 1 : 0; running = false; }
        }
        static IEnumerator Exercise(WorldEntry entry)
        {
            var objects = new List<GameObject>();
            try
            {
                entry.OpenWorld(-1);
                yield return Wait(() => entry.room.Opened && !entry.Busy, "open for capability check");
                entry.sentenceRuntime.presentation.enabledShots = false;
                entry.board.State.ClearWorkspace(); entry.board.Panel.SetOpen(false);
                var registry = entry.Entities;
                var catalog = SentenceCapabilityCatalog.Default;
                var j = entry.journey;
                var farSubjects = registry.QueryEntities(Space.FarRoom, catalog.potentialLight).Where(e => e.Endpoint is DriftingLight).ToArray();
                Check(farSubjects.Length == j.Lamps.Count, "every independent room lamp has a subject record");
                Check(farSubjects.All(e => !e.Has(catalog.light)), "dark room lamps retain potential emission without light property");
                var farOutput = registry.QueryOutputs(Space.FarRoom, catalog.potentialLight, catalog.emission).Single();
                Check(farSubjects.All(e => ReferenceEquals(e.Output(catalog.emission), farOutput)), "room lamps share one existing fade output");
                farOutput.Command(true);
                yield return Wait(() => farOutput.Value > .2f && farOutput.Value < .9f, "partial room illumination");
                Check(farSubjects.All(e => e.Has(catalog.light)), "positive brightness gains live light property");
                farOutput.Hold(); float held = farOutput.Value;
                yield return new WaitForSeconds(.15f);
                Check(farOutput.Value == held, "output hold preserves actual partial brightness");
                farOutput.Command(false);
                yield return Wait(() => farOutput.Value == 0, "room output finishes fading");
                Check(farSubjects.All(e => !e.Has(catalog.light) && e.Has(catalog.potentialLight)), "only full darkness removes light property");

                var sphere = registry.QueryEntities(Space.SphereRoom, catalog.potentialLight).Single(e => e.transform == j.Tutorial.Sphere);
                Check(!sphere.Has(catalog.light), "main teaching sphere begins dark");
                j.Tutorial.SetOn(true);
                Check(!sphere.Has(catalog.light), "requested on state alone does not grant light before brightness changes");
                yield return Wait(() => sphere.Has(catalog.light), "sphere light property follows actual fade");
                j.Tutorial.SetOn(false);
                Check(sphere.Has(catalog.light), "fading sphere remains light until fully dark");
                yield return Wait(() => !sphere.Has(catalog.light), "sphere loses light at zero brightness");
                var stars = registry.QueryEntities(Space.FirstPath, catalog.potentialLight).Single(e => ReferenceEquals(e.Endpoint, j.Stars));
                j.Stars.SetOn(false);
                float starDeadline = Time.realtimeSinceStartup + 15;
                while (j.Stars.Settling(false) && Time.realtimeSinceStartup < starDeadline)
                {
                    Check(stars.Has(catalog.light) == (j.Stars.EmissionLevel > 0), "star property follows remaining visible emission during staggered shrink");
                    yield return null;
                }
                Check(!j.Stars.Settling(false) && !stars.Has(catalog.light), "fully vanished stars lose light while retaining their switch output");
                Check(registry.QueryOutputs(Space.FirstPath, catalog.potentialLight).Single().Source == j.Stars, "dark stars remain available to on light");
                Check(registry.QueryOutputs(Space.All, catalog.door).Count(o => o.Definition == catalog.hinge) == 2 &&
                    registry.QueryOutputs(Space.All, catalog.door).Count(o => o.Definition == catalog.lift) == 1,
                    "hinged and lifting doors expose independent output definitions");
                Check(!registry.QueryOutputs(Space.All, catalog.potentialLight).Any(o => o.Source is ISwitchableDoor), "off light cannot claim door state outputs");

                Func<string, GameObject> create = name => { var go = new GameObject(name); objects.Add(go); return go; };
                var controller = create("Shared state controller");
                var first = new StateProbe(); var sharedOutput = first.Output(controller.transform, catalog.emission);
                var firstHost = create("First physical subject");
                var firstEntity = SentenceSceneBindings.BindLightSubject(registry, firstHost.transform, () => Space.Chamber, () => first.value, sharedOutput);
                var command = VitruvianChecks.Say(entry.board, "positive", "light");
                yield return Wait(() => first.commands == 1, "dispatcher drives an output without a legacy controller interface");
                var sibling = create("Joined shared subject");
                SentenceSceneBindings.BindLightSubject(registry, sibling.transform, () => Space.Chamber, () => first.value, sharedOutput);
                yield return null; yield return null;
                Check(first.commands == 1, "new physical subject does not restart its shared output");
                var independent = new StateProbe(); var independentHost = create("Independent output subject");
                var separateOutput = independent.Output(independentHost.transform, catalog.emission);
                SentenceSceneBindings.BindLightSubject(registry, independentHost.transform, () => Space.Chamber, () => independent.value, separateOutput);
                yield return Wait(() => independent.commands == 1, "new independent output joins an executing command");
                firstHost.SetActive(false); yield return null; yield return null;
                Check(first.holds == 0 && first.commands == 1, "removing one shared subject does not stop the remaining subject");
                sibling.SetActive(false); yield return null; yield return null;
                Check(first.holds == 1 && entry.board.State.Find(command.Id) != null, "last shared subject stops only its output");
                firstHost.SetActive(true); sibling.SetActive(true);
                yield return Wait(() => first.commands == 2, "reactivated shared output resumes once");
                Check(independent.commands == 1 && registry.QueryOutputs(Space.Chamber, catalog.potentialLight).Count(o => o == sharedOutput) == 1,
                    "reactivation neither duplicates outputs nor restarts other work");
                first.Complete(); independent.Complete();
                Check(firstEntity.Has(catalog.light), "properties evaluate source state without rebinding");
                yield return Wait(() => entry.board.State.Find(command.Id) == null, "command retires after independent outputs finish");
                var late = new StateProbe(); var lateHost = create("After completion subject");
                SentenceSceneBindings.BindLightSubject(registry, lateHost.transform, () => Space.Chamber, () => late.value, late.Output(lateHost.transform, catalog.emission));
                yield return null; yield return null;
                Check(late.commands == 0, "new output never replays a completed command");
                UnityEngine.Object.Destroy(controller); yield return null;
                Check(!sharedOutput.Alive && !registry.QueryOutputs(Space.Chamber, catalog.potentialLight).Contains(sharedOutput), "destroyed control source cannot be selected by surviving subjects");
                sharedOutput.Command(false); sharedOutput.Hold(); sharedOutput.Release();
                Result = "PASS: per-lamp properties, actual brightness, output values/hold, hinge/lift definitions, shared-output deduplication, dynamic admission, removal/reactivation and completion";
            }
            finally { foreach (var go in objects) if (go) UnityEngine.Object.Destroy(go); }
        }
    }
}
