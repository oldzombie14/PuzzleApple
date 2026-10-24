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
        [Min(0)] public float feedbackLead=.3f, blendDuration=.65f;
        [Min(.1f)] public float holdDuration=3;
        [Range(0,15)] public float maxCorrectionAngle=6;
        public bool Playing { get; private set; }
        public int PlayCount { get; private set; }
        [Range(0,.2f)] public float letterboxHeight=.09f;
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
            float height=letterboxHeight*progress;
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
        sealed class Observation
        {
            public int id;
            public string rule;
            public CognitionSignal[] signals;
            public bool lightOn, doorOn, doorOff, doorRevealed;
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
                    doorOff=effects.door.appearance&&effects.door.appearance.IsOff,
                    doorRevealed=effects.door.door&&effects.door.door.Revealed};
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
                bool changed=VisibleChange(o);
                if(feedback)feedback.Play(o.id,changed);
                if(changed&&shot==null)shot=o;
            }
            // ALL split paths are excluded, including a previously suppressed sentence taking over.
            if(shot!=null&&kind!=CognitionChange.Split&&kind!=CognitionChange.Layout&&!Playing)
                routine=StartCoroutine(Show(shot,apply));
            else apply();
        }
        bool VisibleChange(Observation o)
        {
            var door=effects.door.door;
            var lamps=effects.lights.Lamps.Where(l=>l&&l.isActiveAndEnabled).ToArray();
            foreach(var signal in o.signals)
            {
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
            }
            return false;
        }
        IEnumerator Show(Observation observation,Action apply)
        {
            Playing=true;PlayCount++;
            var panel=effects.board.Panel;
            restoreOpen=panel.IsOpen;restoreBlocked=panel.InputBlocked;restoreLocked=player.CinematicLocked;
            savedLocalPosition=player.view.localPosition;savedLocalRotation=player.view.localRotation;
            cameraView=player.view.GetComponent<Camera>();savedFov=cameraView.fieldOfView;
            player.CinematicLocked=true;panel.InputBlocked=true;
            effects.board.CancelDrag();
            yield return new WaitForSecondsRealtime(Mathf.Max(feedbackLead, feedback ? feedback.duration + .2f : 1f));
            panel.SetOpen(false);
            Cursor.lockState=CursorLockMode.None;Cursor.visible=false;
            // Keep the camera at the player's existing eye position; only nudge its orientation.
            Quaternion rotation=player.view.localRotation;
            Transform subject=Subject(observation);
            bool doorShot=subject&&effects.door.door&&subject==effects.door.door.transform;
            Vector3 direction=subject ? subject.position+(doorShot?Vector3.up*3:Vector3.zero)-player.view.position
                : player.view.forward+Vector3.up*.15f;
            Quaternion desired=direction.sqrMagnitude>.001f ? Quaternion.Inverse(player.view.parent.rotation)*Quaternion.LookRotation(direction) : rotation;
            Quaternion corrected=Quaternion.RotateTowards(rotation,desired,maxCorrectionAngle);
            for(float t=0;t<blendDuration;t+=Time.unscaledDeltaTime)
            {
                SetLetterbox(Mathf.SmoothStep(0,1,t/Mathf.Max(.001f,blendDuration)));
                player.view.localRotation=Quaternion.Slerp(rotation,corrected,Mathf.SmoothStep(0,1,t/Mathf.Max(.001f,blendDuration)));
                yield return null;
            }
            player.view.localRotation=corrected;
            SetLetterbox(1);
            yield return new WaitForEndOfFrame(); // Present a completed black border before the effect begins.
            apply();
            yield return new WaitForSecondsRealtime(holdDuration);
            for(float t=0;t<blendDuration;t+=Time.unscaledDeltaTime)
            {
                SetLetterbox(1-Mathf.SmoothStep(0,1,t/Mathf.Max(.001f,blendDuration)));
                player.view.localRotation=Quaternion.Slerp(corrected,savedLocalRotation,Mathf.SmoothStep(0,1,t/Mathf.Max(.001f,blendDuration)));
                yield return null;
            }
            Restore();
        }
        Transform Subject(Observation o)
        {
            if(!o.doorRevealed&&effects.door.door&&effects.door.door.Revealed&&o.signals.Contains(CognitionSignal.LightOff))
                return effects.door.door.transform;
            bool door=o.signals.Any(s=>s==CognitionSignal.DoorOn||s==CognitionSignal.DoorOff||s==CognitionSignal.DoorMove||
                s==CognitionSignal.DoorWorldMove||s==CognitionSignal.DoorLightMove||s==CognitionSignal.LightDoorMove||s==CognitionSignal.WorldDoorMove);
            if(door&&effects.door.door&&effects.door.door.Revealed)return effects.door.door.transform;
            if(o.signals.Contains(CognitionSignal.WorldMove))return null;
            var lamp=effects.lights.Nearest(player.transform.position);
            return lamp?lamp.transform:null;
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
            Playing=false;routine=null;
        }
        public void Cancel(){revision++;if(routine!=null)StopCoroutine(routine);Restore();}
        void OnDisable()=>Cancel();
    }
}
