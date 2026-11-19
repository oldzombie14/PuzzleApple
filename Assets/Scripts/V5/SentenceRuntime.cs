using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PuzzleApple.V5.Cognition;
using UnityEngine;

namespace PuzzleApple.V5
{
    // Requested grammar and applied effects are separate so presentation can defer new
    // changes without stopping already-running effects. A split always commits immediately.
    public sealed class SentenceRuntime : MonoBehaviour
    {
        public WorldEntry entry;
        public SentencePresentation presentation;
        readonly Dictionary<int,string> previous=new Dictionary<int,string>();
        readonly Dictionary<int,string> conflicts=new Dictionary<int,string>();
        readonly Dictionary<int,string> pendingTakeovers=new Dictionary<int,string>();
        readonly HashSet<CognitionSignal> applied=new HashSet<CognitionSignal>();
        readonly Dictionary<CognitionSignal,PassageJourney.LightSpace> appliedScopes=new Dictionary<CognitionSignal,PassageJourney.LightSpace>();
        PassageJourney.LightSpace currentScope;
        public PassageJourney.LightSpace Scope(CognitionState.Group group)=>entry.journey.SentenceScope();
        public PassageJourney.LightSpace DoorPresentationScope(CognitionState.Group group)=>doorState.ChangingSpaces(group);
        public PassageJourney.LightSpace EffectScope(CognitionState.Group group){
            if(LightStateEffect.Handles(group))return lightState.TargetScope(group);
            if(DoorStateEffect.Handles(group))return doorState.TargetScope(group);
            // Presentation may choose one focus; effects visit every eligible object in scope.
            return Scope(group);
        }
        void Update(){ReevaluateScope();motion?.TrackLampControl();}
        public void ReevaluateScope(){
            if(!entry || !entry.Entered || entry.board.State==null)return;
            var next=entry.journey.SentenceScope();if(next==currentScope)return;
            currentScope=next;
            // Only retarget effects already committed; pending presentation still owns its start.
            var groups=entry.board.State.Groups.Where(g=>g.Effective && previous.TryGetValue(g.Id,out var rule) && rule==g.RuleId).OrderBy(g=>g.Established).ToArray();
            Commit(groups,System.Array.Empty<CognitionState.Group>(),CognitionChange.Layout);
        }
        public bool HasEffect(CognitionSignal signal,PassageJourney.LightSpace space)=>appliedScopes.TryGetValue(signal,out var scope)&&(scope&space)!=0;
        readonly HashSet<long> requested=new HashSet<long>();
        readonly HashSet<long> rejected=new HashSet<long>();
        int revision;
        LightStateEffect lightState;
        DoorStateEffect doorState;
        MotionEffect motion;
        IlluminationEffects illumination;
        static SentenceKind Kind(CognitionState.Group g)=>g.Rule?g.Rule.Kind:SentenceKind.Sustained;
        static bool WorldFlow(CognitionState.Group g)=>Kind(g)==SentenceKind.WorldFlow;
        static bool Motion(CognitionState.Group g)=>g.Rule&&g.Rule.Motion;
        public float WorldExposure=>illumination?illumination.WorldExposure:0;
        public float FloorIllumination=>illumination?illumination.FloorIllumination:0;

        float runningTimeScale;
        public bool MotionFrozen {get;private set;}

        void SetMotionFrozen(bool freeze)
        {
            if(MotionFrozen==freeze)return;
            if(freeze)runningTimeScale=Time.timeScale;
            Time.timeScale=freeze?0:runningTimeScale;
            MotionFrozen=freeze;
        }

        void OnDisable()
        {
            SetMotionFrozen(false);

        }

