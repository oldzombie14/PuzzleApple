using System.Collections;
using PuzzleApple.Cognition;
using UnityEngine;

namespace PuzzleApple
{
    // Current cognition gates actions; collection and consumption are permanent session facts.
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class CognitionApple : CognitionInteractable
    {
        [SerializeField] CognitionBoard board;
        [SerializeField] GameObject bittenPrefab;
        [SerializeField] CognitionKey key;
        [SerializeField, Min(0)] float moveSpeed = .8f;
        [SerializeField, Min(0)] float bobAmplitude = .035f;
        Vector3 direction;
        float phase, hoverHeight;
        bool wasMoving, resumeGravity;
        Renderer wholeRenderer;
        Collider wholeCollider;
        Rigidbody body;
        GameObject bitten;
        public bool Collected { get; private set; }
        public bool Eaten { get; private set; }
        public bool Busy { get; private set; }
        public bool IsMoving => !Eaten && !Busy && board && board.State != null && board.State.AppleMoving;
        public override CognitionHover Hover => Busy || Eaten ? CognitionHover.Forbidden :
            !Collected ? CognitionHover.Collect : board.State.CanConsumeApple ? CognitionHover.Question : CognitionHover.Forbidden;
        public Vector3 Center => wholeRenderer ? wholeRenderer.bounds.center : transform.position;
        void Awake()
        {
            wholeRenderer = GetComponent<Renderer>(); wholeCollider = GetComponent<SphereCollider>();
            body = GetComponent<Rigidbody>();
        }
        void FixedUpdate()
        {
            if (Busy || Eaten) return;
            if (!IsMoving)
            {
                if (wasMoving)
                {
                    body.useGravity = true;
                    body.constraints = RigidbodyConstraints.None;
                    wasMoving = false;
                }
                return;
            }
            if (!wasMoving)
            {
                float angle = Random.Range(0, Mathf.PI * 2);
                direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                body.isKinematic = false;
                body.useGravity = false;
                body.constraints = RigidbodyConstraints.FreezeRotation;
                body.angularVelocity = Vector3.zero;
                hoverHeight = Center.y + .10f;
                phase = 0;
                wasMoving = true;
            }
            if (body.SweepTest(direction, out var hit, .22f, QueryTriggerInteraction.Ignore))
                SteerAlongSurface(hit.normal);
            phase += Time.fixedDeltaTime * 2;
            float lift = Mathf.Clamp((hoverHeight + Mathf.Sin(phase) * bobAmplitude - Center.y) * 4, -.6f, .6f);
            body.linearVelocity = direction * moveSpeed + Vector3.up * lift;
        }
        void OnCollisionStay(Collision collision)
        {
            if (!IsMoving) return;
            for (int i = 0; i < collision.contactCount; i++) SteerAlongSurface(collision.GetContact(i).normal);
        }
        void SteerAlongSurface(Vector3 normal)
        {
            // Keep moving tangentially at walls; a small outward bias clears corners and friction.
            if (Mathf.Abs(normal.y) > .65f) return;
            normal.y = 0; normal.Normalize();
            if (Vector3.Dot(direction, normal) >= .12f) return;
            var tangent = Vector3.ProjectOnPlane(direction, normal);
            if (tangent.sqrMagnitude < .04f) tangent = Vector3.Cross(Vector3.up, normal);
            var desired = (tangent.normalized + normal * .18f).normalized;
            direction = Vector3.RotateTowards(direction, desired, 5 * Time.fixedDeltaTime, 0).normalized;
        }
        public override void Interact(CognitionWorldInteraction interaction)
        {
            if (Hover == CognitionHover.Collect) interaction.Collect(this);
            else if (Hover == CognitionHover.Question) interaction.Consume(this);
        }
        public bool TryCollect()
        {
            if (Collected || Busy || Eaten) return false;
            Collected = true;
            return true;
        }
        public bool TryBeginConsume()
        {
            if (Hover != CognitionHover.Question || !bittenPrefab || !key) return false;
            Busy = true;
            resumeGravity = !body.isKinematic;
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            body.useGravity = false;
            body.isKinematic = true;
            return true;
        }
        void SetCenter(Vector3 position) { transform.position += position - Center; }
        public IEnumerator ConsumeAnimation(Camera view, FirstPersonController player)
        {
            var start = Center;
            var hand = view.transform.TransformPoint(new Vector3(.38f, -.30f, .85f));
            var mouth = view.transform.TransformPoint(new Vector3(.12f, -.17f, .48f));
            var landing = FindLanding(player);
            float floorHeight = landing.y - .31f;
            var facing = Quaternion.FromToRotation(Vector3.left, Vector3.ProjectOnPlane(player.transform.position - landing, Vector3.up).normalized);
            var handFacing = Quaternion.FromToRotation(Vector3.left, Vector3.ProjectOnPlane(view.transform.position - mouth, Vector3.up).normalized);
            var releaseRotation = facing * Quaternion.Euler(-6, 0, -10);
            var restRotation = facing * Quaternion.Euler(8, 12, 16);
            wholeCollider.enabled = false;
            try
            {
                for (float t = 0; t < 1; t += Time.deltaTime / .85f)
                {
                    float s = Mathf.SmoothStep(0, 1, t);
                    SetCenter((1-s)*(1-s)*start + 2*(1-s)*s*hand + s*s*mouth);
                    yield return null;
                }
                SetCenter(mouth);
                Eaten = true;
                wholeRenderer.enabled = false;
                bitten = Instantiate(bittenPrefab, mouth, restRotation);
                var biteMesh = bitten.GetComponent<MeshFilter>();
                var biteVertices = biteMesh.sharedMesh.vertices;
                // Match the rotated mesh's lowest point to the floor instead of snapping upright on contact.
                landing.y = floorHeight + .005f + bitten.transform.position.y - LowestPointY(biteMesh.transform, biteVertices);
                bitten.transform.rotation = handFacing;
                key.Reveal(bitten.transform);
                yield return new WaitForSeconds(.22f);
                var show = view.transform.TransformPoint(new Vector3(.36f, -.28f, .85f));
                for (float t = 0; t < 1; t += Time.deltaTime / .45f)
                {
                    bitten.transform.position = Vector3.Lerp(mouth, show, Mathf.SmoothStep(0, 1, t));
                    bitten.transform.rotation = Quaternion.Slerp(handFacing, releaseRotation, Mathf.SmoothStep(0, 1, t));
                    yield return null;
                }
                // A gentle upward release followed by gravity gives a readable, accelerating little toss.
                const float tossGravity = 9.81f;
                float riseSpeed = Mathf.Sqrt(2 * tossGravity * .12f);
                float flightTime = (riseSpeed + Mathf.Sqrt(riseSpeed * riseSpeed + 2 * tossGravity * Mathf.Max(0, show.y - landing.y))) / tossGravity;
                for (float elapsed = 0; elapsed < flightTime; elapsed += Time.deltaTime)
                {
                    float t = elapsed / flightTime;
                    var position = Vector3.Lerp(show, landing, t);
                    position.y = show.y + riseSpeed * elapsed - .5f * tossGravity * elapsed * elapsed;
                    bitten.transform.position = position;
                    bitten.transform.rotation = Quaternion.Slerp(releaseRotation, restRotation, t);
                    yield return null;
                }
                // One small contact rebound, then a damped rock; the key is already attached throughout.
                for (float elapsed = 0; elapsed < .3f; elapsed += Time.deltaTime)
                {
                    float bounce = elapsed < .14f ? .025f * Mathf.Sin(Mathf.PI * elapsed / .14f) : 0;
                    float rock = 4 * Mathf.Sin(elapsed * Mathf.PI * 4 / .3f) * Mathf.Exp(-elapsed * 15);
                    bitten.transform.rotation = restRotation * Quaternion.Euler(0, 0, rock);
                    bitten.transform.position = landing + Vector3.up * bounce;
                    // Keep the rolling edge above the floor during the small settling motion.
                    float lowest = LowestPointY(biteMesh.transform, biteVertices);
                    if (lowest < floorHeight + .005f)
                        bitten.transform.position += Vector3.up * (floorHeight + .005f - lowest);
                    yield return null;
                }
            }
            finally
            {
                // Interrupted presentation still settles once bitten; no duplicate consumption or lost key.
                if (Eaten)
                {
                    if (bitten) { bitten.transform.position = landing; bitten.transform.rotation = restRotation; }
                    if (bitten && !key.Revealed) key.Reveal(bitten.transform);
                }
                else
                {
                    SetCenter(start); wholeCollider.enabled = true;
                    if (resumeGravity) { body.isKinematic = false; body.useGravity = true; body.constraints = RigidbodyConstraints.None; }
                }
                wasMoving = false;
                Busy = false;
            }
        }
        static float LowestPointY(Transform mesh, Vector3[] vertices)
        {
            float lowest = float.PositiveInfinity;
            var matrix = mesh.localToWorldMatrix;
            foreach (var vertex in vertices) lowest = Mathf.Min(lowest, matrix.MultiplyPoint3x4(vertex).y);
            return lowest;
        }
        Vector3 FindLanding(FirstPersonController player)
        {
            var origin = player.transform.position;
            // Place just beyond the player's right hand, on reachable floor rather than the pedestal.
            foreach (float side in new[] { 1.25f, -1.25f, 1.8f, -1.8f, 0f })
            {
                var p = origin + player.transform.right * side + player.transform.forward * .8f;
                if (Physics.Raycast(p + Vector3.up * 3, Vector3.down, out var hit, 5, ~0, QueryTriggerInteraction.Ignore)
                    && hit.normal.y > .9f && hit.point.y < origin.y + .3f
                    && !Physics.CheckSphere(hit.point + Vector3.up * .34f, .29f, ~0, QueryTriggerInteraction.Ignore))
                    return hit.point + Vector3.up * .31f;
            }
            return origin + Vector3.up * .32f;
        }
    }
}
