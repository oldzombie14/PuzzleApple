using System;
using System.Linq;
using PuzzleApple.V5.Cognition;
using UnityEditor;
using UnityEngine;

namespace PuzzleApple.V5.Editor
{
    public static class SentenceIntentChecks
    {
        static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        static void Set(UnityEngine.Object asset, string field, UnityEngine.Object value)
        {
            var data = new SerializedObject(asset); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo();
        }
        public static string Verify()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CognitionCatalog>("Assets/GameData/V5/Opening/OpeningCatalog.asset");
            var capabilities = SentenceCapabilityCatalog.Default;
            var parser = new SentenceGrammarResolver(catalog);
            foreach (var rule in catalog.Rules)
            {
                var parsed = parser.Resolve(rule.Words);
                Check(parsed.Recognized && parsed.Signal == rule.Effect && parsed.Effects.SequenceEqual(rule.Effects), "Compatibility changed: " + rule.Id);
            }
            var on = parser.Resolve(new[] { catalog.Word("positive"), catalog.Word("light") }).Intent;
            Check(on.SubjectProperty == capabilities.light && on.StateProperty == capabilities.potentialLight, "Dark emitters must remain state-command targets");
            var moving = parser.Resolve(new[] { catalog.Word("light"), catalog.Word("move"), catalog.Word("door") }).Intent;
            Check(moving.Moves(capabilities.light, capabilities.door) && !moving.Moves(capabilities.door, capabilities.light), "Subject/target identity or order lost");
            var alias = UnityEngine.Object.Instantiate(catalog.Word("light"));
            var changed = UnityEngine.Object.Instantiate(catalog);
            var originalRule = catalog.Rules.Single(r => r.Words.SequenceEqual(new[] { catalog.Word("light"), catalog.Word("move") }));
            var staleRule = UnityEngine.Object.Instantiate(originalRule);
            try
            {
                var text = new SerializedObject(alias); text.FindProperty("id").stringValue = "renamed-light";
                text.FindProperty("displayText").stringValue = "Different spelling"; text.ApplyModifiedPropertiesWithoutUndo();
                var intent = new SentenceIntent(SentenceAction.Move, alias, catalog.Word("door"), SentenceKind.Command, SentenceScopePolicy.CurrentRegion);
                Check(SentenceLegacySignalBridge.TryResolve(intent, originalRule, out var translated) && translated.Single() == CognitionSignal.LightDoorMove, "Semantic identity depends on spelling");
                Set(alias, "property", null);
                Check(intent.SubjectProperty == capabilities.light, "An applied intent must retain its parsed semantic identity");
                var missing = new SentenceIntent(SentenceAction.Move, alias, null, SentenceKind.Sustained, SentenceScopePolicy.CurrentRegion);
                Check(!SentenceLegacySignalBridge.TryResolve(missing, originalRule, out _), "Missing semantics silently fall back to rule effects");
                var ruleData = new SerializedObject(staleRule); ruleData.FindProperty("effect").intValue = (int)CognitionSignal.DoorMove;
                ruleData.FindProperty("channel").stringValue = "different.channel"; ruleData.ApplyModifiedPropertiesWithoutUndo();
                var data = new SerializedObject(changed); var rules = data.FindProperty("rules");
                for (int i = 0; i < rules.arraySize; i++) if (rules.GetArrayElementAtIndex(i).objectReferenceValue == originalRule) rules.GetArrayElementAtIndex(i).objectReferenceValue = staleRule;
                data.ApplyModifiedPropertiesWithoutUndo();
                var parsed = new SentenceGrammarResolver(changed).Resolve(originalRule.Words);
                Check(parsed.Signal == CognitionSignal.LightMove && parsed.Intent.Moves(capabilities.light), "Stale binding overrode native intent");

                // Pure transient groups: executors must route by meaning even with absent legacy metadata.
                var state = new CognitionState(catalog, true);
                state.TryAcquireSentence("intent.light", "positive light", out var light);
                state.TryAcquireSentence("intent.move", "light move", out var motion);
                state.TryAcquireSentence("intent.glow", "light floor", out var glow);
                foreach (var group in new[] { light, motion, glow })
                {
                    typeof(CognitionState.Group).GetProperty("Signal").SetValue(group, CognitionSignal.Empty);
                    typeof(CognitionState.Group).GetProperty("Signals").SetValue(group, Array.Empty<CognitionSignal>());
                }
                Check(LightStateEffect.Commands(light, true) && !DoorStateEffect.Handles(light), "State executor still reads a signal");
                Check(MotionEffect.Handles(motion) && MotionEffect.MovesLamps(motion), "Motion executor still reads a rule channel");
                Check(IlluminationEffects.Handles(glow), "Illumination executor still reads a signal");
                return "PASS: all execution bindings preserved; property identities, dark-emitter switch targets, ordered targets, immutable parse semantics, stale-binding resistance and native executor routing";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(alias); UnityEngine.Object.DestroyImmediate(changed); UnityEngine.Object.DestroyImmediate(staleRule);
            }
        }
    }
}
