using UnityEngine;

namespace PuzzleApple.V4
{
    // Separate presentation: the original fish prefab and behaviour remain reusable.
    public sealed class ThresholdStarlight : MonoBehaviour
    {
        public Material material;
        public Vector3[] doorPoints, floorPoints, movePoints;
        [Min(.1f)] public float gatherDuration = 8f, holdDuration = 4f, releaseDuration = 5.5f, symbolGap = 5f, cycleGap = 9f;
        [Tooltip("聚散阶段内错峰出发所占比例；聚合沿固定起终点匀速移动，散开时平滑回到各自正常 Idle 轨迹。")]
        [Range(0, .35f)] public float staggerFraction = .2f;
        [Min(.01f)] public float starSize = .25f, trailDuration = 1.8f;
        [Min(.01f)] public float revealDuration = .8f;
        public Vector2 symbolSize = new Vector2(3.3f, 4.65f);
        [Range(0, 1)] public float trailOpacity = .3f;
        [Range(.1f, 1)] public float trailWidth = .36f;
        public int starCount = 128;
        [Min(.1f)] public float landingSpreadDuration = 4f;
        [Min(1)] public float landedBrightness = 2f;
        public Vector3 suspendedRadii = new Vector3(2.5f, 2.45f, 1.2f);
        public Vector3 landedRadii = new Vector3(6f, 2.7f, 4f);
        public float Clock { get; private set; }
        public int CurrentSymbol { get; private set; } = -1;
        public float Formation { get; private set; }
        ParticleSystem system;
        ParticleSystemRenderer starRenderer;
        MaterialPropertyBlock starProperties;
        Color baseGlow;
        ParticleSystem.Particle[] particles;
        DoorStateEffects state;
        ThresholdDoor door;
        const int TrailSteps = 24;
        float landingStart = float.PositiveInfinity;
        float reveal;
        readonly Bounds[] symbolBounds = new Bounds[3];
        Bounds[] frameBounds;

