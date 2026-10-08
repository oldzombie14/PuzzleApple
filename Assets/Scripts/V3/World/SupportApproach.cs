using UnityEngine;

namespace PuzzleApple.V3
{
    // A short collision-checked detour lets carried bodies leave the underside of
    // a support before rising. The native body/controller still executes movement.
    public sealed class SupportApproach
    {
        readonly Collider[] overlaps=new Collider[128];
        readonly RaycastHit[] hits=new RaycastHit[64];
        Vector3 escape, destination;
        Vector3 previous;
        int stalledSteps;
        bool escaping;
        public Vector3 Next(Component owner,Bounds bounds,Vector3 landing,float clearance)
        {
            var origin=owner.transform.position;
            stalledSteps=Vector3.Distance(previous,origin)<.001f?stalledSteps+1:0;
            previous=origin;
            if(stalledSteps>20){escaping=false;stalledSteps=0;}
            if(Vector3.Distance(destination,landing)>.3f)escaping=false;
            destination=landing;
            if(escaping)
            {
                if(Vector3.ProjectOnPlane(origin-escape,Vector3.up).magnitude>.04f)return new Vector3(escape.x,origin.y,escape.z);
                escaping=false;
            }
            var raised=landing+Vector3.up*clearance;
            bool aligned=Vector3.ProjectOnPlane(landing-origin,Vector3.up).magnitude<.035f;
            // An aligned body below the pan must also escape; alignment alone is
            // not evidence of having approached from above.
            bool below=origin.y<landing.y-.03f;
            if(aligned&&!below)return landing;
            var lift=new Vector3(origin.x,raised.y,origin.z);
            if(origin.y<raised.y-.025f)
            {
                if(Clear(owner,bounds,origin,lift))return lift;
                float best=float.PositiveInfinity;Vector3 candidate=origin;
                foreach(float radius in new[]{.65f,1.1f,1.65f})
                for(int i=0;i<16;i++)
                {
                    float angle=i*Mathf.PI/8;
                    var side=origin+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius;
                    var above=new Vector3(side.x,raised.y,side.z);
                    float cost=radius+Vector3.Distance(above,raised);
                    if(cost>=best||!Clear(owner,bounds,origin,side)||!Clear(owner,bounds,side,above)||!Clear(owner,bounds,above,raised))continue;
                    best=cost;candidate=side;
                }
                if(!float.IsPositiveInfinity(best)){escape=candidate;escaping=true;return escape;}
                return origin;
            }
            return raised;
        }
        bool Clear(Component owner,Bounds bounds,Vector3 from,Vector3 to)
        {
            float distance=Vector3.Distance(from,to);if(distance<.001f)return true;
            var physics=owner.gameObject.scene.GetPhysicsScene();
            var direction=(to-from)/distance;
            int hitCount=physics.BoxCast(bounds.center+from-owner.transform.position,Vector3.Max(bounds.extents-Vector3.one*.02f,Vector3.one*.01f),direction,hits,
                Quaternion.identity,distance,~0,QueryTriggerInteraction.Ignore);
            if(hitCount==hits.Length)return false;
            for(int i=0;i<hitCount;i++)
            {
                var hit=hits[i];if(hit.collider.transform.IsChildOf(owner.transform))continue;
                if(hit.distance>.002f&&Vector3.Dot(direction,hit.normal)<-.001f)return false;
                if(hit.distance<=.002f&&direction.y>.01f&&hit.collider.bounds.center.y>bounds.center.y+from.y-owner.transform.position.y)return false;
            }
            var colliders=owner.GetComponentsInChildren<Collider>();
            int steps=Mathf.CeilToInt(distance/.06f);
            for(int step=1;step<=steps;step++)
            {
                var offset=Vector3.Lerp(from,to,(float)step/steps)-owner.transform.position;
                int count=physics.OverlapBox(bounds.center+offset,bounds.extents+Vector3.one*.01f,overlaps,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
                if(count==overlaps.Length)return false;
                for(int i=0;i<count;i++)
                {
                    var other=overlaps[i];if(other.transform.IsChildOf(owner.transform))continue;
                    foreach(var own in colliders)
                    {
                        if(!own.enabled||own.isTrigger)continue;
                        if(!Physics.ComputePenetration(own,own.transform.position+offset,own.transform.rotation,
                            other,other.transform.position,other.transform.rotation,out _,out var depth)||depth<.003f)continue;
                        // Existing shallow contacts may be left, but never deepened.
                        Physics.ComputePenetration(own,own.transform.position+from-owner.transform.position,own.transform.rotation,
                            other,other.transform.position,other.transform.rotation,out _,out var originalDepth);
                        if(depth>originalDepth+.002f)return false;
                    }
                }
            }
            return true;
        }
    }
}
