using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PuzzleApple.General.Editor
{
    public static class PrototypeScenes
    {
        public static readonly string[] Paths = {
            "Assets/Scenes/V1/TutorialLevel.unity",
            "Assets/Scenes/V2/SymbolLanguage.unity",
            "Assets/Scenes/V3/SymbolSandbox.unity"
        };

        [MenuItem("PuzzleApple/Prototypes/Open V1 - English")]
        static void OpenV1() => Open(0);
        [MenuItem("PuzzleApple/Prototypes/Open V2 - Symbols")]
        static void OpenV2() => Open(1);
        [MenuItem("PuzzleApple/Prototypes/Open V3 - Sandbox")]
        static void OpenV3() => Open(2);
        static void Open(int index)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(Paths[index]);
        }

        [MenuItem("PuzzleApple/Prototypes/Validate asset isolation")]
        public static void ValidateIsolation()
        {
            for (int i = 0; i < Paths.Length; i++)
            {
                if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(Paths[i])) throw new Exception("Missing scene: " + Paths[i]);
                var invalid = AssetDatabase.GetDependencies(Paths[i], true)
                    .Where(path => Enumerable.Range(1, 3).Any(v => v != i + 1 && path.Contains("/V" + v + "/"))).ToArray();
                if (invalid.Length > 0) throw new Exception(Paths[i] + " depends on another prototype:\n" + string.Join("\n", invalid));
            }
            Debug.Log("PASS: all three scenes have independent prototype dependencies.");
        }
    }
}
