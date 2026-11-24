using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PuzzleApple.General.Editor
{
    // Project-wide isolation checks. Open version scenes directly from Project.
    public static class PrototypeScenes
    {
        public static readonly string[] Paths = {
            "Assets/Scenes/V1/P1.unity",
            "Assets/Scenes/V2/P2.unity",
            "Assets/Scenes/V3/P3.unity",
            "Assets/Scenes/V4/P4_Systems.unity"
        };

        public static string IsolationReport { get; private set; }

        public static void ValidateIsolation()
        {
            var all = AssetDatabase.GetAllAssetPaths();
            var report = new System.Text.StringBuilder();
            for (int version = 1; version <= 4; version++)
            {
                var marker = "/V" + version + "/";
                if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(Paths[version - 1]))
                    throw new Exception("Missing scene: " + Paths[version - 1]);
                var assets = all.Where(path => path.StartsWith("Assets/") &&
                    path.Contains(marker) && !AssetDatabase.IsValidFolder(path)).ToArray();
                var invalid = AssetDatabase.GetDependencies(assets, true)
                    .Where(path => path.StartsWith("Assets/") && !path.Contains(marker) &&
                        !AssetDatabase.IsValidFolder(path)).Distinct().OrderBy(path => path).ToArray();
                if (invalid.Length > 0)
                    throw new Exception("V" + version + " has external project asset dependencies:\n" +
                        string.Join("\n", invalid));
                report.AppendLine("V" + version + ": " + assets.Length +
                    " imported assets, no other-version or General serialized dependencies.");
            }
            report.Append("Project settings, engine/packages and compilation are project-wide; " +
                "this check does not test dynamic code loading or gameplay.");
            IsolationReport = report.ToString();
            Debug.Log(IsolationReport);
        }
    }
}
