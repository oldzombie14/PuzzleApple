using System.Collections.Generic;
using System.Linq;
using PuzzleApple.V5.Cognition;

namespace PuzzleApple.V5
{
    public sealed class DoorStateEffect
    {
        readonly WorldEntry entry;
        readonly Dictionary<ISwitchableDoor,CognitionState.Group> owners=new Dictionary<ISwitchableDoor,CognitionState.Group>();

        public DoorStateEffect(WorldEntry owner){entry=owner;}
        public static bool Commands(CognitionState.Group g,bool open)=>
            g!=null && g.Signals.Contains(open?CognitionSignal.DoorOn:CognitionSignal.DoorOff);
        public static bool Handles(CognitionState.Group g)=>Commands(g,true)||Commands(g,false);

        ISwitchableDoor Target(CognitionState.Group group){
            var scope=entry.sentenceRuntime.Scope(group);
            if((scope&PassageJourney.LightSpace.Outdoor)!=0)return entry.outdoor.World;
            if((scope&PassageJourney.LightSpace.FarRoom)!=0)return entry.journey.EndingDoor;
            return (scope&(PassageJourney.LightSpace.FirstPath|PassageJourney.LightSpace.SphereRoom))!=0?entry.journey.WhiteDoor:null;
        }
        public void ChangeSpace(){foreach(var door in owners.Keys)door.Hold();owners.Clear();}
        public bool WouldChange(CognitionState.Group group){
            var target=Target(group);
            if(target is ChargeDoor slab && owners.TryGetValue(target,out var applied) && applied==group && !slab.Driving)return false;
            return target?.WouldChange(Commands(group,true))??false;
        }
        public void Release(int groupId){
            foreach(var target in owners.Where(p=>p.Value.Id==groupId).Select(p=>p.Key).ToArray()){
                target.Release();owners.Remove(target);
            }
        }
        public void Commit(CognitionState.Group[] groups,CognitionState.Group[] added){
            var next=new Dictionary<ISwitchableDoor,CognitionState.Group>();
            foreach(var group in groups.Where(Handles)){
                var target=Target(group);
                if(target!=null && !next.ContainsKey(target))next.Add(target,group);
            }
            foreach(var pair in owners)if(!next.ContainsKey(pair.Key))pair.Key.Hold();
            foreach(var pair in next)
                if(!owners.TryGetValue(pair.Key,out var old) || old!=pair.Value || added.Contains(pair.Value))pair.Key.SetOpen(Commands(pair.Value,true));
            owners.Clear();foreach(var pair in next)owners.Add(pair.Key,pair.Value);
        }
    }
}
