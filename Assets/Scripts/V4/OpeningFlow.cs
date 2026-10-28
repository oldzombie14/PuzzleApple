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
        public OpeningLightField lightField;
        public ThresholdDoor door;
        public Image collectionSymbol, hoverEye;
        public GameObject reticle;
        public RectTransform hud;
        [Min(.1f)] public float interactionDistance = 7;
        public float wordDisplayDuration = 1.6f, wordFadeInDuration = .3f, wordFlyDuration = .6f;
        [Header("已收集词的重复交互")]
        [Range(0,1)] public float knownEyeAlpha=.35f, knownSymbolAlpha=.9f;
        [Min(.1f)] public float knownSymbolDuration=.9f;
        public Vector2 knownSymbolScale=new Vector2(.8f,1.2f);
        Image knownSymbol;
        Coroutine knownFeedback;
        Color eyeColor;
        public bool Ready { get; private set; }
        public bool Collecting => collecting != null;
        public bool MovementDiscoveryPending => Ready && !board.State.Knows("move") && player.WalkedDistance > .005f;
        public bool CanInteract => Ready && player.CanInteractWithWorld && !MovementDiscoveryPending && !Collecting;
        string collecting;
        Material reticleMaterial;
        Camera worldCamera;
        readonly RaycastHit[] interactionHits = new RaycastHit[32];
        public SentenceEffectManager effects;
        void Start()
        {
            if (door) door.gameObject.SetActive(false);
            board.Panel.InputBlocked = true;
            collectionSymbol.gameObject.SetActive(false);
            hoverEye.gameObject.SetActive(false); reticle.SetActive(false);
            worldCamera = player.view.GetComponent<Camera>();
            reticleMaterial = new Material(hoverEye.material);
            hoverEye.material = reticleMaterial;
            eyeColor=hoverEye.color;
            knownSymbol=Instantiate(collectionSymbol,collectionSymbol.transform.parent);
            knownSymbol.name="Known word feedback";knownSymbol.raycastTarget=false;
            knownSymbol.material=reticleMaterial;
            knownSymbol.gameObject.SetActive(false);
            foreach (var image in reticle.GetComponentsInChildren<Image>(true)) image.material = reticleMaterial;

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
            board.Panel.SetOpen(true);
        }
        void Update()
        {
            if (!Ready) return;
            if (door && !door.Revealed)
            {
                bool allOff = lamp && !lamp.IsOn;
                if (allOff)
                {
                    door.Reveal(player.view);
                    if (effects) effects.DoorRevealed();
                }
            }
            if (player.WalkedDistance >= player.learnMoveDistance && !board.State.Knows("move") && !Collecting) Learn("move");
            string targetWord = CanInteract ? LookedAtWord() : null;
            bool target = targetWord != null;
            var eye=eyeColor;eye.a*=target&&board.State.Knows(targetWord)?knownEyeAlpha:1;
            hoverEye.color=eye;
            bool showingKnown=knownSymbol&&knownSymbol.gameObject.activeSelf;
            hoverEye.gameObject.SetActive(target&&!showingKnown);
            reticle.SetActive(player.CanInteractWithWorld && !target && !Collecting&&!showingKnown);
            if (target && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) Learn(targetWord);
        }
        string LookedAtWord()
        {
            bool sunTarget = LookingAtSun();
            float reach = door ? Mathf.Max(interactionDistance, door.collectionDistance) : interactionDistance;
            // A sky target needs an occlusion check, not a longer collection range.
            if (sunTarget && worldCamera) reach = Mathf.Max(reach, worldCamera.farClipPlane);
            int count = Physics.RaycastNonAlloc(player.view.position, player.view.forward, interactionHits, reach, ~0, QueryTriggerInteraction.Collide);
            var hits = count == interactionHits.Length
                ? Physics.RaycastAll(player.view.position, player.view.forward, reach, ~0, QueryTriggerInteraction.Collide) : interactionHits;
            if (hits != interactionHits) count = hits.Length;
            RaycastHit hit = default;
            float closest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.transform.IsChildOf(player.transform) || hits[i].distance >= closest) continue;
                hit = hits[i]; closest = hit.distance;
            }
            if (!hit.collider) return sunTarget ? "light" : null;
            if (hit.distance <= interactionDistance && hit.collider.GetComponentInParent<OpeningLight>()) return "light";
            if (door && hit.distance <= door.collectionDistance && hit.collider.GetComponentInParent<ThresholdDoor>() == door) return "door";
            return null;
        }
        bool LookingAtSun()
        {
            var sky = RenderSettings.skybox;
            if (!sky || !sky.HasProperty("_SunDirection")) return false;
            if (sky.HasProperty("_Night") && sky.GetFloat("_Night") >= .98f) return false;
            Vector3 direction = sky.GetVector("_SunDirection");
            // Match the visible disk boundary in DaySky and NightSky, excluding the halo.
            return Vector3.Dot(player.view.forward, direction.normalized) >= .9995f;
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
            if(board.State.Knows(id))
            {
                if(knownFeedback!=null)StopCoroutine(knownFeedback);
                knownFeedback=StartCoroutine(ShowKnownWord(board.Catalog.Word(id)));
                return;
            }
            if(knownFeedback!=null){StopCoroutine(knownFeedback);knownFeedback=null;knownSymbol.gameObject.SetActive(false);}
            collecting = id;
            if (id != "move") player.MovementLocked = true;
            if (id == "light" || id == "door") library.Remember(id, worldCamera.targetTexture);
            else library.Remember(id);
            StartCoroutine(Collect(board.Catalog.Word(id)));
        }
        IEnumerator ShowKnownWord(WordDefinition word)
        {
            knownSymbol.sprite=word.Symbol;
            var rect=knownSymbol.rectTransform;
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
            rect.anchoredPosition=Vector2.zero;
            rect.localScale=Vector3.one*knownSymbolScale.x;
            hoverEye.gameObject.SetActive(false);reticle.SetActive(false);
            knownSymbol.gameObject.SetActive(true);
            for(float t=0;t<knownSymbolDuration;t+=Time.unscaledDeltaTime)
            {
                float progress=t/knownSymbolDuration;
                rect.localScale=Vector3.one*Mathf.Lerp(knownSymbolScale.x,knownSymbolScale.y,Mathf.SmoothStep(0,1,progress));
                float reveal=Mathf.SmoothStep(0,1,progress/.08f);
                float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.3f,1,progress));
                knownSymbol.color=new Color(1,1,1,knownSymbolAlpha*reveal*fade);
                yield return null;
            }
            knownSymbol.gameObject.SetActive(false);knownFeedback=null;
        }
        void OnDisable()
        {
            if(knownFeedback!=null)StopCoroutine(knownFeedback);
            knownFeedback=null;
            if(knownSymbol)knownSymbol.gameObject.SetActive(false);
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
        void OnDestroy()
        {
            if (reticleMaterial) Destroy(reticleMaterial);
            if (knownSymbol) Destroy(knownSymbol.gameObject);
        }
    }
}
