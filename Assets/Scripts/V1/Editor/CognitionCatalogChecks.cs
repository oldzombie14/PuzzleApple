using System;
using System.Collections.Generic;
using System.Linq;
using PuzzleApple.Cognition;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PuzzleApple.Editor
{
    public static class CognitionCatalogChecks
    {
        [MenuItem("PuzzleApple/Cognition/Check SO catalog")]
        public static void Run()
        {
            int checks = 0;
            void Require(bool value, string message) { if (!value) throw new Exception(message); checks++; }
            var source = CognitionCatalogEditor.LoadTutorial();
            var originals = new Object[] { source }.Concat(source.Words).Concat(source.Rules).Concat(source.Conflicts).ToArray();
            var before = originals.Select(EditorJsonUtility.ToJson).ToArray();
            var temporary = new List<Object>();
            T Copy<T>(T original) where T : Object { var copy = Object.Instantiate(original); temporary.Add(copy); return copy; }
            try
            {
                var catalog = Copy(source);
                var words = source.Words.Select(Copy).ToArray();
                var rules = source.Rules.Select(Copy).ToArray();
                var conflicts = source.Conflicts.Select(Copy).ToArray();
                var map = words.ToDictionary(w => w.Id);
                foreach (var rule in rules) References(rule, "words", rule.Words.Select(w => map[w.Id]).ToArray());
                References(catalog, "words", words); References(catalog, "rules", rules); References(catalog, "conflicts", conflicts);
                Require(catalog.Validate().Count == 0, "copied catalog valid");
                Set(map["i"], "displayText", "SELF");
                var state = new CognitionState(catalog);
                state.TryAcquireSentence("localized", "I move");
                Require(state.PlayerMoving && state.Groups[0].Words[0].Text == "SELF", "display text comes from SO while semantic matching remains stable");
                Require(state.Groups[0].RuleId == "rule.player.move", "recognized groups expose stable authored rule ID");
                state.TryAcquireSentence("negative", "I no move");
                Require(!state.PlayerMoving && !state.PlayerMovementBlocked, "catalog conflict suppresses both effects");
                References(catalog, "conflicts", new Object[0]);
                var withoutConflict = new CognitionState(catalog);
                withoutConflict.TryAcquireSentence("a", "I move"); withoutConflict.TryAcquireSentence("b", "I no move");
                Require(withoutConflict.PlayerMoving && withoutConflict.PlayerMovementBlocked, "conflict definitions are read from catalog, not hardcoded");
                Require(!state.PlayerMoving && !state.PlayerMovementBlocked, "existing session retains its compiled rules");
                var moving = rules.Single(r => r.Effect == CognitionSignal.PlayerMove);
                References(moving, "words", new[] {map["no"], map["apple"]});
                Set(moving, "effect", (int)CognitionSignal.Open);
                var changed = new CognitionState(catalog);
                changed.TryAcquireSentence("new", "no apple"); changed.TryAcquireSentence("old", "I move");
                Require(changed.CanOpen && changed.Groups[0].Recognized && !changed.Groups[1].Recognized,
                    "new SO pattern and effect replace the previous rule without code changes");
                Require(new CognitionState(catalog).Groups.Count == 0 && !new CognitionState(catalog).HasAcquired("new"), "runtime groups and sources remain session local");
                bool rejected = false;
                try { changed.TryAcquireSentence("bad", "I unknown"); } catch (ArgumentException) { rejected = true; }
                Require(rejected && !changed.HasAcquired("bad") && changed.Groups.Count == 2, "unknown acquisition is rejected atomically");
                Require(changed.TryAcquireSentence("bad", "I open door"), "failed source may retry with valid data");
                Set(map["no"], "id", "i");
                Require(catalog.Validate().Any(e => e.Contains("duplicate ID")), "duplicate IDs detected"); Set(map["no"], "id", "no");
                References(moving, "words", new[] {map["i"], map["open"], map["door"]});
                Require(catalog.Validate().Any(e => e.Contains("duplicate sentence")), "duplicate patterns detected");
                References(moving, "words", new WordDefinition[] {map["i"], null});
                Require(catalog.Validate().Any(e => e.Contains("missing or outside")), "missing word references detected");
                References(moving, "words", new[] {map["no"], map["apple"]});
                Set(moving, "effect", 999);
                Require(catalog.Validate().Any(e => e.Contains("unknown effect")), "invalid effect detected");
                Set(moving, "effect", (int)CognitionSignal.PlayerMove);
                References(catalog, "conflicts", conflicts);
                Set(conflicts[0], "second", (int)CognitionSignal.PlayerMove);
                Require(catalog.Validate().Any(e => e.Contains("two different")), "self conflict detected");
                Set(conflicts[0], "second", (int)CognitionSignal.PlayerNoMove);
                Require(catalog.Validate().Count == 0, "restored catalog validates");
            }
            finally { foreach (var item in temporary) Object.DestroyImmediate(item); }
            Require(originals.Select(EditorJsonUtility.ToJson).SequenceEqual(before), "checks and gameplay do not write production SO assets");
            Debug.Log("PASS: " + checks + " SO catalog assertions");
        }
        static void References(Object target, string field, Object[] values)
        {
            var serialized = new SerializedObject(target); var list = serialized.FindProperty(field); list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        static void Set(Object target, string field, object value)
        {
            var serialized = new SerializedObject(target); var property = serialized.FindProperty(field);
            if (value is string text) property.stringValue = text; else property.intValue = (int)value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
