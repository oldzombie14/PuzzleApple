using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PuzzleApple.V4
{
    // The content stays inactive until the shared session is ready.
    [DefaultExecutionOrder(-10000)]
    public sealed class SceneEntry : MonoBehaviour
    {
        public GameObject content;
        public Transform arrival;
        public bool intro;
        [Tooltip("Eye height for this scene; zero restores the player default.")]
        public float eyeHeight;
        [Tooltip("Optional scene-specific camera presentation; null restores the session default.")]
        public Material pictureMaterialOverride;
        [Tooltip("Vocabulary IDs removed on leaving this scene. Empty until a level defines temporary words.")]
        public string[] temporaryWords = new string[0];
        static bool starting;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetBootstrap() => starting = false;
        void Awake() { if(content)content.SetActive(false); }
        IEnumerator Start()
        {
            if (FindFirstObjectByType<SessionSceneManager>() || starting) yield break;
            starting = true;
            yield return SceneManager.LoadSceneAsync("P4_Systems", LoadSceneMode.Additive);
            starting = false;
        }
    }
}
