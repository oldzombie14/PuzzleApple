using System;
using PuzzleApple.V5.Cognition;

namespace PuzzleApple.V5
{
    // Motion sentences (rules whose channel ends with .motion): light, door and floor movement.
    // Resolves the ISentenceMovable in the player's space (Chapter I room, outdoor world, tutorial
    // spheres, corridor stars, far-room lamps, chamber lamp) and asks whether the sentence would
    // move something there. Motion itself is applied by the objects from the applied effects.
    public sealed class MotionEffect
    {
        readonly WorldEntry entry;
        public MotionEffect(WorldEntry owner){entry=owner;}

        public static bool Handles(CognitionState.Group g)=>g!=null&&g.Rule&&g.Rule.Motion;

        // Set once a player-written light move applies, cleared until "move" is known again.
        public bool PlayerDirectedLampMotion {get;private set;}

        public bool CanMove(CognitionState.Group g,Func<CognitionSignal,bool> applied)
        {
            var signal=g.Signal;
            if(entry.outdoor&&entry.outdoor.InChapter)return ((ISentenceMovable)entry.outdoor.CurrentRoom).CanMove(signal);
            if(entry.outdoor&&entry.outdoor.IsOutside)return ((ISentenceMovable)entry.outdoor.World).CanMove(signal);
            var j=entry.journey;
            // The white door cycles only while no on/off door command holds it.
            if(signal==CognitionSignal.DoorMove)
                return j&&j.WhiteDoorMotion.CanMove(signal)&&!applied(CognitionSignal.DoorOn)&&!applied(CognitionSignal.DoorOff);
            if(j&&j.InTutorial)return j.Tutorial.CanMove(signal);
            if(j&&j.InCorridor)return j.CorridorMotion.CanMove(signal);
            if(j&&j.InSecondRoom)return j.FarRoomMotion.CanMove(signal);
            // The chamber lamp moves only once the box is open and the player is in the chamber.
            return entry.chamberLight&&entry.chamberLight.CanMove(signal)&&entry.room.Opened&&(!j||!j.BeyondChamber)
                &&(!entry.portableClosure||!entry.portableClosure.Isolated);
        }

        public void Commit(CognitionState state,Func<CognitionSignal,bool> applied)
        {
            if(!state.Knows("move"))PlayerDirectedLampMotion=false;
            else if(applied(CognitionSignal.LightMove)||applied(CognitionSignal.LightFloorMove))PlayerDirectedLampMotion=true;
        }
    }
}
