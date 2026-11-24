using System;
using System.Collections;
using System.Linq;
using PuzzleApple.V5.Cognition;
using UnityEditor;
using UnityEngine;

namespace PuzzleApple.V5.Editor
{
    public static class SentenceGrammarChecks
    {
        public static string Result { get; private set; }
        static void Check(bool ok, string label) { if (ok) return; Result = "FAIL: " + label; throw new InvalidOperationException(Result); }
        static void SetGrammar(CognitionCatalog catalog, SentenceGrammar grammar)
        {
            var data = new SerializedObject(catalog); data.FindProperty("grammar").objectReferenceValue = grammar; data.ApplyModifiedPropertiesWithoutUndo();
        }
        static CognitionState.Group Draft(CognitionState state, WordDefinition[] words)
        {
            foreach (var word in words) if (!state.Knows(word.Id)) state.TryAcquireWord("grammar." + word.Id, word);
            var group = state.Spawn(words[0]);
            foreach (var word in words.Skip(1)) state.Join(state.Spawn(word).Id, group.Id, false);
            return group;
        }
        static string Snapshot(CognitionState state) => string.Join("|", state.Groups.Select(g =>
            $"{g.RuleId}:{g.Recognized}:{g.Spoken}:{g.Effective}:{g.Conflicted}:{g.Established}:{g.RejectedAttempts}:{string.Join(",", g.Signals)}")) + " / " +
            string.Join("|", state.History.Select(h => SentenceGrammarResolver.Key(h.Words) + ":" + h.Effective));
        static void Same(CognitionState a, CognitionState b, string label) => Check(Snapshot(a) == Snapshot(b), label);

