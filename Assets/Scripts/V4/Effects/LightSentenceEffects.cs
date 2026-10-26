using System.Collections.Generic;
using UnityEngine;
using PuzzleApple.V4.Cognition;

namespace PuzzleApple.V4
{
    public sealed class LightSentenceEffects : MonoBehaviour
    {
        public OpeningLightField field;
        public OpeningLight stateSource;
        public ThresholdDoor door;
        public Transform observer;
        public Collider ground;
        [Min(1)] public float influenceRadius = 50;
        [Min(.1f)] public float directedSpeed = 2.5f;
        [Min(.1f)] public float clusterSpeed = 1.2f;
        public Vector2 gatheringRadius = new Vector2(2.5f, 5);
        [Min(.1f)] public float spacing = 1.1f;
        [Range(.1f, .95f)] public float targetSwitchRatio = .8f;
        public CognitionSignal Motion { get; private set; }
        public bool CanOperate => !stateSource || !stateSource.IsOff;
        public IReadOnlyList<OpeningLight> Lamps => lamps;
        readonly List<OpeningLight> lamps = new List<OpeningLight>();
        readonly Dictionary<OpeningLight, OpeningLight> neighbours = new Dictionary<OpeningLight, OpeningLight>();
        readonly Dictionary<OpeningLight, Collider[]> colliders = new Dictionary<OpeningLight, Collider[]>();
        float nextRefresh;
        string lightState;

