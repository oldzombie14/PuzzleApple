using PuzzleApple.V5.Cognition;
using Space=PuzzleApple.V5.PassageJourney.LightSpace;

namespace PuzzleApple.V5
{
    // Migration adapters for existing physical controllers. Scene-specific eligibility belongs
    // here, outside the dispatcher; migrate execution to the endpoint one controller at a time.
    public static class SentenceMotionBindings
    {
        static bool Drift(SentenceMotion m)=>m==SentenceMotion.Light;
        static bool Swing(SentenceMotion m)=>m==SentenceMotion.Door;
        static bool MoveLamps(SentenceMotion m)=>m==SentenceMotion.Light || m==SentenceMotion.LightToDoor ||
            m==SentenceMotion.LightToLight || m==SentenceMotion.LightToFloor;

        public static void BindIntro(WorldEntry e)
        {
            var r=e.Entities;var j=e.journey;var c=SentenceCapabilityCatalog.Default;
            e.chamberLight.MotionOutput=SentenceMotionOutput.Bind(r,e.chamberLight,()=>Space.Chamber,c.position,Drift,
                (m,g)=>e.room.Opened&&e.chamberLight.CanMove(m),stateType:typeof(DriftPose),emits:()=>e.chamberLight.Brightness>0);
            j.Stars.MotionOutput=SentenceMotionOutput.Bind(r,j.Stars,()=>Space.FirstPath,c.position,m=>Drift(m)||m==SentenceMotion.LightToDoor,
                (m,g)=>m==SentenceMotion.LightToDoor&&j.Stars.NeedsGather || m==SentenceMotion.Light&&j.Stars.EmissionLevel>0,
                (g,s)=>g.Intent.Moves(c.light,c.door)&&!j.Stars.On,stateType:typeof(CorridorStars.FlightState),emits:()=>j.Stars.EmissionLevel>0);
            j.ReturnStars.MotionOutput=SentenceMotionOutput.Bind(r,j.ReturnStars,()=>Space.ReturnPath,c.position,Drift,
                (m,g)=>m==SentenceMotion.Light&&j.ReturnStars.EmissionLevel>0,stateType:typeof(CorridorStars.FlightState),emits:()=>j.ReturnStars.EmissionLevel>0);
            j.Tutorial.MotionOutput=SentenceMotionOutput.Bind(r,j.Tutorial,()=>Space.SphereRoom,c.position,MoveLamps,
                (m,g)=>j.Tutorial.CanMove(m),(g,s)=>!g.Intent.Moves(c.floor,c.light)&&!j.Tutorial.AnyLit,stateType:typeof(MoveTutorialRoom.MotionState),emits:()=>j.Tutorial.AnyLit);
            j.FarRoomMotionOutput=SentenceMotionOutput.Bind(r,j.Root,()=>Space.FarRoom,c.position,MoveLamps,
                (m,g)=>j.FarRoomMotion.CanMove(m),(g,s)=>!g.Intent.Moves(c.floor,c.light)&&j.Lamps.Count>0&&j.Brightness==0,
                canTakeLampControl:()=>j.LampsLit,stateSource:j,stateType:typeof(PassageJourney.LampMotionState),emits:()=>j.LampsLit);
            if(j.Ascent&&j.Ascent.Stars)j.Ascent.Stars.MotionOutput=SentenceMotionOutput.Bind(r,j.Ascent.Stars,()=>Space.Ascent,c.position,Drift,(m,g)=>j.Ascent.Stars.CanMove(m),stateType:typeof(AscentStars.MotionState),emits:()=>j.Ascent.Stars.Lit);
            if(j.Platform)j.Platform.MotionOutput=SentenceMotionOutput.Bind(r,j.Platform,()=>Space.ReturnPath|Space.RailRoom,c.position,m=>m==SentenceMotion.Floor,(m,g)=>true,stateType:typeof(PassagePlatform.RailState));
            j.WhiteDoorMotionOutput=SentenceMotionOutput.Bind(r,j.Door,()=>j.WhiteDoorSpaces,c.hinge,Swing,(m,g)=>m==SentenceMotion.Door&&j.CanCycleDoor,stateSource:j.WhiteDoor,stateType:typeof(HingeState));
            j.Platform?.MotionOutput?.BindIllumination(SentenceMotion.Floor,()=>e.sentenceRuntime.Emits(c.floor,Space.ReturnPath|Space.RailRoom));
            j.WhiteDoorMotionOutput.BindIllumination(SentenceMotion.Door,()=>e.sentenceRuntime.Emits(c.door,j.WhiteDoorSpaces));
        }
        public static void BindChapter(ChapterRoom room)
        {
            var c=SentenceCapabilityCatalog.Default;
            room.MotionOutput=SentenceMotionOutput.Bind(room.Entry.Entities,room,()=>room.Space,c.position,room.SupportsNativeMotion,
                (m,g)=>room.NativeMotionWouldChange(m),(g,s)=>g.Intent.Moves(c.light,c.floor)&&room.lamp&&room.Brightness==0,
                stateType:typeof(ChapterRoom.MotionState),emits:()=>room.NativeMotionWouldChange(SentenceMotion.Light));
            if(!room.puzzle)return;
            var puzzle=room.puzzle;
            var created=puzzle.CreatedLightMotion();
            created.MotionOutput=SentenceMotionOutput.Bind(room.Entry.Entities,created,()=>room.Space,c.position,
                m=>puzzle.field.ActiveCount>0 && (Drift(m) || m==SentenceMotion.LightToFloor || m==SentenceMotion.LightToLight && puzzle.field.ActiveCount>1),
                (m,g)=>created.CanMove(m),(g,s)=>puzzle.source.IsOff && g.Intent.TargetProperty!=null,
                output=>{if(created)created.Apply(output.HasMotion);},stateType:typeof(Outdoor.LightSentenceEffects.MotionState),emits:()=>puzzle.field.AnyEmitting);
        }
        public static void BindOutdoor(Outdoor.OutdoorWorld world)
        {
            var registry=world.Entry.Entities;var c=SentenceCapabilityCatalog.Default;var position=c.position;
            world.lights.MotionOutput=SentenceMotionOutput.Bind(registry,world.lights,()=>Space.Outdoor,position,
                m=>world.field.ActiveCount>0 && MoveLamps(m) && (m!=SentenceMotion.LightToDoor || world.door.Revealed) && (m!=SentenceMotion.LightToLight || world.field.ActiveCount>1),
                (m,g)=>world.lights.CanMove(m),(g,s)=>{
                    if(!world.stateSource.IsOff||world.field.ActiveCount==0)return false;
                    if(g.Intent.Moves(c.light,c.door))return world.door.Revealed;
                    if(g.Intent.Moves(c.light,c.light))return world.field.ActiveCount>1;
                    return g.Intent.Moves(c.light,c.floor);
                },output=>{if(world.lights)world.lights.Apply(output.HasMotion);},stateType:typeof(Outdoor.LightSentenceEffects.MotionState),emits:()=>world.field.AnyEmitting);
            world.doors.MotionOutput=SentenceMotionOutput.Bind(registry,world.doors,()=>Space.Outdoor,position,m=>Swing(m)&&world.door.Revealed,
                (m,g)=>m==SentenceMotion.Door&&world.door.Revealed,
                apply:output=>{if(world.doors)world.doors.ApplyMotion(output.HasMotion);},stateSource:world.door,stateType:typeof(Outdoor.ThresholdDoor.MotionState));
            world.world.MotionOutput=SentenceMotionOutput.Bind(registry,world.world,()=>Space.Outdoor,position,
                m=>m==SentenceMotion.Floor || m==SentenceMotion.FloorToDoor && world.door.Revealed || m==SentenceMotion.FloorToLight && world.field.ActiveCount>0,
                (m,g)=>m==SentenceMotion.Floor ||
                    (m==SentenceMotion.FloorToLight || m==SentenceMotion.FloorToDoor)&&world.world.CanRise(m),
                (g,s)=>g.Intent.Moves(c.floor,c.light)&&world.stateSource.IsOff&&world.field.ActiveCount>0,
                output=>{if(world.world)world.world.Apply(output.HasMotion);},stateType:typeof(Outdoor.WorldSentenceEffects.MotionState));
            world.world.MotionOutput.BindIllumination(SentenceMotion.Floor,()=>world.Entry.sentenceRuntime.Emits(c.floor,Space.Outdoor));
            world.doors.MotionOutput.BindIllumination(SentenceMotion.Door,()=>world.door.Revealed && world.doors.appearance.SentenceLit);
        }
    }
}