        public static string Verify()
        {
            Result = "Running";
            var catalog = AssetDatabase.LoadAssetAtPath<CognitionCatalog>("Assets/GameData/V5/Opening/OpeningCatalog.asset");
            catalog.ValidateOrThrow(); Check(catalog.Grammar, "production catalog uses grammar database");
            var baseline = UnityEngine.Object.Instantiate(catalog); SetGrammar(baseline, null);
            var edited = UnityEngine.Object.Instantiate(catalog); var editedGrammar = UnityEngine.Object.Instantiate(catalog.Grammar); SetGrammar(edited, editedGrammar);
            var foreign = UnityEngine.Object.Instantiate(catalog.Word("light"));
            try
            {
                var parser = new SentenceGrammarResolver(catalog); int combinations = 0;
                for (int length = 1; length <= 4; length++)
                    foreach (var words in SentenceGrammarDiagnostics.Combinations(catalog.Words, length))
                    {
                        var expected = catalog.Rules.SingleOrDefault(r => r.Words.SequenceEqual(words)); var parsed = parser.Resolve(words);
                        Check(parsed.Rule == expected && parsed.Recognized == (bool)expected, "syntax parity: " + SentenceGrammarResolver.Key(words));
                        combinations++;
                    }
                Check(!parser.Resolve(new[] { catalog.Word("positive"), foreign }).Recognized, "foreign asset with same ID cannot substitute for registered word");
                foreach (var rule in catalog.Rules)
                {
                    var a = new CognitionState(baseline, true); var b = new CognitionState(catalog, true);
                    var ga = Draft(a, rule.Words.ToArray()); var gb = Draft(b, rule.Words.ToArray()); Same(a, b, "draft " + rule.Id);
                    Check(gb.Intent != null && !gb.Effective && b.History.Count == 0, "draft has intent without effect/history");
                    a.Speak(ga.Id); b.Speak(gb.Id); Same(a, b, "speak " + rule.Id);
                    a.Unsay(ga.Id); b.Unsay(gb.Id); Same(a, b, "pause " + rule.Id);
                    a.Speak(ga.Id); b.Speak(gb.Id);
                    var da = a.Detach(ga.Id, ga.Words.Last().Id); var db = b.Detach(gb.Id, gb.Words.Last().Id); Same(a, b, "split " + rule.Id);
                    Check(b.Groups.Where(g => !g.Recognized).All(g => g.Intent == null), "split clears stale intent");
                    if (rule.Kind != SentenceKind.Sustained)
                    {
                        Check(ga.ExecutionLocked && gb.ExecutionLocked && da == null && db == null, "all one-shots reject splitting " + rule.Id);
                        var ids = gb.Words.Select(w => w.Id).ToArray();
                        var extra = b.Spawn(gb.Words[0].Definition);
                        b.Swap(gb.Id, ids[0], ids.Last());
                        b.Join(extra.Id, gb.Id, false);
                        b.Join(gb.Id, extra.Id, false);
                        b.ReturnToLibrary(gb.Id, ids[0], false);
                        b.ReturnToLibrary(gb.Id, 0, true);
                        b.ClearWorkspace();
                        Check(b.Groups.Single() == gb && gb.Effective && gb.Words.Select(w => w.Id).SequenceEqual(ids),
                            "running command survives every workspace edit " + rule.Id);
                        continue;
                    }
                    a.Join(da.Id, ga.Id, false); b.Join(db.Id, gb.Id, false); a.Speak(ga.Id); b.Speak(gb.Id); Same(a, b, "rejoin " + rule.Id);
                    a.Swap(ga.Id, ga.Words.First().Id, ga.Words.Last().Id); b.Swap(gb.Id, gb.Words.First().Id, gb.Words.Last().Id); Same(a, b, "reorder " + rule.Id);
                }
                foreach (var first in catalog.Rules) foreach (var second in catalog.Rules)
                {
                    var a = new CognitionState(baseline, true); var b = new CognitionState(catalog, true);
                    var ga = Draft(a, first.Words.ToArray()); var gb = Draft(b, first.Words.ToArray()); a.Speak(ga.Id); b.Speak(gb.Id);
                    var nextA = Draft(a, second.Words.ToArray()); var nextB = Draft(b, second.Words.ToArray()); a.Speak(nextA.Id); b.Speak(nextB.Id);
                    Same(a, b, "ordered pair " + first.Id + "/" + second.Id);
                    a.Unsay(ga.Id); b.Unsay(gb.Id); Same(a, b, "takeover " + first.Id + "/" + second.Id);
                }
                var world = parser.Resolve(new[] { catalog.Word("positive"), catalog.Word("world") }).Intent;
                Check(world.Action == SentenceAction.On && world.Subject == catalog.Word("world") && world.Kind == SentenceKind.WorldFlow && world.Scope == SentenceScopePolicy.Global, "world-flow override");
                var directed = parser.Resolve(new[] { catalog.Word("door"), catalog.Word("move"), catalog.Word("light") }).Intent;
                Check(directed.Action == SentenceAction.Move && directed.Subject == catalog.Word("door") && directed.Target == catalog.Word("light") && directed.Kind == SentenceKind.Command && directed.Scope == SentenceScopePolicy.CommandRegion, "weak/no-op outcome remains valid directed grammar");
                var freeze = parser.Resolve(new[] { catalog.Word("negative"), catalog.Word("move") }).Intent;
                Check(freeze.Action == SentenceAction.Freeze && freeze.Scope == SentenceScopePolicy.Global, "freeze exception");
                var rows = SentenceGrammarDiagnostics.BuildRows(catalog);
                Check(rows.Count == 576 && rows.Count(r => r.accepted) == catalog.Rules.Count, "preview includes every two/three-word combination");
                var templates = editedGrammar.templates;
                editedGrammar.templates = templates.Concat(new[] { templates[2] }).ToArray();
                Check(edited.Validate().Any(), "duplicate template rejected");
                editedGrammar.templates = templates.Concat(new[] { new SentenceGrammar.Template { id = "overlapping.move", slots = templates[2].slots, intent = templates[2].intent } }).ToArray();
                Check(edited.Validate().Any(e => e.Contains("Ambiguous grammar")), "overlapping templates rejected");
                editedGrammar.templates = templates.Where(t => t.id != "move.free").ToArray();
                Check(edited.Validate().Any(), "missing binding syntax rejected");
                editedGrammar.templates = templates;
                var exceptions = editedGrammar.exceptions;
                editedGrammar.exceptions = exceptions.Where(x => x.id != "unsupported.door.to.door").ToArray();
                Check(edited.Validate().Any(e => e.Contains("Missing execution binding")), "unsupported expansion fails validation");
                editedGrammar.exceptions = exceptions;
                Result = "PASS: " + combinations + " combinations, formal-period drafts/speech/pause/split/reorder, all ordered conflict pairs, intent metadata, deferred mirror parity, preview and invalid asset guards";
                return Result;
            }
            finally { UnityEngine.Object.DestroyImmediate(baseline); UnityEngine.Object.DestroyImmediate(edited); UnityEngine.Object.DestroyImmediate(editedGrammar); UnityEngine.Object.DestroyImmediate(foreign); }
        }

