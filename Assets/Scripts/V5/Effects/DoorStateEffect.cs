using System.Collections.Generic;
using System.Linq;
using PuzzleApple.V5.Cognition;

namespace PuzzleApple.V5
{
    public sealed class DoorStateEffect
    {
        readonly WorldEntry entry;
        readonly Dictionary<ISwitchableDoor,CognitionState.Group> owners=new Dictionary<ISwitchableDoor,CognitionState.Group>();
        readonly Dictionary<int,PassageJourney.LightSpace> commandProgress=new Dictionary<int,PassageJourney.LightSpace>();

        public DoorStateEffect(WorldEntry owner){entry=owner;}
        public static bool Commands(CognitionState.Group g,bool open)=>
            g!=null && g.Signals.Contains(open?CognitionSignal.DoorOn:CognitionSignal.DoorOff);
        public static bool Handles(CognitionState.Group g)=>Commands(g,true)||Commands(g,false);
        public PassageJourney.LightSpace TargetScope(CognitionState.Group group){
            var current=entry.sentenceRuntime.Scope(group);
            return commandProgress.TryGetValue(group.Id,out var progress)?current&progress:current;
        }
        public void Prepare(CognitionState.Group[] groups,CognitionState.Group[] added){
            var commands=groups.Where(Handles).ToArray();
            foreach(var id in commandProgress.Keys.Where(id=>!commands.Any(g=>g.Id==id)).ToArray())commandProgress.Remove(id);
            foreach(var group in commands)
                if(!commandProgress.ContainsKey(group.Id)||added.Contains(group))
                    // A one-shot command never recruits doors behind a lock it just opened.
                    commandProgress[group.Id]=ProgressLock.Accessible(entry.journey.ChapterSpaces);
        }

        IEnumerable<ISwitchableDoor> Targets(CognitionState.Group group){
            var scope=entry.sentenceRuntime.EffectScope(group);
            var journey=entry.journey;
            if((scope&PassageJourney.LightSpace.Outdoor)!=0)yield return entry.outdoor.World;
            if((scope&(PassageJourney.LightSpace.FirstPath|PassageJourney.LightSpace.SphereRoom))!=0)yield return journey.WhiteDoor;
            if((scope&PassageJourney.LightSpace.ReturnPath)!=0 && journey.EntranceDoor)yield return journey.EntranceDoor;
            if((scope&PassageJourney.LightSpace.FarRoom)!=0 && journey.EndingDoor)yield return journey.EndingDoor;
        }
        public void ChangeSpace(){foreach(var door in owners.Keys)door.Hold();owners.Clear();commandProgress.Clear();}
        public PassageJourney.LightSpace ChangingSpaces(CognitionState.Group group){
            var spaces=(PassageJourney.LightSpace)0;
            foreach(var target in Targets(group)){
                if(!target.WouldChange(Commands(group,true)))continue;
                if(ReferenceEquals(target,entry.journey.WhiteDoor))spaces|=PassageJourney.LightSpace.FirstPath|PassageJourney.LightSpace.SphereRoom;
                else if(ReferenceEquals(target,entry.journey.EntranceDoor))spaces|=PassageJourney.LightSpace.ReturnPath;
                else if(ReferenceEquals(target,entry.journey.EndingDoor))spaces|=PassageJourney.LightSpace.FarRoom;
                else spaces|=PassageJourney.LightSpace.Outdoor;
            }
            return spaces&entry.sentenceRuntime.EffectScope(group);
        }
        public bool WouldChange(CognitionState.Group group){
            foreach(var target in Targets(group)){
                if(target is ChargeDoor slab && owners.TryGetValue(target,out var applied) && applied==group && !slab.Driving)continue;
                if(target.WouldChange(Commands(group,true)))return true;
            }
            return false;
        }
        public void Release(int groupId){
            foreach(var target in owners.Where(p=>p.Value.Id==groupId).Select(p=>p.Key).ToArray()){
                target.Release();owners.Remove(target);
            }
            commandProgress.Remove(groupId);
        }
        public void Commit(CognitionState.Group[] groups,CognitionState.Group[] added){
            var next=new Dictionary<ISwitchableDoor,CognitionState.Group>();
            foreach(var group in groups.Where(Handles)){
                foreach(var target in Targets(group))
                    if(!next.ContainsKey(target))next.Add(target,group);
            }
            foreach(var pair in owners)if(!next.ContainsKey(pair.Key))pair.Key.Hold();
            foreach(var pair in next)
                if(!owners.TryGetValue(pair.Key,out var old) || old!=pair.Value || added.Contains(pair.Value))pair.Key.SetOpen(Commands(pair.Value,true));
            owners.Clear();foreach(var pair in next)owners.Add(pair.Key,pair.Value);
        }
    }
}
