using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PuzzleApple.V5.Cognition;
using Space=PuzzleApple.V5.PassageJourney.LightSpace;

namespace PuzzleApple.V5
{
    // A motion endpoint owns eligibility and the applied intent. Physical controllers retain
    // position, phase and momentum; replacing an intent never restarts that state.
    public sealed class SentenceMotionOutput : ISentenceOutputState<SentenceMotionOutput.DemonstrationState>
    {
        public SentenceOutputDefinition Definition {get;private set;}
        public ISentenceOutputState<T> StateSource<T>() where T : struct =>
            owner ? Channel.StateSource<T>() : null;

        Component owner;
        internal SentenceOutputChannel Channel { get; private set; }
        Action<SentenceMotionOutput> apply;
        Func<bool> canTakeLampControl;
        Func<SentenceMotion,bool> accepts;
        Func<SentenceMotion,CognitionState.Group,bool> changes;
        Func<CognitionState.Group,Space,bool> waiting;
        SentenceEntity entity;
        SentenceMotion structuralMotion;
        Func<bool> emitsLight;
        bool lightAtLastDispatch;
        internal bool EmitsLight => emitsLight?.Invoke()==true;
        internal bool LightPresenceChanged => emitsLight!=null && EmitsLight!=lightAtLastDispatch;
        internal bool HasSubject(CognitionState.Group group) => group.Intent.Motion!=SentenceMotion.Light || emitsLight==null || EmitsLight;

        internal void BindIllumination(SentenceMotion native,Func<bool> emits)
        {
            structuralMotion=native;
            emitsLight=emits;
            entity.WithProperty(native.Subject).WithProperty(SentenceCapabilityCatalog.Default.light,emits);
        }

        internal SentenceMotion ResolveMotion(CognitionState.Group group)
        {
            var motion=group.Intent.Motion;
            if(motion==SentenceMotion.Light && structuralMotion!=default)
                motion=EmitsLight?structuralMotion:default;
            return motion!=default && accepts(motion)?motion:default;
        }
        SentenceMotion lampMotion;
        MotionEffect knowledge;
        [Serializable]
        public struct DemonstrationState
        {
            public bool introduced, introducedWithMove, controlsLamps;
            public int revision, knowledgeEpoch;
            public bool[] relitLamps;

            internal DemonstrationState Copy()
            {
                var copy = this;
                if (relitLamps != null) copy.relitLamps = (bool[])relitLamps.Clone();
                return copy;
            }
            internal bool Valid => (!introducedWithMove || introduced) && revision >= 0 && knowledgeEpoch >= -1;
        }
        DemonstrationState demonstration = new DemonstrationState {knowledgeEpoch = -1};
        public DemonstrationState CaptureState()
        {
            SynchronizeKnowledge();
            return demonstration.Copy();
        }

        [Serializable]
        public struct Checkpoint<T> where T : struct
        {
            public T physical;
            public DemonstrationState demonstration;
        }

        public bool TryCapture<T>(out Checkpoint<T> checkpoint) where T : struct
        {
            var source = StateSource<T>();
            checkpoint = default;
            if (source == null) return false;
            checkpoint.physical = source.CaptureState();
            checkpoint.demonstration = CaptureState();
            return true;
        }

        public bool TryRestore<T>(Checkpoint<T> checkpoint) where T : struct
        {
            var source = StateSource<T>();
            if (source == null || !CanRestoreDemonstration(checkpoint.demonstration) ||
                !source.TryRestoreState(checkpoint.physical)) return false;
            ApplyDemonstration(checkpoint.demonstration);
            return true;
        }

        bool CanRestoreDemonstration(DemonstrationState state) =>
            owner && state.Valid && state.knowledgeEpoch == (knowledge?.ControlEpoch ?? -1) &&
            (!state.controlsLamps || knowledge?.MoveKnown == true);
        public bool TryRestoreState(DemonstrationState state)
        {
            if (!CanRestoreDemonstration(state)) return false;
            ApplyDemonstration(state);
            return true;
        }

        void ApplyDemonstration(DemonstrationState state)
        {
            demonstration = state.Copy();
            TrackLampControl();
        }

        public bool ControlsLamps => demonstration.controlsLamps;
        public int LampMotionRevision => demonstration.revision;
        internal void RememberRelitLamp(int slot, int lampCount)
        {
            if (lampMotion != default) return;
            if (demonstration.relitLamps == null || demonstration.relitLamps.Length != lampCount)
                Array.Resize(ref demonstration.relitLamps, lampCount);
            demonstration.relitLamps[slot] = true;
        }
        public bool Introduced => demonstration.introduced;
        public bool IntroducedWithMove => demonstration.introducedWithMove;
        public void Introduce(bool knowsMove){if(Introduced)return;demonstration.introduced=true;demonstration.introducedWithMove=knowsMove;}
        public bool DemonstrationMoving(bool isOn,bool demonstration,bool requirePlayerMotion=false,int lampSlot=-1)
        {
            if(requirePlayerMotion||IntroducedWithMove)return isOn&&HasMotion(SentenceMotion.Light);
            var relit = this.demonstration.relitLamps;
            bool continuesDrift = relit != null && lampSlot >= 0 && lampSlot < relit.Length && relit[lampSlot];
            return ControlsLamps ? !isOn || continuesDrift || HasMotion(SentenceMotion.Light) : demonstration;
        }
        void SetLampMotion(SentenceMotion motion)
        {
            if (lampMotion == motion) return;
            lampMotion = motion;
            demonstration.revision++;
            demonstration.relitLamps = null;
        }
        internal void TrackLampControl()
        {
            if(knowledge?.MoveKnown==true&&lampMotion!=default&&(canTakeLampControl==null||canTakeLampControl()))demonstration.controlsLamps=true;
        }
        internal void ObserveKnowledge(MotionEffect current)
        {
            knowledge = current;
            SynchronizeKnowledge();
        }
        void SynchronizeKnowledge()
        {
            if (knowledge == null) return;
            if (demonstration.knowledgeEpoch != knowledge.ControlEpoch || !knowledge.MoveKnown)
                demonstration.controlsLamps = false;
            demonstration.knowledgeEpoch = knowledge.ControlEpoch;
        }
        readonly List<(CognitionState.Group group, SentenceMotion motion, string rule, long established)> appliedGroups =
            new List<(CognitionState.Group, SentenceMotion, string, long)>();

