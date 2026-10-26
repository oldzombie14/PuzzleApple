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
        public bool Exiting { get; private set; }
        public float ExitProgress { get; private set; }
        public bool ConfirmingExit => exitDialog;
        GameObject exitDialog;
        Coroutine dialogFade;
        bool savedLocked, savedBlocked, savedCursorVisible;
        CursorLockMode savedCursorLock;

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
            bool showExit=opening&&opening.board&&opening.board.Panel.IsOpen;
            if(exitButton.gameObject.activeSelf!=showExit)exitButton.gameObject.SetActive(showExit);
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
            if (entering || Exiting || ConfirmingExit) return;
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
            if(entered&&(!opening||!opening.board||!opening.board.Panel.IsOpen))return;
            QuitApplication();
        }

        public static void QuitApplication()
        {
            var entry=FindFirstObjectByType<WorldEntry>();
            if(entry)entry.ShowExitConfirmation();
        }
        void ShowExitConfirmation()
        {
            if(Exiting||ConfirmingExit)return;
            if(opening&&opening.effects&&opening.effects.presentation)opening.effects.presentation.Cancel();
            savedLocked=player&&player.CinematicLocked;
            savedBlocked=opening&&opening.board&&opening.board.Panel.InputBlocked;
            savedCursorLock=Cursor.lockState;savedCursorVisible=Cursor.visible;
            if(player)player.CinematicLocked=true;
            if(opening&&opening.board){opening.board.CancelDrag();opening.board.Panel.InputBlocked=true;}
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            if(entered&&opening&&opening.effects&&opening.effects.inscriptions)
                opening.effects.inscriptions.Record(Cognition.CognitionSignal.WorldOff);

            exitDialog=new GameObject("Exit confirmation",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            exitDialog.transform.SetParent(transform,false);
            var canvas=exitDialog.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=31000;
            var scaler=exitDialog.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var blocker=DialogImage("Input blocker",exitDialog.transform,Color.clear);Stretch(blocker.rectTransform,0);
            var fill=DialogImage("Black panel",blocker.transform,Color.black);
            fill.rectTransform.sizeDelta=new Vector2(520,260);
            DialogButton(fill.transform,"ON","positive",-100,ConfirmExit);
            DialogButton(fill.transform,"OFF","negative",100,CancelExit);
            var ink=fill.gameObject.AddComponent<CanvasGroup>();ink.alpha=0;ink.interactable=false;
            dialogFade=StartCoroutine(FadeInDialog(ink));
            if(UnityEngine.EventSystems.EventSystem.current)UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }
        IEnumerator FadeInDialog(CanvasGroup ink)
        {
            const float duration=.25f;
            for(float elapsed=0;elapsed<duration;elapsed+=Time.unscaledDeltaTime)
            {
                ink.alpha=Mathf.SmoothStep(0,1,elapsed/duration);
                yield return null;
            }
            ink.alpha=1;ink.interactable=true;dialogFade=null;
        }
        static Image DialogImage(string name,Transform parent,Color color)
        {
            var obj=new GameObject(name,typeof(RectTransform),typeof(Image));obj.transform.SetParent(parent,false);
            var image=obj.GetComponent<Image>();image.color=color;return image;
        }
        static void Stretch(RectTransform rect,float inset)
        {
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
            rect.offsetMin=Vector2.one*inset;rect.offsetMax=-Vector2.one*inset;
        }
        void DialogButton(Transform parent,string name,string word,float x,UnityEngine.Events.UnityAction action)
        {
            var hit=DialogImage(name,parent,Color.clear);hit.rectTransform.sizeDelta=new Vector2(160,140);
            hit.rectTransform.anchoredPosition=new Vector2(x,0);
            var symbol=DialogImage("Symbol",hit.transform,Color.white);symbol.rectTransform.sizeDelta=new Vector2(64,64);
            symbol.sprite=opening.board.Catalog.Word(word).Symbol;symbol.preserveAspect=true;symbol.raycastTarget=false;
            var button=hit.gameObject.AddComponent<Button>();button.targetGraphic=symbol;
            var colors=button.colors;colors.highlightedColor=new Color(.65f,.65f,.65f);colors.pressedColor=new Color(.45f,.45f,.45f);button.colors=colors;
            button.navigation=new Navigation{mode=Navigation.Mode.None};button.onClick.AddListener(action);
        }
        public void CancelExit()
        {
            if(!ConfirmingExit||Exiting)return;
            DismissExitDialog();
            if(player){player.CinematicLocked=savedLocked;player.SuppressInteractionThisFrame();}
            if(opening&&opening.board)opening.board.Panel.InputBlocked=savedBlocked;
            Cursor.lockState=savedCursorLock;Cursor.visible=savedCursorVisible;
        }
        public void ConfirmExit()
        {
            if(!ConfirmingExit||Exiting)return;
            DismissExitDialog();StartCoroutine(FadeOutAndQuit(0));
        }
        void DismissExitDialog()
        {
            if(dialogFade!=null){StopCoroutine(dialogFade);dialogFade=null;}
            exitDialog.SetActive(false);Destroy(exitDialog);exitDialog=null;
            if(UnityEngine.EventSystems.EventSystem.current)UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }
        IEnumerator FadeOutAndQuit(float delay)
        {
            Exiting=true;
            if(opening&&opening.effects&&opening.effects.presentation)opening.effects.presentation.Cancel();
            if(player)player.CinematicLocked=true;
            if(opening&&opening.board)opening.board.Panel.InputBlocked=true;
            if(enterButton)enterButton.interactable=false;
            if(exitButton)exitButton.interactable=false;
            if(delay>0)yield return new WaitForSecondsRealtime(delay);
            var screen=new GameObject("Exit fade",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));
            var canvas=screen.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32000;
            var cover=new GameObject("Black",typeof(RectTransform),typeof(Image));cover.transform.SetParent(screen.transform,false);
            var rect=cover.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var image=cover.GetComponent<Image>();image.color=Color.clear;
            for(float t=0;t<blackFadeDuration;t+=Time.unscaledDeltaTime)
            {
                ExitProgress=Mathf.SmoothStep(0,1,t/Mathf.Max(.001f,blackFadeDuration));image.color=new Color(0,0,0,ExitProgress);
                yield return null;
            }
            ExitProgress=1;image.color=Color.black;
            yield return new WaitForEndOfFrame();
            CloseApplication();
        }
        static void CloseApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
