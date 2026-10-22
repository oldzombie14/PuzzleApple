using System.Collections;
using PuzzleApple.V4.Cognition;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PuzzleApple.V4
{
    [DefaultExecutionOrder(300)]
    public sealed class OpeningFlow : MonoBehaviour
    {
        public FirstPersonController player;
        public CognitionBoard board;
        public WordLibrary library;
        public OpeningLight lamp;
        public ThresholdDoor door;
        public Image collectionSymbol, hoverEye;
        public GameObject reticle;
        public RectTransform hud;
        [Min(.1f)] public float interactionDistance = 7;
        public float wordDisplayDuration = 1.6f, wordFadeInDuration = .3f, wordFlyDuration = .6f;
        public bool Ready { get; private set; }
        public bool Collecting => collecting != null;
        public bool MovementDiscoveryPending => Ready && !board.State.Knows("move") && player.WalkedDistance > .005f;
        public bool CanInteract => Ready && player.CanInteractWithWorld && !MovementDiscoveryPending && !Collecting;
        string collecting;
        Material reticleMaterial;
        Camera worldCamera;
        string lastLightRule;
        void Start()
        {
            board.Panel.InputBlocked = true;
            collectionSymbol.gameObject.SetActive(false);
            hoverEye.gameObject.SetActive(false); reticle.SetActive(false);
            worldCamera = player.view.GetComponent<Camera>();
            reticleMaterial = new Material(hoverEye.material);
            hoverEye.material = reticleMaterial;
            foreach (var image in reticle.GetComponentsInChildren<Image>(true)) image.material = reticleMaterial;
            board.State.Changed += ApplySentences;
        }
        void LateUpdate()
        {
            // V4 presents a render texture through an overlay and a second display camera.
            // Bind its world image locally so later cameras cannot replace the contrast input.
            if (!reticleMaterial || !worldCamera || !worldCamera.targetTexture) return;
            reticleMaterial.SetTexture("_ReticleSceneColor", worldCamera.targetTexture);
            reticleMaterial.SetVector("_ReticleViewportSize", new Vector4(Screen.width, Screen.height, 0, 0));
        }
        public void Begin()
        {
            if (Ready) return;
            foreach (var id in new[] { "positive", "negative", "world" })
            {
                library.Remember(id);
                board.State.TryAcquireWord("opening." + id, board.Catalog.Word(id));
            }
            Ready = true; board.Panel.InputBlocked = false;
        }
        void Update()
        {
            if (!Ready) return;
            if (Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame && !library.NoteField.isFocused)
            {
                bool on = !lamp.IsOn;
                foreach (var light in FindObjectsByType<OpeningLight>(FindObjectsSortMode.None)) light.SetOn(on);
            }
            if (player.WalkedDistance >= player.learnMoveDistance && !board.State.Knows("move") && !Collecting) Learn("move");
            string targetWord = CanInteract ? LookedAtWord() : null;
            bool target = targetWord != null;
            hoverEye.gameObject.SetActive(target);
            reticle.SetActive(player.CanInteractWithWorld && !target && !Collecting);
            if (target && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) Learn(targetWord);
        }
        string LookedAtWord()
        {
            float reach = door ? Mathf.Max(interactionDistance, door.collectionDistance) : interactionDistance;
            if (!Physics.Raycast(player.view.position, player.view.forward, out var hit, reach, ~0, QueryTriggerInteraction.Collide)) return null;
            if (hit.distance <= interactionDistance && hit.collider.GetComponentInParent<OpeningLight>() && !board.State.Knows("light")) return "light";
            if (door && hit.distance <= door.collectionDistance && hit.collider.GetComponentInParent<ThresholdDoor>() == door && !board.State.Knows("door")) return "door";
            return null;
        }
        public bool TryCollectLight()
        {
            if (!CanInteract || LookedAtWord() != "light") return false;
            Learn("light"); return true;
        }
        public bool TryCollectDoor()
        {
            if (!CanInteract || LookedAtWord() != "door") return false;
            Learn("door"); return true;
        }
        void Learn(string id)
        {
            collecting = id;
            if (id != "move") player.MovementLocked = true;
            library.Remember(id);
            StartCoroutine(Collect(board.Catalog.Word(id)));
        }
        IEnumerator Collect(WordDefinition word)
        {
            var rect = collectionSymbol.rectTransform;
            collectionSymbol.sprite = word.Symbol; rect.anchoredPosition = Vector2.zero; rect.localScale = Vector3.one;
            collectionSymbol.gameObject.SetActive(true);
            for (float t = 0; t < wordDisplayDuration; t += Time.unscaledDeltaTime)
            {
                float fly = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(wordDisplayDuration - wordFlyDuration, wordDisplayDuration, t));
                float reveal = Mathf.SmoothStep(0, 1, t / Mathf.Max(.01f, wordFadeInDuration));
                collectionSymbol.color = new Color(1, 1, 1, reveal * (1 - fly * .7f));
                rect.localScale = Vector3.one * Mathf.Lerp(Mathf.Lerp(.9f, 1, reveal), .4f, fly);
                rect.anchoredPosition = Vector2.Lerp(Vector2.zero, new Vector2(-hud.rect.width * .42f, -hud.rect.height * .4f), fly);
                yield return null;
            }
            collectionSymbol.gameObject.SetActive(false);
            board.State.TryAcquireWord("opening." + word.Id, word);
            collecting = null; player.MovementLocked = false;
        }
        void ApplySentences(CognitionChange change)
        {
            if (door) door.SetDescending(board.State.HasEffect(CognitionSignal.DoorWorldMove));
            bool moving = board.State.HasEffect(CognitionSignal.LightMove);
            foreach (var light in FindObjectsByType<OpeningLight>(FindObjectsSortMode.None))
            {
                var motion = light.GetComponent<LightMotion>();
                if (!motion) motion = light.gameObject.AddComponent<LightMotion>();
                motion.SetMoving(moving);
            }
            var environment = FindFirstObjectByType<NightEnvironment>();
            if (environment) environment.WorldMoving = board.State.HasEffect(CognitionSignal.WorldMove);
            // Explicit opening cases. With no effective state sentence, the lamp returns to its initial on state.
            string rule = board.State.HasEffect(CognitionSignal.LightOff) ? "off" : board.State.HasEffect(CognitionSignal.LightOn) ? "on" : "default";
            // X is a temporary preview override, retained until the effective light rule changes.
            if (rule == lastLightRule) return;
            lastLightRule = rule;
            foreach (var light in FindObjectsByType<OpeningLight>(FindObjectsSortMode.None)) light.SetOn(rule != "off");
        }
        void OnDestroy()
        {
            if (board && board.State != null) board.State.Changed -= ApplySentences;
            if (reticleMaterial) Destroy(reticleMaterial);
        }
    }
}
