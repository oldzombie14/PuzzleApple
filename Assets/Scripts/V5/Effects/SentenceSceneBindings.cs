using System;
using UnityEngine;
using Space = PuzzleApple.V5.PassageJourney.LightSpace;

namespace PuzzleApple.V5
{
    // Compatibility boundary between authored capabilities and the existing scene controllers.
    // Shared fades keep one control address while their physical subjects register separately.
    public static class SentenceSceneBindings
    {
        public static void BindIntro(WorldEntry entry)
        {
            var registry = entry.Entities;
            var j = entry.journey;
            entry.chamberLight.LightOutput=BindLight(registry, entry.chamberLight, entry.chamberLight, () => Space.Chamber, () => entry.chamberLight.Brightness, stateType:typeof(EmissionState));
            j.Stars.LightOutput=BindLight(registry, j.Stars, j.Stars, () => Space.FirstPath, () => j.Stars.Brightness, () => j.Stars.EmissionLevel > 0, stateType:typeof(CorridorStars.LightState));
            j.Tutorial.LightOutput=BindLight(registry, j.Tutorial, j.Tutorial, () => Space.SphereRoom, () => j.Tutorial.SphereBrightness, stateType:typeof(MoveTutorialRoom.LightState));
            j.Tutorial.RegisterSentenceSubjects(registry,j.Tutorial.LightOutput);
            j.ReturnStars.LightOutput=BindLight(registry, j.ReturnStars, j.ReturnStars, () => Space.ReturnPath, () => j.ReturnStars.Brightness, () => j.ReturnStars.EmissionLevel > 0, stateType:typeof(CorridorStars.LightState));
            j.FarLightOutput=BindLight(registry, j.Root, j.FarRoom, () => Space.FarRoom, () => j.Brightness, stateType:typeof(EmissionState));
            foreach (var lamp in j.Lamps) BindLightSubject(registry, lamp, () => Space.FarRoom, () => j.Brightness,j.FarLightOutput);
            if (j.Ascent && j.Ascent.Stars)
                j.Ascent.Stars.LightOutput=BindLight(registry, j.Ascent.Stars, j.Ascent.Stars, () => Space.Ascent, () => j.Ascent.Stars.Brightness, () => j.Ascent.Stars.Lit, stateType:typeof(EmissionState));
            var catalog = SentenceCapabilityCatalog.Default;
            j.WhiteDoorOutput=BindDoor(registry, j.Door, j.WhiteDoor, () => j.WhiteDoorSpaces, catalog.hinge, () => j.DoorProgress, open => j.DoorProgress != (open ? 1 : 0), stateType:typeof(HingeState));
            j.EntranceDoor.DoorOutput=BindDoor(registry, j.EntranceDoor, j.EntranceDoor, () => j.EntranceDoor.ControlSpaces, catalog.hinge, () => j.EntranceDoor.Progress, stateType:typeof(HingeState));
            j.EndingDoor.DoorOutput=BindDoor(registry, j.EndingDoor, j.EndingDoor, () => j.EndingDoor.ControlSpaces, catalog.lift, () => j.EndingDoor.Lift, j.EndingDoor.CommandSettling, stateType:typeof(ChargeDoor.LiftState),
                commandAccepted:() => j.EndingDoor.Driving);
            SentenceMotionBindings.BindIntro(entry);
            SentenceFocusBindings.BindIntro(entry);
        }

        public static void BindChapter(ChapterRoom room)
        {
            var registry = room.Entry.Entities;
            room.LightOutput=BindLight(registry, room, room, () => room.Space, () => room.Brightness, stateType:typeof(ChapterRoom.LightState));
            room.RegisterSentenceSubjects(registry,room.LightOutput);
            SentenceMotionBindings.BindChapter(room);
            if (room.puzzle)
            {
                var puzzle = room.puzzle;
                var puzzleOutput = BindLight(registry, puzzle, puzzle, () => room.Space, () => Mathf.Max(puzzle.source.Brightness, puzzle.ball.Brightness), stateType:typeof(VitruvianPuzzle.LightState));
                puzzle.ball.LightOutput=puzzle.source.LightOutput=puzzleOutput;
                BindLightSubject(registry, puzzle.source, () => room.Space, () => puzzle.source.Brightness, puzzleOutput);
                BindLightSubject(registry, puzzle.ball, () => room.Space, () => puzzle.ball.Brightness, puzzleOutput);
                puzzle.field.BindSentenceOutput(room.Entry, () => room.Space, puzzleOutput);
            }
        }

        public static void BindOutdoor(Outdoor.OutdoorWorld world)
        {
            var registry = world.Entry.Entities;
            world.stateSource.LightOutput=BindLight(registry, world.stateSource, world, () => Space.Outdoor, () => world.stateSource.Brightness, stateType:typeof(Outdoor.OutdoorWorld.LightState));
            world.field.BindSentenceOutput(world.Entry, () => Space.Outdoor,world.stateSource.LightOutput);
            world.doors.appearance.DoorOutput=BindDoor(registry, world.door, world, () => Space.Outdoor, SentenceCapabilityCatalog.Default.emission, () => world.doors.appearance.Brightness,
                on => world.door.Revealed && world.doors.appearance.Brightness != (on ? 1 : 0), world.doors.appearance, typeof(EmissionState));
            SentenceMotionBindings.BindOutdoor(world);
            SentenceFocusBindings.BindOutdoor(world);
        }

        public static SentenceStateOutput BindLight(SentenceEntityRegistry registry, Component lifetime,
            ISwitchableLight source, Func<Space> spaces, Func<float> level, Func<bool> lit = null, Type stateType = null)
        {
            var catalog = SentenceCapabilityCatalog.Default;
            var entity = SentenceEntity.Bind(registry, lifetime, source, spaces);
            var output = entity.Output(catalog.emission) ?? new SentenceStateOutput(lifetime, source, catalog.emission,
                level, source.WouldChange, source.Settling, source.SetOn, source.Hold, source.Hold, stateType:stateType);
            output.ControlSpaces=spaces;
            entity.WithProperty(catalog.potentialLight).WithProperty(catalog.light, lit ?? (() => level() > 0)).WithOutput(output);
            return output;
        }
        public static SentenceEntity BindLightSubject(SentenceEntityRegistry registry, Component subject,
            Func<Space> spaces, Func<float> level, SentenceStateOutput output)
        {
            var catalog = SentenceCapabilityCatalog.Default;
            return SentenceEntity.Bind(registry, subject, subject, spaces).WithProperty(catalog.potentialLight)
                .WithProperty(catalog.light, () => level() > 0).WithOutput(output);
        }
        internal static SentenceStateOutput BindDoor(SentenceEntityRegistry registry, Component lifetime, ISwitchableDoor source,
            Func<Space> spaces, SentenceOutputDefinition definition, Func<float> level, Func<bool, bool> settling = null, object stateSource = null,
            Type stateType = null, Func<bool> commandAccepted = null)
        {
            var entity = SentenceEntity.Bind(registry, lifetime, source, spaces);
            var output = entity.Output(definition) ?? new SentenceStateOutput(lifetime, source, definition,
                level, source.WouldChange, settling ?? source.WouldChange, source.SetOpen, source.Hold, source.Release, stateSource, stateType, commandAccepted);
            output.ControlSpaces=spaces;
            entity.WithProperty(SentenceCapabilityCatalog.Default.door).WithOutput(output);
            return output;
        }
    }
}
