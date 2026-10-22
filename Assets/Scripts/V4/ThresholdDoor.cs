using UnityEngine;
using System.Collections;

namespace PuzzleApple.V4
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ThresholdDoor : MonoBehaviour
    {
        public float groundHeight = .02f;
        [Min(.1f)] public float loweringSpeed = 6;
        [Min(.1f)] public float loweringAcceleration = 6;
        [Min(1)] public float collectionDistance = 65;
        public float spawnDistance = 42, suspendedHeight = 18;
        public Material rippleMaterial;
        public float rippleDuration = 2.4f;
        [Min(3)] public float rippleRadius = 12;
        public bool Revealed { get; private set; }
        public bool LandingCompleted { get; private set; }
        public bool Descending { get; private set; }
        public bool Landed => body && body.position.y <= groundHeight + .01f;
        Rigidbody body;
        LightMotion freeMotion;
        GameObject ripple;
        BoxCollider[] frameColliders;
        float speed;
        void Awake()
        {
            body = GetComponent<Rigidbody>();
            frameColliders = GetComponentsInChildren<BoxCollider>();
            body.isKinematic = true; body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            freeMotion = GetComponent<LightMotion>();
            if (!freeMotion) freeMotion = gameObject.AddComponent<LightMotion>();
        }
        public void Reveal(Transform view)
        {
            if (Revealed) return;
            gameObject.SetActive(true);
            var forward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Vector3.ProjectOnPlane(view.up, Vector3.up).normalized;
            var position = view.position + forward * spawnDistance;
            position.y = groundHeight + suspendedHeight;
            body.position = position;
            body.rotation = Quaternion.LookRotation(forward, Vector3.up);
            transform.SetPositionAndRotation(position, body.rotation);
            Revealed = true;
            Physics.SyncTransforms();
        }
        public void SetMotion(bool freelyMoving, bool descending)
        {
            freeMotion.SetMoving(freelyMoving);
            SetDescending(descending && !freelyMoving);
        }
        public void SetDescending(bool value)
        {
            Descending = value;
            if (!value) speed = 0;
        }
        void FixedUpdate()
        {
            if (!Descending) return;
            if (Landed) { CompleteLanding(); return; }
            float remaining = body.position.y - groundHeight;
            float acceleration = Mathf.Max(.1f, loweringAcceleration);
            speed = Mathf.MoveTowards(speed, Mathf.Min(loweringSpeed, Mathf.Sqrt(2 * acceleration * remaining)), Time.fixedDeltaTime * acceleration);
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
        void CompleteLanding()
        {
            if (LandingCompleted) return;
            LandingCompleted = true;
            if (rippleMaterial) StartCoroutine(Ripple());
        }
        IEnumerator Ripple()
        {
            // A world-space ring remains at the contact point even if free movement resumes.
            ripple = new GameObject("Door landing ripple");
            var lines = new LineRenderer[2];
            for (int ring = 0; ring < lines.Length; ring++)
            {
                var child = new GameObject(ring == 0 ? "Broad ripple" : "Fine ripple");
                child.transform.SetParent(ripple.transform, false);
                var line = lines[ring] = child.AddComponent<LineRenderer>();
                line.sharedMaterial = rippleMaterial;
                line.useWorldSpace = true; line.loop = true; line.positionCount = 192;
                line.widthMultiplier = ring == 0 ? .32f : .075f;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
            }
            var origin = body.position; origin.y = groundHeight + .015f;
            const float delay = .25f;
            for (float t = 0; t < rippleDuration + delay; t += Time.deltaTime)
            {
                for (int ring = 0; ring < lines.Length; ring++)
                {
                    var line = lines[ring];
                    float u = Mathf.Clamp01((t - ring * delay) / Mathf.Max(.01f, rippleDuration));
                    float radius = Mathf.Lerp(2.45f, rippleRadius, 1 - Mathf.Pow(1-u, 2));
                    float alpha = Mathf.SmoothStep(0, 1, u * 10) * Mathf.Pow(1-u, 1.7f) * (ring == 0 ? .65f : 1);
                    line.startColor = line.endColor = new Color(1, 1, 1, alpha);
                    for (int i = 0; i < line.positionCount; i++)
                    {
                        float angle = i * Mathf.PI * 2 / line.positionCount;
                        line.SetPosition(i, origin + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius);
                    }
                }
                yield return null;
            }
            Destroy(ripple);
        }
        void OnDestroy() { if (ripple) Destroy(ripple); }
    }
}
