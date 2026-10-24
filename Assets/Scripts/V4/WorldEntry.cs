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
        [Header("In-world exit palette")]
        public NightEnvironment environment;
        public Color dayExitColor = new Color(.39f,.47f,.32f,1);
        public Color dayExitHighlight = new Color(.96f,.99f,.94f,1);
        [Range(0,1)] public float exitSaturation=.35f;
        [Tooltip("Night normal/hover colors inherit the environment's night fog hue, lifted toward white.")]
        [Range(0,1)] public float nightExitLift=.35f, nightHighlightLift=.91f;
        [Tooltip("Downward camera pitch at which the normal symbol starts/finishes becoming the light color.")]
        public Vector2 groundLookAngles = new Vector2(10,35);
        [Min(.1f)] public float exitColorResponse=6;
        bool entering;
        bool entered;

        void Awake()
        {
            if (!player) player = FindFirstObjectByType<FirstPersonController>();
            if (!environment) environment = FindFirstObjectByType<NightEnvironment>();
            enterButton.onClick.AddListener(EnterWorld);
            exitButton.onClick.AddListener(ExitGame);
        }

        void LateUpdate()
        {
            if(!entered||!exitButton||!player)return;
            float night=environment?environment.NightBlend:0;
            Color hue=environment?environment.nightFogColor:new Color(.18f,.22f,.30f);
            Color normal=Color.Lerp(dayExitColor,Color.Lerp(hue,Color.white,nightExitLift),night);
            Color highlight=Color.Lerp(dayExitHighlight,Color.Lerp(hue,Color.white,nightHighlightLift),night);
            normal=Muted(normal);highlight=Muted(highlight);
            float pitch=Mathf.Asin(Mathf.Clamp(-player.view.forward.y,-1,1))*Mathf.Rad2Deg;
            normal=Color.Lerp(normal,highlight,Mathf.SmoothStep(0,1,Mathf.InverseLerp(groundLookAngles.x,groundLookAngles.y,pitch)));
            var colors=exitButton.colors;
            float t=1-Mathf.Exp(-exitColorResponse*Time.unscaledDeltaTime);
            colors.normalColor=Color.Lerp(colors.normalColor,normal,t);
            colors.highlightedColor=colors.selectedColor=Color.Lerp(colors.highlightedColor,highlight,t);
            colors.pressedColor=Color.Lerp(normal,highlight,.65f);
            exitButton.colors=colors;
        }
        Color Muted(Color color)
        {
            float grey=color.grayscale;
            return Color.Lerp(new Color(grey,grey,grey,color.a),color,exitSaturation);
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
            if (blackBackground) blackBackground.gameObject.SetActive(false);
            enterButton.gameObject.SetActive(false);
            exitInk.alpha = 1;
            entered = true;
            var raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster) raycaster.enabled = true;
            if (player) player.enabled = true;
            if (opening) opening.Begin();
        }

        static CanvasGroup FadeGroup(GameObject target)
        {
            var group = target.GetComponent<CanvasGroup>();
            return group ? group : target.AddComponent<CanvasGroup>();
        }

        public void ExitGame()
        {
            if (entering && !entered) return;
            QuitApplication();
        }

        public static void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
