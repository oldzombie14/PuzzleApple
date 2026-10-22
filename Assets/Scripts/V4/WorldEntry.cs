using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace PuzzleApple.V4
{
    // Apple denotes World in V4; this does not change the older prototypes' vocabulary.
    public sealed class WorldEntry : MonoBehaviour
    {
        public FirstPersonController player;
        public OpeningFlow opening;
        public Button enterButton;
        public Button exitButton;
        public Image blackBackground;
        [Min(0)] public float symbolFadeDuration = .4f;
        [Min(0)] public float blackFadeDuration = .8f;
        bool entering;

        void Awake()
        {
            if (!player) player = FindFirstObjectByType<FirstPersonController>();
            enterButton.onClick.AddListener(EnterWorld);
            exitButton.onClick.AddListener(ExitGame);
        }

        void Start()
        {
            if (player) player.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void EnterWorld()
        {
            if (entering) return;
            entering = true;
            Cursor.visible = false;
            var raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster) raycaster.enabled = false;
            if (UnityEngine.EventSystems.EventSystem.current)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            StartCoroutine(RevealWorld());
        }

        IEnumerator RevealWorld()
        {
            var enterInk = FadeGroup(enterButton.gameObject);
            var exitInk = FadeGroup(exitButton.gameObject);
            float elapsed = 0;
            while (elapsed < symbolFadeDuration)
            {
                float alpha = 1-Mathf.SmoothStep(0,1,elapsed/symbolFadeDuration);
                enterInk.alpha = exitInk.alpha = alpha;
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            enterInk.alpha = exitInk.alpha = 0;
            elapsed = 0;
            while (blackBackground && elapsed < blackFadeDuration)
            {
                blackBackground.color = new Color(0,0,0,1-Mathf.SmoothStep(0,1,elapsed/blackFadeDuration));
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            if (blackBackground) blackBackground.color = Color.clear;
            if (player) player.enabled = true;
            if (opening) opening.Begin();
            gameObject.SetActive(false);
        }

        static CanvasGroup FadeGroup(GameObject target)
        {
            var group = target.GetComponent<CanvasGroup>();
            return group ? group : target.AddComponent<CanvasGroup>();
        }

        public void ExitGame()
        {
            if (entering) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
