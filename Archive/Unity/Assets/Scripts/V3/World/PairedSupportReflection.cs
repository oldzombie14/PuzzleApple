using UnityEngine;
namespace PuzzleApple.V3
{
    public sealed class PairedSupportReflection : ReflectionPlacement
    {
        public Transform first,second;
        public float halfWidth=.38f,heightTolerance=.12f;
        public override Vector3 Map(OpeningObject source,Vector3 point,Vector3 reflected)
        {
            if(!first||!second)return reflected;
            var b=source.Bounds;var feet=new Vector3(b.center.x,b.min.y,b.center.z)+(point-source.transform.position);
            if(On(feet,first))reflected.y+=second.position.y-first.position.y;
            else if(On(feet,second))reflected.y+=first.position.y-second.position.y;
            return reflected;
        }
        bool On(Vector3 feet,Transform support)=>Mathf.Abs(feet.x-support.position.x)<halfWidth&&
            Mathf.Abs(feet.z-support.position.z)<halfWidth&&Mathf.Abs(feet.y-support.position.y)<heightTolerance;
    }
}
