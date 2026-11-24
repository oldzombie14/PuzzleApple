using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using PuzzleApple.V5.Cognition;
using UnityEditor;
using UnityEngine;

namespace PuzzleApple.V5.Editor
{
    // Read-only export. Does not enter Play, change assets or save scenes.
    public static class WorkbenchSnapshot
    {
        static object Reference(UnityEngine.Object asset) => new {
            key = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset)),
            name = asset.name, path = AssetDatabase.GetAssetPath(asset)
        };
        static object Intent(SentenceGrammar.IntentPattern intent) => new {
            action = intent.action.ToString(), subject = intent.subject, target = intent.target,
            kind = intent.kind.ToString(), scope = intent.scope.ToString()
        };
        public static string Export()
        {
            const string directory = "Assets/Docs/Workbench";
            var catalog = AssetDatabase.LoadAssetAtPath<CognitionCatalog>("Assets/GameData/V5/Opening/OpeningCatalog.asset");
            var errors = catalog.Validate();
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            var grammar = catalog.Grammar;
            var paths = Directory.GetFiles("Assets/Scripts/V5", "*.cs", SearchOption.AllDirectories)
                .Where(p => !p.Replace('\\', '/').Contains("/Editor/"))
                .Concat(Directory.GetFiles("Assets/GameData/V5", "*.asset", SearchOption.AllDirectories))
                .Concat(catalog.Words.Select(w => AssetDatabase.GetAssetPath(w.Symbol))).Distinct().OrderBy(p => p).ToArray();
            object[] assets<T>() where T : UnityEngine.Object => AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/GameData/V5" })
                .Select(AssetDatabase.GUIDToAssetPath).Select(p => Reference(AssetDatabase.LoadAssetAtPath<T>(p))).ToArray();
            var snapshot = new {
                format = "puzzleapple-v5-implementation", schemaVersion = 1, capturedAt = DateTime.UtcNow.ToString("o"),
                unityVersion = Application.unityVersion,
                words = catalog.Words.Select(w => new { key = w.Id, name = w.DisplayText, classes = (int)w.Classes, symbol = AssetDatabase.GetAssetPath(w.Symbol), asset = Reference(w) }),
                grammars = grammar.templates.Select(t => new { key = t.id, slots = t.slots.Select(s => new { classes = (int)s.classes, words = s.words.Select(w => w.Id) }), intent = Intent(t.intent) }),
                exceptions = grammar.exceptions.Select(e => new { key = e.id, words = e.words.Select(w => w.Id), reject = e.reject, intent = Intent(e.intent) }),
                rules = catalog.Rules.Select(r => new { key = r.Id, words = r.Words.Select(w => w.Id), kind = r.Kind.ToString(), channel = r.Channel, effects = r.Effects.Select(e => e.ToString()), deferred = grammar.legacyRules.Contains(r), asset = Reference(r) }),
                properties = assets<SentenceProperty>(), outputs = assets<SentenceOutputDefinition>(), spaces = assets<SentenceSpace>(),
                presentation = AssetDatabase.LoadAssetAtPath<SentencePresentationSettings>("Assets/GameData/V5/Resources/V5/SentencePresentation.asset").timing,
                combinations = SentenceGrammarDiagnostics.BuildRows(catalog),
                sources = paths.Select(p => { using (var sha = SHA256.Create()) return new { path = p.Replace('\\', '/'), sha256 = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p))).Replace("-", "").ToLowerInvariant() }; })
            };
            Directory.CreateDirectory(directory);
            var output = directory + "/implementation.json";
            File.WriteAllText(output, JsonConvert.SerializeObject(snapshot, Formatting.Indented) + "\n");
            return output;
        }
    }
}
