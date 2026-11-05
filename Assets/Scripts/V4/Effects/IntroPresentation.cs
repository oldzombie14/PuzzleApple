using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PuzzleApple.V4.Cognition;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

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
        public bool CanInterrupt { get; private set; }
        void Update()
        {
            if(Keyboard.current!=null && Keyboard.current.tabKey.wasPressedThisFrame) InterruptForEditing();
        }
        public bool InterruptForEditing()
        {
            if(!Playing || !CanInterrupt)return false;
            restoreOpen=true;
            Cancel();
            return true;
        }
        public int PlayCount { get; private set; }
        public float LetterboxProgress { get; private set; }
        public float ActiveEnterDuration { get; private set; }
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
        readonly Dictionary<int,string> previousConflicts=new Dictionary<int,string>();
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
        bool nearGroundLightMove;
        public Vector3 FocusPoint {get;private set;}
        public bool TrackingTarget {get;private set;}
        sealed class Observation
        {
            public int id;
            public string rule;
            public CognitionSignal[] signals;
            public bool lightOn, lightOff, doorOn, doorOff;
            public float nightBlend, mistVisibility;
            public bool wakingMotion;
        }
        public void Apply(CognitionChange kind, Action apply)
        {
            if(Playing)Cancel();
            int token=++revision;
            var state=effects.board.State;
            var observations=new List<Observation>();
            foreach(var group in state.Groups.Where(g=>g.Conflicted))
                if(!previousConflicts.TryGetValue(group.Id,out var oldConflict)||oldConflict!=group.RuleId)
                    if(feedback&&isActiveAndEnabled)StartCoroutine(ConflictFeedback(group.Id,group.RuleId));
            previousConflicts.Clear();
            foreach(var group in state.Groups.Where(g=>g.Conflicted))previousConflicts[group.Id]=group.RuleId;
            foreach(var group in state.Groups.Where(g=>g.Effective))
            {
                if(previous.TryGetValue(group.Id,out var rule)&&rule==group.RuleId)continue;
                var o=new Observation {id=group.Id,rule=group.RuleId,
                    signals=group.Signals.Where(s=>!activeSignals.Contains(s)).ToArray(),
                    lightOn=effects.lights.stateSource&&effects.lights.stateSource.IsOn,
                    lightOff=effects.lights.stateSource&&effects.lights.stateSource.IsOff,
                    nightBlend=effects.world.environment?effects.world.environment.NightBlend:0,
                    mistVisibility=effects.door.appearance?effects.door.appearance.MistVisibility:0,
                    doorOn=effects.door.appearance&&effects.door.appearance.IsOn,
                    doorOff=effects.door.appearance&&effects.door.appearance.IsOff};
                observations.Add(o);
            }
            if(!effects.lights.CanOperate&&observations.Any(o=>o.signals.Contains(CognitionSignal.LightOn)))
            {
                var waiting=state.Groups.FirstOrDefault(g=>g.Effective&&g.Signals.Contains(CognitionSignal.LightMove)&&previous.ContainsKey(g.Id));
                if(waiting!=null)observations.Add(new Observation{id=waiting.Id,rule=waiting.RuleId,signals=new[]{CognitionSignal.LightMove},wakingMotion=true});
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
        IEnumerator ConflictFeedback(int id,string rule)
        {
            // The board rebuilds the group UI after its state notification.
            yield return null;
            var group=effects.board.State.Find(id);
            if(group!=null&&group.Conflicted&&group.RuleId==rule&&feedback)feedback.Play(id,false);
        }
        IEnumerator Observe(List<Observation> observations,CognitionChange kind,int token,Action apply)
        {
            // Let the UI finish its commit; do not mutate the world during feedback.
            yield return new WaitForSecondsRealtime(.12f);
            if(token!=revision||effects.Suspended)yield break;
            var waking=observations.FirstOrDefault(o=>o.wakingMotion);
            if(waking!=null&&effects.lights.Lamps.Any(l=>l&&l.isActiveAndEnabled)&&kind!=CognitionChange.Split)
            {
                var on=observations.First(o=>o.signals.Contains(CognitionSignal.LightOn));
                if(feedback)feedback.Play(on.id,true);
                yield return new WaitForSecondsRealtime(feedback?feedback.duration+.1f:feedbackLead);
                if(token!=revision||effects.Suspended)yield break;
                var group=effects.board.State.Find(waking.id);
                if(group==null||!group.Effective)yield break;
                if(feedback)feedback.Play(waking.id,true);
                routine=StartCoroutine(Show(waking,apply));
                yield break;
            }
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
            if(!effects.lights.CanOperate&&(signal==CognitionSignal.LightMove||signal==CognitionSignal.LightWorldMove||signal==CognitionSignal.LightDoorMove||signal==CognitionSignal.LightLightMove||signal==CognitionSignal.DoorLightMove||signal==CognitionSignal.WorldLightMove))return false;
            var door=effects.door.door;
            var lamps=effects.lights.Lamps.Where(l=>l&&l.isActiveAndEnabled).ToArray();
                switch(signal)
                {
                    case CognitionSignal.LightOn: if(!o.lightOn||o.nightBlend>0)return true;break;
                    case CognitionSignal.LightOff: if(!o.lightOff||o.nightBlend<1)return true;break;
                    case CognitionSignal.DoorOn:
                        if(door&&door.Revealed&&(o.doorOff||effects.door.appearance.Brightness<1||door.LandingCompleted&&(!o.doorOn||o.mistVisibility<1)))return true;break;
                    case CognitionSignal.DoorOff:
                        if(door&&door.Revealed&&(!o.doorOff||effects.door.appearance.Brightness>0||o.mistVisibility>0))return true;break;
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
                        if(door&&door.Revealed){var target=effects.lights.Nearest(door.transform.position);if(effects.door.CanPursue(target))return true;}break;
                    case CognitionSignal.WorldLightMove:
                    case CognitionSignal.WorldDoorMove:
                        return effects.world.CanRise(signal);
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
            savedLocalPosition=player.view.localPosition;savedLocalRotation=LevelLocal(player.view.localRotation);
            player.view.localRotation=savedLocalRotation;
            cameraView=player.view.GetComponent<Camera>();savedFov=cameraView.fieldOfView;
            player.CinematicLocked=true;panel.InputBlocked=true;
            effects.board.CancelDrag();
            yield return new WaitForSecondsRealtime(Mathf.Max(feedbackLead, feedback ? feedback.duration + .2f : 1f));
            panel.SetOpen(false);
            Cursor.lockState=CursorLockMode.None;Cursor.visible=false;
            // Keep the eye position fixed; world motion needs a clear view of the sky.
            Quaternion rotation=player.view.localRotation;
            SelectLightGroup();
            nearGroundLightMove=observation.signals.Contains(CognitionSignal.LightMove)&&MajorityNearGround();
            focusElevatedGroup=TryGroupPoint(out var center)&&effects.lights.ground&&
                center.y-effects.lights.ground.bounds.max.y>=activeShot.elevatedGroupHeight;
            TrackingTarget=activeShot.followTarget&&activeShot.aim!=IntroShotAim.PitchOnly&&
                (activeShot.aim!=IntroShotAim.AdaptiveLightGround||focusElevatedGroup);
            Quaternion corrected=ShotRotation(rotation);
            if(activeShot.heightBasedPitchSpeed && TryGroupPoint(out var speedCenter))
            {
                float height=effects.lights.ground?speedCenter.y-effects.lights.ground.bounds.max.y:speedCenter.y-player.view.position.y;
                float speed=Mathf.Lerp(activeShot.lowPitchSpeed,activeShot.highPitchSpeed,Mathf.InverseLerp(activeShot.pitchSpeedHeightRange.x,activeShot.pitchSpeedHeightRange.y,height));
                enter=Mathf.Max(.35f,Quaternion.Angle(rotation,corrected)/Mathf.Max(.1f,speed));
            }
            bool snap=activeShot.snapAboveAngle<180&&Quaternion.Angle(rotation,corrected)>activeShot.snapAboveAngle;
            ActiveEnterDuration=enter;
            if(snap){player.view.localRotation=corrected;rotation=corrected;}
            for(float t=0;t<enter;t+=Time.unscaledDeltaTime)
            {
                if(TrackingTarget)corrected=ShotRotation(savedLocalRotation);
                SetLetterbox(Mathf.SmoothStep(0,1,t/Mathf.Max(.001f,enter)));
                player.view.localRotation=BlendLevel(rotation,corrected,Mathf.SmoothStep(0,1,t/Mathf.Max(.001f,enter)));
                yield return null;
            }
            player.view.localRotation=corrected;
            SetLetterbox(1);
            yield return new WaitForEndOfFrame(); // Present a completed black border before the effect begins.
            apply();
            CanInterrupt=true;
            for(float t=0;t<hold;t+=Time.unscaledDeltaTime)
            {
                if(TrackingTarget)
                {
                    var current=player.view.localRotation;
                    var desired=ShotRotation(savedLocalRotation);
                    var smooth=BlendLevel(current,desired,1-Mathf.Exp(-activeShot.followResponse*Time.unscaledDeltaTime));
                    float step=activeShot.maxFollowDegreesPerSecond*Time.unscaledDeltaTime;
                    player.view.localRotation=BlendLevel(current,smooth,Mathf.Min(1,step/Mathf.Max(.001f,Quaternion.Angle(current,smooth))));
                }
                yield return null;
            }
            for(float t=0;t<exit;t+=Time.unscaledDeltaTime)
            {
                SetLetterbox(1-Mathf.SmoothStep(0,1,t/Mathf.Max(.001f,exit)));
                yield return null;
            }
            Restore();
        }
        Quaternion ShotRotation(Quaternion original)
        {
            Quaternion parent=player.view.parent.rotation;
            Vector3 forward=parent*original*Vector3.forward;
            float elevation=Mathf.Asin(Mathf.Clamp(forward.y,-1,1))*Mathf.Rad2Deg;
            float framePitch=Mathf.Atan((2*activeShot.targetViewportY-1)*Mathf.Tan(cameraView.fieldOfView*.5f*Mathf.Deg2Rad))*Mathf.Rad2Deg;
            if(nearGroundLightMove)
                return ElevationRotation(forward,Mathf.Atan(Mathf.Tan(cameraView.fieldOfView*.5f*Mathf.Deg2Rad)/3)*Mathf.Rad2Deg);
            if(activeShot.target==IntroShotTarget.Horizon)
                return ElevationRotation(forward,-framePitch);
            if(activeShot.target==IntroShotTarget.Sky)
                return ElevationRotation(forward,Mathf.Max(elevation,Mathf.Max(activeShot.skyMinimumElevation,-framePitch)));
            if(activeShot.aim==IntroShotAim.PitchOnly||activeShot.aim==IntroShotAim.AdaptiveLightGround&&!focusElevatedGroup)
                return ElevationRotation(forward,elevation-activeShot.angleOffset.x);
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
            if(activeShot.aim==IntroShotAim.PitchToTarget)
                return ElevationRotation(forward,Mathf.Atan2(direction.y,Vector3.ProjectOnPlane(direction,Vector3.up).magnitude)*Mathf.Rad2Deg-framePitch-activeShot.angleOffset.x);
            // A lower-third target needs the camera to look above the target, not below it.
            var desired=Quaternion.Inverse(parent)*Quaternion.LookRotation(direction)*
                Quaternion.Euler((activeShot.aim==IntroShotAim.AdaptiveLightGround?0:activeShot.angleOffset.x)+framePitch,activeShot.angleOffset.y,0);
            return LevelLocal(desired);
        }
        Quaternion LevelLocal(Quaternion local)
        {
            var world=player.view.parent.rotation*local;
            return Quaternion.Inverse(player.view.parent.rotation)*Quaternion.LookRotation(world*Vector3.forward,Vector3.up);
        }
        Quaternion BlendLevel(Quaternion from,Quaternion to,float t)
        {
            var parent=player.view.parent.rotation;
            var a=parent*from*Vector3.forward;var b=parent*to*Vector3.forward;
            float yaw=Mathf.LerpAngle(Mathf.Atan2(a.x,a.z)*Mathf.Rad2Deg,Mathf.Atan2(b.x,b.z)*Mathf.Rad2Deg,t);
            float pitch=Mathf.Lerp(-Mathf.Asin(Mathf.Clamp(a.y,-1,1))*Mathf.Rad2Deg,-Mathf.Asin(Mathf.Clamp(b.y,-1,1))*Mathf.Rad2Deg,t);
            return Quaternion.Inverse(parent)*Quaternion.Euler(pitch,yaw,0);
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
        bool MajorityNearGround()
        {
            if(!effects.lights.ground)return false;
            float groundHeight=effects.lights.ground.bounds.max.y;
            int count=0,near=0;
            foreach(var lamp in shotLights)
            {
                if(!lamp||!lamp.isActiveAndEnabled)continue;
                var collider=lamp.GetComponent<Collider>();
                if(!collider)continue;
                count++;
                if(collider.bounds.min.y-groundHeight<=activeShot.nearGroundHeight)near++;
            }
            return count>0&&near*2>count;
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
            CanInterrupt=false;
            SetLetterbox(0);
            if(!Playing)return;
            player.view.localPosition=savedLocalPosition;
            player.AdoptCurrentView();
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