        public void ChangeSpace()
        {
            currentScope=entry.journey.SentenceScope();
            doorState.ChangeSpace();
            illumination.Apply(this);
        }
        bool observationTransferred,doorLessonTransferred;
        public float LampMultiplier => illumination ? illumination.LampMultiplier : 1;
        public float LampBoost(PassageJourney.LightSpace space)=>illumination?illumination.LampBoost(space):1;
        public bool HasEffect(CognitionSignal effect)=>applied.Contains(effect);
        public bool Owns(CognitionSignal signal,PassageJourney.LightSpace space,long established)
        {
            foreach(var g in entry.board.State.Groups)
                if(g.Established==established && g.Effective && previous.TryGetValue(g.Id,out var rule) && rule==g.RuleId && g.Signal==signal && (EffectScope(g)&space)!=0)return true;
            return false;
        }
        public int LightMotionRevision(PassageJourney.LightSpace space)=>motion.Revision(space);
        public bool HasLampMotion(PassageJourney.LightSpace space)=>motion.HasLampMotion(space);
        public bool ControlsLamps(PassageJourney.LightSpace space)=>motion!=null&&motion.Controls(space);
        public bool PlayerDirectedLampMotion=>motion!=null&&motion.PlayerDirectedLampMotion;
        public void Initialize(WorldEntry owner)
        {
            entry=owner;
            entry.feedback.runtime=this;
            illumination=gameObject.AddComponent<IlluminationEffects>();
            illumination.Bind(this);
            lightState=new LightStateEffect(owner);
            doorState=new DoorStateEffect(owner);
            motion=new MotionEffect(owner);
            if(!presentation)presentation=GetComponent<SentencePresentation>();
            if(!presentation)presentation=gameObject.AddComponent<SentencePresentation>();
            presentation.runtime=this;
            if(!presentation.settings)presentation.settings=Resources.Load<SentencePresentationSettings>("V5/SentencePresentation");
            var lesson=entry.journey&&entry.journey.movementObservation?entry.journey.movementObservation.lesson:null;
            observationTransferred=lesson&&entry.board.State!=null&&entry.board.State.HasAcquired(lesson.acquisitionId);
        }
        public void Refresh(CognitionChange kind)
        {
            if(!entry||!entry.board||entry.board.State==null||kind==CognitionChange.Layout)return;
            var state=entry.board.State;
            SetMotionFrozen(state.HasEffect(CognitionSignal.FreezeMotion));
            entry.InterruptRemovedWorldSentence();
            var groups=state.Groups.Where(g=>g.Effective)
                .Where(g=>!MotionFrozen || !WorldFlow(g)
                    || (previous.TryGetValue(g.Id,out var activeRule) && activeRule==g.RuleId))
                .OrderBy(g=>g.Established).ToArray();
            if(state.RequirePeriod){
                var acceptedIds=groups.Select(g=>g.Established);
                var rejectedIds=state.Groups.Where(g=>g.Conflicted).Select(g=>g.Established);
                if(requested.SetEquals(acceptedIds) && rejected.SetEquals(rejectedIds))return;
                requested.Clear();requested.UnionWith(acceptedIds);
                rejected.Clear();rejected.UnionWith(rejectedIds);
            }
            var added=groups.Where(g=>!previous.TryGetValue(g.Id,out var rule)||rule!=g.RuleId).ToArray();
            doorState.Prepare(groups,added);
            var newConflicts=entry.board.State.Groups.Where(g=>g.Conflicted&&(!conflicts.TryGetValue(g.Id,out var rule)||rule!=g.RuleId)).Select(g=>g.Id).ToArray();
            foreach(var g in added)
                if(conflicts.TryGetValue(g.Id,out var oldRule)&&oldRule==g.RuleId)pendingTakeovers[g.Id]=g.RuleId;
            foreach(var id in pendingTakeovers.Keys.ToArray())
                if(!groups.Any(g=>g.Id==id&&g.RuleId==pendingTakeovers[id]))pendingTakeovers.Remove(id);
            conflicts.Clear();foreach(var g in entry.board.State.Groups.Where(g=>g.Conflicted))conflicts[g.Id]=g.RuleId;
            int version=++revision;
            presentation.Cancel();
            if(!entry.Entered)return;
            var lesson=entry.journey&&entry.journey.movementObservation?entry.journey.movementObservation.lesson:null;
            bool granted=lesson&&!observationTransferred&&entry.board.State.HasAcquired(lesson.acquisitionId);
            if(granted)observationTransferred=true;
            bool doorGranted=!doorLessonTransferred && entry.journey && entry.journey.EndingDoor && entry.journey.EndingDoor.LessonAcquired;
            if(doorGranted)doorLessonTransferred=true;
            foreach(var id in newConflicts)StartCoroutine(ConflictPulse(id,version));
            // A world-close sequence owns the exit UI; it monitors its own sentence.
            if(pendingTakeovers.Count>0&&!entry.ConfirmingExit)
            {
                // Stop the displaced effects at their current values while the new sentence is shown.
                var retained=groups.Where(g=>!pendingTakeovers.ContainsKey(g.Id)&&!added.Contains(g)).ToArray();
                Commit(retained,System.Array.Empty<CognitionState.Group>(),kind);
                var announce=groups.Where(g=>pendingTakeovers.ContainsKey(g.Id)||added.Contains(g)).ToArray();
                StartCoroutine(Present(groups,announce,kind,version));
                return;
            }
            if(state.RequirePeriod && added.Length==0 && previous.Keys.Any(id=>!groups.Any(g=>g.Id==id))){
                Commit(groups,added,kind);return;
            }
            if(kind==CognitionChange.Split||granted||doorGranted){Commit(groups,added,kind);return;}
            if(entry.ConfirmingExit)return;
            StartCoroutine(Present(groups,added,kind,version));
        }
        IEnumerator ConflictPulse(int id,int version)
        {
            yield return null;
            if(version==revision&&entry.board.State.Find(id)?.Conflicted==true)entry.feedback.Play(id,false);
        }
        IEnumerator Present(CognitionState.Group[] groups,CognitionState.Group[] added,CognitionChange kind,int version)
        {
            // Board views are rebuilt after the state event, as in V4 IntroPresentation.
            yield return null;
            if(version!=revision)yield break;
            while(pendingTakeovers.Count>0&&entry.Busy&&version==revision)yield return null;
            if(version!=revision)yield break;
            CognitionState.Group shot=null;
            foreach(var g in added)
            {
                bool changed=VisibleChange(g);
                if(!WorldFlow(g))entry.feedback.Play(g.Id,changed);
                if(changed&&!MotionFrozen&&!(entry.outdoor&&entry.outdoor.InChapter)&&shot==null&&presentation.HasShot(g)
                    &&lightState.ShotAllowed(g))shot=g;
            }
            if(shot!=null&&!entry.Busy)
                yield return presentation.Play(shot.RuleId,()=>{if(version==revision)Commit(groups,added,kind);},Kind(shot)==SentenceKind.Command?shot:null,shot);
            else Commit(groups,added,kind);
        }
        bool VisibleChange(CognitionState.Group g)
        {
            if(MotionFrozen&&Motion(g))return false;
            if(g.Signals.All(s=>appliedScopes.TryGetValue(s,out var active) && (active&Scope(g))==Scope(g)))return false;
            return HasSceneEffect(g);
        }

