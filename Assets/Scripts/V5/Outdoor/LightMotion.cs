using UnityEngine;

namespace PuzzleApple.V5.Outdoor
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class LightMotion : MonoBehaviour
    {
        public float speed = 1.2f;
        Collider landingSurface;
        public bool Launched {get;private set;}
        public bool Moving { get; private set; }
        Rigidbody body;
        Vector3 direction;
        public Vector3 Direction => direction;
        public void RestoreDirection(Vector3 value) { direction = value.normalized; }
        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic = true;
        }
        public void SetMoving(bool value)
        {
            if (Moving == value) return;
            Moving = value;
            if(Launched)return;
            if (value) { direction = Random.onUnitSphere; body.isKinematic = false; }
            else { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; body.isKinematic = true; }
        }
        public void Launch(Vector3 velocity,Collider ground)
        {
            landingSurface=ground;Launched=true;body.isKinematic=false;body.useGravity=true;body.linearVelocity=velocity;
        }
        void FixedUpdate() { if (Moving&&!Launched) body.linearVelocity = direction * speed; }
        void OnCollisionEnter(Collision collision) => Reflect(collision);
        void OnCollisionStay(Collision collision) => Reflect(collision);
        void Reflect(Collision collision)
        {
            if(Launched)
            {
                if(collision.collider!=landingSurface)return;
                for(int i=0;i<collision.contactCount;i++)
                {
                    if(collision.GetContact(i).normal.y<.65f)continue;
                    Launched=false;body.useGravity=false;body.linearVelocity=Vector3.zero;
                    body.isKinematic=!Moving;
                    if(Moving)direction=Random.onUnitSphere;
                    break;
                }
                return;
            }
            if (!Moving) return;
            for (int i = 0; i < collision.contactCount; i++)
            {
                var normal = collision.GetContact(i).normal;
                if (Vector3.Dot(direction, normal) < -.001f) direction = Vector3.Reflect(direction, normal).normalized;
            }
        }
    }
}
