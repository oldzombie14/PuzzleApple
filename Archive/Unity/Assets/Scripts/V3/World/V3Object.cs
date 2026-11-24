using UnityEngine;
namespace PuzzleApple.V3
{
    public enum ObjectKind { Apple, Mirror, Door, Indicator, Balance }
    public sealed class V3Object : MonoBehaviour
    {
        public ObjectKind kind;
        public bool copy;
        public int index;
        [System.NonSerialized] public int slot=-1;
        [System.NonSerialized] public bool wasMoving;
        [System.NonSerialized] public Rigidbody body;
        public Bounds Bounds { get {var rs=GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;} }
    }
}
