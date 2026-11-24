using System;
using System.Linq;
using System.Collections.Generic;
using PuzzleApple.V5.Cognition;

namespace PuzzleApple.V5
{
    // Native motion uses parsed Move intents; deferred legacy rules retain their .motion channel.
    // Queries registered motion endpoints and pushes scoped intents. Controllers migrate
    // individually from the legacy applied-effect reader to their own endpoint.
    public sealed class MotionEffect
    {
        readonly WorldEntry entry;
        public MotionEffect(WorldEntry owner){entry=owner;}
        internal SentenceRuntime Runtime => entry.sentenceRuntime;

        public static bool Handles(CognitionState.Group g)=>g?.Intent?.Action==SentenceAction.Move ||
            g?.Intent?.Action==SentenceAction.Legacy&&g.Rule&&g.Rule.Motion;

        public static bool MovesLamps(CognitionState.Group group)=>group?.Intent?.Is(SentenceAction.Move,SentenceCapabilityCatalog.Default.light)==true;

        internal bool MoveKnown { get; private set; }
        internal int ControlEpoch { get; private set; }
        public void ObserveKnowledge(CognitionState state)
        {
            bool known=state.Knows("move");
            if(known==MoveKnown)return;
            if(!known)ControlEpoch++;
            MoveKnown=known;
            foreach(var output in appliedOutputs)output.ObserveKnowledge(this);
        }
        public void TrackLampControl()
        {
            foreach(var output in appliedOutputs)output.TrackLampControl();
        }

        readonly HashSet<SentenceMotionOutput> appliedOutputs=new HashSet<SentenceMotionOutput>();
        bool tracksDrift;
        public bool LightPresenceChanged(){
            if(!tracksDrift)return false;
            foreach(var output in appliedOutputs)if(output.LightPresenceChanged)return true;
            return false;
        }
        // Compare the first eligible owner at each address. A partially blocked
        // sentence can own its free addresses, never those reserved by an earlier one.
        public CognitionState.Group BlockingOwner(CognitionState.Group group,IReadOnlyList<CognitionState.Group> accepted)
        {
            if(!accepted.Any(g=>g.Intent.Action==SentenceAction.Move))return null;
            CognitionState.Group blocker=null;
            var runtime=entry.sentenceRuntime;
            foreach(var output in entry.Entities.Query<SentenceMotionOutput>(runtime.EffectScope(group))){
                var desired=output.ResolveMotion(group);
                if(desired==default || !output.HasSubject(group))continue;
                var owner=FirstOwner(output,group,accepted,out var held);
                if(held==desired)return null;
                if(owner==null)return null;
                if(blocker==null || owner.ExecutionLocked)blocker=owner;
            }
            return blocker;
        }

        CognitionState.Group FirstOwner(SentenceMotionOutput output,CognitionState.Group candidate,
            IReadOnlyList<CognitionState.Group> groups,out SentenceMotion held)
        {
            var region=entry.Entities.SpacesOf(output);
            CognitionState.Group owner=null;
            held=default;
            foreach(var other in groups){
                if(!other.Effective || other.Intent.Action!=SentenceAction.Move || (candidate.Established!=0 && other.Established>=candidate.Established) ||
                    (owner!=null && other.Established>=owner.Established) || (entry.sentenceRuntime.EffectScope(other)&region)==0)continue;
                var motion=output.ResolveMotion(other);
                if(motion==default)continue;
                owner=other;
                held=motion;
            }
            return owner;
        }

        public bool CanMove(CognitionState.Group group,PassageJourney.LightSpace? targetScope=null,bool newEffect=false)
        {
            var scope=targetScope??entry.sentenceRuntime.EffectScope(group);
            foreach(var output in entry.Entities.Query<SentenceMotionOutput>(scope)){
                if(!output.WouldChange(group,scope))continue;
                // Earlier speech may still be waiting for the same presentation to start.
                if(!newEffect || FirstOwner(output,group,entry.board.State.Groups,out _)==null)return true;
            }
            return false;
        }

        public bool OwnsVisibleMotion(CognitionState.Group group,PassageJourney.LightSpace scope)
        {
            foreach(var output in entry.Entities.Query<SentenceMotionOutput>(scope))
                if(output.WouldChange(group,scope) && output.Owners(output.ResolveMotion(group)).FirstOrDefault()==group.Established)return true;
            return false;
        }

        public bool WaitingForLight(CognitionState.Group group,PassageJourney.LightSpace scope)
        {
            var c=SentenceCapabilityCatalog.Default;
            if(!MovesLamps(group) && group.Intent?.Moves(c.floor,c.light)!=true)return false;
            return entry.Entities.Query<SentenceMotionOutput>(scope).Any(output=>output.WaitingForLight(group,scope));
        }

        public void Commit(CognitionState state,CognitionState.Group[] groups)
        {
            ObserveKnowledge(state);
            // A fully blocked but still spoken drift can become eligible when another light appears.
            tracksDrift=state.Groups.Any(g=>g.Spoken && g.Intent?.Motion==SentenceMotion.Light);
            var outputs=entry.Entities.QueryAll<SentenceMotionOutput>().ToArray();
            foreach(var output in appliedOutputs)if(!outputs.Contains(output))output.Hold();
            appliedOutputs.Clear();
            foreach(var output in outputs){
                var region=entry.Entities.SpacesOf(output);
                output.Apply(groups.Where(g=>Handles(g)&&(entry.sentenceRuntime.EffectScope(g)&region)!=0),this);
                appliedOutputs.Add(output);
            }
        }
    }
}
