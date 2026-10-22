using UnityEngine;

namespace PuzzleApple.V4
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ThresholdDoor : MonoBehaviour
    {
        public float groundHeight = .02f;
        [Min(.1f)] public float loweringSpeed = .85f;
        [Min(1)] public float collectionDistance = 24;
        public bool Descending { get; private set; }
        public bool Landed => body && body.position.y <= groundHeight + .01f;
        Rigidbody body;
        BoxCollider[] frameColliders;
        float speed;
        void Awake()
        {
            body = GetComponent<Rigidbody>();
            frameColliders = GetComponentsInChildren<BoxCollider>();
            body.isKinematic = true; body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }
        public void SetDescending(bool value)
        {
            Descending = value;
            if (!value) speed = 0;
        }
        void FixedUpdate()
        {
            if (!Descending || Landed) return;
            float remaining = body.position.y - groundHeight;
            speed = Mathf.MoveTowards(speed, Mathf.Min(loweringSpeed, Mathf.Sqrt(2 * remaining)), Time.fixedDeltaTime * 1.2f);
            float travel = Mathf.Min(remaining, speed * Time.fixedDeltaTime);
            // The opening remains passable; solid frame pieces must not descend through a player or prop.
            foreach (var frame in frameColliders)
            {
                if (!frame || !frame.enabled || frame.isTrigger) continue;
                var scale = frame.transform.lossyScale;
                var halfSize = Vector3.Scale(frame.size * .5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                foreach (var hit in Physics.BoxCastAll(frame.transform.TransformPoint(frame.center), halfSize,
                    Vector3.down, frame.transform.rotation, travel + .01f, ~0, QueryTriggerInteraction.Ignore))
                    if (hit.rigidbody != body && !hit.collider.transform.IsChildOf(transform))
                        travel = Mathf.Min(travel, Mathf.Max(0, hit.distance - .01f));
            }
            body.MovePosition(body.position + Vector3.down * travel);
        }
    }
}
