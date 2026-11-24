using System.Collections.Generic;
using System.Linq;
using PuzzleApple.V5.Cognition;

namespace PuzzleApple.V5
{
    public sealed class LightStateEffect
    {
        readonly WorldEntry entry;
        readonly Dictionary<SentenceStateOutput,CognitionState.Group> commanded=new Dictionary<SentenceStateOutput,CognitionState.Group>();
        public LightStateEffect(WorldEntry owner){entry=owner;}
        public static bool Commands(CognitionState.Group g,bool on)=>
            g?.Intent?.Is(on?SentenceAction.On:SentenceAction.Off,SentenceCapabilityCatalog.Default.light)==true;
        public static bool Handles(CognitionState.Group g)=>Commands(g,true)||Commands(g,false);
        IEnumerable<SentenceStateOutput> Targets(CognitionState.Group g)
        {
            var catalog = SentenceCapabilityCatalog.Default;
            return entry.Entities.QueryOutputs(entry.sentenceRuntime.EffectScope(g), g.Intent.StateProperty, catalog.emission);
        }
        public bool WouldChange(CognitionState.Group g,PassageJourney.LightSpace scope)=>
            entry.Entities.QueryOutputs(scope,g.Intent.StateProperty,SentenceCapabilityCatalog.Default.emission)
                .Any(output=>output.WouldChange(Commands(g,true)));
        public bool Settling(CognitionState.Group g,PassageJourney.LightSpace scope)=>
            entry.Entities.QueryOutputs(scope,g.Intent.StateProperty,SentenceCapabilityCatalog.Default.emission)
                .Any(output=>output.Settling(Commands(g,true)));
        public void Release(int id){
            foreach(var target in commanded.Where(p=>p.Value.Id==id).Select(p=>p.Key).ToArray())
            {
                target.Release();
                commanded.Remove(target);
            }
        }
        public void Commit(CognitionState.Group[] groups,CognitionState.Group[] added){
            var next=new Dictionary<SentenceStateOutput,CognitionState.Group>();
            foreach(var group in groups.Where(Handles))
                foreach(var target in Targets(group))if(!next.ContainsKey(target))next.Add(target,group);
            foreach(var pair in commanded)if(!next.ContainsKey(pair.Key))pair.Key.Hold();
            foreach(var pair in next)
                if(!commanded.TryGetValue(pair.Key,out var old) || old!=pair.Value || added.Contains(pair.Value))
                    pair.Key.Command(Commands(pair.Value,true),entry.sentenceRuntime,pair.Value);
            commanded.Clear();foreach(var pair in next)commanded.Add(pair.Key,pair.Value);
        }
    }
}
