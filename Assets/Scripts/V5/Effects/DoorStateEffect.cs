using System.Collections.Generic;
using System.Linq;
using PuzzleApple.V5.Cognition;

namespace PuzzleApple.V5
{
    public sealed class DoorStateEffect
    {
        readonly WorldEntry entry;
        readonly Dictionary<SentenceStateOutput,CognitionState.Group> owners=new Dictionary<SentenceStateOutput,CognitionState.Group>();

        public DoorStateEffect(WorldEntry owner){entry=owner;}
        public static bool Commands(CognitionState.Group g,bool open)=>
            g?.Intent?.Is(open?SentenceAction.On:SentenceAction.Off,SentenceCapabilityCatalog.Default.door)==true;
        public static bool Handles(CognitionState.Group g)=>Commands(g,true)||Commands(g,false);

        IEnumerable<SentenceStateOutput> Targets(CognitionState.Group group)=>entry.Entities.QueryOutputs(entry.sentenceRuntime.EffectScope(group), group.Intent.StateProperty);
        public void ChangeSpace(){foreach(var door in owners.Keys)door.Hold();owners.Clear();}
        public PassageJourney.LightSpace ChangingSpaces(CognitionState.Group group){
            var spaces=(PassageJourney.LightSpace)0;
            foreach(var target in Targets(group)){
                if(!target.WouldChange(Commands(group,true)))continue;
                spaces|=SentenceSpaceCatalog.Default.ToLegacy(entry.Entities.RegionOf(target));
            }
            return spaces&entry.sentenceRuntime.EffectScope(group);
        }
        public bool WouldChange(CognitionState.Group group)=>WouldChange(group,entry.sentenceRuntime.EffectScope(group));
        public bool WouldChange(CognitionState.Group group,PassageJourney.LightSpace scope){
            foreach(var target in entry.Entities.QueryOutputs(scope,group.Intent.StateProperty)){
                if(entry.sentenceRuntime.MotionFrozen && target.Definition!=SentenceCapabilityCatalog.Default.emission)continue;
                bool commanded = owners.TryGetValue(target,out var applied) && applied==group;
                if(commanded ? target.Settling(Commands(group,true)) : target.WouldChange(Commands(group,true)))return true;
            }
            return false;
        }
        public void Release(int groupId){
            foreach(var target in owners.Where(p=>p.Value.Id==groupId).Select(p=>p.Key).ToArray()){
                target.Release();owners.Remove(target);
            }
        }
        public void Commit(CognitionState.Group[] groups,CognitionState.Group[] added){
            var next=new Dictionary<SentenceStateOutput,CognitionState.Group>();
            foreach(var group in groups.Where(Handles)){
                foreach(var target in Targets(group))
                    if(!next.ContainsKey(target))next.Add(target,group);
            }
            foreach(var pair in owners)if(!next.ContainsKey(pair.Key))pair.Key.Hold();
            foreach(var pair in next)
                if(!owners.TryGetValue(pair.Key,out var old) || old!=pair.Value || added.Contains(pair.Value))
                    pair.Key.Command(Commands(pair.Value,true),entry.sentenceRuntime,pair.Value);
            owners.Clear();foreach(var pair in next)owners.Add(pair.Key,pair.Value);
        }
    }
}
