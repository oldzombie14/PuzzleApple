using UnityEngine;

namespace PuzzleApple.V4
{
    // Shared collision-safe translation for directed movement. No sentence knowledge here.
    public static class DirectedMotion
    {
        static readonly RaycastHit[] hits = new RaycastHit[64];
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
