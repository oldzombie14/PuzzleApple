#if UNITY_EDITOR
using System;
using UnityEngine;

namespace PuzzleApple.V5.Editor
{
    // Test source deliberately unrelated to any game door type.
    public sealed class SentenceBoundaryProbe : MonoBehaviour, IProgressBoundary
    {
        float progress;
        public int ProgressReads { get; private set; }
        public int ConnectionReads { get; private set; }
        public float BoundaryProgress { get { ProgressReads++; return progress; } }
        public bool AllowsConnection { get { ConnectionReads++; return progress > 0; } }
        public event Action BoundaryChanged;
        public void SetProgress(float value) { progress = value; BoundaryChanged?.Invoke(); }
    }
}
#endif
