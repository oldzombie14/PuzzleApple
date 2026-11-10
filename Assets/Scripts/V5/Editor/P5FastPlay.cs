using UnityEditor;
using UnityEngine.SceneManagement;

namespace PuzzleApple.V5.Editor
{
    // P5 has no mutable static gameplay state. Keep scene reload so every run is fresh.
    [InitializeOnLoad]
    public static class P5FastPlay
    {
        const string Key = "PuzzleApple.P5FastPlay.";
        static double started;
        public static double LastStartupSeconds { get; private set; }

        static P5FastPlay()
        {
            EditorApplication.playModeStateChanged += Changed;
            EditorApplication.quitting += Restore;
        }

        static void Changed(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode &&
                SceneManager.sceneCount == 1 &&
                SceneManager.GetActiveScene().path == "Assets/Scenes/V5/P5.unity")
            {
                started = EditorApplication.timeSinceStartup;
                SessionState.SetBool(Key + "Enabled", EditorSettings.enterPlayModeOptionsEnabled);
                SessionState.SetInt(Key + "Options", (int)EditorSettings.enterPlayModeOptions);
                SessionState.SetBool(Key + "Active", true);
                EditorSettings.enterPlayModeOptionsEnabled = true;
                EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            }
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key + "Active", false))
                LastStartupSeconds = EditorApplication.timeSinceStartup - started;
            if (state == PlayModeStateChange.EnteredEditMode) Restore();
        }

        static void Restore()
        {
            if (!SessionState.GetBool(Key + "Active", false)) return;
            EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Key + "Enabled", false);
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(Key + "Options", 0);
            SessionState.SetBool(Key + "Active", false);
        }
    }
}
