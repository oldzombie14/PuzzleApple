using UnityEngine;

namespace PuzzleApple.V4
{
    // A prerecorded first-person clip, packed in a grid to avoid a video decoder for a tiny looping memory.
    [CreateAssetMenu(menuName="PuzzleApple/V4/Snapshot sequence")]
    public sealed class SnapshotSequence : ScriptableObject
    {
        public Texture2D atlas;
        public int columns=8, rows=8, frames=64;
        public float framesPerSecond=12, aspect=16f/9;
        public Rect Frame(float seconds)
        {
            int index=Mathf.FloorToInt(seconds*framesPerSecond)%Mathf.Max(1,frames);
            return new Rect((float)(index%columns)/columns,1f-(float)(index/columns+1)/rows,1f/columns,1f/rows);
        }
    }
}
