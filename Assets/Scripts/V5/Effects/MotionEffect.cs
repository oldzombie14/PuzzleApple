using System;
using System.Linq;
using System.Collections.Generic;
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

        public static bool MovesLamps(CognitionRule rule)=>rule && rule.Channel=="light.motion";

        // Set once a player-written light move applies, cleared until "move" is known again.
        public bool PlayerDirectedLampMotion {get;private set;}
        PassageJourney.LightSpace controlledSpaces;
        public bool Controls(PassageJourney.LightSpace space)=>(controlledSpaces&space)!=0;
        readonly Dictionary<PassageJourney.LightSpace,CognitionSignal> activeMotions=new Dictionary<PassageJourney.LightSpace,CognitionSignal>();
        readonly Dictionary<PassageJourney.LightSpace,int> revisions=new Dictionary<PassageJourney.LightSpace,int>();
        public int Revision(PassageJourney.LightSpace space)=>revisions.TryGetValue(space,out var version)?version:0;
        public bool HasLampMotion(PassageJourney.LightSpace space)=>activeMotions.TryGetValue(space,out var signal) && signal!=CognitionSignal.Empty;
        public CognitionSignal LightMotion {get;private set;}

        public bool CanMove(CognitionState.Group g,Func<CognitionSignal,bool> applied,PassageJourney.LightSpace? targetScope=null)
        {
            var signal=g.Signal;
            var scope=targetScope??entry.sentenceRuntime.Scope(g);
            bool Includes(PassageJourney.LightSpace room)=>(scope&room)!=0;
            var outside=entry.outdoor;
            if(Includes(PassageJourney.LightSpace.Outdoor))return ((ISentenceMovable)outside.World).CanMove(signal);
            if(Includes(PassageJourney.LightSpace.Gallery)&&((ISentenceMovable)outside.Chapter).CanMove(signal))return true;
            if(Includes(PassageJourney.LightSpace.Vitruvian)&&((ISentenceMovable)outside.Vitruvian).CanMove(signal))return true;
            if(Includes(PassageJourney.LightSpace.Garden)&&((ISentenceMovable)outside.Garden).CanMove(signal))return true;
            var j=entry.journey;
            if(signal==CognitionSignal.WorldMove)return j.Platform&&Includes(PassageJourney.LightSpace.ReturnPath);
            if(signal==CognitionSignal.DoorMove && Includes(PassageJourney.LightSpace.FarRoom))return j.EndingDoor && j.EndingDoor.CanCycle && (!entry.sentenceRuntime.Owns(signal,PassageJourney.LightSpace.FarRoom,g.Established) || j.EndingDoor.AcceptedCycle(g.Established));
            if(signal==CognitionSignal.LightDoorMove && Includes(PassageJourney.LightSpace.FirstPath) && j.Stars && j.Stars.NeedsGather)return true;
            if(signal==CognitionSignal.DoorMove)return (scope&(PassageJourney.LightSpace.FirstPath|PassageJourney.LightSpace.SphereRoom))!=0 && j.CanCycleDoor && !applied(CognitionSignal.DoorOn)&&!applied(CognitionSignal.DoorOff);
            if(Includes(PassageJourney.LightSpace.SphereRoom)&&j.Tutorial&&j.Tutorial.CanMove(signal))return true;
            if(Includes(PassageJourney.LightSpace.FarRoom)&&j.FarRoomMotion.CanMove(signal))return true;
            if(signal==CognitionSignal.LightMove && (Includes(PassageJourney.LightSpace.FirstPath)&&j.Stars&&j.Stars.Brightness>0 || Includes(PassageJourney.LightSpace.ReturnPath)&&j.ReturnStars&&j.ReturnStars.Brightness>0))return true;
            return Includes(PassageJourney.LightSpace.Chamber)&&entry.room.Opened&&entry.chamberLight.CanMove(signal);
        }

        public bool WaitingForLight(CognitionState.Group group,PassageJourney.LightSpace scope)
        {
            if(group.Rule.Channel!="light.motion" && group.Signal!=CognitionSignal.WorldLightMove)return false;
            var outside=entry.outdoor;
            var signal=group.Signal;
            if(scope==PassageJourney.LightSpace.Outdoor){
                var world=outside.World;
                if(!world.stateSource.IsOff || world.field.ActiveCount==0)return false;
                if(signal==CognitionSignal.LightDoorMove)return world.door.Revealed;
                if(signal==CognitionSignal.LightLightMove)return world.field.ActiveCount>1;
                return signal==CognitionSignal.LightFloorMove || signal==CognitionSignal.WorldLightMove;
            }
            // Only the outdoor ground can rise to a light. Darkness elsewhere supplies no motion target.
            if(signal==CognitionSignal.WorldLightMove)return false;
            if(scope==PassageJourney.LightSpace.Vitruvian){
                var fixture=outside.Vitruvian.puzzle;
                return fixture.source.IsOff && fixture.field.PlayerCount>0 &&
                    (signal==CognitionSignal.LightFloorMove || signal==CognitionSignal.LightLightMove && fixture.field.PlayerCount>1);
            }
            if(group.Signal==CognitionSignal.LightDoorMove && (scope&PassageJourney.LightSpace.FirstPath)!=0 && entry.journey.Stars && !entry.journey.Stars.On)return true;
            if((scope&PassageJourney.LightSpace.FarRoom)!=0 && entry.journey.Lamps.Count>0 && entry.journey.Brightness==0)return true;
            if((scope&PassageJourney.LightSpace.SphereRoom)!=0 && entry.journey.Tutorial && !entry.journey.Tutorial.AnyLit)return true;
            if(signal!=CognitionSignal.LightFloorMove)return false;
            return ((scope&PassageJourney.LightSpace.Garden)!=0 && outside.Garden.lamp && outside.Garden.Brightness==0)
                || ((scope&PassageJourney.LightSpace.Gallery)!=0 && outside.Chapter.lamp && outside.Chapter.Brightness==0);
        }

        public void Commit(CognitionState state,CognitionState.Group[] groups)
        {
            var next=groups.FirstOrDefault(g=>MovesLamps(g.Rule))?.Signal??CognitionSignal.Empty;
            LightMotion=next;
            for(int bit=0;bit<9;bit++){
                var space=(PassageJourney.LightSpace)(1<<bit);
                var current=groups.FirstOrDefault(g=>MovesLamps(g.Rule) && (entry.sentenceRuntime.Scope(g)&space)!=0)?.Signal??CognitionSignal.Empty;
                activeMotions.TryGetValue(space,out var old);
                if(current==old)continue;
                activeMotions[space]=current;revisions[space]=Revision(space)+1;
            }
            foreach(var group in groups)if(MovesLamps(group.Rule))controlledSpaces|=entry.sentenceRuntime.Scope(group);
            if(!state.Knows("move")){PlayerDirectedLampMotion=false;controlledSpaces=0;}
            else if(next!=CognitionSignal.Empty)PlayerDirectedLampMotion=true;
        }
    }
}