        public bool HasMotion(SentenceMotion motion) => Channel.Drives(motion);
        internal float DirectedStep(float normal,float distance,float dt)
        {
            if(knowledge==null)return normal;
            var runtime=knowledge.Runtime;
            foreach(var applied in appliedGroups){
                if(!runtime.IsFinishing(applied.established) || !Owns(applied.motion,applied.established))continue;
                float fraction=runtime.FinishFraction(applied.group,runtime.entry.Entities.SpacesOf(this),dt);
                normal=Mathf.Max(normal,distance*fraction);
            }
            return normal;
        }

        public IEnumerable<long> Owners(SentenceMotion motion)
        {
            foreach (var applied in appliedGroups)
                if (applied.motion == motion && applied.group.Effective && applied.group.RuleId == applied.rule &&
                    applied.group.Established == applied.established)
                    yield return applied.established;
        }
        public bool Owns(SentenceMotion motion, long established)
        {
            foreach (var applied in appliedGroups)
                if (applied.established == established && applied.motion == motion && applied.group.Effective &&
                    applied.group.Established == established && applied.group.RuleId == applied.rule) return true;
            return false;
        }
        bool BlockedByEarlierMotion(CognitionState.Group group,SentenceMotion motion)
        {
            foreach(var applied in appliedGroups)
                if(applied.established<group.Established && applied.group.Effective && applied.group.Established==applied.established &&
                    applied.group.RuleId==applied.rule && applied.motion!=motion)return true;
            return false;
        }
        public bool WouldChange(CognitionState.Group group,Space scope)
        {
            var motion=ResolveMotion(group);
            return motion!=default && !Channel.CommandActive && !BlockedByEarlierMotion(group,motion) && changes(motion,group);
        }
        public bool WaitingForLight(CognitionState.Group group,Space scope)
        {
            var motion=ResolveMotion(group);
            return motion!=default && waiting!=null && !BlockedByEarlierMotion(group,motion) && waiting(group,scope);
        }
        internal void Apply(IEnumerable<CognitionState.Group> groups, MotionEffect current)
        {
            // Forgetting move resets control even if this output was disabled at the time.
            ObserveKnowledge(current);
            lightAtLastDispatch=EmitsLight;
            Channel.HoldMotion();appliedGroups.Clear();
            SentenceMotion nextLamp = default;
            foreach(var group in groups){
                // Groups are edited in place; an edit must not give an old commit a new owner.
                var motion = ResolveMotion(group);
                if (motion == default) continue;
                if (appliedGroups.Count>0 && appliedGroups[0].motion!=motion) continue;
                appliedGroups.Add((group, motion, group.RuleId, group.Established));
                Channel.ApplyMotion(motion);
                if (nextLamp == default && MotionEffect.MovesLamps(group)) nextLamp = motion;
            }
            SetLampMotion(nextLamp);TrackLampControl();
            apply?.Invoke(this);
        }
        internal void Hold(){Channel.HoldMotion();appliedGroups.Clear();SetLampMotion(default);apply?.Invoke(this);}

        public static SentenceMotionOutput Bind(SentenceEntityRegistry registry,Component lifetime,
            Func<Space> region,SentenceOutputDefinition definition,Func<SentenceMotion,bool> accepts,Func<SentenceMotion,CognitionState.Group,bool> changes,
            Func<CognitionState.Group,Space,bool> waiting=null,Action<SentenceMotionOutput> apply=null,Func<bool> canTakeLampControl=null, object stateSource=null, Type stateType=null, Func<bool> emits=null)
        {
            var output=lifetime.GetComponents<SentenceEntity>().Select(e=>e.Endpoint).OfType<SentenceMotionOutput>().FirstOrDefault(o=>o.owner==lifetime)
                ??new SentenceMotionOutput();
            output.Channel = SentenceOutputChannel.Bind(lifetime, stateSource ?? lifetime, definition, stateType);
            output.owner=lifetime;output.Definition=definition;output.accepts=accepts;output.changes=changes;output.waiting=waiting;output.apply=apply;
            output.canTakeLampControl=canTakeLampControl;output.emitsLight=emits;
            output.entity=SentenceEntity.Bind(registry,lifetime,output,region);
            apply?.Invoke(output);
            return output;
        }
    }
}
