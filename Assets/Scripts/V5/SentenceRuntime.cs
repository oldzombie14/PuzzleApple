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
        ChapterRoom chapterLight;
        IlluminationEffects illumination;
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
            // A retained room command does not re-fire merely because the player arrived.
            var light=entry.board.State.Groups.Where(g=>g.Effective).OrderBy(g=>g.Established)
                .FirstOrDefault(g=>g.Signals.Contains(CognitionSignal.LightOn)||g.Signals.Contains(CognitionSignal.LightOff));
            lightGroup=light==null?-1:light.Id;doorGroup=-1;
            illumination.Apply(this);
        }
        bool observationTransferred;
        public float LampMultiplier => illumination ? illumination.LampMultiplier : 1;
        public bool HasEffect(CognitionSignal effect)=>applied.Contains(effect);
        public bool PlayerDirectedLampMotion {get;private set;}
        public void Initialize(WorldEntry owner)
        {
            entry=owner;
            entry.feedback.runtime=this;
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
            var state=entry.board.State;
            SetMotionFrozen(state.HasEffect(CognitionSignal.FreezeMotion));
            entry.InterruptRemovedWorldSentence();
            var groups=state.Groups.Where(g=>g.Effective)
                .Where(g=>!MotionFrozen || (g.RuleId!="world.on" && g.RuleId!="world.off")
                    || (previous.TryGetValue(g.Id,out var activeRule) && activeRule==g.RuleId))
                .OrderBy(g=>g.Established).ToArray();
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
                bool changed=VisibleChange(g);
                if(g.RuleId!="world.on"&&g.RuleId!="world.off")entry.feedback.Play(g.Id,changed);
                if(changed&&!MotionFrozen&&!(entry.outdoor&&entry.outdoor.InChapter)&&shot==null&&presentation.HasShot(g.RuleId)
                    &&(g.RuleId!="light.on"||LightOnShotWouldBrighten()))shot=g;
            }
            if(shot!=null&&!entry.Busy)
                yield return presentation.Play(shot.RuleId,()=>{if(version==revision)Commit(groups,added,kind);},IsSwitch(shot)?shot:null);
            else Commit(groups,added,kind);
        }
        bool LightOnShotWouldBrighten()
        {
            if(entry.outdoor&&entry.outdoor.IsOutside)return entry.outdoor.World.stateSource.Brightness<.999f;
            var j=entry.journey;
            if(j&&j.InSecondRoom)return j.Brightness<.999f;
            if(j&&j.InCorridor&&!(entry.portableClosure&&entry.portableClosure.Isolated)&&j.Brightness<.999f)return true;
            return entry.chamberLight.Brightness<.999f;
        }
        bool VisibleChange(CognitionState.Group g)
        {
            if(MotionFrozen&&g.Signals.Any(s=>CognitionRule.Channel(s)?.EndsWith(".motion")==true))return false;
            if(g.Signals.All(s=>applied.Contains(s)))return false;
            return HasSceneEffect(g);
        }

        public bool SustainsFeedback(CognitionState.Group g)
        {
            if(!g.Effective || !entry.Entered)return false;
            if(MotionFrozen&&g.Signals.Any(s=>CognitionRule.Channel(s)?.EndsWith(".motion")==true))return false;
            if(g.RuleId=="world.on"||g.RuleId=="world.off")return entry.RunningWorldCommand(g.Id);
            if(!previous.TryGetValue(g.Id,out var rule)||rule!=g.RuleId||g.Signals.Count==0)return false;
            // One applied effect has one feedback owner. A surviving duplicate can take over.
            foreach(var other in entry.board.State.Groups)
                if(other.Effective && other.RuleId==rule && other.Established<g.Established)return false;
            return HasSceneEffect(g);
        }
        bool HasSceneEffect(CognitionState.Group g)
        {
            if(g.Signal==CognitionSignal.FreezeMotion)return true;
            if(entry.outdoor&&entry.outdoor.InChapter)
            {
                var chapter=entry.outdoor.CurrentRoom;
                switch(g.RuleId)
                {
                    case "light.on":return chapter.LightWouldChange(true);
                    case "light.off":return chapter.LightWouldChange(false);
                    case "light.move":case "light.move.floor":return chapter.lamp&&chapter.Brightness>0;
                    case "light.light":return chapter.Brightness>0;
                }
                return g.Signals.Any(s=>s==CognitionSignal.IlluminateWorld||s==CognitionSignal.IlluminateDoor||s==CognitionSignal.IlluminateFloor);
            }
            var j=entry.journey;
            switch(g.Signal)
            {
                case CognitionSignal.IlluminateLight:
                    if(entry.outdoor&&entry.outdoor.IsOutside)return entry.outdoor.World.stateSource.Brightness>0;
                    return j&&j.InSecondRoom?j.Brightness>0:entry.chamberLight.Brightness>0;
                case CognitionSignal.IlluminateWorld:
                case CognitionSignal.IlluminateDoor:
                case CognitionSignal.IlluminateFloor:return true;
            }
            if(entry.outdoor&&entry.outdoor.IsOutside)
            {
                var w=entry.outdoor.World;
                switch(g.RuleId)
                {
                    case "light.on":return w.stateSource.Brightness<.999f||w.environment.NightBlend>.001f;
                    case "light.off":return w.stateSource.Brightness>.001f||w.environment.NightBlend<.999f;
                    case "door.on":return w.door.Revealed&&w.doors.appearance.Brightness<.999f;
                    case "door.off":return w.door.Revealed&&w.doors.appearance.Brightness>.001f;
                    case "light.move":case "light.move.floor":case "light.door.move":case "light.light.move":return w.lights.CanMove(g.Signal);
                    case "door.light.move":return w.door.Revealed&&w.doors.CanPursue(w.lights.Nearest(w.door.transform.position));
                    case "floor.light.move":return w.world.CanRise(CognitionSignal.WorldLightMove);
                    case "floor.door.move":return w.world.CanRise(CognitionSignal.WorldDoorMove);
                    case "door.floor.move":return w.door.Revealed&&!w.door.Landed;
                    case "door.move":return w.door.Revealed;
                    case "floor.move":return true;
                }
            }
            switch(g.RuleId)
            {
                case "light.on":case "light.off":
                    float target=g.RuleId=="light.on"?1:0;
                    bool chamber=Mathf.Abs(entry.chamberLight.Brightness-target)>.001f;
                    if(!j||!j.BeyondChamber||(entry.portableClosure&&entry.portableClosure.Isolated))return chamber;
                    bool farRoom=Mathf.Abs(j.Brightness-target)>.001f;
                    return j.InCorridor?(chamber||farRoom||Mathf.Abs(j.CorridorBrightness-target)>.001f):farRoom;
                case "door.on":return j&&j.DoorProgress<.999f;
                case "door.off":return j&&j.DoorProgress>.001f;
                case "light.move":return j&&j.InSecondRoom?j.Brightness>0&&j.Lamps.Count>0:entry.chamberLight&&entry.chamberLight.Brightness>0&&entry.room.Opened&&(!j||!j.BeyondChamber)&&(!entry.portableClosure||!entry.portableClosure.Isolated);
                case "light.door.move":case "light.light.move":
                    return j&&j.InSecondRoom&&j.TryGetComponent<RoomLightGathering>(out var gathering)&&gathering.CanMove(g.Signal);
                case "light.move.floor":return j&&j.InSecondRoom&&j.Brightness>0&&j.floorMotion&&j.Lamps.Any(l=>!j.floorMotion.IsArrived(l));
                default:return false;
            }
        }
        static bool IsSwitch(CognitionState.Group group) =>
            group.RuleId=="light.on" || group.RuleId=="light.off" || group.RuleId=="door.on" || group.RuleId=="door.off";

        public void FinishCommand(int id)
        {
            var group=entry.board.State.Find(id);
            if(group!=null)StartCoroutine(Retire(group,group.Established));
        }
        IEnumerator Retire(CognitionState.Group group,long established)
        {
            var board=entry.board;
            float elapsed=0,duration=Mathf.Max(.1f,entry.feedback.duration);
            yield return null;
            while(board.State.Find(group.Id)==group && group.Established==established && group.Effective)
            {
                bool indoorLight=(group.RuleId=="light.on"||group.RuleId=="light.off")&&(!entry.outdoor||!entry.outdoor.IsOutside&&!entry.outdoor.InChapter);
                float target=group.RuleId=="light.on"?1:0;
                bool lightChanging=indoorLight&&(entry.chamberLight.Brightness!=target||
                    (entry.journey&&entry.journey.Root&&(entry.journey.Brightness!=target||
                    entry.journey.CorridorLightChanging)));
                if(lightChanging||SustainsFeedback(group)){yield return null;continue;}
                elapsed+=Time.unscaledDeltaTime;
                float opacity=1-Mathf.SmoothStep(0,1,elapsed/duration);
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
        void Commit(CognitionState.Group[] groups,CognitionState.Group[] added,CognitionChange kind)
        {
            committedRevision=revision;
            previous.Clear();foreach(var g in groups)previous[g.Id]=g.RuleId;
            foreach(var g in groups)pendingTakeovers.Remove(g.Id);
            applied.Clear();foreach(var g in groups)foreach(var signal in g.Signals)applied.Add(signal);
            if(!entry.board.State.Knows("move"))PlayerDirectedLampMotion=false;
            else if(HasEffect(CognitionSignal.LightMove)||HasEffect(CognitionSignal.LightFloorMove))PlayerDirectedLampMotion=true;
            illumination.Apply(this);
            var light=groups.FirstOrDefault(g=>g.Signals.Contains(CognitionSignal.LightOn)||g.Signals.Contains(CognitionSignal.LightOff));
            bool changedLight=lightGroup!=(light==null?-1:light.Id)||light!=null&&added.Contains(light);
            if(changedLight)
            {
                if(lightGroup>=0)
                {
                    if(chapterLight)chapterLight.HoldLight();
                    else if(outdoorLight)entry.outdoor.World.stateSource.HoldCurrentState();
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
                    chapterLight=entry.outdoor&&entry.outdoor.InChapter?entry.outdoor.CurrentRoom:null;
                    if(chapterLight)chapterLight.SetLight(on);
                    else if(outdoorLight)entry.outdoor.World.stateSource.SetOn(on);
                    else
                    {
                        entry.chamberLight.SetOn(on);
                        if(entry.journey)entry.journey.SetLights(on);
                    }
                }
            }
            if(entry.outdoor&&entry.outdoor.IsOutside)entry.outdoor.World.ApplyMotion(HasEffect,kind==CognitionChange.Split);
            var door=groups.FirstOrDefault(g=>g.Signals.Contains(CognitionSignal.DoorOn)||g.Signals.Contains(CognitionSignal.DoorOff));
            if(door==null){if(doorGroup>=0&&entry.journey&&(!entry.outdoor||!entry.outdoor.IsOutside&&!entry.outdoor.InChapter))entry.journey.HoldDoor();doorGroup=-1;}
            else if(doorGroup!=door.Id||added.Contains(door)){doorGroup=door.Id;if(entry.journey&&(!entry.outdoor||!entry.outdoor.IsOutside&&!entry.outdoor.InChapter))entry.journey.SetDoor(door.Signals.Contains(CognitionSignal.DoorOn));}
            foreach(var g in added)
            {
                if(IsSwitch(g))FinishCommand(g.Id);
                else if(g.RuleId=="world.on")entry.OpenWorld(g.Id);
                else if(g.RuleId=="world.off")entry.CloseWorldFromSentence(g.Id);
            }
        }
    }
}
