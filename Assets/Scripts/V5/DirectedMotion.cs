using UnityEngine;

namespace PuzzleApple.V5
{
    // Shared collision-safe translation for directed movement. No sentence knowledge here.
    public static class DirectedMotion
    {
        static readonly RaycastHit[] hits = new RaycastHit[64];
        static readonly Collider[] overlaps = new Collider[64];

        // A short physics step may leave an existing contact, but must not deepen it.
        public static bool SphereStepClear(SphereCollider sphere, Vector3 offset, bool ignorePlayer)
            => SphereStepClear(sphere,offset,ignorePlayer,out _);

        public static bool SphereStepClear(SphereCollider sphere, Vector3 offset, bool ignorePlayer, out Vector3 contactNormal)
        {
            contactNormal = Vector3.zero;
            var t = sphere.transform;
            Vector3 centre = t.TransformPoint(sphere.center);
            float radius = sphere.bounds.extents.x;
            int count = Physics.OverlapSphereNonAlloc(centre + offset, radius + .02f, overlaps, ~0, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var other = overlaps[i];
                if (other == sphere || other.transform.IsChildOf(t) || (ignorePlayer && other is CharacterController)) continue;
                var pose = other.transform;
                if (!Physics.ComputePenetration(sphere,t.position + offset,t.rotation,other,pose.position,pose.rotation,
                    out var normal,out float next)) continue;
                if (!Physics.ComputePenetration(sphere,t.position,t.rotation,other,pose.position,pose.rotation,
                    out _,out float current) || next > current + .000001f)
                {
                    contactNormal = normal;
                    return false;
                }
            }
            return true;
        }

        static readonly float[] approachAngles = {0, 35, -35, 65, -65, 85, -85};
        public static Vector3 ApproachStep(Vector3 delta, System.Predicate<Vector3> clear)
        {
            foreach (float angle in approachAngles)
            {
                Vector3 offset = Quaternion.AngleAxis(angle,Vector3.up) * delta;
                if (clear(offset)) return offset;
            }
            return Vector3.zero;
        }
        public static float Move(Rigidbody body, Collider[] colliders, Vector3 delta, bool ignorePlayer)
        {
            float distance = delta.magnitude;
            if (!body || distance < .00001f) return 0;
            var direction = delta / distance;
            float allowed = distance;
            foreach (var collider in colliders)
            {
                if (!collider || !collider.enabled || collider.isTrigger) continue;
                int count;
                if (collider is BoxCollider box)
                {
                    var s = box.transform.lossyScale;
                    var extent = Vector3.Scale(box.size * .5f, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)));
                    count = Physics.BoxCastNonAlloc(box.transform.TransformPoint(box.center), extent * .98f,
                        direction, hits, box.transform.rotation, distance + .02f, ~0, QueryTriggerInteraction.Ignore);
                }
                else
                {
                    var b = collider.bounds;
                    count = Physics.BoxCastNonAlloc(b.center, b.extents * .98f, direction, hits,
                        Quaternion.identity, distance + .02f, ~0, QueryTriggerInteraction.Ignore);
                }
                if (count == hits.Length) return 0;
                for (int i = 0; i < count; i++)
                {
                    var hit = hits[i];
                    if (hit.rigidbody == body || hit.collider.transform.IsChildOf(body.transform)) continue;
                    if (ignorePlayer && hit.collider is CharacterController) continue;
                    // Ignore contact with a supporting surface when moving tangentially or away.
                    if (Vector3.Dot(direction, hit.normal) >= -.001f) continue;
                    allowed = Mathf.Min(allowed, Mathf.Max(0, hit.distance - .015f));
                }
            }
            body.MovePosition(body.position + direction * allowed);
            return allowed;
        }
    }
}
