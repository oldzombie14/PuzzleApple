using UnityEngine;

namespace PuzzleApple
{
    public static class FrameRateSettings
    {
        // Runs on every Play entry, including when domain reload is disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply()
        {
            // Desktop VSync overrides targetFrameRate and follows the monitor refresh rate.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
        }
    }
}
