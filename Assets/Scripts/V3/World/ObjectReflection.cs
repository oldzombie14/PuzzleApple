using UnityEngine;

namespace PuzzleApple.V3
{
    // This effect owns only its copy. Removing it never resets the source's motion or pose.
    [DisallowMultipleComponent]
    public sealed class ObjectReflection : MonoBehaviour
    {
        public ReflectionPlacement placement;
        public OpeningObject Copy { get; private set; }
        Transform liveAxis, destinationAxis;
        bool customAxis;
        public bool HasCustomAxis=>customAxis;
        Vector3 axisPosition;
        Quaternion axisRotation;
        public void TrackAxis(Transform axis)
        {customAxis=true;liveAxis=axis;destinationAxis=null;axisPosition=axis.position;axisRotation=axis.rotation;}
        public void FreezeAxis()
        {if(liveAxis){axisPosition=liveAxis.position;axisRotation=liveAxis.rotation;}liveAxis=null;destinationAxis=null;}
        public void RetargetAxis(Transform axis)
        {
            if(liveAxis==axis||destinationAxis==axis)return;
            if(!customAxis){axisPosition=GetComponent<OpeningObject>().reflectionPlane.position;axisRotation=GetComponent<OpeningObject>().reflectionPlane.rotation;customAxis=true;}
            FreezeAxis();destinationAxis=axis;
            if(!Copy)TrackAxis(axis);
        }
        public Vector3 Normal(OpeningObject source,OpeningMirrorView fallback)=>customAxis?(liveAxis?liveAxis.forward:axisRotation*Vector3.forward):source.reflectionPlane?source.reflectionPlane.forward:fallback.plane.forward;
        public void AdvanceAxis(OpeningObject source,OpeningMirrorView fallback,float dt)
        {
            if(!destinationAxis)return;
            var oldPosition=axisPosition;var oldRotation=axisRotation;
            axisPosition=Vector3.MoveTowards(axisPosition,destinationAxis.position,1.4f*dt);
            axisRotation=Quaternion.RotateTowards(axisRotation,destinationAxis.rotation,65*dt);
            if(Copy)
            {
                var delta=Point(source,source.Body.position,fallback)-Copy.Body.position;
                foreach(var hit in delta.sqrMagnitude>.000001f?Copy.Body.SweepTestAll(delta.normalized,delta.magnitude+.002f,QueryTriggerInteraction.Ignore):new RaycastHit[0])
                    if(hit.rigidbody!=source.Body&&hit.rigidbody!=Copy.Body&&Vector3.Dot(delta,hit.normal)<-.00001f&&hit.distance<delta.magnitude)
                    {axisPosition=oldPosition;axisRotation=oldRotation;return;}
                Copy.Body.position+=delta;
            }
            if(Vector3.Distance(axisPosition,destinationAxis.position)<.001f&&Quaternion.Angle(axisRotation,destinationAxis.rotation)<.1f)TrackAxis(destinationAxis);
        }
        public Vector3 Point(OpeningObject source,Vector3 point,OpeningMirrorView fallback)
        {
            if(customAxis){var center=liveAxis?liveAxis.position:axisPosition;var normal=Normal(source,fallback);return point-2*Vector3.Dot(point-center,normal)*normal;}
            var reflected=source.reflectionPlane?source.ReflectPoint(point):fallback.ReflectPoint(point);
            return placement&&placement.isActiveAndEnabled?placement.Map(source,point,reflected):reflected;
        }
        public Quaternion Rotation(OpeningObject source,Quaternion rotation,OpeningMirrorView fallback)
        {var normal=Normal(source,fallback);return Quaternion.LookRotation(Vector3.Reflect(rotation*Vector3.forward,normal),Vector3.Reflect(rotation*Vector3.up,normal));}
        public OpeningObject Create(OpeningObject source,OpeningMirrorView fallback)
        {
            if(Copy)return Copy;
            var tint=source.GetComponent<WordTransformTarget>();bool tinted=tint&&tint.IsRed;
            if(tinted)tint.SetRed(false);
            try{Copy=Instantiate(source,source.transform.parent);}
            finally{if(tinted)tint.SetRed(true);}
            Copy.name=source.name+" — Reflection";Copy.Initialize();Copy.Stop();
            Copy.Body.interpolation=RigidbodyInterpolation.None;
            Copy.transform.SetPositionAndRotation(Point(source,source.transform.position,fallback),Rotation(source,source.transform.rotation,fallback));
            Physics.SyncTransforms();
            if(Overlaps(Copy)){Remove();return null;}
            return Copy;
        }
        public void Remove()
        {
            if(!Copy)return;
            Copy.gameObject.SetActive(false);
            if(Application.isPlaying)Destroy(Copy.gameObject);else DestroyImmediate(Copy.gameObject);
            Copy=null;
        }
        public void Sync(OpeningObject source,OpeningMirrorView fallback,bool physics)
        {
            if(!Copy)return;
            var p=Point(source,physics?source.Body.position:source.transform.position,fallback);
            var q=Rotation(source,physics?source.Body.rotation:source.transform.rotation,fallback);
            if(physics){Copy.Body.MovePosition(p);Copy.Body.MoveRotation(q);}
            else Copy.transform.SetPositionAndRotation(p,q);
        }
        // Both halves use one allowed displacement. Sweeping the relative motion also
        // catches a pair approaching each other at twice either body's speed.
        public Vector3 MovePair(OpeningObject source,OpeningMirrorView fallback,Vector3 delta)
        {
            source.Stop();Copy.Stop();
            var start=source.Body.position;
            var copyStart=Copy.Body.position;
            var copyDelta=Point(source,start+delta,fallback)-Point(source,start,fallback);
            float fraction=1;Vector3 normal=Vector3.zero;
            Sweep(source.Body,Copy.Body,delta,false,false,source,fallback,ref fraction,ref normal);
            Sweep(Copy.Body,source.Body,copyDelta,false,true,source,fallback,ref fraction,ref normal);
            Sweep(source.Body,Copy.Body,delta-copyDelta,true,false,source,fallback,ref fraction,ref normal);
            // Commit the swept poses before the next pair queries the scene. This
            // prevents two kinematic pairs each claiming the same empty space.
            source.Body.position=start+delta*fraction;
            Copy.Body.position=copyStart+copyDelta*fraction;
            return normal;
        }
        static bool Overlaps(OpeningObject obj)
        {
            var hits=new Collider[256];var bounds=obj.Bounds;
            int count=obj.gameObject.scene.GetPhysicsScene().OverlapBox(bounds.center,bounds.extents,hits,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
            if(count==hits.Length)return true;
            foreach(var own in obj.GetComponentsInChildren<Collider>())
            {
                if(!own.enabled||own.isTrigger)continue;
                for(int i=0;i<count;i++)
                {
                    var other=hits[i];if(other.attachedRigidbody==obj.Body)continue;
                    if(Physics.ComputePenetration(own,own.transform.position,own.transform.rotation,other,other.transform.position,other.transform.rotation,out _,out var depth)&&depth>.002f)return true;
                }
            }
            return false;
        }
        public void ResolveShapeChange(OpeningObject source,OpeningMirrorView fallback)
        {
            // Restoring a wider apple can start inside a neighbour. Separate using
            // the native minimum-translation vector, moving both halves together.
            var hits=new Collider[256];
            for(int attempt=0;attempt<12;attempt++)
            {
                Physics.SyncTransforms();Vector3 correction=Vector3.zero;
                foreach(var obj in Copy?new[]{source,Copy}:new[]{source})
                {
                    var b=obj.Bounds;int count=obj.gameObject.scene.GetPhysicsScene().OverlapBox(b.center,b.extents,hits,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
                    foreach(var own in obj.GetComponentsInChildren<Collider>())
                    {
                        if(!own.enabled||own.isTrigger)continue;
                        for(int i=0;i<count;i++)
                        {
                            var other=hits[i];if(other.attachedRigidbody==obj.Body)continue;
                            if(!Physics.ComputePenetration(own,own.transform.position,own.transform.rotation,other,other.transform.position,other.transform.rotation,out var normal,out var depth)||depth<=.002f)continue;
                            var plane=Normal(source,fallback);
                            bool pair=Copy&&(other.attachedRigidbody==source.Body||other.attachedRigidbody==Copy.Body);
                            float divisor=pair?Mathf.Max(.1f,1-Vector3.Dot(normal,Vector3.Reflect(normal,plane))):1;
                            if(obj==Copy)normal=Vector3.Reflect(normal,plane);
                            var candidate=normal*((depth+.002f)/divisor);
                            if(candidate.sqrMagnitude>correction.sqrMagnitude)correction=candidate;
                        }
                    }
                }
                if(correction.sqrMagnitude<.000001f)break;
                source.Body.position+=correction;source.transform.position=source.Body.position;
                if(Copy){Copy.Body.position=Point(source,source.Body.position,fallback);Copy.transform.position=Copy.Body.position;}
            }
        }
        static void Sweep(Rigidbody body,Rigidbody partner,Vector3 delta,bool partnerOnly,bool reflected,
            OpeningObject source,OpeningMirrorView fallback,ref float fraction,ref Vector3 normal)
        {
            float distance=delta.magnitude;if(distance<.000001f)return;
            var direction=delta/distance;
            foreach(var hit in body.SweepTestAll(direction,distance+.002f,QueryTriggerInteraction.Ignore))
            {
                bool pair=hit.rigidbody==partner;
                if(pair!=partnerOnly||hit.rigidbody==body||Vector3.Dot(direction,hit.normal)>=-.0001f)continue;
                float allowed=Mathf.Clamp01((hit.distance-.002f)/distance);
                if(allowed>=fraction)continue;
                fraction=allowed;
                normal=reflected?Vector3.Reflect(hit.normal,source.GetComponent<ObjectReflection>().Normal(source,fallback)):hit.normal;
            }
        }
        void OnDisable()=>Remove();
    }
}
