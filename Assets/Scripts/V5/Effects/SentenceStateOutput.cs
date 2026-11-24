using System;
using UnityEngine;

namespace PuzzleApple.V5
{
    // A stable control address. Several physical subjects may share one existing controller.
    // The controller owns transition rules; the dispatcher never inspects its concrete type.
    public sealed class SentenceStateOutput
    {
        readonly Component lifetime;
        internal SentenceOutputChannel Channel { get; }
        readonly Func<float> value;
        readonly Func<bool, bool> wouldChange, settling;
        readonly Action<bool> command;
        readonly Action hold, release;
        readonly Func<bool> commandAccepted;
        internal bool Accepted => commandAccepted == null || commandAccepted();
        public SentenceOutputDefinition Definition { get; }
        public object Source { get; }
        public bool Alive => lifetime;
        public float Value => Alive ? value() : 0;
        public ISentenceOutputState<T> StateSource<T>() where T : struct =>
            Alive ? Channel.StateSource<T>() : null;

        public SentenceStateOutput(Component lifetime, object source, SentenceOutputDefinition definition,
            Func<float> value, Func<bool, bool> wouldChange, Func<bool, bool> settling,
            Action<bool> command, Action hold, Action release = null, object stateSource = null, Type stateType = null,
            Func<bool> commandAccepted = null)
        {
            if (!lifetime || source == null || !definition) throw new ArgumentNullException();
            Channel = SentenceOutputChannel.Bind(lifetime, stateSource ?? source, definition, stateType);
            this.commandAccepted = commandAccepted;
            this.lifetime = lifetime; Source = source; Definition = definition;
            this.value = value ?? throw new ArgumentNullException(nameof(value));
            this.wouldChange = wouldChange ?? throw new ArgumentNullException(nameof(wouldChange));
            this.settling = settling ?? throw new ArgumentNullException(nameof(settling));
            this.command = command ?? throw new ArgumentNullException(nameof(command));
            this.hold = hold ?? throw new ArgumentNullException(nameof(hold)); this.release = release;
        }
        public bool WouldChange(bool on) => Alive && wouldChange(on);
        public bool Settling(bool on) => Alive && settling(on);
        SentenceRuntime runtime;
        Cognition.CognitionState.Group owner;
        internal Func<PassageJourney.LightSpace> ControlSpaces;
        internal float FinishFraction {
            get {
                if(!runtime || owner==null || !runtime.IsFinishing(owner.Established))return 0;
                var spaces=ControlSpaces?.Invoke()??SentenceSpaceCatalog.Default.ToLegacy(runtime.entry.Entities.RegionOf(this));
                return runtime.FinishFraction(owner,spaces,Time.unscaledDeltaTime);
            }
        }
        internal static float Advance(float current,float target,float step,SentenceStateOutput output) =>
            step<=0?current:Mathf.MoveTowards(current,target,Mathf.Max(step,Mathf.Abs(target-current)*(output?.FinishFraction??0)));

        public void Command(bool on,SentenceRuntime runtime=null,Cognition.CognitionState.Group owner=null)
        {
            if (!Alive) return;
            this.runtime=runtime;
            this.owner=owner;
            Channel.BeginCommand(this);
            command(on);
        }
        public void Hold()
        {
            owner=null;
            if (Channel.EndCommand(this) && Alive) hold();
        }
        public void Release()
        {
            owner=null;
            if (Channel.EndCommand(this) && Alive) release?.Invoke();
        }
    }
}
