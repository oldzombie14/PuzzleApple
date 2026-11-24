using UnityEngine;

namespace PuzzleApple.V3
{
    [RequireComponent(typeof(Rigidbody),typeof(ObjectReflection))]
    public sealed class OpeningObject : MonoBehaviour
    {
        public string wordId="mirror";
        public Transform reflectionPlane;
        public BoxCollider movementBounds;
        [Tooltip("Optional landing slots; the closest free slot is selected when movement begins.")]
        public Transform[] destinationSlots=new Transform[0];
        public Rigidbody Body { get; private set; }
        public Vector3 Home { get; private set; }
        public Quaternion HomeRotation { get; private set; }
        readonly Vector3[] contactNormals=new Vector3[12];
        int contactCount;
        public void ClearContacts()=>contactCount=0;
        void OnCollisionEnter(Collision collision)=>RecordContacts(collision);
        void OnCollisionStay(Collision collision)=>RecordContacts(collision);
        void RecordContacts(Collision collision)
        {
            for(int i=0;i<collision.contactCount;i++)
            {
                var normal=collision.GetContact(i).normal;
                bool duplicate=false;
                for(int j=0;j<contactCount;j++)if(Vector3.Dot(contactNormals[j],normal)>.995f){duplicate=true;break;}
                if(!duplicate&&contactCount<contactNormals.Length)contactNormals[contactCount++]=normal;
            }
        }
        public Vector3 ReflectMotion(Vector3 direction)
        {
            // Preserve the component along the surface. Only an incoming contact
            // reflects, so persistent contact cannot flip an already outgoing ray.
            for(int i=0;i<contactCount;i++)
                if(Vector3.Dot(direction,contactNormals[i])<-.001f)
                    direction=Vector3.Reflect(direction,contactNormals[i]);
            contactCount=0;return direction.normalized;
        }
        public Bounds Bounds
        {
            get
            {
                var renderers=GetComponentsInChildren<Renderer>();
                var b=new Bounds(transform.position,Vector3.zero);
                bool first=true;
                foreach(var r in renderers)if(r.enabled)
                {if(first){b=r.bounds;first=false;}else b.Encapsulate(r.bounds);}
                return b;
            }
        }
        public Bounds PhysicalBounds
        {
            get
            {
                var bounds=new Bounds(transform.position,Vector3.zero);bool first=true;
                foreach(var collider in GetComponentsInChildren<Collider>())
                {
                    if(!collider.enabled||collider.isTrigger)continue;
                    if(first){bounds=collider.bounds;first=false;}else bounds.Encapsulate(collider.bounds);
                }
                return first?Bounds:bounds;
            }
        }
        void Awake()=>Initialize();
        public void Initialize(){if(Body)return;Body=GetComponent<Rigidbody>();Home=transform.position;HomeRotation=transform.rotation;}
        public void Stop()
        {
            contactCount=0;
            if(!Body.isKinematic){Body.linearVelocity=Vector3.zero;Body.angularVelocity=Vector3.zero;}
            Body.isKinematic=true;
        }
        public void MoveToSupport(Vector3 delta)
        {
            Stop();float distance=delta.magnitude;if(distance<.000001f)return;
            float fraction=1;var direction=delta/distance;
            foreach(var hit in Body.SweepTestAll(direction,distance+.002f,QueryTriggerInteraction.Ignore))
                if(hit.rigidbody!=Body&&Vector3.Dot(direction,hit.normal)<-.0001f)
                    fraction=Mathf.Min(fraction,Mathf.Clamp01((hit.distance-.002f)/distance));
            Body.position+=delta*fraction;
        }
        public void Restore(){Stop();transform.SetPositionAndRotation(Home,HomeRotation);Body.position=Home;Body.rotation=HomeRotation;}
        public bool AtHome(float tolerance=.025f)=>Vector3.Distance(transform.position,Home)<=tolerance&&Quaternion.Angle(transform.rotation,HomeRotation)<1;
        public Vector3 ReflectPoint(Vector3 point)
        {var p=reflectionPlane.InverseTransformPoint(point);p.z=-p.z;return reflectionPlane.TransformPoint(p);}
        public Quaternion ReflectRotation(Quaternion rotation)
        {var n=reflectionPlane.forward;return Quaternion.LookRotation(Vector3.Reflect(rotation*Vector3.forward,n),Vector3.Reflect(rotation*Vector3.up,n));}
    }
}
