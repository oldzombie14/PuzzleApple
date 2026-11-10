using UnityEngine;

namespace PuzzleApple.V5
{
    // Future spaces can opt into the same closure system without referencing the first room.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ClosureRegion : MonoBehaviour
    {
        public bool allowBox=true,isolateEnvironment=true;
        public int priority;
        void Reset(){GetComponent<BoxCollider>().isTrigger=true;}
        public bool Contains(Vector3 point)
        {
            var box=GetComponent<BoxCollider>();var p=transform.InverseTransformPoint(point)-box.center;var half=box.size*.5f;
            return Mathf.Abs(p.x)<=half.x&&Mathf.Abs(p.y)<=half.y&&Mathf.Abs(p.z)<=half.z;
        }
    }
}
