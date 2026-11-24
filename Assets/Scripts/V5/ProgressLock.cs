using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Space = PuzzleApple.V5.PassageJourney.LightSpace;

namespace PuzzleApple.V5
{
    public sealed class ProgressLock : MonoBehaviour
    {
        public SentenceSpace[] unlockedRegion = new SentenceSpace[0], prerequisiteRegion = new SentenceSpace[0];
        [Tooltip("Component implementing IProgressBoundary; leave empty for a manually unlocked boundary.")]
        public MonoBehaviour boundarySource;
        [Range(0, 1)] public float passableProgress = .5f;
        public bool Unlocked { get; private set; }
        IProgressBoundary source;
        SentenceSpaceSet unlockedSpaces, requiredSpaces;
        bool observedConnected;
        public SentenceSpaceSet UnlockedRegion => unlockedSpaces ??= new SentenceSpaceSet(unlockedRegion);
        public SentenceSpaceSet RequiredRegion => requiredSpaces ??= new SentenceSpaceSet(prerequisiteRegion);
        public SentenceSpaceSet BoundaryRegion => new SentenceSpaceSet(UnlockedRegion.Concat(RequiredRegion));
        public bool Connected => Unlocked && (source == null || source.AllowsConnection);
        public MonoBehaviour BoundarySource => boundarySource;

        static readonly HashSet<ProgressLock> active = new HashSet<ProgressLock>();
        public static int TopologyVersion { get; private set; }
        public static void CopyActiveTo(List<ProgressLock> destination)
        {
            destination.Clear();
            foreach (var gate in active) if (gate) destination.Add(gate);
        }
        void OnEnable()
        {
            if (active.Add(this)) TopologyVersion++;
            AttachBoundary();
        }
        void OnDisable()
        {
            if (source != null) source.BoundaryChanged -= ObserveBoundary;
            if (active.Remove(this)) TopologyVersion++;
        }
        void OnValidate()
        {
            unlockedSpaces = requiredSpaces = null;
            TopologyVersion++;
            if (Application.isPlaying && isActiveAndEnabled) AttachBoundary();
        }
        public void SetRegions(SentenceSpaceSet unlocked, SentenceSpaceSet required)
        {
            unlockedRegion = unlocked.ToArray(); prerequisiteRegion = required.ToArray();
            unlockedSpaces = unlocked; requiredSpaces = required; TopologyVersion++;
        }
        public void BindBoundary(MonoBehaviour boundary)
        {
            if (boundary && !(boundary is IProgressBoundary)) throw new ArgumentException("Boundary must implement IProgressBoundary.");
            boundarySource = boundary;
            AttachBoundary();
        }
        void AttachBoundary()
        {
            if (source != null) source.BoundaryChanged -= ObserveBoundary;
            source = boundarySource as IProgressBoundary;
            if (boundarySource && source == null) throw new InvalidOperationException("Invalid progress boundary: " + boundarySource.GetType().Name);
            if (source != null && isActiveAndEnabled) source.BoundaryChanged += ObserveBoundary;
            ObserveBoundary();
        }
        public void Unlock()
        {
            if (Unlocked) return;
            Unlocked = true; TopologyVersion++;
        }
        void ObserveBoundary()
        {
            if (!Unlocked && source != null && source.BoundaryProgress >= passableProgress) Unlock();
            bool connected = Connected;
            if (connected == observedConnected) return;
            observedConnected = connected; TopologyVersion++;
        }

        // Temporary bridge for existing gameplay/rendering and the independent legacy oracle.
        public Space unlocksSpaces
        {
            get => SentenceSpaceCatalog.Default.ToLegacy(UnlockedRegion);
            set => SetRegions(SentenceSpaceCatalog.Default.FromLegacy(value), RequiredRegion);
        }
        public Space requiresSpaces
        {
            get => SentenceSpaceCatalog.Default.ToLegacy(RequiredRegion);
            set => SetRegions(UnlockedRegion, SentenceSpaceCatalog.Default.FromLegacy(value));
        }
        public Space BoundarySpaces => unlocksSpaces | requiresSpaces;
        public static Space DoorBoundary(Component owner)
        {
            foreach (var gate in active) if (gate && gate.boundarySource == owner) return gate.BoundarySpaces;
            return 0;
        }
        public static Space Accessible(Space chapter)
        {
            Space result = chapter;
            foreach (var gate in active) if (gate) result &= ~gate.unlocksSpaces;
            Space previous;
            do
            {
                previous = result;
                foreach (var gate in active)
                    if (gate && gate.Unlocked && (result & gate.requiresSpaces) == gate.requiresSpaces)
                        result |= gate.unlocksSpaces & chapter;
            } while (previous != result);
            return result;
        }
        public static Space Region(Space chapter, Space playerSpace)
        {
            var accessible = Accessible(chapter);
            var initial = chapter;
            foreach (var gate in active) if (gate) initial &= ~gate.unlocksSpaces;
            var result = playerSpace & accessible;
            if (result == 0) result = initial & accessible;
            Space previous;
            do
            {
                previous = result;
                if ((result & initial) != 0) result |= initial & accessible;
                foreach (var gate in active)
                {
                    if (!gate) continue;
                    var inside = gate.unlocksSpaces & accessible;
                    var outside = gate.requiresSpaces & accessible;
                    if ((result & inside) != 0) result |= inside;
                    if (!gate.Connected) continue;
                    if ((result & outside) != 0) result |= inside;
                    if ((result & inside) != 0) result |= outside;
                }
            } while (result != previous);
            return result;
        }
    }
}
