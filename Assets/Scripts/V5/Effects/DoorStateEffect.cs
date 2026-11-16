using System.Linq;
using PuzzleApple.V5.Cognition;

namespace PuzzleApple.V5
{
    // "door on" / "door off": the door in the current space goes to the commanded state.
    // Resolves the ISwitchableDoor in scope (outdoor threshold door, or the indoor white door;
    // Chapter I has none) and remembers which command owns it. Door motion (door.move,
    // door.floor.move, door.light.move) is still handled by SentenceRuntime / OutdoorWorld.ApplyMotion.
    public sealed class DoorStateEffect
    {
        readonly WorldEntry entry;
        int commandGroup=-1;

        public DoorStateEffect(WorldEntry owner){entry=owner;}

        public static bool Commands(CognitionState.Group g,bool open)=>
            g!=null&&g.Signals.Contains(open?CognitionSignal.DoorOn:CognitionSignal.DoorOff);
        public static bool Handles(CognitionState.Group g)=>Commands(g,true)||Commands(g,false);

        bool InChapter=>entry.outdoor&&entry.outdoor.InChapter;
        bool Outside=>entry.outdoor&&entry.outdoor.IsOutside;
        // Indoor commands go to the white door; outdoors the door follows the effective signals.
        ISwitchableDoor Indoor=>entry.journey&&!Outside&&!InChapter?entry.journey.WhiteDoor:null;

        // A retained door command does not re-fire merely because the player arrived.
        public void ChangeSpace(){commandGroup=-1;}

        public bool WouldChange(CognitionState.Group g)
        {
            bool open=Commands(g,true);
            if(InChapter)return false;
            if(Outside)return ((ISwitchableDoor)entry.outdoor.World).WouldChange(open);
            return entry.journey&&entry.journey.WhiteDoor.WouldChange(open);
        }

        // The command sentence has retired; the door keeps its final state.
        public void Release(int groupId)
        {
            if(commandGroup!=groupId)return;
            commandGroup=-1;
            if(Outside)((ISwitchableDoor)entry.outdoor.World).Release();
        }

        public void Commit(CognitionState.Group[] groups,CognitionState.Group[] added)
        {
            var door=groups.FirstOrDefault(Handles);
            if(door==null)
            {
                if(commandGroup>=0)Indoor?.Hold();
                commandGroup=-1;
            }
            else if(commandGroup!=door.Id||added.Contains(door))
            {
                commandGroup=door.Id;
                Indoor?.SetOpen(Commands(door,true));
            }
        }
    }
}
