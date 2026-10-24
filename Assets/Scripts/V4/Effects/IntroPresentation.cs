using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PuzzleApple.V4.Cognition;
using UnityEngine;
using UnityEngine.UI;

namespace PuzzleApple.V4
{
    // Plans new sentence effects before presentation, then commits them after the letterbox arrives.
    public sealed class IntroPresentation : MonoBehaviour
    {
        public SentenceEffectManager effects;
        public FirstPersonController player;
        public SentenceFeedback feedback;
        public IntroShotProfile[] shots=Array.Empty<IntroShotProfile>();
        public string LastShotRule { get; private set; }
        IntroShotProfile activeShot;
        [Min(0)] public float feedbackLead=.3f;
        public bool Playing { get; private set; }
        public int PlayCount { get; private set; }
        public float LetterboxProgress { get; private set; }
        RectTransform topBar,bottomBar;
        void Start()
        {
            var root=new GameObject("Intro letterbox",typeof(RectTransform),typeof(Canvas));
            root.transform.SetParent(transform,false);
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=40;
            topBar=CreateBar(root.transform,"Top",true);bottomBar=CreateBar(root.transform,"Bottom",false);
            SetLetterbox(0);
        }
        static RectTransform CreateBar(Transform parent,string name,bool top)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var image=go.GetComponent<Image>();image.color=Color.black;image.raycastTarget=false;
            var rect=image.rectTransform;rect.pivot=new Vector2(.5f,top?1:0);
            return rect;
        }
        void SetLetterbox(float progress)
        {
            LetterboxProgress=progress;if(!topBar)return;
            float height=(activeShot?activeShot.letterboxHeight:0)*progress;
            topBar.anchorMin=new Vector2(0,1-height);topBar.anchorMax=Vector2.one;
            bottomBar.anchorMin=Vector2.zero;bottomBar.anchorMax=new Vector2(1,height);
            topBar.offsetMin=topBar.offsetMax=bottomBar.offsetMin=bottomBar.offsetMax=Vector2.zero;
            topBar.gameObject.SetActive(progress>0);bottomBar.gameObject.SetActive(progress>0);
        }
        readonly Dictionary<int,string> previous=new Dictionary<int,string>();
        readonly HashSet<CognitionSignal> activeSignals=new HashSet<CognitionSignal>();
        int revision;
        Coroutine routine;
        bool restoreOpen, restoreBlocked, restoreLocked;
        Vector3 savedLocalPosition;
        Quaternion savedLocalRotation;
        float savedFov;
        Camera cameraView;
        readonly List<OpeningLight> shotLights=new List<OpeningLight>();
        bool focusElevatedGroup;
        public Vector3 FocusPoint {get;private set;}
        public bool TrackingTarget {get;private set;}
        sealed class Observation
        {
            public int id;
            public string rule;
            public CognitionSignal[] signals;
            public bool lightOn, doorOn, doorOff;
        }
        public void Apply(CognitionChange kind, Action apply)
        {
            if(Playing)Cancel();
            int token=++revision;
            var state=effects.board.State;
            var observations=new List<Observation>();
            foreach(var group in state.Groups.Where(g=>g.Effective))
            {
                if(previous.TryGetValue(group.Id,out var rule)&&rule==group.RuleId)continue;
                var o=new Observation {id=group.Id,rule=group.RuleId,
                    signals=group.Signals.Where(s=>!activeSignals.Contains(s)).ToArray(),
                    lightOn=effects.lights.stateSource&&effects.lights.stateSource.IsOn,
                    doorOn=effects.door.appearance&&effects.door.appearance.IsOn,
                    doorOff=effects.door.appearance&&effects.door.appearance.IsOff};
                observations.Add(o);
            }
            previous.Clear(); activeSignals.Clear();
            foreach(var group in state.Groups.Where(g=>g.Effective))
            {previous[group.Id]=group.RuleId;foreach(var signal in group.Signals)activeSignals.Add(signal);}
            if(kind==CognitionChange.Split)
            {
                apply();
                if(observations.Count>0&&isActiveAndEnabled)StartCoroutine(Observe(observations,kind,token,()=>{}));
                return;
            }
            if(observations.Count>0&&isActiveAndEnabled)StartCoroutine(Observe(observations,kind,token,apply));
            else apply();
        }
        IEnumerator Observe(List<Observation> observations,CognitionChange kind,int token,Action apply)
        {
            // Let the UI finish its commit; do not mutate the world during feedback.
            yield return new WaitForSecondsRealtime(.12f);
            if(token!=revision||effects.Suspended)yield break;
            Observation shot=null;
            foreach(var o in observations)
            {
                var group=effects.board.State.Find(o.id);
                if(group==null||!group.Effective||group.RuleId!=o.rule)continue;
                bool changed=o.signals.Any(s=>VisibleChange(o,s));
                if(feedback)feedback.Play(o.id,changed);
                if(changed&&shot==null&&shots.Any(p=>p&&p.rule&&p.rule.Id==o.rule))shot=o;
            }
            // ALL split paths are excluded, including a previously suppressed sentence taking over.
            if(shot!=null&&kind!=CognitionChange.Split&&kind!=CognitionChange.Layout&&!Playing)
                routine=StartCoroutine(Show(shot,apply));
            else apply();
        }
        bool VisibleChange(Observation o,CognitionSignal signal)
        {
            var door=effects.door.door;
            var lamps=effects.lights.Lamps.Where(l=>l&&l.isActiveAndEnabled).ToArray();
                switch(signal)
                {
                    case CognitionSignal.LightOn: if(!o.lightOn)return true;break;
                    case CognitionSignal.LightOff: if(o.lightOn)return true;break;
                    case CognitionSignal.DoorOn:
                        if(door&&door.Revealed&&(o.doorOff||door.LandingCompleted&&!o.doorOn))return true;break;
                    case CognitionSignal.DoorOff:
                        if(door&&door.Revealed&&!o.doorOff)return true;break;
                    case CognitionSignal.WorldMove:return true;
                    case CognitionSignal.LightMove: if(lamps.Length>0)return true;break;
                    case CognitionSignal.LightWorldMove:
                        if(effects.lights.ground&&lamps.Any(l=>l.GetComponent<Collider>().bounds.min.y>effects.lights.ground.bounds.max.y+.03f))return true;break;
                    case CognitionSignal.LightDoorMove:
                        if(door&&door.Revealed&&lamps.Length>0)return true;break;
                    case CognitionSignal.LightLightMove:
                        if(lamps.Any(l=>lamps.Any(other=>other!=l&&Vector3.Distance(l.transform.position,other.transform.position)>effects.lights.spacing+l.GetComponent<Collider>().bounds.extents.magnitude+other.GetComponent<Collider>().bounds.extents.magnitude)))return true;break;
                    case CognitionSignal.DoorMove: if(door&&door.Revealed)return true;break;
                    case CognitionSignal.DoorWorldMove: if(door&&door.Revealed&&!door.Landed)return true;break;
                    case CognitionSignal.DoorLightMove:
                        if(door&&door.Revealed){var target=effects.lights.Nearest(door.transform.position);if(target&&Vector3.Distance(target.transform.position,door.transform.position)>effects.door.arrivalDistance)return true;}break;
                    case CognitionSignal.WorldLightMove:
                        if(effects.world.ground&&lamps.Length>0&&effects.lights.LowestBottom()>effects.world.ground.bounds.max.y+.025f)return true;break;
                    case CognitionSignal.WorldDoorMove:
                        if(door&&door.Revealed&&effects.world.ground&&door.transform.position.y>effects.world.ground.bounds.max.y+.025f)return true;break;
                }
            return false;
        }
        IEnumerator Show(Observation observation,Action apply)
        {
            Playing=true;PlayCount++;
            LastShotRule=observation.rule;
            activeShot=shots.FirstOrDefault(p=>p&&p.rule&&p.rule.Id==observation.rule);
            float enter=activeShot.enterDuration;
            float hold=activeShot.holdDuration;
            float exit=activeShot.exitDuration;
            var panel=effects.board.Panel;
            restoreOpen=panel.IsOpen;restoreBlocked=panel.InputBlocked;restoreLocked=player.CinematicLocked;
            savedLocalPosition=player.view.localPosition;savedLocalRotation=player.view.localRotation;
            cameraView=player.view.GetComponent<Camera>();savedFov=cameraView.fieldOfView;
            player.CinematicLocked=true;panel.InputBlocked=true;
            effects.board.CancelDrag();
            yield return new WaitForSecondsRealtime(Mathf.Max(feedbackLead, feedback ? feedback.duration + .2f : 1f));
            panel.SetOpen(false);
            Cursor.lockState=CursorLockMode.None;Cursor.visible=false;
            // Keep the eye position fixed; world motion needs a clear view of the sky.
            Quaternion rotation=player.view.localRotation;
            SelectLightGroup();
            focusElevatedGroup=TryGroupPoint(out var center)&&effects.lights.ground&&
                center.y-effects.lights.ground.bounds.max.y>=activeShot.elevatedGroupHeight;
            TrackingTarget=activeShot.followTarget&&activeShot.aim!=IntroShotAim.PitchOnly&&
                (activeShot.aim!=IntroShotAim.AdaptiveLightGround||focusElevatedGroup);
            Quaternion corrected=ShotRotation(rotation);
            bool snap=activeShot.snapAboveAngle<180&&Quaternion.Angle(rotation,corrected)>activeShot.snapAboveAngle;
            if(snap){player.view.localRotation=corrected;rotation=corrected;}
            for(float t=0;t<enter;t+=Time.unscaledDeltaTime)
            {
                if(TrackingTarget)corrected=ShotRotation(savedLocalRotation);
                SetLetterbox(Mathf.SmoothStep(0,1,t/Mathf.Max(.001f,enter)));
                player.view.localRotation=Quaternion.Slerp(rotation,corrected,Mathf.SmoothStep(0,1,t/Mathf.Max(.001f,enter)));
                yield return null;
            }
            player.view.localRotation=corrected;
            SetLetterbox(1);
            yield return new WaitForEndOfFrame(); // Present a completed black border before the effect begins.
            apply();
            for(float t=0;t<hold;t+=Time.unscaledDeltaTime)
            {
                if(TrackingTarget)
                {
                    var current=player.view.localRotation;
                    var desired=ShotRotation(savedLocalRotation);
                    var smooth=Quaternion.Slerp(current,desired,1-Mathf.Exp(-activeShot.followResponse*Time.unscaledDeltaTime));
                    player.view.localRotation=Quaternion.RotateTowards(current,smooth,activeShot.maxFollowDegreesPerSecond*Time.unscaledDeltaTime);
                }
                yield return null;
            }
            corrected=player.view.localRotation;
            for(float t=0;t<exit;t+=Time.unscaledDeltaTime)
            {
                SetLetterbox(1-Mathf.SmoothStep(0,1,t/Mathf.Max(.001f,exit)));
                player.view.localRotation=Quaternion.Slerp(corrected,savedLocalRotation,Mathf.SmoothStep(0,1,t/Mathf.Max(.001f,exit)));
                yield return null;
            }
            Restore();
        }
        Quaternion ShotRotation(Quaternion original)
        {
            Quaternion parent=player.view.parent.rotation;
            Vector3 forward=parent*original*Vector3.forward;
            float elevation=Mathf.Asin(Mathf.Clamp(forward.y,-1,1))*Mathf.Rad2Deg;
            if(activeShot.target==IntroShotTarget.Sky)
                return ElevationRotation(forward,Mathf.Max(elevation,activeShot.skyMinimumElevation));
            if(activeShot.aim==IntroShotAim.PitchOnly||activeShot.aim==IntroShotAim.AdaptiveLightGround&&!focusElevatedGroup)
                return ElevationRotation(forward,elevation-Mathf.Clamp(activeShot.angleOffset.x,-activeShot.maxCorrectionAngle,activeShot.maxCorrectionAngle));
            Vector3 point;
            if(activeShot.target==IntroShotTarget.LightGroup)
            {if(!TryGroupPoint(out point))return original;}
            else if(activeShot.target==IntroShotTarget.Door)
            {if(!effects.door.door||!effects.door.door.Revealed)return original;point=effects.door.door.transform.position;}
            else
            {var nearest=effects.lights.Nearest(player.transform.position);if(!nearest)return original;point=nearest.transform.position;}
            FocusPoint=point+activeShot.targetOffset;
            Vector3 direction=FocusPoint-player.view.position;
            if(direction.sqrMagnitude<.001f)return original;
            float framePitch=Mathf.Atan((2*activeShot.targetViewportY-1)*Mathf.Tan(cameraView.fieldOfView*.5f*Mathf.Deg2Rad))*Mathf.Rad2Deg;
            // A lower-third target needs the camera to look above the target, not below it.
            var desired=Quaternion.Inverse(parent)*Quaternion.LookRotation(direction)*
                Quaternion.Euler((activeShot.aim==IntroShotAim.AdaptiveLightGround?0:activeShot.angleOffset.x)+framePitch,activeShot.angleOffset.y,0);
            return Quaternion.RotateTowards(original,desired,activeShot.maxCorrectionAngle);
        }
        Quaternion ElevationRotation(Vector3 forward,float degrees)
        {
            Vector3 heading=Vector3.ProjectOnPlane(forward,Vector3.up).normalized;
            if(heading.sqrMagnitude<.1f)heading=Vector3.ProjectOnPlane(player.transform.forward,Vector3.up).normalized;
            float angle=Mathf.Clamp(degrees,-85,85)*Mathf.Deg2Rad;
            return Quaternion.Inverse(player.view.parent.rotation)*Quaternion.LookRotation(heading*Mathf.Cos(angle)+Vector3.up*Mathf.Sin(angle));
        }
        void SelectLightGroup()
        {
            shotLights.Clear();
            if(activeShot.target!=IntroShotTarget.LightGroup)return;
            var eye=player.view.position;var heading=Vector3.ProjectOnPlane(player.view.forward,Vector3.up).normalized;
            OpeningLight seed=null;float best=float.NegativeInfinity;
            foreach(var lamp in effects.lights.Lamps)
            {
                if(!lamp||!lamp.isActiveAndEnabled)continue;
                var delta=lamp.transform.position-eye;var horizontal=Vector3.ProjectOnPlane(delta,Vector3.up);
                if(horizontal.sqrMagnitude<4)continue;
                float score=Vector3.Dot(heading,horizontal.normalized)-.15f*delta.magnitude/Mathf.Max(1,effects.lights.influenceRadius);
                if(score>best){best=score;seed=lamp;}
            }
            if(!seed)return;
            foreach(var lamp in effects.lights.Lamps)
                if(lamp&&lamp.isActiveAndEnabled&&Vector3.Distance(lamp.transform.position,seed.transform.position)<=activeShot.lightGroupRadius)
                    shotLights.Add(lamp);
        }
        bool TryGroupPoint(out Vector3 point)
        {
            point=Vector3.zero;int count=0;
            foreach(var lamp in shotLights)if(lamp&&lamp.isActiveAndEnabled){point+=lamp.transform.position;count++;}
            if(count==0)return false;
            point/=count;return true;
        }
        void Restore()
        {
            SetLetterbox(0);
            if(!Playing)return;
            player.view.localPosition=savedLocalPosition;player.view.localRotation=savedLocalRotation;
            if(cameraView)cameraView.fieldOfView=savedFov;
            player.CinematicLocked=restoreLocked;
            effects.board.Panel.InputBlocked=restoreBlocked;
            effects.board.Panel.SetOpen(restoreOpen);
            Cursor.lockState=restoreOpen?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=restoreOpen;
            Playing=false;routine=null;activeShot=null;TrackingTarget=false;shotLights.Clear();
        }
        public void Cancel(){revision++;if(routine!=null)StopCoroutine(routine);Restore();}
        void OnDisable()=>Cancel();
    }
}
