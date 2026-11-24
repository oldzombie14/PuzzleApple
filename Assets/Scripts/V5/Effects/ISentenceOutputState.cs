using System.Collections.Generic;
using UnityEngine;

namespace PuzzleApple.V5
{
    [System.Serializable]
    public struct EmissionState
    {
        public float brightness;
        public bool Valid => brightness >= 0 && brightness <= 1;
        public EmissionState(float value) { brightness = value; }
    }

    [System.Serializable]
    public struct HingeState
    {
        public float progress;
        public bool cycleOpening;

        // Both the command and the sustained sentence write progress on this same hinge.
        public bool CycleTarget()
        {
            if (progress <= 0) cycleOpening = true;
            else if (progress >= 1) cycleOpening = false;
            return cycleOpening;
        }
    }

    [System.Serializable]
    public struct DriftPose
    {
        public Vector3 position, direction;
        public bool parked;
        public bool Valid => Finite(position) && Finite(direction) &&
            (direction == Vector3.zero || Mathf.Abs(direction.sqrMagnitude - 1) < .001f);
        public static bool Finite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        internal static void Place(Rigidbody body, Vector3 position)
        {
            body.position = position;
            body.transform.position = position;
        }
    }

    // One physical actuator can have a command and a sustained driver. Holding a sentence
    // releases its drive in place. Other simulation (gravity, hover, another command) keeps
    // writing the live state that a resumed driver will advance from.
    internal sealed class SentenceOutputChannel
    {
        readonly object source;
        System.Type stateType;
        SentenceStateOutput command;
        internal bool CommandActive => command != null && command.Accepted;
        readonly HashSet<SentenceMotion> motions = new HashSet<SentenceMotion>();

        SentenceOutputChannel(object source, System.Type stateType)
        {
            this.source = source;
            this.stateType = stateType;
        }

        internal static SentenceOutputChannel Bind(Component lifetime, object source,
            SentenceOutputDefinition definition, System.Type stateType)
        {
            foreach (var entity in lifetime.GetComponents<SentenceEntity>())
            {
                var channel = entity.Output(definition)?.Channel;
                if (channel == null || !ReferenceEquals(channel.source, source))
                {
                    if (!(entity.Endpoint is SentenceMotionOutput motion) || motion.Definition != definition) continue;
                    channel = motion.Channel;
                }
                if (channel == null || !ReferenceEquals(channel.source, source)) continue;
                if (stateType != null && channel.stateType != null && channel.stateType != stateType)
                    throw new System.InvalidOperationException("Conflicting state types for " + definition.name);
                channel.stateType ??= stateType;
                return channel;
            }
            return new SentenceOutputChannel(source, stateType);
        }

        internal void BeginCommand(SentenceStateOutput owner) => command = owner;
        internal bool EndCommand(SentenceStateOutput owner)
        {
            if (command != null && !ReferenceEquals(command, owner)) return false;
            command = null;
            return true;
        }
        internal void HoldMotion() => motions.Clear();
        internal void ApplyMotion(SentenceMotion motion) => motions.Add(motion);
        internal bool Drives(SentenceMotion motion) => !CommandActive && motions.Contains(motion);
        internal ISentenceOutputState<T> StateSource<T>() where T : struct =>
            stateType == null || stateType == typeof(T) ? source as ISentenceOutputState<T> : null;
    }

    // T contains the physical state of one output. Scope and active sentences are recomputed separately.
    public interface ISentenceOutputState<T> where T : struct
    {
        T CaptureState();
        bool TryRestoreState(T state);
    }
}
