using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using PuzzleApple.V5.Cognition;

namespace PuzzleApple.V5
{
    public sealed class WorldEntry : MonoBehaviour
    {
        public FirstPersonController player;
        public CognitionBoard board;
        public FoldingRoom room;
        public Button enterButton, exitButton, confirmButton, cancelButton;
        public Image blackBackground;
        public GameObject dialog;
        public SentenceFeedback feedback;
        public ChamberLight chamberLight;
        public PassageJourney journey;
        public PortableClosure portableClosure;
        public SentenceRuntime sentenceRuntime;
        public OutdoorPassage outdoor;
        [Min(0)] public float symbolFadeDuration = .4f, blackFadeDuration = .8f;
        [Min(0)] public float closedPauseDuration = .65f, dialogFadeDuration = .5f;
        public bool Entered { get; private set; }
        public bool Busy { get; private set; }
        public bool ConfirmingExit => dialog.activeSelf;
        public bool Exiting { get; private set; }
        public bool DialogReady { get; private set; }
        bool wasPanelOpen;
        Coroutine worldRoutine;
        int worldGroup=-1;
        string worldRule;


        void Start()
        {
            player.enabled = false;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = false;
            // All world controls share subdued idle ink and bright hover/focus ink.
            foreach (var option in new[] { enterButton, exitButton, confirmButton, cancelButton })
            {
                // A fresh title screen must not inherit a disabled transition state.
                option.interactable = true;
                var palette = option.colors;
                palette.normalColor = new Color(.65f, .65f, .65f);
                palette.highlightedColor = palette.selectedColor = Color.white;
                option.colors = palette;
            }
            dialog.SetActive(false);
            enterButton.onClick.AddListener(EnterWorld);
            exitButton.onClick.AddListener(RequestExit);
            confirmButton.onClick.AddListener(ConfirmExit);
            cancelButton.onClick.AddListener(CancelExit);
            if(!sentenceRuntime)sentenceRuntime=GetComponent<SentenceRuntime>();
            if(!sentenceRuntime)sentenceRuntime=gameObject.AddComponent<SentenceRuntime>();
            sentenceRuntime.Initialize(this);
            if(!outdoor)outdoor=GetComponent<OutdoorPassage>();
            if(!outdoor)outdoor=gameObject.AddComponent<OutdoorPassage>();
            outdoor.entry=this;
            board.State.Changed+=OnSentences;
        }
        void OnDestroy(){if(board && board.State!=null)board.State.Changed-=OnSentences;}
        void OnSentences(CognitionChange change)=>sentenceRuntime.Refresh(change);
        void Update()
        {
            if(Entered) exitButton.gameObject.SetActive(board.Panel.IsOpen && !Busy && !ConfirmingExit);
        }
        void LateUpdate()
        {
            // Resolve after player input so cancelling restores the same cursor state as OFF.
            if (ConfirmingExit && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                CancelExit();
        }
        public void EnterWorld() { if(!Busy && !Entered && !ConfirmingExit) StartCoroutine(Enter()); }
        IEnumerator Enter()
        {
            Busy = true; enterButton.interactable = exitButton.interactable = false;
            var enterInk = Ink(enterButton.gameObject); var exitInk = Ink(exitButton.gameObject);
            for(float t=0;t<symbolFadeDuration;t+=Time.unscaledDeltaTime)
            {
                enterInk.alpha = exitInk.alpha = 1-Mathf.SmoothStep(0,1,t/Mathf.Max(.001f,symbolFadeDuration));
                yield return null;
            }
            enterButton.gameObject.SetActive(false); exitInk.alpha=0;
            for(float t=0;t<blackFadeDuration;t+=Time.unscaledDeltaTime)
            {
                blackBackground.color=new Color(0,0,0,1-Mathf.SmoothStep(0,1,t/Mathf.Max(.001f,blackFadeDuration)));
                yield return null;
            }
            blackBackground.gameObject.SetActive(false);
            foreach(var id in new[]{"positive","negative","world"}) board.State.TryAcquireWord("entry."+id,board.Catalog.Word(id));
            Entered=true; Busy=false; player.enabled=true;
            board.Panel.InputBlocked=false; board.Panel.SetOpen(true);
            exitInk.alpha=1; exitButton.interactable=true;
        }
        static CanvasGroup Ink(GameObject target)
        {
            var ink=target.GetComponent<CanvasGroup>();
            return ink ? ink : target.AddComponent<CanvasGroup>();
        }
        public void RequestExit()
        {
            if(Busy || ConfirmingExit || Exiting) return;
            if(Entered) StartCoroutine(CloseWorld(-1));
            else ShowDialog();
        }
        public void OpenWorld(int groupId)
        {
            if(!Entered || Busy || ConfirmingExit) return;
            if(room.Opened) { if(groupId>=0)feedback.Play(groupId,false); return; }
            worldGroup=groupId;worldRule="world.on";worldRoutine=StartCoroutine(Open(groupId));
        }
        IEnumerator Open(int groupId)
        {
            Busy=true; board.Panel.InputBlocked=true;
            if(groupId>=0) {feedback.Play(groupId,true);yield return new WaitForSecondsRealtime(feedback.duration);}
            board.Panel.SetOpen(false);
            if(portableClosure)portableClosure.Restore();
            room.Open();
            board.Panel.InputBlocked=false;
            while(room.Animating)yield return null;
            Busy=false;player.MovementLocked=false;worldGroup=-1; board.Panel.InputBlocked=false;
        }
        public void CloseWorldFromSentence(int groupId)
        {
            if(Entered && !Busy && !ConfirmingExit){worldGroup=groupId;worldRule="world.off";worldRoutine=StartCoroutine(CloseWorld(groupId));}
        }
        public void InterruptRemovedWorldSentence()
        {
            if(worldGroup<0||ConfirmingExit||Exiting)return;
            var group=board.State.Find(worldGroup);
            if(group!=null&&group.Effective&&group.RuleId==worldRule)return;
            if(worldRoutine!=null)StopCoroutine(worldRoutine);
            room.HoldCurrentPose();worldGroup=-1;Busy=false;
            board.Panel.InputBlocked=false;board.Panel.SetOpen(true);
            // A half-closed portable box still owns the frozen position. Opening it
            // releases the lock; view and editing remain available while paused.
        }
        IEnumerator CloseWorld(int groupId)
        {
            Busy=true; player.MovementLocked=true;
            board.CancelDrag(); board.Panel.InputBlocked=true;
            // Showing exit confirmation is a visible effect even when no fold is needed.
            if(groupId>=0) {feedback.Play(groupId,true);yield return new WaitForSecondsRealtime(feedback.duration);}
            wasPanelOpen=board.Panel.IsOpen; board.Panel.SetOpen(false);
            if(portableClosure&&!portableClosure.CanFold)
            {
                worldGroup=-1;ShowDialog();yield break;
            }
            if(portableClosure)portableClosure.Prepare();
            room.CloseAt(player.transform.position);
            board.Panel.InputBlocked=false;
            while(room.Animating) yield return null;
            yield return new WaitForSecondsRealtime(closedPauseDuration);
            worldGroup=-1;
            ShowDialog();
        }
        void ShowDialog()
        {
            board.CancelDrag(); board.Panel.InputBlocked=true;
            // Position stays frozen; view remains controlled during folding.
            player.MovementLocked=true;
            player.PointerUIActive=true;
            Cursor.lockState=CursorLockMode.None; Cursor.visible=false;
            Busy=true;DialogReady=false;
            var ink=Ink(dialog);ink.alpha=0;ink.interactable=false;
            dialog.SetActive(true);
            StartCoroutine(FadeDialog(ink));
        }
        IEnumerator FadeDialog(CanvasGroup ink)
        {
            for(float t=0;t<dialogFadeDuration;t+=Time.unscaledDeltaTime)
            {
                ink.alpha=Mathf.SmoothStep(0,1,t/Mathf.Max(.001f,dialogFadeDuration));
                yield return null;
            }
            ink.alpha=1;ink.interactable=true;DialogReady=true;Busy=false;
        }
        public void CancelExit()
        {
            if(!ConfirmingExit || !DialogReady || Exiting)return;
            dialog.SetActive(false);
            DialogReady=false;
            player.PointerUIActive=false;
            if(Entered)
            {
                player.MovementLocked=false;
                board.Panel.InputBlocked=false; board.Panel.SetOpen(wasPanelOpen);
                Cursor.lockState=wasPanelOpen?CursorLockMode.None:CursorLockMode.Locked;
                Cursor.visible=false; player.SuppressInteractionThisFrame();
            }
        }
        public void ConfirmExit()
        {
            if(ConfirmingExit&&DialogReady&&!Exiting)StartCoroutine(Quit());
        }
        IEnumerator Quit()
        {
            Exiting=true; dialog.SetActive(false); player.CinematicLocked=true;
            blackBackground.gameObject.SetActive(true);blackBackground.transform.SetAsLastSibling();
            for(float t=0;t<blackFadeDuration;t+=Time.unscaledDeltaTime)
            {blackBackground.color=new Color(0,0,0,Mathf.SmoothStep(0,1,t/Mathf.Max(.001f,blackFadeDuration)));yield return null;}
            blackBackground.color=Color.black;yield return new WaitForEndOfFrame();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }
    }
}



