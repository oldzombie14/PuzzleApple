using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Space = PuzzleApple.V5.PassageJourney.LightSpace;

namespace PuzzleApple.V5.Editor
{
    // Compare identities, not just counts, while the existing gameplay checks drive real gates
    // and scene transitions. The legacy selectors stay here as an independent migration oracle.
    public static class SentenceQueryChecks
    {
        public static string Result { get; private set; }
        static void Check(bool value, string label)
        {
            if (value) return;
            Result = "FAIL: " + label;
            throw new InvalidOperationException(Result);
        }

        public static void RunConnectivity() => Begin(ConnectivityChecks.Run, () => ConnectivityChecks.Result);
        public static void RunScope() => Begin(SentenceScopeChecks.Run, () => SentenceScopeChecks.Result);
        public static void RunReveal() => Begin(SentenceScopeChecks.RunFirstPassage, () => SentenceScopeChecks.Result);
        public static void RunOutdoor() => Begin(OutdoorChecks.RunCompletion, () => OutdoorChecks.Result);
        public static void RunCharge() => Begin(ChargeDoorChecks.Run, () => ChargeDoorChecks.Result);
        public static void RunDrops() => Begin(WorldDropChecks.Run, () => WorldDropChecks.Result);
        public static void RunDoors() => Begin(RoomThreeChecks.RunGlobalDoors, () => RoomThreeChecks.Result);
        public static void RunAdmission()
        {
            Result = "Running";
            var entry = UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            entry.StartCoroutine(CommandAdmission(entry));
        }

        sealed class CommandProbe : ISwitchableLight
        {
            bool value, requested, running;
            public int Commands { get; private set; }
            public bool WouldChange(bool on) => value != on;
            public bool Settling(bool on) => running && WouldChange(on);
            public void SetOn(bool on) { requested = on; running = true; Commands++; }
            public void Hold() { running = false; }
            public void Complete() { value = requested; running = false; }
        }

        static IEnumerator Wait(Func<bool> done, string label)
        {
            float end = Time.realtimeSinceStartup + 35;
            while (!done() && Time.realtimeSinceStartup < end) yield return null;
            Check(done(), label);
        }

        static IEnumerator CommandAdmission(WorldEntry entry)
        {
            var hosts = new List<GameObject>();
            try
            {
                entry.OpenWorld(-1);
                yield return Wait(() => entry.room.Opened && !entry.Busy, "open for admission check");
                entry.sentenceRuntime.presentation.enabledShots = false;
                entry.board.State.ClearWorkspace();
                entry.board.Panel.SetOpen(false);
                var first = new CommandProbe();
                var firstHost = new GameObject("Initial command participant"); hosts.Add(firstHost);
                SentenceSceneBindings.BindLight(entry.Entities, firstHost.transform, first, () => Space.Chamber, () => 0);
                var command = VitruvianChecks.Say(entry.board, "positive", "light");
                yield return Wait(() => first.Commands == 1, "initial registered target receives command");

                var joined = new CommandProbe();
                var joinedHost = new GameObject("Mid-command participant"); hosts.Add(joinedHost);
                SentenceSceneBindings.BindLight(entry.Entities, joinedHost.transform, joined, () => Space.Chamber, () => 0);
                yield return Wait(() => joined.Commands == 1, "new in-scope target joins executing command");
                Check(first.Commands == 1, "adding target does not restart original target");

                var outside = new CommandProbe();
                var outsideHost = new GameObject("Locked-region participant"); hosts.Add(outsideHost);
                SentenceSceneBindings.BindLight(entry.Entities, outsideHost.transform, outside, () => Space.FirstPath, () => 0);
                var reveal = UnityEngine.Object.FindFirstObjectByType<WallPassage>().GetComponentsInChildren<ProgressLock>().Single(g => g.BoundarySource is WallPassage);
                reveal.Unlock();
                yield return null; yield return null;
                Check((entry.journey.SentenceScope() & Space.FirstPath) != 0 && outside.Commands == 0,
                    "unlock expands live scope without recruiting a new region into the running command");

                UnityEngine.Object.Destroy(joinedHost);
                yield return null; yield return null;
                Check(entry.board.State.Find(command.Id) != null, "destroyed target does not finish another target's work");
                first.Complete();
                yield return Wait(() => entry.board.State.Find(command.Id) == null, "remaining target completes and command retires");

                var late = new CommandProbe();
                var lateHost = new GameObject("After-command participant"); hosts.Add(lateHost);
                SentenceSceneBindings.BindLight(entry.Entities, lateHost.transform, late, () => Space.Chamber, () => 0);
                yield return null; yield return null;
                Check(late.Commands == 0 && first.Commands == 1, "completed command does not replay for new targets");
                Result = "PASS: dynamic command admission, no restart, fixed spatial limit, target removal and no post-completion replay";
            }
            finally { foreach (var host in hosts) if (host) UnityEngine.Object.Destroy(host); }
        }

        static void Begin(Action run, Func<string> status)
        {
            Result = "Running";
            var entry = UnityEngine.Object.FindFirstObjectByType<WorldEntry>();
            entry.StartCoroutine(Exercise(entry, run, status));
        }

        static IEnumerator Exercise(WorldEntry entry, Action run, Func<string> status)
        {
            yield return RegistryLifecycle();
            run();
            int samples = 0;
            while (true)
            {
                Compare(entry);
                samples++;
                string current = status();
                Check(current == null || !current.StartsWith("FAIL"), current);
                if (current != null && current.StartsWith("PASS")) break;
                yield return null;
            }
            var pool = entry.journey.ScopePool;
            var scope = entry.journey.SentenceScope();
            int builds = pool.GraphBuilds, resolutions = pool.Resolutions;
            for (int i = 0; i < 100; i++) Check(entry.journey.SentenceScope() == scope, "stable cached scope");
            Check(builds == pool.GraphBuilds && resolutions == pool.Resolutions, "stable queries do not repeat graph work");
            Result = "PASS: entity lifecycle, old/new target identity and scope parity over " + samples +
                " frames; cached scope; " + status();
        }

