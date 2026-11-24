using System;
using System.Linq;

namespace PuzzleApple.V5.Cognition
{
    // Only the remaining physical controllers and catalog conflict declarations need signals.
    // Native V5 meaning comes from the parsed intent; a stale rule.effect cannot redefine it.
    public static class SentenceLegacySignalBridge
    {
        public static bool TryResolve(SentenceIntent intent, CognitionRule binding, out CognitionSignal[] effects)
        {
            effects = Array.Empty<CognitionSignal>();
            if (intent.Action == SentenceAction.Legacy) { effects = binding.Effects.Distinct().ToArray(); return true; }
            if (intent.Subject && !intent.SubjectProperty || intent.Target && !intent.TargetProperty) return false;
            var c = SentenceCapabilityCatalog.Default;
            var subject = intent.SubjectProperty; var target = intent.TargetProperty;
            var signal = CognitionSignal.Empty;
            if (intent.Action == SentenceAction.On || intent.Action == SentenceAction.Off)
            {
                bool on = intent.Action == SentenceAction.On;
                if (subject == c.light && c.light) signal = on ? CognitionSignal.LightOn : CognitionSignal.LightOff;
                else if (subject == c.door && c.door) signal = on ? CognitionSignal.DoorOn : CognitionSignal.DoorOff;
                else if (subject == c.world && c.world) signal = on ? CognitionSignal.WorldOn : CognitionSignal.WorldOff;
                else if (!(on && subject == c.motion && c.motion)) return false; // Existing on move stays a valid no-op.
            }
            else if (intent.Action == SentenceAction.Freeze && subject == c.motion && c.motion) signal = CognitionSignal.FreezeMotion;
            else if (intent.Action == SentenceAction.Illuminate)
            {
                if (subject == c.world && c.world) signal = CognitionSignal.IlluminateWorld;
                else if (subject == c.light && c.light) signal = CognitionSignal.IlluminateLight;
                else if (subject == c.door && c.door) signal = CognitionSignal.IlluminateDoor;
                else if (subject == c.floor && c.floor) signal = CognitionSignal.IlluminateFloor;
                else return false;
            }
            else if (intent.Action == SentenceAction.Move)
            {
                if (subject == c.light && c.light)
                    signal = !target ? CognitionSignal.LightMove : target == c.floor ? CognitionSignal.LightFloorMove :
                        target == c.door ? CognitionSignal.LightDoorMove : target == c.light ? CognitionSignal.LightLightMove : CognitionSignal.Empty;
                else if (subject == c.door && c.door)
                    signal = !target ? CognitionSignal.DoorMove : target == c.floor ? CognitionSignal.DoorWorldMove :
                        target == c.light ? CognitionSignal.DoorLightMove : CognitionSignal.Empty;
                else if (subject == c.floor && c.floor)
                    signal = !target ? CognitionSignal.WorldMove : target == c.light ? CognitionSignal.WorldLightMove :
                        target == c.door ? CognitionSignal.WorldDoorMove : CognitionSignal.Empty;
                if (signal == CognitionSignal.Empty) return false;
            }
            else return false;
            if (signal != CognitionSignal.Empty) effects = new[] { signal };
            return true;
        }
    }
}