        void Awake()
        {
            state = GetComponentInParent<DoorStateEffects>();
            door = GetComponentInParent<ThresholdDoor>();
            var frames = new System.Collections.Generic.List<Bounds>();
            if (door) foreach (var box in door.GetComponentsInChildren<BoxCollider>(true))
            {
                if (box.isTrigger || !box.enabled) continue;
                var bounds = new Bounds(transform.InverseTransformPoint(box.transform.TransformPoint(box.center)), Vector3.zero);
                for (int corner = 0; corner < 8; corner++)
                    bounds.Encapsulate(transform.InverseTransformPoint(box.transform.TransformPoint(box.center +
                        Vector3.Scale(box.size * .5f, new Vector3((corner & 1) == 0 ? -1 : 1,
                            (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)))));
                frames.Add(bounds);
            }
            frameBounds = frames.ToArray();
            system = gameObject.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false; main.startSpeed = 0; main.maxParticles = starCount * (TrailSteps + 1);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.enabled = false;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            starRenderer = renderer;
            starProperties = new MaterialPropertyBlock();
            baseGlow = material.GetColor("_Color");
            renderer.minParticleSize = 0; renderer.maxParticleSize = .15f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            particles = new ParticleSystem.Particle[main.maxParticles];
            CacheSymbolBounds();
            system.Play();
        }

        static float Seed(int i, int channel)
        {
            uint hash = unchecked((uint)(i * 374761393 + channel * 668265263));
            hash = (hash ^ (hash >> 13)) * 1274126177u;
            return (hash ^ (hash >> 16)) / (float)uint.MaxValue;
        }

        void CacheSymbolBounds()
        {
            for (int s = 0; s < 3; s++)
            {
                var points = s == 0 ? doorPoints : s == 1 ? floorPoints : movePoints;
                if (points == null || points.Length == 0) continue;
                var bounds = new Bounds(points[0], Vector3.zero);
                foreach (var point in points) bounds.Encapsulate(point);
                symbolBounds[s] = bounds;
            }
        }

        Vector3 Drift(int i, float time)
        {
            // Independent paths and phases: no shared projected curve or fixed school silhouette.
            float angle = time * Mathf.Lerp(.14f, .34f, Seed(i, 1)) + Seed(i, 2) * Mathf.PI * 2;
            float spread = LandingBlend(time);
            var r = Vector3.Lerp(suspendedRadii, landedRadii, spread);
            // Most stars stay near the opening; a small tail of the distribution wanders well outside it.
            float radius = Mathf.Lerp(.18f, 1.45f, Mathf.Pow(Seed(i, 3), 1.7f));
            var point = new Vector3(Mathf.Sin(angle) * r.x * radius,
                3.05f + ((Seed(i, 4) * 2 - 1) * .78f + Mathf.Sin(time * .31f + Seed(i, 5) * 6.28f) * .19f) * r.y * radius,
                Mathf.Cos(angle) * r.z * radius);
            float side = Seed(i, 6) < .5f ? -1 : 1;
            foreach (var frame in frameBounds)
            {
                var delta = point - frame.center;
                float x = Mathf.Max(0, Mathf.Abs(delta.x) - frame.extents.x - .18f);
                float y = Mathf.Max(0, Mathf.Abs(delta.y) - frame.extents.y - .18f);
                float weight = 1 - Mathf.SmoothStep(0, .55f, Mathf.Sqrt(x*x + y*y));
                float safeDepth = Mathf.Max(side * point.z, side * frame.center.z + frame.extents.z + .25f);
                point.z = Mathf.Lerp(point.z, side * safeDepth, weight);
            }
            return point;
        }

        void LateUpdate()
        {
            if (!system) return;
            if (door && door.Appearing) reveal = 0;
            else reveal = Mathf.MoveTowards(reveal, 1, Time.deltaTime / Mathf.Max(.01f, revealDuration));
            bool running = (!state || state.IsOn) && (!door || !door.Appearing);
            if (door && door.LandingCompleted && float.IsPositiveInfinity(landingStart)) landingStart = Clock;
            if (running)
            {
                Clock += Time.deltaTime;
            }
            RenderAt(Clock);
        }

        // Also used by editor visual checks; does not change gameplay progression.
        public void PreviewAt(float time)
        {
            Clock = time; reveal = 1;
            CacheSymbolBounds();
            RenderAt(time);
        }

        static Vector3 FloatOffset(int i, float time) => new Vector3(
            Mathf.Sin(time * .63f + i * 2.4f), Mathf.Sin(time * .79f + i * 1.7f),
            Mathf.Sin(time * .51f + i)) * .008f;

        Vector3 Glyph(int i, int symbol)
        {
            var points = symbol == 0 ? doorPoints : symbol == 1 ? floorPoints : movePoints;
            if (points == null || points.Length == 0) return new Vector3(0, 3.05f, 0);
            var bounds = symbolBounds[symbol];
            float scale = Mathf.Min(symbolSize.x / Mathf.Max(.01f, bounds.size.x),
                symbolSize.y / Mathf.Max(.01f, bounds.size.y));
            var glyph = points[i % points.Length] - bounds.center;
            glyph.x *= scale; glyph.y *= scale;
            return glyph + new Vector3(0, 3.05f, bounds.center.z);
        }

        float LeaveTime(int i, float start) => start + Mathf.Max(.1f, gatherDuration) + holdDuration
            + Mathf.Max(.1f, releaseDuration) * Mathf.Clamp(staggerFraction, 0, .35f) * Seed(i, 13);

        Vector3 ReleasedPosition(int i, float time, float start, int symbol)
        {
            float leave = LeaveTime(i, start);
            // Rejoin each star's existing idle path and phase, rather than flying outward.
            float duration = Mathf.Max(.1f, releaseDuration) * (1 - Mathf.Clamp(staggerFraction, 0, .35f));
            float blend = Mathf.SmoothStep(0, 1, Mathf.Clamp01((time - leave) / duration));
            var origin = Glyph(i, symbol) + FloatOffset(i, time);
            return Vector3.Lerp(origin, Drift(i, time), blend);
        }

        Vector3 BeforeGather(int i, float time, float start, int symbol, float cycle, float slot)
        {
            float previous = symbol > 0 ? start - slot : start - cycle + slot * 2;
            return previous < cycleGap - .01f ? Drift(i, time)
                : ReleasedPosition(i, time, previous, symbol > 0 ? symbol - 1 : 2);
        }

        Vector3 PositionAt(int i, float time, out float formation, out int symbol)
        {
            if (time < landingStart) return SymbolPositionAt(i, time, out formation, out symbol);
            formation = 0; symbol = -1;
            // Capture the interrupted formation analytically; historical trail samples remain valid.
            var origin = SymbolPositionAt(i, landingStart, out _, out _);
            return Vector3.Lerp(origin, Drift(i, time), LandingBlend(time));
        }

        float LandingBlend(float time) => Mathf.SmoothStep(0, 1,
            Mathf.Clamp01((time - landingStart) / Mathf.Max(.1f, landingSpreadDuration)));

        Vector3 SymbolPositionAt(int i, float time, out float formation, out int symbol)
        {
            float gather = Mathf.Max(.1f, gatherDuration), release = Mathf.Max(.1f, releaseDuration);
            float active = gather + holdDuration + release;
            float slot = active + symbolGap;
            float cycle = active * 3 + symbolGap * 2 + cycleGap;
            formation = 0; symbol = -1;
            if (time < cycleGap) return Drift(i, time);
            float t = Mathf.Repeat(time - cycleGap, cycle);
            int s = Mathf.Min(2, Mathf.FloorToInt(t / slot));
            float local = t - s * slot;
            float start = time - local;
            if (local >= active) return ReleasedPosition(i, time, start, s);
            symbol = s;
            float departure = start + gather * Mathf.Clamp(staggerFraction, 0, .35f) * Seed(i, 11);
            float arrival = departure + gather * (1 - Mathf.Clamp(staggerFraction, 0, .35f))
                * Mathf.Lerp(.88f, 1, Seed(i, 12));
            float leave = LeaveTime(i, start);
            var glyph = Glyph(i, s);
            if (time < departure) return BeforeGather(i, time, start, s, cycle, slot);
            if (time < arrival)
            {
                formation = Mathf.Clamp01((time - departure) / (arrival - departure));
                return Vector3.Lerp(BeforeGather(i, departure, start, s, cycle, slot),
                    glyph + FloatOffset(i, arrival), formation);
            }
            if (time < leave)
            {
                formation = 1;
                return glyph + FloatOffset(i, time);
            }
            formation = 1 - Mathf.Clamp01((time - leave) / (release * (1 - Mathf.Clamp(staggerFraction, 0, .35f))));
            return ReleasedPosition(i, time, start, s);
        }

        void RenderAt(float time)
        {
            // HDR gain belongs on the renderer, not byte-clamped particle alpha or the shared material.
            var glowColor = baseGlow * Mathf.Lerp(1, landedBrightness, LandingBlend(time));
            glowColor.a = baseGlow.a;
            starProperties.SetColor("_Color", glowColor);
            starRenderer.SetPropertyBlock(starProperties);
            float visibility = (state ? state.Brightness : 1) * Mathf.SmoothStep(0, 1, reveal);
            if (door && door.Appearing) visibility = 0;
            int count = 0;
            Formation = 0; CurrentSymbol = -1;
            for (int i = 0; i < starCount; i++)
            {
                var position = PositionAt(i, time, out float formation, out int symbol);
                Formation += formation / starCount;
                if (symbol >= 0) CurrentSymbol = symbol;
                float variation = Seed(i, 15);
                // More even cores preserve the narrow gaps and corners of the glyphs.
                float size = starSize * Mathf.Lerp(.68f, 1.25f, variation);
                float glow = (.86f + .1f * Mathf.Sin(time * (.7f + variation * .6f) + i)) * visibility;
                SetParticle(count++, position, size, glow);
                for (int j = 0; j < TrailSteps; j++)
                {
                    float age = (j + 1f) / TrailSteps;
                    float fade = 1 - age;
                    // Sample actual past positions at fixed time intervals, independent of frame rate.
                    var tail = PositionAt(i, Mathf.Max(0, time - age * trailDuration), out _, out _);
                    float separation = Vector3.Distance(position, tail);
                    float moving = Mathf.Clamp01(separation / Mathf.Max(.01f, size * .35f));
                    // Dim blue-grey dust, not another bright stellar core. Slightly irregular density
                    // breaks the solid tapered silhouette while retaining a readable motion trace.
                    float grain = Mathf.Lerp(.45f, 1, Seed(i, 60 + j));
                    SetParticle(count++, tail, size * trailWidth * Mathf.Lerp(.65f, 1, fade),
                        glow * fade * fade * trailOpacity * grain * moving * (1 - formation * .92f), true);
                }
            }
            system.SetParticles(particles, count);
        }

        void SetParticle(int index, Vector3 position, float size, float alpha, bool tail = false)
        {
            particles[index].position = position;
            particles[index].startSize = size;
            particles[index].startColor = tail ? new Color(.16f, .32f, .52f, alpha) : new Color(.8f, .9f, 1, alpha);
            particles[index].startLifetime = particles[index].remainingLifetime = 1000;
        }
    }
}
