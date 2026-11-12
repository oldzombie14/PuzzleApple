using System;
using System.Collections;
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
            [Min(0)] public float enter=.65f,hold=2.4f,exit=.65f;
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
        int fadingGroup=-1;
        bool previousBlocked,previousLock;
        RectTransform top,bottom;
        Shot current;
        float shotRoll;
        bool roomCut;
        Vector3 returnPosition;
        Quaternion returnRotation;
        WorldEntry Entry=>runtime.entry;
        Shot[] Profiles=>settings?settings.shots:shots;
        readonly Shot[] migrated={new Shot("light.light.move",Focus.LightGroup,true),new Shot("light.door.move",Focus.Door),
            new Shot("door.move",Focus.Door,true),new Shot("door.floor.move",Focus.Door),new Shot("door.light.move",Focus.Door,true),
            new Shot("floor.move",Focus.Sky),new Shot("floor.light.move",Focus.Floor),new Shot("floor.door.move",Focus.Floor)};
        Shot Profile(string rule)=>Array.Find(Profiles,s=>s!=null&&s.rule==rule)??Array.Find(migrated,s=>s.rule==rule);
        public bool HasShot(string rule)=>enabledShots&&Profile(rule)!=null;
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
            if(!Playing)return;
            if(fadingGroup>=0)Entry.board.SetSentenceOpacity(fadingGroup,1);
            fadingGroup=-1;
            Playing=false;CanInterrupt=false;if(top)Bars(0);
            if(!runtime||!Entry||!Entry.player||!Entry.board)return;
            if(roomCut)
            {
                Entry.player.view.localPosition=returnPosition;
                Entry.player.view.rotation=returnRotation;
                roomCut=false;
            }
            Entry.player.AdoptCurrentView();Entry.player.CinematicLocked=previousLock;
            Entry.board.Panel.InputBlocked=previousBlocked;
            Entry.player.RestoreGameplayCursor();
            Entry.player.SuppressInteractionThisFrame();
        }
        public IEnumerator Play(string rule,Action apply,int oneShotGroup=-1)
        {
            current=Profile(rule);
            if(current==null){apply();yield break;}
            int token=++revision;
            Playing=true;CanInterrupt=false;PlayCount++;LastRule=rule;
            previousBlocked=Entry.board.Panel.InputBlocked;previousLock=Entry.player.CinematicLocked;
            Entry.board.CancelDrag();Entry.board.Panel.InputBlocked=true;Entry.player.CinematicLocked=true;
            float lead=Entry.feedback?Entry.feedback.duration+.2f:1;
            fadingGroup=oneShotGroup;
            for(float t=0;t<lead&&token==revision;t+=Time.unscaledDeltaTime)
            {
                if(fadingGroup>=0)Entry.board.SetSentenceOpacity(fadingGroup,1-Mathf.SmoothStep(0,1,t/lead));
                yield return null;
            }
            if(token!=revision)yield break;
            Entry.board.Panel.SetOpen(false);Cursor.lockState=CursorLockMode.None;Cursor.visible=false;
            var view=Entry.player.view;
            shotRoll=view.eulerAngles.z;
            var journey=Entry.journey;
            bool corridorShot=(rule=="light.on"||rule=="light.off")&&journey&&journey.InCorridor&&
                !(Entry.outdoor&&Entry.outdoor.IsOutside)&&!(Entry.portableClosure&&Entry.portableClosure.Isolated);
            bool farSide=corridorShot&&journey.Root.InverseTransformPoint(Entry.player.transform.position).z>journey.pathLength*.5f;
            if(corridorShot)
            {
                float requested=rule=="light.on"?1:0;
                bool chamberChanges=Mathf.Abs(Entry.chamberLight.Brightness-requested)>.001f;
                bool lampRoomChanges=Mathf.Abs(journey.Brightness-requested)>.001f;
                if(chamberChanges!=lampRoomChanges)farSide=lampRoomChanges;
            }
            Vector3 cameraPoint=corridorShot?journey.Root.TransformPoint(new Vector3(0,1.7f,farSide?journey.pathLength+2:-2)):view.position;
            bool corridorCut=corridorShot&&rule=="light.off";
            Vector3 focus=corridorShot&&!corridorCut?
                (farSide?LampCentre(journey):Entry.chamberLight.surface.bounds.center)+current.offset:
                (corridorCut?cameraPoint:Target(current));
            Quaternion start=view.rotation,goal=LookAt(focus);
            for(float t=0;t<current.enter&&token==revision;t+=Time.unscaledDeltaTime)
            {
                float u=Mathf.SmoothStep(0,1,t/Mathf.Max(.001f,current.enter));
                view.rotation=Blend(start,goal,u);Bars(u);yield return null;
            }
            if(token!=revision)yield break;
            view.rotation=goal;Bars(1);
            if(corridorCut)
            {
                returnPosition=view.localPosition;
                returnRotation=goal;
                roomCut=true;
                view.position=cameraPoint;
                view.rotation=LookAt(farSide?LampCentre(journey):Entry.chamberLight.surface.bounds.center);
                // Establish the unchanged room in frame before starting the light transition.
                yield return null;
                for(float t=0;t<.35f&&token==revision;t+=Time.unscaledDeltaTime)yield return null;
                if(token!=revision)yield break;
            }
            if(fadingGroup>=0)Entry.board.SetSentenceOpacity(fadingGroup,0);
            fadingGroup=-1;
            apply();CanInterrupt=true;
            for(float t=0;t<current.hold&&token==revision;t+=Time.unscaledDeltaTime)
            {
                if(current.follow&&!roomCut)
                {
                    var target=LookAt(Target(current));var e=view.eulerAngles;var d=target.eulerAngles;
                    view.rotation=Quaternion.Euler(Mathf.MoveTowardsAngle(e.x,d.x,current.followSpeed*Time.unscaledDeltaTime),
                        Mathf.MoveTowardsAngle(e.y,d.y,current.followSpeed*Time.unscaledDeltaTime),shotRoll);
                }
                yield return null;
            }
            for(float t=0;t<current.exit&&token==revision;t+=Time.unscaledDeltaTime)
            {Bars(1-t/Mathf.Max(.001f,current.exit));yield return null;}
            if(token==revision)Cancel();
        }
        Vector3 Target(Shot shot)
        {
            if(Entry.outdoor&&Entry.outdoor.IsOutside)return Entry.outdoor.World.Focus(shot.focus)+shot.offset;
            var j=Entry.journey;Vector3 target=Entry.player.view.position+Entry.player.view.forward*5;
            if(shot.focus==Focus.Door&&j&&j.Door)target=j.Door.position;
            else if(shot.focus==Focus.Floor&&j&&j.floorSurface&&j.floorSurface.Surface)target=j.floorSurface.Surface.bounds.center;
            else if(j&&j.InSecondRoom&&j.Lamps.Count>0)
                target=LampCentre(j);
            else if(Entry.chamberLight&&Entry.chamberLight.surface)target=Entry.chamberLight.surface.bounds.center;
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
        void Bars(float amount)
        {
            if(!top)
            {
                var canvas=new GameObject("V5 sentence letterbox",typeof(RectTransform),typeof(Canvas));canvas.transform.SetParent(transform,false);
                var c=canvas.GetComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.sortingOrder=40;
                var rect=canvas.GetComponent<RectTransform>();
                rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
                top=Bar(canvas.transform,"Top");bottom=Bar(canvas.transform,"Bottom");
            }
            float height=(current==null?0:current.letterbox)*amount;
            top.anchorMin=new Vector2(0,1-height);top.anchorMax=Vector2.one;
            bottom.anchorMin=Vector2.zero;bottom.anchorMax=new Vector2(1,height);
            top.offsetMin=top.offsetMax=bottom.offsetMin=bottom.offsetMax=Vector2.zero;
            top.gameObject.SetActive(amount>0);bottom.gameObject.SetActive(amount>0);
        }
        static RectTransform Bar(Transform parent,string name)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var image=go.GetComponent<Image>();image.color=Color.black;image.raycastTarget=false;return image.rectTransform;
        }
        void OnDisable(){if(runtime&&runtime.entry)Cancel();}
    }
}
