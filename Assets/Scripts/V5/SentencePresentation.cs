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
        public bool HasShot(string rule)=>enabledShots&&Profile(rule)!=null&&
            !(rule=="light.move"&&Entry.journey&&Entry.journey.InTutorial&&tutorialMoveShown);
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
            bool corridorShot=(rule=="light.on"||rule=="light.off")&&journey&&journey.InCorridor&&
                !(Entry.outdoor&&Entry.outdoor.IsOutside)&&!(Entry.portableClosure&&Entry.portableClosure.Isolated);
            bool holdView=corridorShot;
            bool roomMotion=journey && !journey.InCorridor && MotionEffect.MovesLamps(Entry.board.Catalog.Rules.FirstOrDefault(r=>r.Id==rule));
            Quaternion start=view.rotation,goal=start;
            if(!holdView)goal=LookAt(Target(current,roomMotion));
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
                if(current.follow&&!holdView)
                {
                    var target=LookAt(Target(current,roomMotion));var e=view.eulerAngles;var d=target.eulerAngles;
                    view.rotation=Quaternion.Euler(Mathf.MoveTowardsAngle(e.x,d.x,current.followSpeed*Time.unscaledDeltaTime),
                        Mathf.MoveTowardsAngle(e.y,d.y,current.followSpeed*Time.unscaledDeltaTime),shotRoll);
                }
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
        Vector3 Target(Shot shot,bool roomMotion)
        {
            if(Entry.outdoor&&Entry.outdoor.IsOutside)return Entry.outdoor.World.Focus(shot.focus)+shot.offset;
            var j=Entry.journey;
            var group=focusGroup!=null&&focusGroup.RuleId==shot.rule?focusGroup:Entry.board.State.Groups.FirstOrDefault(g=>g.Effective&&g.RuleId==shot.rule);
            if(Entry.outdoor&&Entry.outdoor.InChapter){
                var room=Entry.outdoor.CurrentRoom;
                var point=shot.focus==Focus.Floor?room.floor.bounds.center:room.puzzle?room.puzzle.ball.transform.position:room.lamp?room.lamp.position:room.paintingLens?room.paintingLens.bounds.center:room.floor.bounds.center;
                return point+shot.offset;
            }
            var candidates=shot.focus==Focus.Door?PassageJourney.LightSpace.FirstPath|PassageJourney.LightSpace.SphereRoom|PassageJourney.LightSpace.ReturnPath|PassageJourney.LightSpace.FarRoom:PassageJourney.LightSpace.All;
            if(shot.focus==Focus.Door && DoorStateEffect.Handles(group)){
                var changing=runtime.DoorPresentationScope(group);
                if(changing!=0)candidates&=changing;
            }
            var space=runtime.PreferredSpace(group,group!=null?candidates&runtime.EffectScope(group):candidates);
            if(space==PassageJourney.LightSpace.FarRoom && (shot.rule=="light.move" || shot.focus==Focus.Light)){
                if(shot.rule=="light.move" && !j.LampsLit && j.ReturnStars && j.ReturnStars.Brightness>0 &&
                    (runtime.EffectScope(group)&PassageJourney.LightSpace.ReturnPath)!=0)
                    return j.ReturnStars.FocusNear(j.Root.TransformPoint(new Vector3(0,1.5f,j.RoomStart-4)));
                var eye=Entry.player.view;
                var lamp=j.Lamps.Where(l=>l).OrderByDescending(l=>Vector3.Dot(eye.forward,(l.transform.position-eye.position).normalized)).FirstOrDefault();
                if(lamp)return lamp.transform.position+shot.offset;
            }
            if(roomMotion && shot.focus!=Focus.Door && (space==PassageJourney.LightSpace.Chamber || space==PassageJourney.LightSpace.SphereRoom || space==PassageJourney.LightSpace.FarRoom)){
                var centre=space==PassageJourney.LightSpace.SphereRoom?j.Tutorial.Fill.transform.position:space==PassageJourney.LightSpace.FarRoom?j.FarRoomFill.transform.position:Entry.chamberLight.RoomCentre;
                var eye=Entry.player.view;var heading=Vector3.ProjectOnPlane(centre-eye.position,Vector3.up);
                if(heading.sqrMagnitude<.01f)heading=Quaternion.Euler(0,eye.eulerAngles.y,0)*Vector3.forward;
                return eye.position+heading;
            }
            if(shot.rule=="floor.move" && (runtime.Scope(group)&PassageJourney.LightSpace.ReturnPath)!=0 && j.Platform)
                return Entry.player.StandingOn(j.Platform.Deck)?Entry.player.view.position+j.Platform.TravelDirection:j.Platform.Deck.bounds.center;
            Vector3 target=Entry.player.view.position+Entry.player.view.forward*5;
            if(shot.focus==Focus.Door)target=space==PassageJourney.LightSpace.FarRoom?j.EndingDoor.Leaf.position:space==PassageJourney.LightSpace.ReturnPath?j.EntranceDoor.Leaf.position:j.Door.position;
            else if(space==PassageJourney.LightSpace.FarRoom)target=shot.focus==Focus.Floor?j.floorSurface.Surface.bounds.center:LampCentre(j);
            else if(space==PassageJourney.LightSpace.SphereRoom)target=j.Tutorial.Sphere.position;
            else if(space==PassageJourney.LightSpace.FirstPath)target=j.Stars.FocusNear(Entry.player.view.position);
            else if(space==PassageJourney.LightSpace.ReturnPath)target=j.ReturnStars.FocusNear(Entry.player.view.position);
            else if(space==PassageJourney.LightSpace.Ascent)target=j.Ascent.transform.TransformPoint(new Vector3(0,2,5));
            else if(space==PassageJourney.LightSpace.Chamber)target=Entry.chamberLight.surface.bounds.center;
            return target+shot.offset;
        }
        static Vector3 LampCentre(PassageJourney journey)
        {
            Vector3 sum=Vector3.zero;
            foreach(var lamp in journey.Lamps)sum+=lamp.transform.position;
            return journey.Lamps.Count>0?sum/journey.Lamps.Count:journey.Root.TransformPoint(new Vector3(0,0,journey.floorSurface.HoleCentreZ));
        }
        Quaternion LookAt(Vector3 point)
        {
            var delta=point-Entry.player.view.position;
            return delta.sqrMagnitude<.001f?Level(Entry.player.view.rotation):Level(Quaternion.LookRotation(delta,Vector3.up));
        }
        Quaternion Level(Quaternion q){var e=q.eulerAngles;return Quaternion.Euler(e.x,e.y,shotRoll);}
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