        public void Apply(CognitionState state)
        {
            string current = state.HasEffect(CognitionSignal.LightOff) ? "off" : state.HasEffect(CognitionSignal.LightOn) ? "on" : "default";
            if (current != lightState)
            {
                lightState = current;
                if (stateSource)
                {
                    if (current == "default") stateSource.HoldCurrentState();
                    else stateSource.SetOn(current == "on");
                }
                foreach (var lamp in lamps) if (lamp)
                {
                    if (current == "default") lamp.HoldCurrentState();
                    else lamp.SetOn(current == "on");
                }
            }
            var motion = state.HasEffect(CognitionSignal.LightMove) ? CognitionSignal.LightMove :
                state.HasEffect(CognitionSignal.LightWorldMove) ? CognitionSignal.LightWorldMove :
                state.HasEffect(CognitionSignal.LightDoorMove) ? CognitionSignal.LightDoorMove :
                state.HasEffect(CognitionSignal.LightLightMove) ? CognitionSignal.LightLightMove : CognitionSignal.Empty;
            if (motion != Motion) neighbours.Clear();
            Motion = motion;
            if (field) field.SetMoving(CanOperate && Motion == CognitionSignal.LightMove);
            RefreshLamps();
        }
        void Update()
        {
            if(field)field.SetMoving(CanOperate&&Motion==CognitionSignal.LightMove);
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .25f;
            RefreshLamps();
        }
        void RefreshLamps()
        {
            lamps.Clear();
            if (field) { foreach (var lamp in field.ActiveLamps) if (lamp) lamps.Add(lamp); }
            else { foreach (var lamp in FindObjectsByType<OpeningLight>(FindObjectsSortMode.None))
                if (lamp != stateSource) lamps.Add(lamp); }
            foreach (var lamp in lamps)
            {
                var motion = lamp.GetComponent<LightMotion>();
                if (motion) motion.SetMoving(CanOperate && Motion == CognitionSignal.LightMove);
                if (!colliders.ContainsKey(lamp)) colliders.Add(lamp, lamp.GetComponentsInChildren<Collider>());
            }
            var stale = new List<OpeningLight>();
            foreach (var key in colliders.Keys) if (!key) stale.Add(key);
            foreach (var key in stale) { colliders.Remove(key); neighbours.Remove(key); }
        }
        bool InRange(OpeningLight lamp) => lamp && lamp.isActiveAndEnabled &&
            (!observer || (lamp.transform.position - observer.position).sqrMagnitude <= influenceRadius * influenceRadius);
        public OpeningLight Nearest(Vector3 position, OpeningLight previous = null, OpeningLight exclude = null)
        {
            OpeningLight best = null; float distance = float.PositiveInfinity;
            foreach (var lamp in lamps)
            {
                if (lamp == exclude || !InRange(lamp)) continue;
                float d = (lamp.transform.position - position).sqrMagnitude;
                if (d < distance) { distance = d; best = lamp; }
            }
            if (previous && previous != exclude && InRange(previous) && best != previous &&
                distance >= (previous.transform.position-position).sqrMagnitude * targetSwitchRatio * targetSwitchRatio) return previous;
            return best;
        }
        public float LowestBottom(bool visibleOnly=false)
        {
            float height = float.PositiveInfinity;
            var camera=visibleOnly&&observer?observer.GetComponentInChildren<Camera>():null;
            foreach (var lamp in lamps) if (InRange(lamp))
            {
                var bounds=lamp.GetComponent<Collider>().bounds;
                if(visibleOnly && camera)
                {
                    var point=camera.WorldToViewportPoint(bounds.center);
                    if(point.z<=0||point.x<0||point.x>1||point.y<0||point.y>1)continue;
                }
                height = Mathf.Min(height, bounds.min.y);
            }
            return height;
        }
        Vector3 GatherOffset(OpeningLight lamp)
        {
            uint id = unchecked((uint)lamp.GetInstanceID() * 2654435761u);
            float a = (id & 65535) / 65535f * Mathf.PI * 2;
            float y = ((id >> 16) & 65535) / 65535f * 2 - 1;
            float ring = Mathf.Sqrt(Mathf.Max(0, 1-y*y));
            float r = Mathf.Lerp(gatheringRadius.x, gatheringRadius.y, (id % 997) / 997f);
            return new Vector3(Mathf.Cos(a)*ring, y, Mathf.Sin(a)*ring)*r;
        }
        void FixedUpdate()
        {
            if (!CanOperate || Motion == CognitionSignal.Empty || Motion == CognitionSignal.LightMove) return;
            foreach (var lamp in lamps)
            {
                if (!InRange(lamp)) continue;
                var body = lamp.GetComponent<Rigidbody>(); var collider = lamp.GetComponent<Collider>();
                if (!body || !collider) continue;
                Vector3 target = body.position;
                float stopDistance = .04f;
                if (Motion == CognitionSignal.LightWorldMove)
                {
                    if (!ground) continue;
                    target.y = ground.bounds.max.y + (body.position.y - collider.bounds.min.y) + .025f;
                    if (target.y >= body.position.y) continue;
                }
                else if (Motion == CognitionSignal.LightDoorMove)
                {
                    if (!door || !door.Revealed) continue;
                    target = door.transform.TransformPoint(new Vector3(0,3,0)) + GatherOffset(lamp);
                    if (ground) target.y = Mathf.Max(target.y, ground.bounds.max.y + collider.bounds.extents.y + .1f);
                    stopDistance = .16f;
                }
                else
                {
                    neighbours.TryGetValue(lamp, out var old);
                    var nearest = Nearest(body.position, old, lamp);
                    neighbours[lamp] = nearest;
                    if (!nearest) continue;
                    target = nearest.transform.position;
                    stopDistance = spacing + collider.bounds.extents.magnitude + nearest.GetComponent<Collider>().bounds.extents.magnitude;
                }
                var delta = target - body.position;
                float distance = delta.magnitude;
                if (distance <= stopDistance) continue;
                var direction = delta / distance;
                if (Motion != CognitionSignal.LightWorldMove)
                {
                    Vector3 separation = Vector3.zero;
                    foreach (var other in lamps)
                    {
                        if (!other || other == lamp) continue;
                        var away = body.position - other.transform.position;
                        float d = away.magnitude;
                        if (d > .001f && d < spacing) separation += away/d * (1-d/spacing);
                    }
                    direction = (direction + separation * 2).normalized;
                }
                float speed=Motion==CognitionSignal.LightLightMove?clusterSpeed:directedSpeed;
                DirectedMotion.Move(body, colliders[lamp], direction * Mathf.Min(speed * Time.fixedDeltaTime, distance-stopDistance), true);
            }
        }
    }
}
