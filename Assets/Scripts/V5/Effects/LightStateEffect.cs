using System.Collections.Generic;
using System.Linq;
using PuzzleApple.V5.Cognition;

namespace PuzzleApple.V5
{
    // "light on" / "light off": light sources in the current scope go to the commanded state.
    // This handler only resolves which ISwitchableLight targets are in scope (Chapter I room,
    // outdoor world, or the indoor lights connected to the player) and which command owns them.
    // Each light object presents the change itself. New indoor light sources join through
    // PassageJourney.Lights; SentenceRuntime only schedules.
    public sealed class LightStateEffect
    {
        readonly WorldEntry entry;
        int commandGroup=-1;
        long scopedCommand=-1;
        PassageJourney.LightSpace scope=PassageJourney.LightSpace.Chamber;
        bool outdoorCommanded;
        ChapterRoom chapterCommanded;

        public LightStateEffect(WorldEntry owner){entry=owner;}

        public static bool Commands(CognitionState.Group g,bool on)=>
            g!=null&&g.Signals.Contains(on?CognitionSignal.LightOn:CognitionSignal.LightOff);
        public static bool Handles(CognitionState.Group g)=>Commands(g,true)||Commands(g,false);

        bool InChapter=>entry.outdoor&&entry.outdoor.InChapter;
        bool Outside=>entry.outdoor&&entry.outdoor.IsOutside;
        bool Isolated=>entry.portableClosure&&entry.portableClosure.Isolated;
        ISwitchableLight Chapter=>entry.outdoor.CurrentRoom;
        ISwitchableLight OutdoorWorld=>entry.outdoor.World;
        IEnumerable<ISwitchableLight> Indoor(PassageJourney.LightSpace space)=>
            entry.journey?entry.journey.Lights(space):new ISwitchableLight[]{entry.chamberLight};

        // A retained room command does not re-fire merely because the player arrived.
        public void ChangeSpace(IEnumerable<CognitionState.Group> groups)
        {
            var light=groups.Where(g=>g.Effective).OrderBy(g=>g.Established).FirstOrDefault(Handles);
            commandGroup=light==null?-1:light.Id;
        }

        // The indoor scope is fixed when a light command is established (ordered by establishment).
        public void Rescope(IEnumerable<CognitionState.Group> ordered)
        {
            long established=ordered.FirstOrDefault(Handles)?.Established ?? -1;
            if(established==scopedCommand)return;
            scopedCommand=established;
            scope=entry.journey?entry.journey.ConnectedLights():PassageJourney.LightSpace.Chamber;
        }

        public void Release(int groupId){if(commandGroup==groupId)commandGroup=-1;}

        // Visible change for feedback where the player stands.
        public bool WouldChange(CognitionState.Group g)
        {
            bool on=Commands(g,true);
            if(InChapter)return Chapter.WouldChange(on);
            if(Outside)return OutdoorWorld.WouldChange(on);
            return LocalWouldChange(on,true);
        }

        // Only "light on" gates its shot: it plays when the local light would brighten.
        public bool ShotAllowed(CognitionState.Group g)
        {
            if(!Commands(g,true))return true;
            // The outdoor shot follows the lamps only, not the remaining night sky blend.
            if(Outside)return entry.outdoor.World.stateSource.Brightness<.999f;
            return LocalWouldChange(true,false);
        }

        bool LocalWouldChange(bool on,bool isolationFirst)
        {
            var j=entry.journey;
            if(isolationFirst&&(!j||!j.BeyondChamber||Isolated))return entry.chamberLight.WouldChange(on);
            if(j&&j.InTutorial)return j.Tutorial.WouldChange(on);
            if(j&&j.InSecondRoom)return j.FarRoom.WouldChange(on);
            if(j&&j.InCorridor)return j.Lights(scope).Any(l=>l.Settling(on));
            return entry.chamberLight.WouldChange(on);
        }

        // Indoor commands stay until every light in their scope settles, wherever the player is.
        // Outside and in Chapter I the runtime's generic in-progress check decides.
        public bool TrySettling(CognitionState.Group g,out bool settling)
        {
            settling=false;
            if(!Handles(g)||InChapter||Outside)return false;
            bool on=Commands(g,true);
            settling=Indoor(scope).Any(l=>l.Settling(on));
            return true;
        }

        public void Commit(CognitionState.Group[] groups,CognitionState.Group[] added)
        {
            var light=groups.FirstOrDefault(Handles);
            if(commandGroup==(light==null?-1:light.Id)&&(light==null||!added.Contains(light)))return;
            if(commandGroup>=0)
            {
                if(chapterCommanded)((ISwitchableLight)chapterCommanded).Hold();
                else if(outdoorCommanded)OutdoorWorld.Hold();
                else foreach(var l in Indoor(PassageJourney.LightSpace.All))l.Hold();
            }
            commandGroup=light==null?-1:light.Id;
            if(light==null)return;
            bool on=Commands(light,true);
            outdoorCommanded=Outside;
            chapterCommanded=InChapter?entry.outdoor.CurrentRoom:null;
            if(chapterCommanded)((ISwitchableLight)chapterCommanded).SetOn(on);
            else if(outdoorCommanded)OutdoorWorld.SetOn(on);
            else foreach(var l in Indoor(scope))l.SetOn(on);
        }
    }
}
