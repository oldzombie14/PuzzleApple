using System.Collections.Generic;
using System.Linq;
using PuzzleApple.V5.Cognition;

namespace PuzzleApple.V5
{
    public sealed class LightStateEffect
    {
        readonly WorldEntry entry;
        readonly Dictionary<ISwitchableLight,CognitionState.Group> commanded=new Dictionary<ISwitchableLight,CognitionState.Group>();
        readonly Dictionary<int,PassageJourney.LightSpace> commandProgress=new Dictionary<int,PassageJourney.LightSpace>();
        public LightStateEffect(WorldEntry owner){entry=owner;}
        public static bool Commands(CognitionState.Group g,bool on)=>g!=null && g.Signals.Contains(on?CognitionSignal.LightOn:CognitionSignal.LightOff);
        public static bool Handles(CognitionState.Group g)=>Commands(g,true)||Commands(g,false);
        public PassageJourney.LightSpace TargetScope(CognitionState.Group group){
            var current=entry.sentenceRuntime.Scope(group);
            return commandProgress.TryGetValue(group.Id,out var progress)?current&progress:current;
        }
        IEnumerable<ISwitchableLight> Targets(CognitionState.Group g)
        {
            var scope=TargetScope(g);
            var passage=entry.outdoor;
            if((scope&PassageJourney.LightSpace.Outdoor)!=0)yield return passage.World;
            if((scope&PassageJourney.LightSpace.Gallery)!=0)yield return passage.Chapter;
            if((scope&PassageJourney.LightSpace.Vitruvian)!=0){yield return passage.Vitruvian;yield return passage.Vitruvian.puzzle;}
            if((scope&PassageJourney.LightSpace.Garden)!=0)yield return passage.Garden;
            if(entry.journey)foreach(var light in entry.journey.Lights(scope))yield return light;
        }
        public bool WouldChange(CognitionState.Group g)=>Targets(g).Any(l=>l.WouldChange(Commands(g,true)));
        public bool ShotAllowed(CognitionState.Group g)=>!Commands(g,true) || (entry.outdoor && entry.outdoor.IsOutside?entry.outdoor.World.stateSource.Brightness<.999f:WouldChange(g));
        public bool TrySettling(CognitionState.Group g,out bool settling){
            settling=Handles(g)&&Targets(g).Any(l=>l.Settling(Commands(g,true)));
            return Handles(g);
        }
        public void Release(int id){
            foreach(var target in commanded.Where(p=>p.Value.Id==id).Select(p=>p.Key).ToArray())commanded.Remove(target);
            commandProgress.Remove(id);
        }
        public void Prepare(CognitionState.Group[] groups,CognitionState.Group[] added){
            var lights=groups.Where(Handles).ToArray();
            foreach(var id in commandProgress.Keys.Where(id=>!lights.Any(g=>g.Id==id)).ToArray())commandProgress.Remove(id);
            foreach(var group in lights)
                if(!commandProgress.ContainsKey(group.Id)||added.Contains(group))
                    // Unlocking expands sustained effects, not a light command already in flight.
                    commandProgress[group.Id]=ProgressLock.Accessible(entry.journey.ChapterSpaces);
        }
        public void Commit(CognitionState.Group[] groups,CognitionState.Group[] added){
            var next=new Dictionary<ISwitchableLight,CognitionState.Group>();
            foreach(var group in groups.Where(Handles))
                foreach(var target in Targets(group))if(!next.ContainsKey(target))next.Add(target,group);
            foreach(var pair in commanded)if(!next.ContainsKey(pair.Key))pair.Key.Hold();
            foreach(var pair in next)
                if(!commanded.TryGetValue(pair.Key,out var old) || old!=pair.Value || added.Contains(pair.Value))pair.Key.SetOn(Commands(pair.Value,true));
            commanded.Clear();foreach(var pair in next)commanded.Add(pair.Key,pair.Value);
        }
    }
}