        static IEnumerator Wait(Func<bool> done, string label)
        {
            float end = Time.realtimeSinceStartup + 35;
            while (!done() && Time.realtimeSinceStartup < end) yield return null;
            Check(done(), label);
        }
        public static void RunInteraction()
        {
            Result = "Running"; var e = UnityEngine.Object.FindFirstObjectByType<WorldEntry>(); e.StartCoroutine(Interaction(e));
        }
        static IEnumerator Interaction(WorldEntry e)
        {
            var board = e.board; var state = board.State; Check(state.RequirePeriod, "formal period only");
            board.Panel.SetOpen(true); yield return Wait(() => board.Panel.FullyOpen, "open editor");
            var open = PeriodChecks.Draft(board, "positive", "world"); yield return null;
            Check(open.Intent.Kind == SentenceKind.WorldFlow && !open.Spoken && state.History.Count == 0, "world draft classified but unapplied");
            PeriodChecks.Click(board, open.Id); yield return Wait(() => e.room.Opened && !e.Busy, "clicked world flow opens box");
            var show = e.sentenceRuntime.presentation; int shots = show.PlayCount;
            board.Panel.SetOpen(true); yield return Wait(() => board.Panel.FullyOpen, "reopen editor");
            var invalid = PeriodChecks.Draft(board, "light", "positive"); yield return null;
            PeriodChecks.Click(board, invalid.Id); yield return null;
            Check(!invalid.Recognized && invalid.Intent == null && invalid.RejectedAttempts == 1 && state.History.Any(h => !h.Effective) && show.PlayCount == shots, "bad word order records rejection without effect/shot");
            state.ClearWorkspace();
            var light = PeriodChecks.Draft(board, "negative", "light"); yield return null;
            Check(light.Intent.Action == SentenceAction.Off && !light.Effective, "light draft produces intent without applying");
            PeriodChecks.Click(board, light.Id);
            yield return Wait(() => show.Playing && show.CanInterrupt, "clicked intent reaches deferred effect");
            Check(show.InterruptForEditing(), "Tab returns to editing");
            yield return Wait(() => e.chamberLight.Brightness == 0 && state.Find(light.Id) == null, "command completes and retires");
            var floor = PeriodChecks.Draft(board, "light", "floor"); yield return null; PeriodChecks.Click(board, floor.Id);
            yield return Wait(() => show.Playing && show.CanInterrupt, "sustained intent plays"); show.InterruptForEditing();
            PeriodChecks.Click(board, floor.Id, true); yield return null;
            Check(!floor.Spoken && floor.Recognized && floor.Intent.Action == SentenceAction.Illuminate && !e.sentenceRuntime.HasEffect(CognitionSignal.IlluminateFloor), "right-click period pauses retained sentence and effect");
            Result = "PASS: actual period clicks, world flow, rejected syntax/history, deferred command and retirement, sustained illumination and period-only pause";
        }
    }
}
