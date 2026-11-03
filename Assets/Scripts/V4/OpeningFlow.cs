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
        [Header("统一收词范围")]
        [Tooltip("普通实体共用的收词距离，从视点到命中表面；首次收集与重复提示使用同一范围。太阳和远处的门为特例。")]
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
        public bool MovementDiscoveryPending => !explorationOnly && Ready && !board.State.Knows("move") && player.WalkedDistance > .005f;
        public bool CanInteract => Ready && player.CanInteractWithWorld && !MovementDiscoveryPending && !Collecting;
        [Min(0)] public float walkAfterFloorDistance = 1.2f;
        float floorCollectedAt;
        bool floorCollectedBeforeMove;
        string collecting;
        Material reticleMaterial;
        Camera worldCamera;
        readonly RaycastHit[] interactionHits = new RaycastHit[32];
        public SentenceEffectManager effects;
        [Tooltip("Collect placed vocabulary without Intro discovery or grammar effects.")]
        public bool explorationOnly;
        SessionSceneManager session;
        bool IsCurrent => !explorationOnly || (session && session.Current && session.Current.gameObject.scene == gameObject.scene);
        void Start()
        {
            session = FindFirstObjectByType<SessionSceneManager>();
            if (door) door.gameObject.SetActive(false);
            if (!explorationOnly) board.Panel.InputBlocked = true;
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
            if (explorationOnly) Ready = true;
        }
        void LateUpdate()
        {
            // V4 presents a render texture through an overlay and a second display camera.
            // Bind its world image locally so later cameras cannot replace the contrast input.
            if (!IsCurrent || !reticleMaterial || !worldCamera || !worldCamera.targetTexture) return;
            hoverEye.material = reticleMaterial;
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
            if (!Ready || !IsCurrent) return;
            if (lightField && !lightField.Released && board.State.Knows("ground") && board.State.Knows("move") &&
                (floorCollectedBeforeMove || player.WalkedDistance - floorCollectedAt >= walkAfterFloorDistance))
                lightField.Release();
            if (door && !door.Revealed)
            {
                bool allOff = lamp && lamp.IsOff;
                if (allOff)
                {
                    door.Reveal(player.view);
                    if (effects) effects.DoorRevealed();
                }
            }
            if (!explorationOnly && player.WalkedDistance >= player.learnMoveDistance && !board.State.Knows("move") && !Collecting) Learn("move");
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
            bool sun = LookingAtSun();
            float reach = door ? Mathf.Max(interactionDistance, door.collectionDistance) : interactionDistance;
            // The sky has no collider. Check the whole visible ray only when aiming at
            // its sun, so even geometry beyond the collection range can occlude it.
            if (sun && worldCamera) reach = Mathf.Max(reach, worldCamera.farClipPlane);
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
            if (!hit.collider) return sun ? "light" : null;
            string word = null;
            var vocabulary = hit.collider.GetComponentInParent<ChapterVocabulary>();
            if (explorationOnly && vocabulary) word = vocabulary.wordId;
            else if (effects && effects.world && hit.collider == effects.world.ground) word = "ground";
            else if (hit.collider.GetComponentInParent<OpeningLight>()) word = "light";
            else if (door && !door.Appearing && hit.collider.GetComponentInParent<ThresholdDoor>() == door) word = "door";
            float collectionRange = word == "door" ? door.collectionDistance : interactionDistance;
            return hit.distance <= collectionRange ? word : null;
        }
        bool LookingAtSun()
        {
            // Read the displayed sky, including retained partial day/night transitions.
            // .9995 is the outer disc edge in both DaySky and NightSky; exclude the halo.
            var sky = RenderSettings.skybox;
            if (!sky || !sky.shader ||
                (sky.shader.name != "PuzzleApple/V4/DaySky" && sky.shader.name != "PuzzleApple/V4/NightSky")) return false;
            if (sky.HasProperty("_Night") && sky.GetFloat("_Night") >= .98f) return false;
            Vector3 direction = sky.GetVector("_SunDirection");
            return direction.sqrMagnitude > .001f &&
                Vector3.Dot(player.view.forward, direction.normalized) >= .9995f;
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
        public bool TryCollectGround()
        {
            if (!CanInteract || LookedAtWord() != "ground") return false;
            Learn("ground"); return true;
        }
        public bool TryCollectPlacedWord()
        {
            if (!explorationOnly || !IsCurrent || !CanInteract) return false;
            var id = LookedAtWord();
            if (id == null) return false;
            Learn(id); return true;
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
            if(id=="door")RememberDoor();
            else if (id == "ground") RememberGround();
            else if (id == "mirror") library.Remember(id);
            else if (id == "light" || explorationOnly) library.Remember(id, worldCamera.targetTexture);
            else library.Remember(id);
            StartCoroutine(Collect(board.Catalog.Word(id)));
        }
        public void RememberGround()
        {
            var go = new GameObject("Floor memory camera", typeof(Camera));
            var camera = go.GetComponent<Camera>();
            camera.CopyFrom(worldCamera); camera.enabled = false;
            camera.transform.SetPositionAndRotation(player.view.position, Quaternion.Euler(12, player.view.eulerAngles.y, 0));
            camera.aspect = 1; camera.allowHDR = false;
            var lamps = FindObjectsByType<OpeningLight>(FindObjectsSortMode.None);
            var visible = new bool[lamps.Length];
            var frame = RenderTexture.GetTemporary(640, 640, 24, RenderTextureFormat.ARGB32);
            try
            {
                for (int i=0;i<lamps.Length;i++) if(lamps[i].surface) { visible[i]=lamps[i].surface.enabled; lamps[i].surface.enabled=false; }
                camera.targetTexture=frame; camera.Render(); library.Remember("ground",frame);
            }
            finally
            {
                for(int i=0;i<lamps.Length;i++) if(lamps[i].surface) lamps[i].surface.enabled=visible[i];
                camera.targetTexture=null; RenderTexture.ReleaseTemporary(frame); Destroy(go);
            }
        }
        public void RememberDoor()
        {
            if(!door||door.Appearing)return;
            var cameraObject=new GameObject("Door memory camera",typeof(Camera));
            var camera=cameraObject.GetComponent<Camera>();camera.enabled=false;
            camera.CopyFrom(worldCamera);camera.enabled=false;camera.orthographic=true;
            camera.orthographicSize=5.2f;camera.aspect=1;camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.08f,.09f,.09f);camera.nearClipPlane=.1f;camera.farClipPlane=40;
            camera.cullingMask=1<<30;camera.allowHDR=false;
            var center=door.transform.TransformPoint(new Vector3(0,3,0));
            camera.transform.SetPositionAndRotation(center-door.transform.forward*20,door.transform.rotation);
            var objects=door.GetComponentsInChildren<Transform>(true);var layers=new int[objects.Length];
            var frame=RenderTexture.GetTemporary(640,640,24,RenderTextureFormat.ARGB32);
            try
            {
                for(int i=0;i<objects.Length;i++){layers[i]=objects[i].gameObject.layer;objects[i].gameObject.layer=30;}
                camera.targetTexture=frame;camera.Render();library.Remember("door",frame);
            }
            finally
            {
                for(int i=0;i<objects.Length;i++)objects[i].gameObject.layer=layers[i];
                camera.targetTexture=null;RenderTexture.ReleaseTemporary(frame);Destroy(cameraObject);
            }
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
            if (word.Id == "ground")
            {
                floorCollectedAt = player.WalkedDistance;
                floorCollectedBeforeMove = !board.State.Knows("move");
            }
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
