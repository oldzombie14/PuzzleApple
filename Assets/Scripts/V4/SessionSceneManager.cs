using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PuzzleApple.V4.Cognition;

namespace PuzzleApple.V4
{
    public sealed class SessionSceneManager : MonoBehaviour
    {
        public FirstPersonController player;
        public CognitionBoard board;
        public WordLibrary library;
        public WorldEntry menu;
        public Image collectionSymbol, hoverEye;
        public GameObject reticle;
        public RectTransform hud;
        public CanvasGroup loading;
        public Image progress;
        public SceneEntry Current { get; private set; }
        public bool Busy { get; private set; }
        public float Progress { get; private set; }
        public string LastError { get; private set; }
        readonly System.Collections.Generic.HashSet<string> previewLoads = new System.Collections.Generic.HashSet<string>();
        public SceneEntry LoadedEntry(string sceneName)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            return scene.IsValid() && scene.isLoaded ? scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SceneEntry>(true)).FirstOrDefault() : null;
        }
        public IEnumerator PreparePreview(string sceneName)
        {
            if (previewLoads.Contains(sceneName)) { while (previewLoads.Contains(sceneName)) yield return null; yield break; }
            if (LoadedEntry(sceneName)) yield break;
            if (!Application.CanStreamedLevelBeLoaded(sceneName)) { Debug.LogError("Portal scene missing: " + sceneName); yield break; }
            previewLoads.Add(sceneName);
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            var entry = LoadedEntry(sceneName);
            if (entry) { ConfigureContent(entry); entry.content.SetActive(true); }
            previewLoads.Remove(sceneName);
        }
        public bool TraversePortal(SceneEntry entry, Vector3 position, Quaternion viewRotation)
        {
            if (Busy || !entry || !entry.content || player.CinematicLocked || player.MovementLocked || board.Panel.IsOpen || menu.ConfirmingExit) return false;
            var old = Current;
            if (old && old != entry) { board.State.ForgetWords(old.temporaryWords); library.ForgetMemories(old.temporaryWords); }
            Current = entry;
            SceneManager.SetActiveScene(entry.gameObject.scene);
            ConfigureContent(entry);
            entry.content.SetActive(true);
            player.Teleport(position, Quaternion.Euler(0, viewRotation.eulerAngles.y, 0));
            player.view.rotation = viewRotation;
            player.AdoptCurrentView();
            collectionSymbol.gameObject.SetActive(false); hoverEye.gameObject.SetActive(false);
            player.SuppressInteractionThisFrame();
            return true;
        }
        IEnumerator Start()
        {
            var existing = FindObjectsByType<SceneEntry>(FindObjectsSortMode.None).FirstOrDefault();
            if (existing)
            {
                Bind(existing);
                yield return null;
                FinishArrival(existing, true);
                loading.alpha = 0; loading.blocksRaycasts = false;
            }
            else Request("P4");
        }
        public bool Request(string destination)
        {
            if (Busy || (Current && Current.gameObject.scene.name == destination)) return false;
            if (!Application.CanStreamedLevelBeLoaded(destination))
            {
                LastError = "Scene is not in Build Settings: " + destination;
                Debug.LogError(LastError); return false;
            }
            Busy = true; LastError = null;
            StartCoroutine(Transition(destination));
            return true;
        }
        IEnumerator Fade(float target)
        {
            loading.blocksRaycasts = true;
            while (!Mathf.Approximately(loading.alpha, target))
            {
                loading.alpha = Mathf.MoveTowards(loading.alpha, target, Time.unscaledDeltaTime / .25f);
                yield return null;
            }
        }
        void SetProgress(float value) { Progress = value; progress.rectTransform.localScale = new Vector3(value, 1, 1); }
        IEnumerator Transition(string destination)
        {
            bool open = board.Panel.IsOpen;
            bool locked = player.CinematicLocked, blocked = board.Panel.InputBlocked;
            board.CancelDrag(); player.CinematicLocked = true; board.Panel.InputBlocked = true;
            SetProgress(0);
            yield return Fade(1);
            while (previewLoads.Count > 0) yield return null;
            var previousEntries = FindObjectsByType<SceneEntry>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (!LoadedEntry(destination))
            {
                var operation = SceneManager.LoadSceneAsync(destination, LoadSceneMode.Additive);
                while (!operation.isDone) { SetProgress(Mathf.Clamp01(operation.progress / .9f) * .8f); yield return null; }
            }
            var scene = SceneManager.GetSceneByName(destination);
            var entry = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SceneEntry>(true)).FirstOrDefault();
            if (!entry || !entry.content || !entry.arrival)
            {
                LastError = "Scene has no valid SceneEntry: " + destination;
                Debug.LogError(LastError);
                yield return SceneManager.UnloadSceneAsync(scene);
                yield return Fade(0);
                player.CinematicLocked = locked; board.Panel.InputBlocked = blocked;
                loading.blocksRaycasts = false; Busy = false; yield break;
            }
            var old = Current;
            bool initial = !old;
            if (old)
            {
                var effects = old.content.GetComponentInChildren<SentenceEffectManager>(true);
                if (effects) effects.Suspend();
                player.CinematicLocked = true;
                board.Panel.InputBlocked = true;
                old.content.SetActive(false);
                board.State.ForgetWords(old.temporaryWords);
                library.ForgetMemories(old.temporaryWords);
            }
            Bind(entry);
            SetProgress(.85f);
            yield return null; // Let content Start initialize before exposing it.
            FinishArrival(entry, old == null);
            board.Panel.InputBlocked = true;
            if (old) yield return SceneManager.UnloadSceneAsync(old.gameObject.scene);
            // Portal neighbours are retained only while exploring their connected spaces.
            foreach (var neighbour in previousEntries)
                if (neighbour && neighbour != entry && neighbour.gameObject.scene.isLoaded)
                    yield return SceneManager.UnloadSceneAsync(neighbour.gameObject.scene);
            // Unloading the old environment can run cleanup; restore the destination's lighting.
            SceneManager.SetActiveScene(scene);
            SetProgress(1);
            board.Panel.SetOpen(open);
            yield return Fade(0);
            player.CinematicLocked = locked;
            board.Panel.InputBlocked = initial ? entry.intro : blocked;
            loading.blocksRaycasts = false; Busy = false;
        }
        void Bind(SceneEntry entry)
        {
            Current = entry;
            SceneManager.SetActiveScene(entry.gameObject.scene);
            ConfigureContent(entry);
            collectionSymbol.gameObject.SetActive(false);
            hoverEye.gameObject.SetActive(false); reticle.SetActive(false);
            entry.content.SetActive(true);
            player.Teleport(entry.arrival.position, entry.arrival.rotation);
            player.view.localRotation = Quaternion.identity;
        }
        void ConfigureContent(SceneEntry entry)
        {
            if (entry == Current)
            {
                player.SetEyeHeight(entry.eyeHeight);
                var picture = FindFirstObjectByType<WorldPictureStyle>();
                if (picture) picture.SetSceneMaterial(entry.pictureMaterialOverride);
            }
            foreach (var mirror in entry.content.GetComponentsInChildren<PlanarMirror>(true))
                mirror.SourceCamera = player.view.GetComponent<Camera>();
            var flow = entry.content.GetComponentInChildren<OpeningFlow>(true);
            if (entry == Current) { menu.opening = flow; menu.environment = entry.content.GetComponentInChildren<NightEnvironment>(true); }
            if (flow)
            {
                flow.player = player; flow.board = board; flow.library = library;
                flow.collectionSymbol = collectionSymbol; flow.hoverEye = hoverEye;
                flow.reticle = reticle; flow.hud = hud;
                var effects = flow.effects;
                if (!effects) return;
                effects.board = board;
                effects.lights.observer = player.transform;
                effects.world.player = player;
                effects.presentation.player = player;
                effects.presentation.feedback.board = board;
                if (flow.lightField) flow.lightField.observer = player.transform;
                var clouds = entry.content.GetComponentInChildren<CloudLightCookie>(true);
                if (clouds) clouds.player = player.transform;
            }
        }
        void FinishArrival(SceneEntry entry, bool initial)
        {
            if (!entry.intro)
            {
                menu.SkipIntroMenu();
                board.Panel.InputBlocked = false;
                player.enabled = true;
            }
            else if (!initial)
            {
                menu.SkipIntroMenu();
                menu.opening.Begin();
            }
        }
    }
}
