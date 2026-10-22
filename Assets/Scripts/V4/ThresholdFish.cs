using UnityEngine;

namespace PuzzleApple.V4
{
    public sealed class ThresholdFish : MonoBehaviour
    {
        public Transform tail;
        public Vector3 center;
        public Vector3 radii = new Vector3(1.3f, .6f, .65f);
        public float phase, orbitSpeed = .25f, tailSpeed = 5;
        public float expansionDuration = 2.5f;
        ThresholdDoor door;
        float expansion;
        public bool Paused { get; set; }
        float swimTime;
        Bounds[] frameBounds;
        float clearance, bypassSide;

        void Awake()
        {
            swimTime = Time.time;
            door = GetComponentInParent<ThresholdDoor>();
            if (!door) return;
            // A conservative body envelope includes fins and the tail throughout its swing.
            float bodyRadius = 0;
            foreach (var mesh in GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mesh.sharedMesh) continue;
                var bounds = mesh.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var local = transform.InverseTransformPoint(mesh.transform.TransformPoint(corner));
                    float distance = local.magnitude;
                    if (tail && mesh.transform.IsChildOf(tail))
                    {
                        var pivot = transform.InverseTransformPoint(tail.position);
                        distance = pivot.magnitude + (local - pivot).magnitude;
                    }
                    bodyRadius = Mathf.Max(bodyRadius, distance);
                }
            }
            var scale = transform.localScale;
            clearance = bodyRadius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)) + .08f;
            bypassSide = Mathf.Sin(phase * 1.73f) >= 0 ? 1 : -1;
            var frames = new System.Collections.Generic.List<Bounds>();
            foreach (var box in door.GetComponentsInChildren<BoxCollider>(true))
            {
                if (box.isTrigger || !box.enabled) continue;
                var bounds = new Bounds(transform.parent.InverseTransformPoint(box.transform.TransformPoint(box.center)), Vector3.zero);
                for (int i = 0; i < 8; i++)
                {
                    var corner = box.center + Vector3.Scale(box.size * .5f,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    bounds.Encapsulate(transform.parent.InverseTransformPoint(box.transform.TransformPoint(corner)));
                }
                frames.Add(bounds);
            }
            frameBounds = frames.ToArray();
        }

        Vector3 AvoidFrame(Vector3 point)
        {
            if (frameBounds == null) return point;
            float influence = 0, depth = bypassSide * point.z;
            foreach (var frame in frameBounds)
            {
                var delta = point - frame.center;
                float x = Mathf.Max(0, Mathf.Abs(delta.x) - frame.extents.x - clearance);
                float y = Mathf.Max(0, Mathf.Abs(delta.y) - frame.extents.y - clearance);
                // Start curving before reaching the solid frame, then pass in front or behind it.
                float weight = 1 - Mathf.SmoothStep(0, 1, Mathf.Sqrt(x*x + y*y) / .7f);
                if (weight <= 0) continue;
                influence = Mathf.Max(influence, weight);
                depth = Mathf.Max(depth, bypassSide * frame.center.z + frame.extents.z + clearance + .04f);
            }
            point.z = Mathf.Lerp(point.z, bypassSide * depth, influence);
            return point;
        }
        Vector3 Point(float t)
        {
            var r = Vector3.Lerp(radii, new Vector3(radii.x + 1.8f, radii.y + .45f, radii.z + 1.8f), expansion);
            return AvoidFrame(center + new Vector3(Mathf.Sin(t)*r.x,
                Mathf.Sin(t*2+phase)*r.y, Mathf.Cos(t+expansion*Mathf.Sin(t*.7f+phase)) * r.z));
        }
        void Update()
        {
            if (Paused) return;
            swimTime += Time.deltaTime;
            if (door && door.LandingCompleted) expansion = Mathf.MoveTowards(expansion, 1, Time.deltaTime / Mathf.Max(.01f, expansionDuration));
            float t = swimTime * orbitSpeed + phase;
            transform.localPosition = Point(t);
            var forward = (Point(t+.015f)-Point(t)).normalized;
            transform.localRotation = Quaternion.LookRotation(forward,Vector3.up)
                * Quaternion.Euler(0,0,Mathf.Sin(t+phase)*8);
            if (tail) tail.localRotation = Quaternion.Euler(0,Mathf.Sin(swimTime*tailSpeed+phase)*23,0);
        }
    }
}
