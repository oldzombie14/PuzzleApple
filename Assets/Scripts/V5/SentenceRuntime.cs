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
        int lightGroup=-1,doorGroup=-1,revision;
        bool outdoorLight;
        IlluminationEffects illumination;
        public void EnterOutdoor()
        {
            // A retained room command does not re-fire merely because the player arrived.
            var light=entry.board.State.Groups.Where(g=>g.Effective).OrderBy(g=>g.Established)
                .FirstOrDefault(g=>g.Signals.Contains(CognitionSignal.LightOn)||g.Signals.Contains(CognitionSignal.LightOff));
            lightGroup=light==null?-1:light.Id;doorGroup=-1;outdoorLight=true;
            illumination.Apply(this);
        }
        bool observationTransferred;
        public bool HasEffect(CognitionSignal effect)=>applied.Contains(effect);
        public void Initialize(WorldEntry owner)
        {
            entry=owner;
            illumination=gameObject.AddComponent<IlluminationEffects>();
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
            while(pendingTakeovers.Count>0&&entry.Busy&&version==revision)yield return null;
            if(version!=revision)yield break;
            CognitionState.Group shot=null;
            foreach(var g in added)
            {
                bool changed=pendingTakeovers.ContainsKey(g.Id)||VisibleChange(g);
                if(g.RuleId!="world.on"&&g.RuleId!="world.off")entry.feedback.Play(g.Id,changed);
                if(changed&&shot==null&&presentation.HasShot(g.RuleId))shot=g;
            }
            if(shot!=null&&!entry.Busy)
                yield return presentation.Play(shot.RuleId,()=>{if(version==revision)Commit(groups,added,kind,shot.Id);},IsSwitch(shot)?shot.Id:-1);
            else Commit(groups,added,kind);
        }
        bool VisibleChange(CognitionState.Group g)
        {
            if(g.Signals.All(s=>applied.Contains(s)))return false;
            if(g.Signals.Any(s=>s==CognitionSignal.IlluminateWorld||s==CognitionSignal.IlluminateDoor||s==CognitionSignal.IlluminateFloor))return true;
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
                case "light.on":return entry.chamberLight.Brightness<.999f || (j&&j.Brightness<.999f);
                case "light.off":return entry.chamberLight.Brightness>.001f || (j&&j.Brightness>.001f);
                case "door.on":return j&&j.DoorProgress<.999f;
                case "door.off":return j&&j.DoorProgress>.001f;
                case "light.move":return j&&j.InSecondRoom?j.Lamps.Count>0:entry.chamberLight&&(!j||!j.BeyondChamber);
                case "light.door.move":case "light.light.move":return j&&j.InSecondRoom&&j.Lamps.Count>0;
                case "light.floor.move":return j&&j.floorMotion&&j.Lamps.Any(l=>!j.floorMotion.IsArrived(l));
                default:return false;
            }
        }
        static bool IsSwitch(CognitionState.Group group) =>
            group.RuleId=="light.on" || group.RuleId=="light.off" || group.RuleId=="door.on" || group.RuleId=="door.off";

        public void FinishCommand(int id,bool alreadyFaded=false)
        {
            var group=entry.board.State.Find(id);
            if(group!=null)StartCoroutine(Retire(group,group.Established,alreadyFaded));
        }
        IEnumerator Retire(CognitionState.Group group,long established,bool alreadyFaded)
        {
            var board=entry.board;
            float elapsed=0,duration=Mathf.Max(.1f,entry.feedback.duration);
            yield return null;
            while(board.State.Find(group.Id)==group && group.Established==established && group.Effective)
            {
                elapsed+=Time.unscaledDeltaTime;
                float opacity=alreadyFaded?0:1-Mathf.SmoothStep(0,1,elapsed/duration);
                board.SetSentenceOpacity(group.Id,opacity);
                if(opacity<=0 && !presentation.Playing && !entry.Busy && committedRevision==revision)
                {
                    // Let the commanded object finish its transition after its sentence leaves.
                    if(lightGroup==group.Id)lightGroup=-1;
                    if(doorGroup==group.Id)
                    {
                        doorGroup=-1;
                        if(entry.outdoor&&entry.outdoor.IsOutside)entry.outdoor.World.doors.ReleaseStateCommand();
                    }
                    board.State.ReturnToLibrary(group.Id,0,true);
                    yield break;
                }
                yield return null;
            }
        }
        int committedRevision;
        void Commit(CognitionState.Group[] groups,CognitionState.Group[] added,CognitionChange kind,int fadedGroup=-1)
        {
            committedRevision=revision;
            previous.Clear();foreach(var g in groups)previous[g.Id]=g.RuleId;
            foreach(var g in groups)pendingTakeovers.Remove(g.Id);
            applied.Clear();foreach(var g in groups)foreach(var signal in g.Signals)applied.Add(signal);
            illumination.Apply(this);
            var light=groups.FirstOrDefault(g=>g.Signals.Contains(CognitionSignal.LightOn)||g.Signals.Contains(CognitionSignal.LightOff));
            bool changedLight=lightGroup!=(light==null?-1:light.Id)||light!=null&&added.Contains(light);
            if(changedLight)
            {
                if(lightGroup>=0)
                {
                    if(outdoorLight)entry.outdoor.World.stateSource.HoldCurrentState();
                    else
                    {
                        entry.chamberLight.HoldCurrentState();
                        if(entry.journey)entry.journey.HoldLights();
                    }
                }
                lightGroup=light==null?-1:light.Id;
                if(light!=null)
                {
                    outdoorLight=entry.outdoor&&entry.outdoor.IsOutside;
                    bool on=light.Signals.Contains(CognitionSignal.LightOn);
                    if(outdoorLight)entry.outdoor.World.stateSource.SetOn(on);
                    else
                    {
                        entry.chamberLight.SetOn(on);
                        if(entry.journey)entry.journey.SetLights(on);
                    }
                }
            }
            if(entry.outdoor&&entry.outdoor.IsOutside)entry.outdoor.World.ApplyMotion(HasEffect,kind==CognitionChange.Split);
            var door=groups.FirstOrDefault(g=>g.Signals.Contains(CognitionSignal.DoorOn)||g.Signals.Contains(CognitionSignal.DoorOff));
            if(door==null){if(doorGroup>=0&&entry.journey&&(!entry.outdoor||!entry.outdoor.IsOutside))entry.journey.HoldDoor();doorGroup=-1;}
            else if(doorGroup!=door.Id||added.Contains(door)){doorGroup=door.Id;if(entry.journey&&(!entry.outdoor||!entry.outdoor.IsOutside))entry.journey.SetDoor(door.Signals.Contains(CognitionSignal.DoorOn));}
            foreach(var g in added)
            {
                if(IsSwitch(g))FinishCommand(g.Id,g.Id==fadedGroup);
                else if(g.RuleId=="world.on")entry.OpenWorld(g.Id);
                else if(g.RuleId=="world.off")entry.CloseWorldFromSentence(g.Id);
            }
        }
    }
}
