using System;

namespace PuzzleApple.V5
{
    // Motion identity survives reparsing and does not depend on wording or sentence ownership.
    public readonly struct SentenceMotion : IEquatable<SentenceMotion>
    {
        public SentenceProperty Subject { get; }
        public SentenceProperty Target { get; }

        public SentenceMotion(SentenceProperty subject, SentenceProperty target = null)
        {
            Subject = subject;
            Target = target;
        }

        static SentenceCapabilityCatalog Properties => SentenceCapabilityCatalog.Default;
        public static SentenceMotion Light => new SentenceMotion(Properties.light);
        public static SentenceMotion Door => new SentenceMotion(Properties.door);
        public static SentenceMotion Floor => new SentenceMotion(Properties.floor);
        public static SentenceMotion LightToFloor => new SentenceMotion(Properties.light, Properties.floor);
        public static SentenceMotion LightToDoor => new SentenceMotion(Properties.light, Properties.door);
        public static SentenceMotion LightToLight => new SentenceMotion(Properties.light, Properties.light);
        public static SentenceMotion FloorToLight => new SentenceMotion(Properties.floor, Properties.light);
        public static SentenceMotion FloorToDoor => new SentenceMotion(Properties.floor, Properties.door);

        public bool Equals(SentenceMotion other) => ReferenceEquals(Subject, other.Subject) && ReferenceEquals(Target, other.Target);
        public override bool Equals(object obj) => obj is SentenceMotion other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Subject, Target);
        public static bool operator ==(SentenceMotion a, SentenceMotion b) => a.Equals(b);
        public static bool operator !=(SentenceMotion a, SentenceMotion b) => !a.Equals(b);
    }
}
