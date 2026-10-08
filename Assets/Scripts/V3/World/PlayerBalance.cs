using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace PuzzleApple.V3
{
    // A mobile support, deliberately separate from MainRoute's puzzle/pressure plate.
    public sealed class PlayerBalance : MonoBehaviour
    {
        public OpeningRoom room;
        public GameObject visual;
        public DestinationSlots slots;
        public Transform axis;
        public Renderer[] hideInFirstPerson;
        public bool Active { get; private set; }
        readonly Dictionary<OpeningObject,Vector3> passengers=new Dictionary<OpeningObject,Vector3>();
        readonly Dictionary<OpeningObject,Quaternion> rotations=new Dictionary<OpeningObject,Quaternion>();
        readonly Collider[] hits=new Collider[128];
        Vector3 previousPosition;
        Quaternion previousRotation;
        public bool Carries(OpeningObject obj)=>Active&&passengers.ContainsKey(obj);
        public void SetIdentity(bool value)
        {
            if(value==Active)return;
            if(!value)
            {
                SyncPose();
                foreach(var obj in passengers.Keys.Where(o=>o)){obj.Stop();obj.GetComponent<ObjectReflection>().FreezeAxis();}
                passengers.Clear();rotations.Clear();
            }
            Active=value;visual.SetActive(value);
            previousPosition=transform.position;previousRotation=transform.rotation;
            if(value)foreach(var c in visual.GetComponentsInChildren<Collider>())Physics.IgnoreCollision(room.player.GetComponent<CharacterController>(),c,true);
        }
        public void Release(OpeningObject obj)
        {if(passengers.Remove(obj)){rotations.Remove(obj);obj.GetComponent<ObjectReflection>().FreezeAxis();}}
        public void SyncPose()
        {
            if(!Active)return;
            Physics.SyncTransforms();
            bool moved=transform.position!=previousPosition||transform.rotation!=previousRotation;
            if(moved&&passengers.Count>0)
            {
                bool blocked=false;
                foreach(var pair in passengers)
                {
                    var obj=pair.Key;if(!obj)continue;
                    var target=transform.TransformPoint(pair.Value);var delta=target-obj.Body.position;
                    if(delta.sqrMagnitude>.000001f)
                        foreach(var hit in obj.Body.SweepTestAll(delta.normalized,delta.magnitude+.002f,QueryTriggerInteraction.Ignore))
                            if(!Ignore(hit.collider)&&Vector3.Dot(delta,hit.normal)<-.00001f&&hit.distance<delta.magnitude)blocked=true;
                    var b=obj.PhysicalBounds;
                    int count=obj.gameObject.scene.GetPhysicsScene().OverlapBox(b.center+delta,b.extents,hits,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
                    for(int i=0;i<count;i++)if(!Ignore(hits[i]))foreach(var own in obj.GetComponentsInChildren<Collider>())
                        if(own.enabled&&!own.isTrigger&&Physics.ComputePenetration(own,own.transform.position+delta,own.transform.rotation,
                            hits[i],hits[i].transform.position,hits[i].transform.rotation,out _,out var depth)&&depth>.005f)blocked=true;
                }
                if(blocked){room.player.Teleport(previousPosition);transform.rotation=previousRotation;Physics.SyncTransforms();}
                else foreach(var pair in passengers)
                {
                    if(!pair.Key)continue;var obj=pair.Key;obj.Stop();
                    obj.Body.position=transform.TransformPoint(pair.Value);obj.Body.rotation=transform.rotation*rotations[obj];
                    obj.transform.SetPositionAndRotation(obj.Body.position,obj.Body.rotation);
                }
            }
            previousPosition=transform.position;previousRotation=transform.rotation;
            foreach(var obj in room.objects.Concat(room.Copies.Values).Where(o=>o&&o.wordId=="apple"))
            {
                bool on=Enumerable.Range(0,slots.places.Length).Any(i=>slots.Contains(i,obj));
                if(!on){Release(obj);continue;}
                // A moving arrival must finish descending before it becomes cargo.
                if(!passengers.ContainsKey(obj)&&!room.IsSettledOnSupport(obj))continue;
                passengers[obj]=transform.InverseTransformPoint(obj.transform.position);
                rotations[obj]=Quaternion.Inverse(transform.rotation)*obj.transform.rotation;
                obj.GetComponent<ObjectReflection>().TrackAxis(axis);
            }
            slots.RefreshOccupancy(room.objects.Concat(room.Copies.Values).Where(o=>o).Select(o=>new DestinationSlots.Presence(o,o.transform.position)));
        }
        bool Ignore(Collider c)=>c.transform.IsChildOf(transform)||passengers.Keys.Any(o=>o&&c.attachedRigidbody==o.Body);
        void OnEnable(){RenderPipelineManager.beginCameraRendering+=BeforeCamera;RenderPipelineManager.endCameraRendering+=AfterCamera;}
        void OnDisable(){RenderPipelineManager.beginCameraRendering-=BeforeCamera;RenderPipelineManager.endCameraRendering-=AfterCamera;}
        void BeforeCamera(ScriptableRenderContext context,Camera camera)
        {foreach(var r in hideInFirstPerson)if(r)r.forceRenderingOff=camera==room.presentation.view;}
        void AfterCamera(ScriptableRenderContext context,Camera camera)
        {foreach(var r in hideInFirstPerson)if(r)r.forceRenderingOff=false;}
    }
}
