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
        bool WordOnly => Target.wordOnly || (Target == entry.journey.movementObservation && !entry.journey.LampsLit) ||
            (Target==sphereObservation && trackedMember is Collider member && !entry.journey.Tutorial.IsLit(member));
        public bool Known => Target && (entry.board.State.HasAcquired(Target.lesson.acquisitionId) ||
            WordOnly && entry.board.State.Knows(Target.lesson.verb.Id));
        public bool Showing=>Target&&CanObserve;
        bool completedHold;
        void Start(){ring.raycastTarget=false;}
        public bool CanObserve=>entry.Entered&&!entry.Busy&&!entry.ConfirmingExit&&entry.room.Opened&&
            entry.player.CanInteractWithWorld&&!entry.chamberLight.Collecting&&!entry.chamberLight.ShowingKnown;
        public ObservableBehaviour FindTarget()=>FindTarget(out _);
        Component trackedMember;
        ObservableBehaviour FindTarget(out Component member)
        {
            member=null;
            if(!CanObserve)return null;
            var ray=new Ray(entry.player.view.position,entry.player.view.forward);
            var journey = entry.journey;
            if (journey && journey.InCorridor && journey.ActiveStars && journey.ActiveStars.Moving)
            {
                member = entry.chamberLight.PickStar(ray, range, out _);
                if (member) return journey.ActiveStars.Observation;
            }
            var spheres = SphereObservation();
            if (spheres && journey.InTutorial && !entry.sentenceRuntime.MotionFrozen &&
                (!entry.portableClosure || !entry.portableClosure.Isolated))
            {
                spheres.members = journey.Tutorial.MovingSpheres();
                member = spheres.MemberAt(ray, range);
                if (member) return spheres;
            }
            foreach(var target in targets)
                if(target&&(member=target.MemberAt(ray,range)))return target;
            return null;
        }
        // The move tutorial's floating and blocking spheres teach the same lesson as the corridor stars.
        ObservableBehaviour sphereObservation;
        ObservableBehaviour SphereObservation()
        {
            var journey = entry.journey;
            if (sphereObservation || !journey || !journey.Tutorial || !journey.movementObservation) return sphereObservation;
            sphereObservation = journey.Tutorial.gameObject.AddComponent<ObservableBehaviour>();
            sphereObservation.lesson = journey.movementObservation.lesson;
            sphereObservation.aimToleranceDegrees = journey.movementObservation.aimToleranceDegrees;
            return sphereObservation;
        }
        void NoteMoveSource()
        {
            if (Target.lesson.verb.Id != "move") return;
            var library = entry.board.GetComponentInChildren<WordLibrary>(true);
            if (!library) library = FindFirstObjectByType<WordLibrary>();
            if (library) library.NoteMoveSource(Target == sphereObservation && sphereObservation
                ? WordLibrary.MoveOrigin.Spheres : WordLibrary.MoveOrigin.Stars);
        }
        // ChamberLight routes the shared left-button gesture before deciding noun collection.
        public void Advance(bool held,float deltaTime)
        {
            if(!held){completedHold=false;Progress=0;Target=FindTarget(out trackedMember);return;}
            if(completedHold)return;
            var next=FindTarget(out var member);
            if(next!=Target||member!=trackedMember){Progress=0;Target=next;trackedMember=member;}
            if(!Target){Progress=0;return;}
            Progress=Mathf.Clamp01(Progress+Mathf.Max(0,deltaTime)/Mathf.Max(.1f,Target.lesson.holdDuration));
            if(Progress<1)return;
            completedHold=true;
            if(!Known){NoteMoveSource();entry.chamberLight.CollectObservation(Target.lesson,WordOnly);}
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
