using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PuzzleApple.V5
{
    // V4's presentation contract, with targets resolved from this room rather than
    // Intro's sun, horizon and ThresholdDoor. One profile per complete sentence.
    public sealed class SentencePresentation : MonoBehaviour
    {
        public enum Focus { Light, LightGroup, Floor, Door, Sky }
        [Serializable] public sealed class Shot
        {
            public string rule;
            public Focus focus;
            public bool follow;
            [Min(0)] public float enter=.65f,hold=1.4f,exit=.65f;
            [Range(0,.2f)] public float letterbox=.09f;
            [Min(.1f)] public float followSpeed=12;
            public Vector3 offset;
            public Shot(){}
            public Shot(string id,Focus target,bool track=false){rule=id;focus=target;follow=track;}
        }
        public SentenceRuntime runtime;
        public SentencePresentationSettings settings;
        public bool enabledShots=true;
        public Shot[] shots={new Shot("light.on",Focus.Light),new Shot("light.off",Focus.Light),
            new Shot("door.on",Focus.Door),new Shot("door.off",Focus.Door),
            new Shot("light.move",Focus.LightGroup),new Shot("light.move.floor",Focus.LightGroup,true)};
        public bool Playing {get;private set;}
        public bool CanInterrupt {get;private set;}
        public int PlayCount {get;private set;}
        public string LastRule {get;private set;}
        int revision;
        bool previousBlocked,previousLock;
        RectTransform top,bottom;
        Shot current;
        Cognition.CognitionState.Group focusGroup;
        CanvasGroup caption;
        float shotRoll;
        WorldEntry Entry=>runtime.entry;
        Shot[] Profiles=>settings?settings.shots:shots;
        readonly Shot[] migrated={new Shot("light.move.light",Focus.LightGroup,true),new Shot("light.move.door",Focus.Door),
            new Shot("door.move",Focus.Door,true),new Shot("door.move.floor",Focus.Door),new Shot("door.move.light",Focus.Door,true),
            new Shot("floor.move",Focus.Sky),new Shot("floor.move.light",Focus.Floor),new Shot("floor.move.door",Focus.Floor)};
        Shot Profile(string rule)=>Array.Find(Profiles,s=>s!=null&&s.rule==rule)??Array.Find(migrated,s=>s.rule==rule);
        bool tutorialMoveShown;
        public bool HasShot(Cognition.CognitionState.Group group)=>enabledShots&&Profile(group.RuleId)!=null&&
            (group.RuleId!="light.move" || !Entry.journey || !Entry.journey.InTutorial || !tutorialMoveShown ||
             Entry.journey.Tutorial.SphereWillChange(group));
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
            current=Profile(rule);focusGroup=affected??sentence;
            if(current==null){apply();yield break;}
            int token=++revision;
            Playing=true;CanInterrupt=false;PlayCount++;LastRule=rule;
            previousBlocked=Entry.board.Panel.InputBlocked;previousLock=Entry.player.CinematicLocked;
            Entry.board.CancelDrag();Entry.board.Panel.InputBlocked=true;Entry.player.CinematicLocked=true;
            float lead=Entry.feedback?Entry.feedback.duration+.2f:1;
            for(float t=0;t<lead&&token==revision;t+=Time.unscaledDeltaTime)
                yield return null;
            if(token!=revision)yield break;
            Entry.board.Panel.SetOpen(false);Cursor.lockState=CursorLockMode.None;Cursor.visible=false;
            caption=SentenceCaption.Show(transform,sentence);
            var view=Entry.player.view;
            shotRoll=view.eulerAngles.z;
            var journey=Entry.journey;
            Quaternion start=view.rotation;
            Vector3? point=LocalTarget(current,focusGroup);
            bool holdView=!point.HasValue;
            Quaternion goal=holdView?start:Nudge(start,point.Value+current.offset);
            for(float t=0;t<current.enter&&token==revision;t+=Time.unscaledDeltaTime)
            {
                float u=Mathf.SmoothStep(0,1,t/Mathf.Max(.001f,current.enter));
                if(!holdView)view.rotation=Blend(start,goal,u);
                Bars(u);yield return null;
            }
            if(token!=revision)yield break;
            if(!holdView)view.rotation=goal;
            Bars(1);
            apply();CanInterrupt=true;
            if(rule=="light.move"&&journey&&journey.InTutorial)tutorialMoveShown=true;
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
        Vector3? LocalTarget(Shot shot,Cognition.CognitionState.Group group)
        {
            if (group==null || shot.rule=="light.move.door" || shot.rule=="door.off") return null;
            var j=Entry.journey;
            var here=j.PlayerSpace;
            if ((runtime.EffectScope(group)&here)==0) return null;
            if (here==PassageJourney.LightSpace.Outdoor)
            {
                if (shot.focus==Focus.Door && (shot.rule!="door.on" || ((ISwitchableDoor)Entry.outdoor.World).WouldChange(true)))
                    return Entry.outdoor.World.Focus(shot.focus);
                return null;
            }
            if (shot.focus==Focus.Door)
            {
                bool opening=shot.rule=="door.on";
                switch(here)
                {
                    case PassageJourney.LightSpace.FarRoom:
                    case PassageJourney.LightSpace.Ascent:
                        if (opening?j.EndingDoor.WouldChange(true):j.EndingDoor.CanCycle) return j.EndingDoor.Leaf.position;
                        break;
                    case PassageJourney.LightSpace.FirstPath:
                    case PassageJourney.LightSpace.SphereRoom:
                        if (opening?j.WhiteDoor.WouldChange(true):j.CanCycleDoor) return j.Door.position;
                        break;
                    case PassageJourney.LightSpace.ReturnPath:
                        if (opening?j.EntranceDoor.WouldChange(true):j.EntranceDoor.CanCycle) return j.EntranceDoor.Leaf.position;
                        break;
                }
                return null;
            }
            if (here==PassageJourney.LightSpace.ReturnPath && shot.rule=="floor.move" && j.Platform)
                return j.Platform.Deck.bounds.center;
            if (here==PassageJourney.LightSpace.SphereRoom && j.Tutorial.SphereWillChange(group))
                return j.Tutorial.Sphere.position;
            if (here==PassageJourney.LightSpace.Chamber &&
                (LightStateEffect.Handles(group)?Entry.chamberLight.WouldChange(LightStateEffect.Commands(group,true)):Entry.chamberLight.CanMove(group.Signal)))
                return Entry.chamberLight.surface.bounds.center;
            return null;
        }

        Quaternion Nudge(Quaternion start,Vector3 point)
        {
            Vector3 delta=point-Entry.player.view.position;
            if (delta.sqrMagnitude<.0001f) return start;
            var desired=Quaternion.LookRotation(delta,Vector3.up).eulerAngles;
            var initial=start.eulerAngles;
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
