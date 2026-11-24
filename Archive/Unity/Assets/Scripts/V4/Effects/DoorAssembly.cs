using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PuzzleApple.V4
{
    // Grow scattered stitches on the existing surfaces, keeping the doorway open.
    public sealed class DoorAssembly : MonoBehaviour
    {
        [Min(.1f)] public float duration = 5.5f;
        [Min(.04f)] public float stitchSize = .22f;
        [Range(8, 96)] public int threadCount = 48;
        public Vector2 threadReach = new Vector2(1.1f, 2.8f);
        [Min(.002f)] public float threadWidth = .04f;
        public bool Playing { get; private set; }
        public float Progress { get; private set; }
        readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();
        readonly List<Material> woven = new List<Material>();
        sealed class FeedThread
        {
            public LineRenderer line;
            public Vector3 origin, anchor, end;
            public float start, span;
        }
        readonly List<FeedThread> threads = new List<FeedThread>();
        Material threadMaterial;
        GameObject threadRoot;
        const int ThreadSegments = 28;
        // Final strands finish by .85: the complete frame must already exist at that point.
        const float SolidStart = .6f, SolidComplete = .85f;
        public void Begin(Material particleMaterial) { if (!Playing) StartCoroutine(Assemble()); }
        IEnumerator Assemble()
        {
            var shader = Resources.Load<Shader>("DoorWeave");
            if (!shader || !shader.isSupported)
            {
                Debug.LogError("Door weave shader is missing or unsupported.", this);
                Progress = 1; yield break;
            }
            Playing = true; Progress = 0;
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.GetComponentInParent<ThresholdMist>() || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                var materials = renderer.sharedMaterials;
                originals[renderer] = materials;
                var temporary = new Material[materials.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    if (source && source.HasProperty("_SolidProgress"))
                    {
                        // Reveal the frame's own finish, including its two-tone moulding.
                        temporary[i] = new Material(source);
                        woven.Add(temporary[i]);
                        continue;
                    }
                    var material = new Material(shader);
                    material.SetColor("_BaseColor", source && source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : Color.gray);
                    material.SetColor("_EmissionColor", source && source.IsKeywordEnabled("_EMISSION") && source.HasProperty("_EmissionColor") ? source.GetColor("_EmissionColor") : Color.black);
                    material.SetFloat("_StitchSize", stitchSize);
                    temporary[i] = material; woven.Add(material);
                }
                renderer.sharedMaterials = temporary;
            }
            CreateThreads();
            float started = Time.unscaledTime;
            while (Time.unscaledTime - started < Mathf.Max(.1f, duration))
            {
                SetProgress((Time.unscaledTime - started) / Mathf.Max(.1f, duration)); yield return null;
            }
            SetProgress(1); Cleanup();
        }
        void SetProgress(float progress)
        {
            Progress = Mathf.Clamp01(progress);
            foreach (var material in woven)
            {
                material.SetFloat("_SolidProgress", Mathf.SmoothStep(0, 1,
                    Mathf.InverseLerp(SolidStart, SolidComplete, Progress)));
            }
            // Show the actual finished frame before retiring the deposited strands.
            if (Progress >= SolidComplete)
                foreach (var pair in originals) if (pair.Key) pair.Key.sharedMaterials = pair.Value;
            UpdateThreads();
        }

        void CreateThreads()
        {
            var shader = Resources.Load<Shader>("DoorWeaveThread");
            if (!shader || !shader.isSupported || originals.Count == 0) return;
            threadMaterial = new Material(shader);
            threadMaterial.SetColor("_Color", new Color(2, 2.8f, 3.6f, 1));
            threadRoot = new GameObject("Weaving feed threads");
            threadRoot.transform.SetParent(transform, false);
            var surfaces = new List<Bounds>();
            var area = new List<float>();
            float total = 0;
            foreach (var pair in originals)
            {
                var filter = pair.Key.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh) continue;
                var mesh = filter.sharedMesh.bounds;
                var bounds = new Bounds(transform.InverseTransformPoint(filter.transform.TransformPoint(mesh.center)), Vector3.zero);
                for (int c = 0; c < 8; c++) bounds.Encapsulate(transform.InverseTransformPoint(filter.transform.TransformPoint(
                    mesh.center + Vector3.Scale(mesh.extents, new Vector3((c & 1) == 0 ? -1 : 1,
                        (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)))));
                var frame = filter.GetComponent<WeaveFrameOpening>();
                if (!frame)
                {
                    surfaces.Add(bounds);
                    continue;
                }
                var opening = Vector2.Scale(bounds.extents, frame.openingFraction);
                var innerMin = bounds.center - (Vector3)opening;
                var innerMax = bounds.center + (Vector3)opening;
                var min = bounds.min;
                var max = bounds.max;
                surfaces.Add(Rail(min, new Vector3(innerMin.x, max.y, max.z)));
                surfaces.Add(Rail(new Vector3(innerMax.x, min.y, min.z), max));
                surfaces.Add(Rail(new Vector3(innerMin.x, min.y, min.z), new Vector3(innerMax.x, innerMin.y, max.z)));
                surfaces.Add(Rail(new Vector3(innerMin.x, innerMax.y, min.z), new Vector3(innerMax.x, max.y, max.z)));
            }
            foreach (var surface in surfaces)
            {
                total += surface.size.x * surface.size.y;
                area.Add(total);
            }
            if (surfaces.Count == 0) return;
            var random = new System.Random(7349);
            var laneCounts = new int[surfaces.Count];
            Vector3 pairAnchor = Vector3.zero;
            for (int i = 0; i < threadCount; i++)
            {
                // Paired strands land along the body of a frame edge and lay in opposite
                // directions. Together they still cover the full edge, including its corners.
                float pick = (i / 2 + .5f) / Mathf.CeilToInt(threadCount / 2f) * total;
                int index = 0; while (index < area.Count - 1 && pick > area[index]) index++;
                var bounds = surfaces[index];
                bool vertical = bounds.size.y > bounds.size.x;
                if (i % 2 == 0)
                {
                    int laneIndex = laneCounts[index]++;
                    float lane = laneIndex == 0 ? .02f : laneIndex == 1 ? .98f
                        : .08f + .84f * (float)random.NextDouble();
                    float along = .15f + .7f * (float)random.NextDouble();
                    pairAnchor = vertical
                        ? new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,lane), Mathf.Lerp(bounds.min.y,bounds.max.y,along), bounds.min.z-.018f)
                        : new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,along), Mathf.Lerp(bounds.min.y,bounds.max.y,lane), bounds.min.z-.018f);
                }
                var anchor = pairAnchor;
                var end = anchor;
                if (vertical) end.y = i % 2 == 0 ? bounds.min.y : bounds.max.y;
                else end.x = i % 2 == 0 ? bounds.min.x : bounds.max.x;
                var outward = new Vector3(anchor.x, (anchor.y - 3.05f) * .7f, 0).normalized;
                var tangent = new Vector3(-outward.y, outward.x, 0);
                float reach = Mathf.Lerp(threadReach.x, threadReach.y, (float)random.NextDouble());
                var origin = anchor + outward * reach + tangent * ((float)random.NextDouble() - .5f) * 1.5f;
                origin.z -= Mathf.Lerp(.35f, 1.5f, (float)random.NextDouble());
                var cell = new Vector2(Mathf.Floor(anchor.x / Mathf.Max(.04f, stitchSize)), Mathf.Floor(anchor.y / Mathf.Max(.04f, stitchSize)));
                float hash = Mathf.Repeat(Mathf.Sin(Vector2.Dot(cell, new Vector2(127.1f, 311.7f))) * 43758.5453f, 1);
                var line = new GameObject("Feeding thread").AddComponent<LineRenderer>();
                line.gameObject.layer = gameObject.layer;
                line.transform.SetParent(threadRoot.transform, false);
                line.useWorldSpace = false; line.positionCount = ThreadSegments;
                line.sharedMaterial = threadMaterial;
                line.widthMultiplier = threadWidth * Mathf.Lerp(.7f, 1.3f, (float)random.NextDouble());
                line.widthCurve = new AnimationCurve(new Keyframe(0, .6f), new Keyframe(1, .6f));
                line.numCapVertices = 3;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
                line.enabled = false;
                threads.Add(new FeedThread { line = line, anchor = anchor, origin = origin, end = end,
                    start = .02f + .51f * hash, span = .32f });
            }
        }

        static Bounds Rail(Vector3 min, Vector3 max)
        {
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        void UpdateThreads()
        {
            foreach (var thread in threads)
            {
                float t = (Progress - thread.start) / thread.span;
                thread.line.enabled = t > 0 && Progress < 1;
                if (!thread.line.enabled) continue;
                // One path: incoming section [0,1], deposited section [1,2].
                // The tail stops at the anchor, leaving this same strand on the frame.
                float head = Mathf.Clamp01(t / .45f) + Mathf.Clamp01((t - .45f) / .55f);
                float tail = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - .35f) / .65f));
                float alpha = Mathf.SmoothStep(0, 1, t / .12f)
                    * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(SolidComplete, 1, Progress)));
                thread.line.startColor = thread.line.endColor = new Color(.7f, .82f, 1, alpha * .8f);
                int incoming = head > 1 ? Mathf.RoundToInt((1-tail)/(head-tail)*(ThreadSegments-2)) : ThreadSegments-1;
                incoming = Mathf.Clamp(incoming, 0, ThreadSegments-1);
                for (int j = 0; j < ThreadSegments; j++)
                {
                    float u = head <= 1 ? Mathf.Lerp(tail,head,j/(ThreadSegments-1f))
                        : j <= incoming ? Mathf.Lerp(tail,1,incoming == 0 ? 1 : j/(float)incoming)
                        : Mathf.Lerp(1,head,(j-incoming)/(float)(ThreadSegments-1-incoming));
                    var point = u <= 1
                        ? Vector3.Lerp(thread.origin, thread.anchor, u)
                        : Vector3.Lerp(thread.anchor, thread.end, u-1);
                    thread.line.SetPosition(j, point);
                }
            }
        }
        void Cleanup()
        {
            foreach (var pair in originals) if (pair.Key) pair.Key.sharedMaterials = pair.Value;
            originals.Clear();
            foreach (var material in woven) if (material) Release(material);
            woven.Clear(); Playing = false;
            threads.Clear();
            if (threadRoot) { threadRoot.SetActive(false); Release(threadRoot); threadRoot = null; }
            if (threadMaterial) { Release(threadMaterial); threadMaterial = null; }
        }
        static void Release(Object value) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        void OnDisable() { StopAllCoroutines(); Cleanup(); }
    }
}