        sealed class Probe { }
        static IEnumerator RegistryLifecycle()
        {
            var registry = new SentenceEntityRegistry();
            var go = new GameObject("Sentence registry lifecycle check");
            var endpoint = new Probe();
            var spaces = Space.Gallery;
            try
            {
                var entity = SentenceEntity.Bind(registry, go.transform, endpoint, () => spaces);
                Check(registry.Query<Probe>(Space.Gallery).Single() == endpoint, "created endpoint joins immediately");
                Check(SentenceEntity.Bind(registry, go.transform, endpoint, () => spaces) == entity && registry.Count == 1,
                    "repeat binding does not duplicate registration");
                go.transform.position = Vector3.one * 100;
                Check(registry.SpacesOf(endpoint) == Space.Gallery, "physical movement does not rewrite control ownership");
                spaces = Space.Garden;
                Check(!registry.Query<Probe>(Space.Gallery).Any() && registry.Query<Probe>(Space.Garden).Single() == endpoint,
                    "explicit ownership changes are queried live");
                go.SetActive(false);
                Check(registry.Count == 0, "disabled objects unregister");
                go.SetActive(true);
                Check(registry.Count == 1, "reactivated objects register once");
                UnityEngine.Object.Destroy(go);
                yield return null;
                Check(registry.Count == 0, "destroyed objects unregister");
            }
            finally { if (go) UnityEngine.Object.Destroy(go); }
        }

        static void Compare(WorldEntry entry)
        {
            var j = entry.journey;
            var actual = j.SentenceScope();
            var expected = MembraneDoor.Region(entry.player.transform.position, j.ChapterSpaces) &
                ProgressLock.Region(j.ChapterSpaces, j.PlayerSpace);
            Check(actual == expected, "scope parity: " + actual + " versus " + expected);
            CompareTargets(entry, actual);
            // Also cover registered targets behind closed progress boundaries.
            CompareTargets(entry, j.ChapterSpaces);
        }

        static void CompareTargets(WorldEntry entry, Space scope)
        {
            var oldLights = new HashSet<ISwitchableLight>(LegacyLights(entry, scope).Where(x => x != null));
            var newLights = entry.Entities.Query<ISwitchableLight>(scope).ToArray();
            Check(newLights.Length == oldLights.Count && oldLights.SetEquals(newLights), "light target identities in " + scope);
            var catalog = SentenceCapabilityCatalog.Default;
            var lightOutputs = entry.Entities.QueryOutputs(scope, catalog.potentialLight, catalog.emission).ToArray();
            Check(lightOutputs.Length == oldLights.Count && oldLights.SetEquals(lightOutputs.Select(output => output.Source as ISwitchableLight)), "emission output identities in " + scope);
            var oldDoors = new HashSet<ISwitchableDoor>(LegacyDoors(entry, scope).Where(x => x != null));
            var newDoors = entry.Entities.Query<ISwitchableDoor>(scope).ToArray();
            Check(newDoors.Length == oldDoors.Count && oldDoors.SetEquals(newDoors), "door target identities in " + scope);
            var doorOutputs = entry.Entities.QueryOutputs(scope, catalog.door).ToArray();
            Check(doorOutputs.Length == oldDoors.Count && oldDoors.SetEquals(doorOutputs.Select(output => output.Source as ISwitchableDoor)), "door output identities in " + scope);
            CheckField(entry.outdoor.World ? entry.outdoor.World.field : null);
            CheckField(entry.outdoor.Vitruvian && entry.outdoor.Vitruvian.puzzle ? entry.outdoor.Vitruvian.puzzle.field : null);
        }

        static void CheckField(Outdoor.OpeningLightField field)
        {
            if (!field) return;
            var catalog = SentenceCapabilityCatalog.Default;
            foreach (var lamp in field.ActiveLamps)
            {
                if (!lamp || !lamp.gameObject.activeInHierarchy) continue;
                var records = lamp.GetComponents<SentenceEntity>();
                Check(records.Length == 1 && records[0].Has(catalog.potentialLight), "created light has exactly one potential-emitter record");
                Check(records[0].Has(catalog.light) == (lamp.Brightness * lamp.RevealAmount > 0), "created light property follows actual brightness and reveal");
            }
        }

        static IEnumerable<ISwitchableLight> LegacyLights(WorldEntry e, Space scope)
        {
            var p = e.outdoor;
            if ((scope & Space.Outdoor) != 0) yield return p.World;
            if ((scope & Space.Gallery) != 0) yield return p.Chapter;
            if ((scope & Space.Vitruvian) != 0) { yield return p.Vitruvian; yield return p.Vitruvian.puzzle; }
            if ((scope & Space.Garden) != 0) yield return p.Garden;
            foreach (var light in e.journey.Lights(scope)) yield return light;
        }

        static IEnumerable<ISwitchableDoor> LegacyDoors(WorldEntry e, Space scope)
        {
            var j = e.journey;
            if ((scope & Space.Outdoor) != 0) yield return e.outdoor.World;
            if ((scope & j.WhiteDoorSpaces) != 0) yield return j.WhiteDoor;
            if (j.EntranceDoor && (scope & j.EntranceDoor.ControlSpaces) != 0) yield return j.EntranceDoor;
            if (j.EndingDoor && (scope & j.EndingDoor.ControlSpaces) != 0) yield return j.EndingDoor;
        }
    }
}
