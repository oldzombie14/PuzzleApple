using System;
using PuzzleApple.V5.Cognition;
using UnityEngine;
using Space = PuzzleApple.V5.PassageJourney.LightSpace;

namespace PuzzleApple.V5
{
    // Existing scene hints, kept beside the mechanisms rather than in the camera director.
    // Chapter rooms and ordinary illumination offer no focus: their shots keep the current view.
    public static class SentenceFocusBindings
    {
        static void Bind(SentenceEntityRegistry registry, Component owner, Space visibleFrom,
            Func<CognitionState.Group, bool> changes, Func<Vector3> point) =>
            SentenceFocusSource.Bind(registry, owner, () => SentenceSpaceCatalog.Default.FromLegacy(visibleFrom),
                g => changes(g) ? point() : (Vector3?)null);

        static bool LampHint(CognitionState.Group g) => LightStateEffect.Handles(g) ||
            MotionEffect.MovesLamps(g) && g.Intent.TargetProperty != SentenceCapabilityCatalog.Default.door;

        static bool DoorHint(CognitionState.Group g, Func<bool> opens, Func<bool> moves) =>
            DoorStateEffect.Commands(g, true) ? opens() :
            g.Intent?.Is(SentenceAction.Move, SentenceCapabilityCatalog.Default.door) == true && moves();

        public static void BindIntro(WorldEntry e)
        {
            var r = e.Entities; var j = e.journey;
            Bind(r, e.chamberLight, Space.Chamber,
                g => LampHint(g) && (LightStateEffect.Handles(g) ? e.chamberLight.WouldChange(LightStateEffect.Commands(g, true)) : e.chamberLight.CanMove(g.Intent.Motion)),
                () => e.chamberLight.surface.bounds.center);
            Bind(r, j.Tutorial, Space.SphereRoom, g => LampHint(g) && j.Tutorial.SphereWillChange(g), () => j.Tutorial.Sphere.position);
            if (j.Platform) Bind(r, j.Platform, Space.ReturnPath | Space.RailRoom, g => g.Intent?.Moves(SentenceCapabilityCatalog.Default.floor) == true, () => j.Platform.Deck.bounds.center);
            Bind(r, j.Door, Space.FirstPath | Space.SphereRoom,
                g => DoorHint(g, () => j.WhiteDoor.WouldChange(true), () => j.CanCycleDoor), () => j.Door.position);
        }

        public static void BindOutdoor(Outdoor.OutdoorWorld world)
        {
            Bind(world.Entry.Entities, world.door, Space.Outdoor,
                g => DoorHint(g, () => ((ISwitchableDoor)world).WouldChange(true), () => world.door.Revealed),
                () => world.door.transform.TransformPoint(new Vector3(0, 3, 0)));
        }
    }
}
