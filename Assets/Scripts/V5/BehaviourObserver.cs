using UnityEngine;

namespace PuzzleApple.V5
{
    [DefaultExecutionOrder(-50)]
    public sealed class BehaviourObserver : MonoBehaviour
    {
        public WorldEntry entry;
        public ObservableBehaviour[] targets=new ObservableBehaviour[0];
        public ObservationRing ring;
        [Min(.1f)] public float range=40;
        public ObservableBehaviour Target {get;private set;}
        public float Progress {get;private set;}
        public bool Known=>Target&&entry.board.State.HasAcquired(Target.lesson.acquisitionId);
        public bool Showing=>Target&&CanObserve;
        bool completedHold;
        void Start(){ring.raycastTarget=false;}
        public bool CanObserve=>entry.Entered&&!entry.Busy&&!entry.ConfirmingExit&&entry.room.Opened&&
            entry.player.CanInteractWithWorld&&!entry.chamberLight.Collecting&&!entry.chamberLight.ShowingKnown;
        public ObservableBehaviour FindTarget()
        {
            if(!CanObserve)return null;
            var ray=new Ray(entry.player.view.position,entry.player.view.forward);
            ObservableBehaviour nearest=null;float distance=range;
            foreach(var target in targets)
                if(target&&target.Hit(ray,range,out float hit)&&hit<=distance){nearest=target;distance=hit;}
            if(nearest&&Physics.Raycast(ray,out var solid,range,~0,QueryTriggerInteraction.Ignore)&&solid.distance<distance&&!nearest.Contains(solid.collider))return null;
            return nearest;
        }
        // ChamberLight routes the shared left-button gesture before deciding noun collection.
        public void Advance(bool held,float deltaTime)
        {
            if(!held){completedHold=false;Progress=0;Target=FindTarget();return;}
            if(completedHold)return;
            var next=FindTarget();
            if(next!=Target){Progress=0;Target=next;}
            if(!Target){Progress=0;return;}
            Progress=Mathf.Clamp01(Progress+Mathf.Max(0,deltaTime)/Mathf.Max(.1f,Target.lesson.holdDuration));
            if(Progress<1)return;
            completedHold=true;
            if(!Known)entry.chamberLight.CollectObservation(Target.lesson);
            else entry.chamberLight.ShowKnownVocabulary(Target.lesson.verb.Id);
        }
        void LateUpdate()
        {
            ring.gameObject.SetActive(Showing);
            if(Showing)
            {
                ring.material=entry.chamberLight.hoverEye.material;
                // Keep loading legible even when this action was learned already.
                ring.SetState(Progress,Progress>0?1:Known?entry.chamberLight.knownEyeAlpha:1);
            }
        }
        void OnDisable(){Target=null;Progress=0;completedHold=false;if(ring)ring.gameObject.SetActive(false);}
    }
}
