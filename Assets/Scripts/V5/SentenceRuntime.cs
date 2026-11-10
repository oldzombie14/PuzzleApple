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
        readonly HashSet<CognitionSignal> applied=new HashSet<CognitionSignal>();
        int lightGroup=-1,doorGroup=-1,revision;
        bool secondLight,outdoorLight;
        public void EnterOutdoor()
        {
            // A retained room command does not re-fire merely because the player arrived.
            var light=entry.board.State.Groups.Where(g=>g.Effective).OrderBy(g=>g.Established)
                .FirstOrDefault(g=>g.Signals.Contains(CognitionSignal.LightOn)||g.Signals.Contains(CognitionSignal.LightOff));
            lightGroup=light==null?-1:light.Id;doorGroup=-1;outdoorLight=true;secondLight=false;
        }
        bool observationTransferred;
        public bool HasEffect(CognitionSignal effect)=>applied.Contains(effect);
        public void Initialize(WorldEntry owner)
        {
            entry=owner;
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
            entry.InterruptRemovedWorldSentence();
            var groups=entry.board.State.Groups.Where(g=>g.Effective).OrderBy(g=>g.Established).ToArray();
            var added=groups.Where(g=>!previous.TryGetValue(g.Id,out var rule)||rule!=g.RuleId).ToArray();
            var newConflicts=entry.board.State.Groups.Where(g=>g.Conflicted&&(!conflicts.TryGetValue(g.Id,out var rule)||rule!=g.RuleId)).Select(g=>g.Id).ToArray();
            previous.Clear();foreach(var g in groups)previous[g.Id]=g.RuleId;
            conflicts.Clear();foreach(var g in entry.board.State.Groups.Where(g=>g.Conflicted))conflicts[g.Id]=g.RuleId;
            int version=++revision;
            presentation.Cancel();
            if(!entry.Entered)return;
            var lesson=entry.journey&&entry.journey.movementObservation?entry.journey.movementObservation.lesson:null;
            bool granted=lesson&&!observationTransferred&&entry.board.State.HasAcquired(lesson.acquisitionId);
            if(granted)observationTransferred=true;
            foreach(var id in newConflicts)StartCoroutine(ConflictPulse(id,version));
            // A world-close sequence owns the exit UI; it monitors its own sentence.
            if(kind==CognitionChange.Split||granted){Commit(groups,added,kind);return;}
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
            CognitionState.Group shot=null;
            foreach(var g in added)
            {
                bool changed=VisibleChange(g);
                if(g.RuleId!="world.on"&&g.RuleId!="world.off")entry.feedback.Play(g.Id,changed);
                if(changed&&shot==null&&presentation.HasShot(g.RuleId))shot=g;
            }
            if(shot!=null&&!entry.Busy)
                yield return presentation.Play(shot.RuleId,()=>{if(version==revision)Commit(groups,added,kind);});
            else Commit(groups,added,kind);
        }
        bool VisibleChange(CognitionState.Group g)
        {
            if(g.Signals.All(s=>applied.Contains(s)))return false;
            var j=entry.journey;
            if(entry.outdoor&&entry.outdoor.IsOutside)
            {
                var w=entry.outdoor.World;
                switch(g.RuleId)
                {
                    case "light.on":return w.stateSource.Brightness<.999f||w.environment.NightBlend>.001f;
                    case "light.off":return w.stateSource.Brightness>.001f||w.environment.NightBlend<.999f;
                    case "door.on":return w.doors.appearance.Brightness<.999f;
                    case "door.off":return w.doors.appearance.Brightness>.001f;
                    case "light.move":case "light.floor.move":case "light.door.move":return w.lights.CanOperate&&w.field.ActiveCount>0;
                    case "light.light.move":return w.lights.CanOperate&&w.field.ActiveCount>1;
                    case "door.light.move":return w.doors.CanPursue(w.lights.Nearest(w.door.transform.position));
                    case "floor.light.move":return w.world.CanRise(CognitionSignal.WorldLightMove);
                    case "floor.door.move":return w.world.CanRise(CognitionSignal.WorldDoorMove);
                    case "door.floor.move":return !w.door.Landed;
                    case "door.move":case "floor.move":return true;
                }
            }
            switch(g.RuleId)
            {
                case "light.on":return (j&&j.InSecondRoom?j.Brightness:entry.chamberLight.Brightness)<.999f;
                case "light.off":return (j&&j.InSecondRoom?j.Brightness:entry.chamberLight.Brightness)>.001f;
                case "door.on":return j&&j.DoorProgress<.999f;
                case "door.off":return j&&j.DoorProgress>.001f;
                case "light.move":return j&&j.InSecondRoom?j.Lamps.Count>0:entry.chamberLight&&(!j||!j.BeyondChamber);
                case "light.door.move":case "light.light.move":return j&&j.InSecondRoom&&j.Lamps.Count>0;
                case "light.floor.move":return j&&j.floorMotion&&j.Lamps.Any(l=>!j.floorMotion.IsArrived(l));
                default:return false;
            }
        }
        void Commit(CognitionState.Group[] groups,CognitionState.Group[] added,CognitionChange kind)
        {
            applied.Clear();foreach(var g in groups)foreach(var signal in g.Signals)applied.Add(signal);
            var light=groups.FirstOrDefault(g=>g.Signals.Contains(CognitionSignal.LightOn)||g.Signals.Contains(CognitionSignal.LightOff));
            if(light==null)
            {
                if(lightGroup>=0){if(outdoorLight&&entry.outdoor.IsOutside)entry.outdoor.World.stateSource.HoldCurrentState();else if(secondLight)entry.journey.HoldLights();else entry.chamberLight.HoldCurrentState();}
                lightGroup=-1;
            }
            else if(lightGroup!=light.Id||added.Contains(light))
            {
                // Keep existing local-space targeting: entering a new room does not
                // silently replay a command that was applied in the previous room.
                if(lightGroup>=0){if(outdoorLight&&entry.outdoor.IsOutside)entry.outdoor.World.stateSource.HoldCurrentState();else if(secondLight)entry.journey.HoldLights();else entry.chamberLight.HoldCurrentState();}
                lightGroup=light.Id;outdoorLight=entry.outdoor&&entry.outdoor.IsOutside;secondLight=!outdoorLight&&entry.journey&&entry.journey.InSecondRoom;
                bool on=light.Signals.Contains(CognitionSignal.LightOn);
                if(outdoorLight)entry.outdoor.World.stateSource.SetOn(on);else if(secondLight)entry.journey.SetLights(on);else entry.chamberLight.SetOn(on);
            }
            if(entry.outdoor&&entry.outdoor.IsOutside)entry.outdoor.World.ApplyMotion(entry.board.State,kind==CognitionChange.Split);
            var door=groups.FirstOrDefault(g=>g.Signals.Contains(CognitionSignal.DoorOn)||g.Signals.Contains(CognitionSignal.DoorOff));
            if(door==null){if(doorGroup>=0&&entry.journey&&(!entry.outdoor||!entry.outdoor.IsOutside))entry.journey.HoldDoor();doorGroup=-1;}
            else if(doorGroup!=door.Id||added.Contains(door)){doorGroup=door.Id;if(entry.journey&&(!entry.outdoor||!entry.outdoor.IsOutside))entry.journey.SetDoor(door.Signals.Contains(CognitionSignal.DoorOn));}
            foreach(var g in added)
            {
                if(g.RuleId=="world.on")entry.OpenWorld(g.Id);
                else if(g.RuleId=="world.off")entry.CloseWorldFromSentence(g.Id);
            }
        }
    }
}
