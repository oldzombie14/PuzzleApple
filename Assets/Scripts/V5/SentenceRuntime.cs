using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PuzzleApple.V5.Cognition;
using UnityEngine;
using Space=PuzzleApple.V5.PassageJourney.LightSpace;

namespace PuzzleApple.V5
{
    internal enum SentenceResponse { NoTarget, Waiting, Remote, Local }

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
        readonly List<(SentenceIntent intent, PassageJourney.LightSpace scope, long established)> appliedIntents =
            new List<(SentenceIntent, PassageJourney.LightSpace, long)>();
        PassageJourney.LightSpace currentScope;
        int entityRevision=-1;
        public bool Emits(SentenceProperty property,PassageJourney.LightSpace scope)=>illumination && illumination.Emits(property,scope);
        public PassageJourney.LightSpace Scope(CognitionState.Group group)=>entry.journey.SentenceScope();
        public PassageJourney.LightSpace DoorPresentationScope(CognitionState.Group group)=>doorState.ChangingSpaces(group);
        readonly Dictionary<long,PassageJourney.LightSpace> commandRegions = new Dictionary<long,PassageJourney.LightSpace>();
        public PassageJourney.LightSpace EffectScope(CognitionState.Group group)
        {
            var current = Scope(group);
            return commandRegions.TryGetValue(group.Established,out var region) ? current & region : current;
        }

