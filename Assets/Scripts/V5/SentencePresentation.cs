using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PuzzleApple.V5
{
    // Shared presentation timeline. Mechanisms supply focus through the camera director.
    public sealed class SentencePresentation : MonoBehaviour
    {
        [Serializable] public sealed class Timing
        {
            [Min(0)] public float enter=1.3f,hold=1.4f,exit=.65f;
            [Range(0,.2f)] public float letterbox=.09f;
        }
        public SentenceRuntime runtime;
        public SentencePresentationSettings settings;
        public bool enabledShots=true;
        public bool Playing {get;private set;}
        public bool CanInterrupt {get;private set;}
        public int PlayCount {get;private set;}
        public string LastRule {get;private set;}
        int revision;
        bool previousBlocked,previousLock;
        RectTransform top,bottom;
        Timing current;
        CanvasGroup caption;
        float shotRoll;
        WorldEntry Entry=>runtime.entry;
        readonly Timing defaultTiming=new Timing();
        public bool HasShot(Cognition.CognitionState.Group group)=>enabledShots&&group!=null;
        void Update()
        {
            if(Playing&&CanInterrupt&&Keyboard.current!=null&&Keyboard.current.tabKey.wasPressedThisFrame)InterruptForEditing();
        }
        public bool InterruptForEditing()
        {
            if(!Playing||!CanInterrupt)return false;
            Cancel();Entry.board.Panel.SetOpen(true);return true;
        }
        public void Cancel()
        {
            revision++;
            SentenceCaption.Clear(ref caption);
            if(!Playing)return;
            Playing=false;CanInterrupt=false;if(top)Bars(0);
            if(!runtime||!Entry||!Entry.player||!Entry.board)return;
            Entry.player.AdoptCurrentView();Entry.player.CinematicLocked=previousLock;
            Entry.board.Panel.InputBlocked=previousBlocked;
            Entry.player.RestoreGameplayCursor();
            Entry.player.SuppressInteractionThisFrame();
        }
        public IEnumerator Play(string rule,Action apply,Cognition.CognitionState.Group sentence=null,Cognition.CognitionState.Group affected=null)
        {
            current=settings&&settings.timing!=null?settings.timing:defaultTiming;
            int token=++revision;
            Playing=true;CanInterrupt=false;PlayCount++;LastRule=rule;
            previousBlocked=Entry.board.Panel.InputBlocked;previousLock=Entry.player.CinematicLocked;
            Entry.board.CancelDrag();Entry.board.Panel.InputBlocked=true;Entry.player.CinematicLocked=true;
            float lead=Entry.feedback?Entry.feedback.duration+.2f:1;
            for(float t=0;t<lead&&token==revision;t+=Time.unscaledDeltaTime)
                yield return null;
            if(token!=revision)yield break;
            var group=affected??sentence;
            if(group!=null && runtime.Response(group,starting:true)!=SentenceResponse.Local){
                Cancel();apply();yield break;
            }
            Entry.board.Panel.SetOpen(false);Cursor.lockState=CursorLockMode.None;Cursor.visible=false;
            caption=SentenceCaption.Show(transform,sentence);
            var view=Entry.player.view;
            shotRoll=view.eulerAngles.z;
            Quaternion start=view.rotation;
            Vector3? point=FocusTarget(group);
            bool holdView=!point.HasValue;
            Quaternion goal=holdView?start:Nudge(start,point.Value,group.Intent?.Motion==SentenceMotion.Floor);
            for(float t=0;t<current.enter&&token==revision;t+=Time.unscaledDeltaTime)
            {
                float u=Mathf.SmoothStep(0,1,t/Mathf.Max(.001f,current.enter));
                if(!holdView)view.rotation=Blend(start,goal,u);
                Bars(u);yield return null;
            }
            if(token!=revision)yield break;
            if(!holdView)view.rotation=goal;
            Bars(1);
            apply();
            if(token!=revision)yield break;
            CanInterrupt=true;
            for(float t=0;t<current.hold&&token==revision;t+=Time.unscaledDeltaTime)
            {
                FadeCaption(t);
                yield return null;
            }
            for(float t=0;t<current.exit&&token==revision;t+=Time.unscaledDeltaTime)
            {
                float remaining=1-t/Mathf.Max(.001f,current.exit);
                Bars(remaining);
                FadeCaption(current.hold+t);
                yield return null;
            }
            if(token==revision)Cancel();
        }
        void FadeCaption(float elapsed)
        {
            if(caption)caption.alpha=1-Mathf.SmoothStep(0,1,elapsed/Mathf.Max(.001f,current.hold+current.exit));
        }
        public Vector3? FocusTarget(Cognition.CognitionState.Group group)
        {
            if(group==null)return null;
            var spaces=SentenceSpaceCatalog.Default;
            return SentenceCameraDirector.Resolve(Entry.Entities,group,spaces.FromLegacy(Entry.journey.PlayerSpace),
                spaces.FromLegacy(runtime.EffectScope(group)));
        }

        Quaternion Nudge(Quaternion start,Vector3 point,bool floorMove)
        {
            Vector3 delta=point-Entry.player.view.position;
            if (delta.sqrMagnitude<.0001f) return start;
            var desired=Quaternion.LookRotation(delta,Vector3.up).eulerAngles;
            var initial=start.eulerAngles;
            if(floorMove)
            {
                float pitch=Mathf.DeltaAngle(0,initial.x);
                float targetPitch=Mathf.Clamp(Mathf.DeltaAngle(0,desired.x)+4f,pitch,89f);
                return Quaternion.Euler(Mathf.MoveTowards(pitch,targetPitch,14f),initial.y,shotRoll);
            }
            return Quaternion.Euler(Mathf.MoveTowardsAngle(initial.x,desired.x,10),
                Mathf.MoveTowardsAngle(initial.y,desired.y,15),shotRoll);
        }
        static Quaternion Blend(Quaternion a,Quaternion b,float t)
        {var x=a.eulerAngles;var y=b.eulerAngles;return Quaternion.Euler(Mathf.LerpAngle(x.x,y.x,t),Mathf.LerpAngle(x.y,y.y,t),x.z);}
        void Bars(float amount)=>SetBarsHeight(current==null?0:current.letterbox*amount);
        internal void SetBarsHeight(float height)
        {
            if(!top)
            {
                var canvas=new GameObject("V5 sentence letterbox",typeof(RectTransform),typeof(Canvas));canvas.transform.SetParent(transform,false);
                var c=canvas.GetComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.sortingOrder=40;
                var rect=canvas.GetComponent<RectTransform>();
                rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
                top=Bar(canvas.transform,"Top");bottom=Bar(canvas.transform,"Bottom");
            }

            top.anchorMin=new Vector2(0,1-height);top.anchorMax=Vector2.one;
            bottom.anchorMin=Vector2.zero;bottom.anchorMax=new Vector2(1,height);
            top.offsetMin=top.offsetMax=bottom.offsetMin=bottom.offsetMax=Vector2.zero;
            top.gameObject.SetActive(height>0);bottom.gameObject.SetActive(height>0);
        }
        static RectTransform Bar(Transform parent,string name)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var image=go.GetComponent<Image>();image.color=Color.black;image.raycastTarget=false;return image.rectTransform;
        }
        void OnDisable(){if(runtime&&runtime.entry)Cancel();}
    }
}
