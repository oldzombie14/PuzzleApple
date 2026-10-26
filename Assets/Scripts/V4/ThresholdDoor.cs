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
        [Min(0)] public float releaseLandingTolerance = .5f;
        public Material rippleMaterial;
        public float rippleDuration = 2.4f;
        [Min(3)] public float rippleRadius = 12;
        public bool Revealed { get; private set; }
        public bool Appearing => assembly&&assembly.Playing;
        DoorAssembly assembly;
        public bool LandingCompleted { get; private set; }
        public bool Descending { get; private set; }
        public bool Landed => body && body.position.y <= groundHeight + .01f;
        Rigidbody body;
        LightMotion freeMotion;
        GameObject ripple;
        BoxCollider[] frameColliders;
        Collider ground;
        float speed;
        Transform facingPlayer;
        void Awake()
        {
            body = GetComponent<Rigidbody>();
            frameColliders = GetComponentsInChildren<BoxCollider>();
            body.isKinematic = true; body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            freeMotion = GetComponent<LightMotion>();
            if (!freeMotion) freeMotion = gameObject.AddComponent<LightMotion>();
            var effects = FindFirstObjectByType<WorldSentenceEffects>();
            if (effects) ground = effects.ground;
        }
        public void Reveal(Transform view)
        {
            if (Revealed) return;
            gameObject.SetActive(true);
            facingPlayer=view;
            var forward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Vector3.ProjectOnPlane(view.up, Vector3.up).normalized;
            var position = view.position + forward * spawnDistance;
            position.y = groundHeight + suspendedHeight;
            body.position = position;
            // Aim the frame from the actual camera-to-door bearing, with no pitch or roll.
            var bearing = Vector3.ProjectOnPlane(position - view.position, Vector3.up).normalized;
            body.rotation = Quaternion.LookRotation(bearing, Vector3.up);
            transform.SetPositionAndRotation(position, body.rotation);
            Revealed = true;
            assembly=GetComponent<DoorAssembly>();if(!assembly)assembly=gameObject.AddComponent<DoorAssembly>();
            assembly.Begin(rippleMaterial);
            Physics.SyncTransforms();
        }
        public void SetMotion(bool freelyMoving, bool descending)
        {
            bool starting=freelyMoving&&!freeMotion.Moving;
            freeMotion.SetMoving(freelyMoving);
            if(starting&&Landed)
            {
                var direction=freeMotion.Direction;direction.y=Mathf.Max(.25f,Mathf.Abs(direction.y));
                freeMotion.RestoreDirection(direction);
            }
            SetDescending(descending && !freelyMoving);
        }
        public void SetDescending(bool value)
        {
            Descending = value;
            if (!value) speed = 0;
        }
        public void SettleAfterFreeMotion()
        {
            if(!Revealed)return;
            if(ground)groundHeight=ground.bounds.max.y+.02f;
            if(Mathf.Abs(body.position.y-groundHeight)>releaseLandingTolerance)return;
            var position=body.position;position.y=groundHeight;
            body.position=position;transform.position=position;
            CompleteLanding();Physics.SyncTransforms();
        }
        void FixedUpdate()
        {
            if(Revealed&&facingPlayer)
            {
                var bearing=Vector3.ProjectOnPlane(body.position-facingPlayer.position,Vector3.up);
                if(bearing.sqrMagnitude>.0001f)body.MoveRotation(Quaternion.LookRotation(bearing,Vector3.up));
            }
            if (ground) groundHeight = ground.bounds.max.y + .02f;
            if (Revealed && Landed && !freeMotion.Moving) CompleteLanding();
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
                    if (hit.rigidbody != body && !hit.collider.transform.IsChildOf(transform) && !hit.collider.GetComponentInParent<OpeningLight>())
                        travel = Mathf.Min(travel, Mathf.Max(0, hit.distance - .01f));
            }
            body.MovePosition(body.position + Vector3.down * travel);
        }
        void OnCollisionEnter(Collision collision) => RecordGroundContact(collision);
        void OnCollisionStay(Collision collision) => RecordGroundContact(collision);
        void RecordGroundContact(Collision collision)
        {
            if (!Revealed || LandingCompleted || freeMotion.Moving || collision.collider != ground) return;
            for (int i = 0; i < collision.contactCount; i++)
                if (Vector3.Dot(collision.GetContact(i).normal, Vector3.up) > .5f)
                {
                    CompleteLanding();
                    return;
                }
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