        public bool SustainsFeedback(CognitionState.Group g)
        {
            if(Kind(g)!=SentenceKind.Sustained || (g.Signal!=CognitionSignal.FreezeMotion && g.Signal!=CognitionSignal.IlluminateWorld && (Scope(g)&entry.journey.PlayerSpace)==0))return false;
            if(Motion(g) && !motion.CanMove(g,HasEffect,EffectScope(g)&entry.journey.PlayerSpace))return false;
            return EffectInProgress(g);
        }

        bool EffectInProgress(CognitionState.Group g)
        {
            if(!g.Effective || !entry.Entered)return false;
            if(MotionFrozen&&Motion(g))return false;
            if(WorldFlow(g))return entry.RunningWorldCommand(g.Id);
            if(!previous.TryGetValue(g.Id,out var rule)||rule!=g.RuleId||g.Signals.Count==0)return false;
            // One applied effect has one feedback owner. A surviving duplicate can take over.
            foreach(var other in entry.board.State.Groups)
                if(other.Effective && other.RuleId==rule && other.Established<g.Established && (Scope(other)&Scope(g))==Scope(g))return false;
            return HasSceneEffect(g);
        }
        bool HasSceneEffect(CognitionState.Group g)
        {
            if(g.Signal==CognitionSignal.FreezeMotion)return true;
            if(LightStateEffect.Handles(g))return lightState.WouldChange(g);
            if(DoorStateEffect.Handles(g))return doorState.WouldChange(g);
            if(g.Signal==CognitionSignal.MirrorCopy || g.Signal==CognitionSignal.MirrorMove){
                var pass=entry.outdoor;
                var room=(Scope(g)&PassageJourney.LightSpace.Gallery)!=0?pass.Chapter:
                    (Scope(g)&PassageJourney.LightSpace.Vitruvian)!=0?pass.Vitruvian:null;
                return room && room.mirrors && room.mirrors.HasTargets;
            }
            if(MotionEffect.Handles(g))return motion.CanMove(g,HasEffect);
            if(IlluminationEffects.Handles(g))return illumination.WouldChange(g);
            return false;
        }
        public void FinishCommand(int id)
        {
            var group=entry.board.State.Find(id);
            if(group!=null)StartCoroutine(Retire(group,group.Established));
        }
        public void RetireFoldedReflection(CognitionState.Group group)
        {
            StartCoroutine(Retire(group,group.Established,true));
        }
        IEnumerator Retire(CognitionState.Group group,long established,bool finished=false)
        {
            var board=entry.board;
            float elapsed=0,duration=Mathf.Max(.1f,entry.feedback.duration);
            yield return null;
            while(board.State.Find(group.Id)==group && group.Established==established && group.Effective)
            {
                bool inProgress=!finished && (lightState.TrySettling(group,out bool settling)?settling:EffectInProgress(group));
                if(Motion(group) && (MotionFrozen || motion.WaitingForLight(group,EffectScope(group))))inProgress=true;
                if(inProgress){elapsed=0;board.SetSentenceOpacity(group.Id,1);yield return null;continue;}
                if(!board.Panel.IsOpen) elapsed=duration;
                else elapsed+=Time.unscaledDeltaTime;
                float opacity=1-Mathf.SmoothStep(0,1,elapsed/duration);
                board.SetSentenceOpacity(group.Id,opacity);
                if(opacity<=0 && !presentation.Playing && !entry.Busy && committedRevision==revision)
                {
                    // Let the commanded object finish its transition after its sentence leaves.
                    lightState.Release(group.Id);
                    doorState.Release(group.Id);
                    board.State.ReturnToLibrary(group.Id,0,true);
                    yield break;
                }
                yield return null;
            }
        }
        int committedRevision;
        void Commit(CognitionState.Group[] groups,CognitionState.Group[] added,CognitionChange kind)
        {
            committedRevision=revision;
            previous.Clear();foreach(var g in groups)previous[g.Id]=g.RuleId;
            foreach(var g in groups)pendingTakeovers.Remove(g.Id);
            lightState.Prepare(groups,added);
            applied.Clear();appliedScopes.Clear();
            foreach(var g in groups)foreach(var signal in g.Signals) {
                applied.Add(signal);
                appliedScopes.TryGetValue(signal,out var mask);
                appliedScopes[signal]=mask|EffectScope(g);
            }
            motion.Commit(entry.board.State,groups);
            illumination.Apply(this);
            lightState.Commit(groups,added);
            if(entry.outdoor&&entry.outdoor.IsOutside)entry.outdoor.World.ApplyMotion(HasEffect,kind==CognitionChange.Split);
            doorState.Commit(groups,added);
            foreach(var g in groups)
                if(g.Signal==CognitionSignal.DoorMove && entry.journey.EndingDoor && (EffectScope(g)&entry.journey.EndingDoor.ControlSpaces)!=0 && !entry.journey.EndingDoor.AcceptedCycle(g.Established))entry.journey.EndingDoor.StartCycle(g.Established);
            foreach(var g in added)
            {
                if(Kind(g)==SentenceKind.Command)FinishCommand(g.Id);
                else if(WorldFlow(g)&&g.Signals.Contains(CognitionSignal.WorldOn))entry.OpenWorld(g.Id);
                else if(WorldFlow(g)&&g.Signals.Contains(CognitionSignal.WorldOff))entry.CloseWorldFromSentence(g.Id);
            }
        }
    }
}
