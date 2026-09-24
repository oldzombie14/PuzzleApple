using System.Collections;
using PuzzleApple.Cognition;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PuzzleApple
{
    [DefaultExecutionOrder(100)]
    public sealed class CognitionWorldInteraction : MonoBehaviour
    {
        [SerializeField] CognitionBoard board;
        [SerializeField] FirstPersonController player;
        [SerializeField] Camera view;
        [SerializeField] Sprite eyeSprite;
        [SerializeField] Sprite forbiddenSprite;
        [SerializeField] Material reticleContrastMaterial;
        [SerializeField, Min(.2f)] float interactionDistance = 2.8f;
        [SerializeField, Min(8)] float reticleSize = 38;
        [SerializeField, Min(1)] float reticleThickness = 6;
        [SerializeField, Min(40)] float eyeImageSize = 140;
        [SerializeField, Min(8)] float forbiddenImageSize = 38;
        RectTransform hud;
        Image eye, forbidden;
        Material forbiddenMaterial;
        GameObject crosshair;
        Text collection, question;
        bool presenting, firstSentenceCollected;
        OpeningTutorial tutorial;
        public bool IsPresenting => presenting;
        public CognitionApple HoveredApple { get; private set; }
        public CognitionInteractable HoveredTarget { get; private set; }

        void Start()
        {
            tutorial = FindFirstObjectByType<OpeningTutorial>();
            hud = CognitionUI.Rect("Cognition HUD", board.transform);
            CognitionUI.Stretch(hud);
            var cross = CognitionUI.Rect("Temporary X reticle", hud); crosshair = cross.gameObject;
            Center(cross, reticleSize,reticleSize);
            foreach (float angle in new[] {45f,-45f})
            {
                var bar = CognitionUI.Image("Reticle stroke", cross,Color.white);
                bar.material = reticleContrastMaterial;
                Center(bar.rectTransform,reticleSize,reticleThickness); bar.rectTransform.localRotation = Quaternion.Euler(0,0,angle);
            }
            eye = CognitionUI.Image("Collect eye", hud, Color.white); eye.sprite = eyeSprite; eye.preserveAspect = true;
            eye.material = reticleContrastMaterial;
            // Preserve the PNG's transparency. Its generous margins make the visible eye about 1/3 of this size.
            Center(eye.rectTransform,eyeImageSize,eyeImageSize); eye.gameObject.SetActive(false);
            forbidden = CognitionUI.Image("Collected apple forbidden", hud, Color.white);
            forbidden.sprite = forbiddenSprite; forbidden.preserveAspect = true;
            if (reticleContrastMaterial)
            {
                forbiddenMaterial = new Material(reticleContrastMaterial);
                // The supplied icon contains faint diagonal background pixels (alpha 15/255).
                forbiddenMaterial.SetFloat("_AlphaCutoff",.1f);
                forbidden.material = forbiddenMaterial;
            }
            Center(forbidden.rectTransform,forbiddenImageSize,forbiddenImageSize);
            forbidden.gameObject.SetActive(false);
            question = CognitionUI.Text("Interaction question", hud, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), "?", 46, Color.white);
            Center(question.rectTransform, 60, 60); question.alignment = TextAnchor.MiddleCenter;
            question.material = reticleContrastMaterial;
            question.gameObject.SetActive(false);
            collection = CognitionUI.Text("Collected sentence", hud, board.ChalkFont,"",64,new Color(.94f,.95f,.89f));
            Center(collection.rectTransform,900,110); collection.alignment = TextAnchor.MiddleCenter;
            var shadow = collection.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0,0,0,.8f); shadow.effectDistance = new Vector2(2,-2);
            collection.gameObject.SetActive(false);
        }
        static void Center(RectTransform r,float width,float height)
        { r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f,.5f); r.anchoredPosition = Vector2.zero; r.sizeDelta = new Vector2(width,height); }
        void Update()
        {
            if (!hud) return;
            bool available = !presenting && player.CanInteractWithWorld && Cursor.lockState == CursorLockMode.Locked;
            HoveredApple = null;
            HoveredTarget = null;
            if (available && Physics.Raycast(view.ViewportPointToRay(new Vector3(.5f,.5f)), out var hit, interactionDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                HoveredTarget = hit.collider.GetComponentInParent<CognitionInteractable>();
                if (HoveredTarget is CognitionApple apple && !apple.Collected) HoveredApple = apple;
            }
            bool mirrorHover = available && tutorial && tutorial.isActiveAndEnabled && tutorial.MirrorInSight;
            eye.gameObject.SetActive((HoveredTarget && HoveredTarget.Hover == CognitionHover.Collect) || mirrorHover);
            forbidden.gameObject.SetActive(HoveredTarget && HoveredTarget.Hover == CognitionHover.Forbidden);
            question.gameObject.SetActive(HoveredTarget && HoveredTarget.Hover == CognitionHover.Question);
            crosshair.SetActive(available && !HoveredTarget && !mirrorHover);
            if (HoveredTarget && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                HoveredTarget.Interact(this);
        }
        public void Collect(CognitionApple apple)
        {
            if (presenting || !player.CanInteractWithWorld || !apple || !apple.TryCollect()) return;
            StartCoroutine(PresentSentence("I no consume apple"));
        }
        IEnumerator PresentSentence(string text)
        {
            presenting = true;
            board.Panel.InputBlocked = true;
            player.SetPresentationLocked(true);
            yield return AnimateCollection(text);
            board.State.TryAcquireSentence("tutorial.apple.sentence", text);
            player.SetPresentationLocked(false);
            board.Panel.InputBlocked = false;
            if (!firstSentenceCollected) { firstSentenceCollected = true; board.Panel.SetOpen(true); }
            presenting = false;
        }
        public void Consume(CognitionApple apple)
        {
            if (presenting || !player.CanInteractWithWorld || !apple || !apple.TryBeginConsume()) return;
            StartCoroutine(ConsumeRoutine(apple));
        }
        IEnumerator ConsumeRoutine(CognitionApple apple)
        {
            presenting = true; board.Panel.InputBlocked = true; player.SetPresentationLocked(true);
            try { yield return apple.ConsumeAnimation(view, player); }
            finally { presenting = false; board.Panel.InputBlocked = false; player.SetPresentationLocked(false); }
        }
        public void LearnFromKey(CognitionKey key)
        {
            if (presenting || !player.CanInteractWithWorld || !key || key.Hover != CognitionHover.Question) return;
            StartCoroutine(LearnWord(CognitionKey.SourceId, "open"));
        }
        public void LearnFromDoor(CognitionDoor door)
        {
            if (presenting || !player.CanInteractWithWorld || !door || door.Hover != CognitionHover.Collect) return;
            StartCoroutine(LearnWord(CognitionDoor.SourceId, "door"));
        }
        IEnumerator LearnWord(string sourceId, string wordId)
        {
            board.Panel.InputBlocked = true; player.SetPresentationLocked(true);
            try { yield return PresentWord(sourceId, board.Catalog.Word(wordId)); }
            finally { board.Panel.InputBlocked = false; player.SetPresentationLocked(false); }
        }
        // Callers own any camera/input locks. Walking can award the word as the overlay starts.
        public IEnumerator PresentWord(string sourceId, WordDefinition word, bool acquireOnStart = false)
        {
            if (presenting || board.State.HasAcquired(sourceId)) yield break;
            presenting = true;
            if (acquireOnStart) board.State.TryAcquireWord(sourceId, word);
            yield return AnimateCollection(word.DisplayText);
            if (!acquireOnStart) board.State.TryAcquireWord(sourceId, word);
            presenting = false;
        }
        IEnumerator AnimateCollection(string text)
        {
            eye.gameObject.SetActive(false); forbidden.gameObject.SetActive(false); question.gameObject.SetActive(false); crosshair.SetActive(false);
            collection.text = text; collection.gameObject.SetActive(true);
            var rect = collection.rectTransform;
            rect.anchoredPosition = Vector2.zero;
            float elapsed = 0;
            while (elapsed < 1.25f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/.6f));
                rect.localScale = Vector3.one * Mathf.Lerp(.18f,1,t);
                collection.color = new Color(.94f,.95f,.89f,Mathf.Clamp01(elapsed/.2f));
                yield return null;
            }
            elapsed = 0;
            Vector2 destination = new Vector2(-hud.rect.width*.5f + 96,-hud.rect.height*.5f + 60);
            while (elapsed < .65f)
            {
                elapsed += Time.unscaledDeltaTime; float t = Mathf.SmoothStep(0,1,elapsed/.65f);
                rect.anchoredPosition = Vector2.Lerp(Vector2.zero,destination,t);
                rect.localScale = Vector3.one*Mathf.Lerp(1,.13f,t);
                yield return null;
            }
            collection.gameObject.SetActive(false);
        }
        void OnDisable()
        {
            StopAllCoroutines();
            if (presenting)
            { if (player) player.SetPresentationLocked(false); if (board) board.Panel.InputBlocked = false; }
            presenting = false;
            if (hud) hud.gameObject.SetActive(false);
        }
        void OnEnable() { if (hud) hud.gameObject.SetActive(true); }
        void OnDestroy() { if (forbiddenMaterial) Destroy(forbiddenMaterial); }
    }
}