        void UpdateCommandRegions(CognitionState state)
        {
            // Only successfully started commands capture a region, before their presentation begins.
            var commands = state.Groups.Where(g => g.Spoken && g.Intent?.Scope == SentenceScopePolicy.CommandRegion).ToArray();
            foreach (var key in commandRegions.Keys.Where(key => !commands.Any(g => g.Established == key)).ToArray())
                commandRegions.Remove(key);
            foreach (var g in commands)
                if (!commandRegions.ContainsKey(g.Established)) commandRegions.Add(g.Established,Scope(g));
        }
        void Update(){ReevaluateScope();motion?.TrackLampControl();}
        public void ReevaluateScope(){
            if(!entry || !entry.Entered || entry.board.State==null)return;
            var next=entry.journey.SentenceScope();
            int nextEntities=entry.Entities.Revision;
            if(next==currentScope && nextEntities==entityRevision && !motion.LightPresenceChanged())return;
            currentScope=next;
            entityRevision=nextEntities;
            entry.board.State.ReevaluateMotionConflicts();
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
        static SentenceKind Kind(CognitionState.Group g)=>g.Intent?.Kind??(g.Rule?g.Rule.Kind:SentenceKind.Sustained);
        static bool WorldFlow(CognitionState.Group g)=>Kind(g)==SentenceKind.WorldFlow;
        static bool Motion(CognitionState.Group g)=>MotionEffect.Handles(g);
        public bool HasIntent(SentenceAction action, SentenceProperty subject, PassageJourney.LightSpace? scope = null)
        {
            foreach (var applied in appliedIntents)
                if (applied.intent.Is(action, subject) && (!scope.HasValue || (applied.scope & scope.Value) != 0)) return true;
            return false;
        }
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
            entityRevision=-1;
            doorState.ChangeSpace();
            illumination.Apply(this);
        }
        bool observationTransferred,doorLessonTransferred;
        public float LampMultiplier => illumination ? illumination.LampMultiplier : 1;
        public float LampBoost(PassageJourney.LightSpace space)=>illumination?illumination.LampBoost(space):1;
        public bool HasEffect(CognitionSignal effect)=>applied.Contains(effect);
        public bool Owns(CognitionState.Group group, PassageJourney.LightSpace space)
        {
            if (!group.Effective) return false;
            foreach (var applied in appliedIntents)
                if (applied.established == group.Established && (applied.scope & space) != 0) return true;
            return false;
        }
        public void Initialize(WorldEntry owner)
        {
            entry=owner;
            entry.feedback.runtime=this;
            illumination=gameObject.AddComponent<IlluminationEffects>();
            illumination.Bind(this);
            lightState=new LightStateEffect(owner);
            doorState=new DoorStateEffect(owner);
            motion=new MotionEffect(owner);
            owner.board.State.MotionBlocker=motion.BlockingOwner;
            owner.board.State.CanStartCommand=CanStartCommand;
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
            UpdateCommandRegions(state);
            motion.ObserveKnowledge(state);
            SetMotionFrozen(state.Groups.Any(g=>g.Effective&&g.Intent?.Action==SentenceAction.Freeze));
            entry.InterruptRemovedWorldSentence();
            var groups=state.Groups.Where(g=>g.Effective)
                .Where(g=>!MotionFrozen || !WorldFlow(g)
                    || (previous.TryGetValue(g.Id,out var activeRule) && activeRule==g.RuleId))
                .OrderBy(g=>g.Established).ToArray();
            var lesson=entry.journey&&entry.journey.movementObservation?entry.journey.movementObservation.lesson:null;
            bool granted=lesson&&!observationTransferred&&entry.board.State.HasAcquired(lesson.acquisitionId);
            if(granted)observationTransferred=true;
            bool doorGranted=!doorLessonTransferred && entry.journey && entry.journey.EndingDoor && entry.journey.EndingDoor.LessonAcquired;
            if(doorGranted)doorLessonTransferred=true;
            if(state.RequirePeriod){
                var acceptedIds=groups.Select(g=>g.Established);
                var rejectedIds=state.Groups.Where(g=>g.Conflicted).Select(g=>g.Established);
                if(requested.SetEquals(acceptedIds) && rejected.SetEquals(rejectedIds))return;
                requested.Clear();requested.UnionWith(acceptedIds);
                rejected.Clear();rejected.UnionWith(rejectedIds);
            }
            var added=groups.Where(g=>!previous.TryGetValue(g.Id,out var rule)||rule!=g.RuleId).ToArray();
            var newConflicts=entry.board.State.Groups.Where(g=>g.Conflicted&&(!conflicts.TryGetValue(g.Id,out var rule)||rule!=g.RuleId)).Select(g=>g.Id).ToArray();
            foreach(var g in added)
                if(conflicts.TryGetValue(g.Id,out var oldRule)&&oldRule==g.RuleId)pendingTakeovers[g.Id]=g.RuleId;
            foreach(var id in pendingTakeovers.Keys.ToArray())
                if(!groups.Any(g=>g.Id==id&&g.RuleId==pendingTakeovers[id]))pendingTakeovers.Remove(id);
            conflicts.Clear();foreach(var g in entry.board.State.Groups.Where(g=>g.Conflicted))conflicts[g.Id]=g.RuleId;
            int version=++revision;
            presentation.Cancel();
            if(!entry.Entered)return;
            if(kind==CognitionChange.Add || kind==CognitionChange.Join)
                foreach(var id in newConflicts)StartCoroutine(ConflictPulse(id,version));
            // A world-close sequence owns the exit UI; it monitors its own sentence.
            if(state.RequirePeriod && pendingTakeovers.Count>0 && added.All(g=>pendingTakeovers.ContainsKey(g.Id))){
                Commit(groups,added,kind);return;
            }
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
            if(kind==CognitionChange.Split||granted||doorGranted||entry.outdoor && entry.outdoor.InLesson && entry.outdoor.Lesson.TransferringMirror){Commit(groups,added,kind);return;}
            if(entry.ConfirmingExit)return;
            StartCoroutine(Present(groups,added,kind,version));
        }
        IEnumerator ConflictPulse(int id,int version)
        {
            yield return null;
            if(version==revision&&entry.board.State.Find(id)?.Conflicted==true)entry.feedback.Play(id,UnexecutedResponse(entry.board.State.Find(id)));
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
                if(WorldFlow(g))continue; // The box owns its camera and feedback.
                var response=Response(g,starting:true);
                entry.feedback.Play(g.Id,response);
                if(response==SentenceResponse.Local&&shot==null&&presentation.HasShot(g))shot=g;
            }
            if(shot!=null&&!entry.Busy)
                yield return presentation.Play(shot.RuleId,()=>{if(version==revision)Commit(groups,added,kind);},Kind(shot)==SentenceKind.Command?shot:null,shot);
            else Commit(groups,added,kind);
        }
        internal bool Committed(CognitionState.Group group)=>
            previous.TryGetValue(group.Id,out var rule) && rule==group.RuleId;

        internal SentenceResponse Response(CognitionState.Group group,bool starting=false)
        {
            var scope=EffectScope(group);
            var local=scope & entry.journey.PlayerSpace;
            bool moving=group.Intent?.Action==SentenceAction.Move;
            bool responds=group.Effective && (!Motion(group) || !MotionFrozen);
            if(responds && !moving){
                // One shared effect has one feedback owner, including while a copy is being added.
                responds=!entry.board.State.Groups.Any(other=>other!=group && other.Effective &&
                    other.RuleId==group.RuleId && other.Established<group.Established &&
                    (EffectScope(other)&scope)==scope);
            }
            bool Active(Space region){
                if(region==0)return false;
                if(moving && !starting && !group.OneShot)
                    return motion.OwnsVisibleMotion(group,region);
                if(moving)return motion.CanMove(group,region,starting);
                return HasSceneEffect(group,region);
            }
            if(responds && Active(local))return SentenceResponse.Local;
            if(responds && Active(scope & ~local))return SentenceResponse.Remote;
            return HasPotential(group,scope)?SentenceResponse.Waiting:SentenceResponse.NoTarget;
        }

        internal SentenceResponse UnexecutedResponse(CognitionState.Group group)=>
            HasPotential(group,EffectScope(group))?SentenceResponse.Waiting:SentenceResponse.NoTarget;

        bool HasPotential(CognitionState.Group group,Space scope)
        {
            if(group.Intent?.Action==SentenceAction.Move)return entry.Entities.Query<SentenceMotionOutput>(scope)
                .Any(output=>output.HasPotentialTarget(group));
            if(DoorStateEffect.Handles(group))return entry.Entities.QueryOutputs(scope,group.Intent.StateProperty)
                .Any(output=>output.WouldChange(DoorStateEffect.Commands(group,true)));
            if(WorldFlow(group))return entry.CanStartWorldCommand(group.Intent.Action==SentenceAction.On);
            return HasSceneEffect(group,scope);
        }

        public bool SustainsFeedback(CognitionState.Group g)=>
            !g.OneShot && g.Effective && Committed(g) && Responding(Response(g));

        internal static bool Responding(SentenceResponse response)=>
            response==SentenceResponse.Local || response==SentenceResponse.Remote;

        bool CanStartCommand(CognitionState.Group group)
        {
            if (!entry.Entered || (MotionFrozen && (Motion(group) || WorldFlow(group)))) return false;
            if (WorldFlow(group)) return entry.CanStartWorldCommand(group.Intent.Action==SentenceAction.On);
            return HasSceneEffect(group,EffectScope(group));
        }

        bool HasSceneEffect(CognitionState.Group g,Space scope)
        {
            if(scope==0)return false;
            if(g.Intent?.Action==SentenceAction.Freeze)return true;
            if(LightStateEffect.Handles(g))return lightState.WouldChange(g,scope);
            if(DoorStateEffect.Handles(g))return doorState.WouldChange(g,scope);
            if(entry.outdoor && (scope&Space.ChapterLesson)!=0 && entry.outdoor.InLesson && entry.outdoor.Lesson.HandlesMirror(g.Signal))
                return entry.outdoor.Lesson.MirrorWouldChange(g.Signal);
            if(g.Signal==CognitionSignal.MirrorCopy || g.Signal==CognitionSignal.MirrorMove){
                var pass=entry.outdoor;
                return pass && ((scope&Space.Gallery)!=0 && pass.Chapter && pass.Chapter.mirrors && pass.Chapter.mirrors.HasTargets ||
                    (scope&Space.Vitruvian)!=0 && pass.Vitruvian && pass.Vitruvian.mirrors && pass.Vitruvian.mirrors.HasTargets);
            }
            if(MotionEffect.Handles(g))return motion.CanMove(g,scope);
            if(IlluminationEffects.Handles(g))return illumination.WouldChange(g,scope);
            return false;
        }
        const float CommandFinishSeconds=.5f;
        readonly Dictionary<long,double> finishDeadlines=new Dictionary<long,double>();

        static double CommandTime(CognitionState.Group group) =>
            Motion(group) ? Time.timeAsDouble : Time.unscaledTimeAsDouble;

        internal bool IsFinishing(long established)=>finishDeadlines.ContainsKey(established);
        internal float FinishFraction(CognitionState.Group group,PassageJourney.LightSpace spaces,float delta)
        {
            if(delta<=0 || (spaces&entry.journey.PlayerSpace)!=0 ||
                !finishDeadlines.TryGetValue(group.Established,out var end))return 0;
            float remaining=(float)(end-CommandTime(group))-delta;
            return delta/Mathf.Max(delta,remaining);
        }

        public void FinishCommand(int id)
        {
            var group=entry.board.State.Find(id);
            if(group!=null)StartCoroutine(Retire(group,group.Established));
        }
        public void RetireFoldedReflection(CognitionState.Group group)
        {
            StartCoroutine(FadeFoldedReflection(group,group.Established));
        }
        IEnumerator FadeFoldedReflection(CognitionState.Group group,long established)
        {
            var board=entry.board;
            float elapsed=0,duration=Mathf.Max(.1f,entry.feedback.duration);
            yield return null;
            while(board.State.Find(group.Id)==group && group.Established==established && group.Effective){
                elapsed=board.Panel.IsOpen?elapsed+Time.unscaledDeltaTime:duration;
                board.SetSentenceOpacity(group.Id,1-Mathf.SmoothStep(0,1,elapsed/duration));
                if(elapsed>=duration && !presentation.Playing && !entry.Busy && committedRevision==revision){
                    board.State.CompleteExecution(group,established);
                    yield break;
                }
                yield return null;
            }
        }
        bool LocalCommandRunning(CognitionState.Group group)
        {
            var local=EffectScope(group)&entry.journey.PlayerSpace;
            if(WorldFlow(group))return entry.RunningWorldCommand(group.Id) || entry.Busy;
            if(Motion(group))return MotionFrozen || motion.CanMove(group,local);
            if(LightStateEffect.Handles(group))return lightState.Settling(group,local);
            if(DoorStateEffect.Handles(group))return doorState.WouldChange(group,local);
            return HasSceneEffect(group,local);
        }
        IEnumerator Retire(CognitionState.Group group,long established)
        {
            var board=entry.board;
            yield return null;
            try{
                while(board.State.Find(group.Id)==group && group.Established==established && group.Effective){
                    if(LocalCommandRunning(group))
                        finishDeadlines.Remove(established);
                    else if(!finishDeadlines.TryGetValue(established,out var deadline))
                        finishDeadlines.Add(established,CommandTime(group)+CommandFinishSeconds);
                    else if(CommandTime(group)>=deadline){
                        board.FadeCompletedSentence(group.Id,Mathf.Max(.1f,entry.feedback.duration));
                        lightState.Release(group.Id);
                        doorState.Release(group.Id);
                        board.State.CompleteExecution(group,established);
                        yield break;
                    }
                    yield return null;
                }
            }finally{finishDeadlines.Remove(established);}
        }
        int committedRevision;
        void Commit(CognitionState.Group[] groups,CognitionState.Group[] added,CognitionChange kind)
        {
            if(entry.board.State.RequirePeriod){
                // The camera lead can outlast a target or a connection. Release an unstarted command for a manual retry.
                var unavailable=added.FirstOrDefault(g=>g.ExecutionLocked && !Committed(g) && !CanStartCommand(g));
                if(unavailable!=null){entry.board.State.RejectBeforeExecution(unavailable);return;}
            }
            committedRevision=revision;
            previous.Clear();foreach(var g in groups)previous[g.Id]=g.RuleId;
            foreach(var g in groups)pendingTakeovers.Remove(g.Id);
            appliedIntents.Clear();
            foreach(var g in groups)if(g.Intent!=null)appliedIntents.Add((g.Intent,EffectScope(g),g.Established));
            applied.Clear();appliedScopes.Clear();
            foreach(var g in groups)foreach(var signal in g.Signals) {
                applied.Add(signal);
                appliedScopes.TryGetValue(signal,out var mask);
                appliedScopes[signal]=mask|EffectScope(g);
            }
            motion.Commit(entry.board.State,groups);
            illumination.Apply(this);
            lightState.Commit(groups,added);
            doorState.Commit(groups,added);
            foreach(var g in added)
            {
                if(Kind(g)==SentenceKind.Command || WorldFlow(g))FinishCommand(g.Id);
                if(WorldFlow(g)&&g.Intent?.Action==SentenceAction.On)entry.OpenWorld(g.Id);
                else if(WorldFlow(g)&&g.Intent?.Action==SentenceAction.Off)entry.CloseWorldFromSentence(g.Id);
            }
        }
    }
}
