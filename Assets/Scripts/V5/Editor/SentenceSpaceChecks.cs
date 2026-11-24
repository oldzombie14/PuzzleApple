using System;
using System.Linq;
using UnityEngine;
using LegacySpace = PuzzleApple.V5.PassageJourney.LightSpace;

namespace PuzzleApple.V5.Editor
{
    public static class SentenceSpaceChecks
    {
        public static string Result { get; private set; }
        static void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
        }
        static SentenceSpaceSet Set(params SentenceSpace[] spaces) => new SentenceSpaceSet(spaces);

        public static void Run()
        {
            Result = "Running";
            var spaces = Enumerable.Range(0, 40).Select(_ => ScriptableObject.CreateInstance<SentenceSpace>()).ToArray();
            var host = new GameObject("Native sentence space checks");
            try
            {
                // Identical names with independent asset identities; none have a legacy mapping.
                foreach (var space in spaces) space.name = "Unmapped test space";
                var chapter = Set(spaces);
                var start = Set(spaces[0]);
                var pool = new SentenceScopePool();
                var doorA = host.AddComponent<SentenceBoundaryProbe>();
                var doorB = host.AddComponent<SentenceBoundaryProbe>();
                var gateA = host.AddComponent<ProgressLock>();
                var gateB = host.AddComponent<ProgressLock>();
                gateA.SetRegions(Set(spaces[36], spaces[37], spaces[38]), Set(spaces[35]));
                gateB.SetRegions(Set(spaces[39]), Set(spaces[38]));
                gateA.BindBoundary(doorA); gateB.BindBoundary(doorB);
                Func<SentenceSpaceSet> scope = () => pool.Resolve(chapter, start, chapter);
                Check(scope().Count == 36, "initial pool contains 36 independent spaces");
                doorB.SetProgress(1);
                Check(gateB.Unlocked && scope().Count == 36, "later unlock cannot bypass a missing prerequisite");
                doorA.SetProgress(.49f);
                Check(!gateA.Unlocked && scope().Count == 36, "first unlock waits for passable threshold");
                doorA.SetProgress(.5f);
                Check(gateA.Unlocked && scope().SetEquals(chapter), "event immediately unlocks chained regions");
                doorA.SetProgress(0);
                Check(gateA.Unlocked && scope().Count == 36, "closed door disconnects without relocking");
                Check(pool.Resolve(chapter, Set(spaces[39]), chapter).Count == 4, "inside side resolves its own connected component");
                var emptyGate = host.AddComponent<ProgressLock>();
                emptyGate.SetRegions(SentenceSpaceSet.Empty, Set(spaces[0], spaces[39]));
                emptyGate.Unlock();
                Check(scope().Count == 36, "boundary without an accessible inside cannot join its prerequisite regions");
                emptyGate.enabled = false;
                doorA.SetProgress(.01f);
                Check(scope().Count == 40, "unlocked door reconnects below its first-unlock threshold");

                int version = ProgressLock.TopologyVersion, builds = pool.GraphBuilds;
                doorA.SetProgress(.2f);
                int reads = doorA.ProgressReads + doorA.ConnectionReads;
                for (int i = 0; i < 100; i++) Check(scope().Count == 40 && ProgressLock.TopologyVersion == version, "unchanged topology");
                Check(pool.GraphBuilds == builds && reads == doorA.ProgressReads + doorA.ConnectionReads,
                    "stable queries neither poll boundaries nor rebuild graph");

                var registry = new SentenceEntityRegistry();
                object endpoint = new object();
                var ownership = Set(spaces[39]);
                SentenceEntity.Bind(registry, host.transform, endpoint, () => ownership);
                Check(registry.Query<object>(Set(spaces[39])).Single() == endpoint && !registry.Query<object>(Set(spaces[7])).Any(),
                    "registry selects asset identities beyond bit 32 without collisions");
                Check(registry.RegionOf(endpoint).SetEquals(ownership), "native ownership survives lookup");
                Check(pool.Resolve(chapter, start, Set(spaces[0], spaces[39])).Count == 2, "membrane intersection keeps native identities");
                var fill = host.AddComponent<RoomLightBalance>();
                var membrane = host.AddComponent<MembraneDoor>();
                membrane.interior = fill;
                membrane.SetInteriorRegion(Set(spaces[39]));
                Check(MembraneDoor.Region(host.transform.position, chapter).SetEquals(Set(spaces[39])), "inside membrane uses native space asset");
                Check(MembraneDoor.Region(host.transform.position + Vector3.up * 100, chapter).Count == 39, "outside membrane excludes native space asset");

                var replacement = host.AddComponent<SentenceBoundaryProbe>();
                gateA.BindBoundary(replacement);
                Check(scope().Count == 36, "rebinding observes the replacement source immediately");
                version = ProgressLock.TopologyVersion;
                doorA.SetProgress(0); doorA.SetProgress(1);
                Check(ProgressLock.TopologyVersion == version, "rebinding unsubscribes the old source");
                gateA.enabled = false;
                version = ProgressLock.TopologyVersion;
                replacement.SetProgress(1);
                Check(ProgressLock.TopologyVersion == version, "disabled boundary has no event subscription");
                gateA.enabled = true;
                Check(scope().Count == 40, "reenabling observes source progress and reconnects");
                replacement.SetProgress(0);
                Check(scope().Count == 36, "reenabled boundary resumes event updates");

                gateA.SetRegions(Set(spaces[38]), Set(spaces[35]));
                Check(scope().Count == 38, "changing authored regions invalidates graph membership");
                host.SetActive(false);
                version = ProgressLock.TopologyVersion;
                replacement.SetProgress(1); doorB.SetProgress(0);
                Check(ProgressLock.TopologyVersion == version && registry.Count == 0 && scope().Count == 40,
                    "disabled objects unregister both boundaries and endpoints");
                host.SetActive(true);
                Check(registry.Count == 1 && scope().Count == 39, "reactivation restores current membership and closed boundary");

                var catalog = SentenceSpaceCatalog.Default;
                Check(catalog.mappings.Any(m=>m.legacy==LegacySpace.ChapterLesson) &&
                    catalog.mappings.Select(m => m.space).Distinct().Count() == catalog.mappings.Length,
                    "existing spaces retain unique asset identities");
                foreach (var mapping in catalog.mappings)
                    Check(mapping.space && (int)mapping.legacy > 0 && ((int)mapping.legacy & ((int)mapping.legacy - 1)) == 0 &&
                        catalog.ToLegacy(catalog.FromLegacy(mapping.legacy)) == mapping.legacy, "single-bit legacy mapping round trip");
                Check(catalog.FromLegacy(LegacySpace.All).Count == 6, "Intro compatibility union preserves six spaces");
                bool rejected = false;
                try { catalog.ToLegacy(Set(spaces[39])); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "legacy consumers reject unmapped assets instead of silently truncating them");
                Result = "PASS: 40 native spaces, event-driven unlock/connectivity, prerequisite chain, registry identity, cache, rebind/disable/reactivate, asset compatibility";
            }
            catch (Exception exception) { Result = "FAIL: " + exception.Message; Debug.LogException(exception); }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                foreach (var space in spaces) UnityEngine.Object.DestroyImmediate(space);
            }
        }
    }
}
