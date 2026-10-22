using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PuzzleApple.V4
{
    /// <summary>Session-local lamp records; only nearby records own pooled Unity objects.</summary>
    public sealed class OpeningLightField : MonoBehaviour
    {
        // Align a body diagonal vertically so a single vertex points down.
        static readonly Quaternion LampRotation = Quaternion.FromToRotation(new Vector3(1, -1, 1), Vector3.down);
        static readonly float VerticalHalfExtent = Mathf.Sqrt(3) * .5f;
        [Header("引用")]
        public Transform observer;
        public OpeningLight stateSource;
        public Collider ground;
        [Header("初始分布（重新进入 Play 生效）")]
        public int seed = 427;
        [Min(2)] public float cellSize = 12;
        [Range(0, 12)] public int lampsPerCell = 2;
        public Vector2 scaleRange = new Vector2(.12f, .35f);
        [Tooltip("方块底部与地面的距离；只约束初始位置。")]
        public Vector2 clearanceRange = new Vector2(.15f, .8f);
        [Header("动态范围与预算")]
        [Min(5)] public float spawnRadius = 55;
        [Min(1)] public float recycleBuffer = 12;
        [Range(8, 512)] public int maxActive = 192;
        [Range(1, 64)] public int spawnBudget = 16;
        [Min(.05f)] public float refreshInterval = .2f;
        [Header("移动与局部照明")]
        [Min(0)] public float moveSpeed = 1.2f;
        [Range(0, 16)] public int maxPointLights = 6;
        [Min(0)] public float pointLightDistance = 14;
        public int ActiveCount => active.Count;
        public int RecordedCount { get; private set; }
        public int PooledCount => pool.Count;
        public bool Moving { get; private set; }
        public IEnumerable<OpeningLight> ActiveLamps
        {
            get { foreach (var record in active) if (record.lamp) yield return record.lamp; }
        }

        sealed class Record
        {
            public Vector3 position, direction;
            public float scale;
            public OpeningLight lamp;
            public LightMotion motion;
        }
        readonly HashSet<Vector2Int> generated = new HashSet<Vector2Int>();
        readonly Dictionary<Vector2Int, List<Record>> sleeping = new Dictionary<Vector2Int, List<Record>>();
        readonly List<Record> active = new List<Record>();
        readonly List<Record> candidates = new List<Record>();
        readonly Stack<OpeningLight> pool = new Stack<OpeningLight>();
        Collider playerCollider;
        float nextRefresh, runtimeCellSize;
        Transform poolRoot;

        void Start()
        {
            if (!observer || !stateSource || !ground) { enabled = false; return; }
            runtimeCellSize = Mathf.Max(2, cellSize);
            playerCollider = observer.GetComponentInParent<CharacterController>();
            poolRoot = new GameObject("Inactive lamps").transform;
            poolRoot.SetParent(transform, false);
            poolRoot.gameObject.SetActive(false);
            // Keep an approachable lamp at the previous tutorial landmark.
            var first = stateSource.transform.position;
            if (TryGround(first, out float height))
            {
                float size = Mathf.Clamp(.3f, scaleRange.x, scaleRange.y);
                Sleep(new Record { position = new Vector3(first.x, height + size * VerticalHalfExtent + clearanceRange.y, first.z), scale = size, direction = Vector3.up });
                RecordedCount++;
            }
            RefreshField();
        }
        void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + Mathf.Max(.05f, refreshInterval);
            RefreshField();
        }
        Vector2Int Cell(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x / runtimeCellSize), Mathf.FloorToInt(p.z / runtimeCellSize));
        bool TryGround(Vector3 p, out float height)
        {
            var bounds = ground.bounds;
            var ray = new Ray(new Vector3(p.x, bounds.max.y + 10, p.z), Vector3.down);
            if (ground.Raycast(ray, out var hit, bounds.size.y + 20)) { height = hit.point.y; return true; }
            height = 0; return false;
        }
        void Generate(Vector2Int cell)
        {
            if (!generated.Add(cell)) return;
            var random = new System.Random(unchecked(seed ^ cell.x * 73856093 ^ cell.y * 19349663));
            for (int i = 0; i < lampsPerCell; i++)
            {
                var p = new Vector3((cell.x + (float)random.NextDouble()) * runtimeCellSize, 0,
                    (cell.y + (float)random.NextDouble()) * runtimeCellSize);
                float size = Mathf.Lerp(scaleRange.x, scaleRange.y, (float)random.NextDouble());
                float clearance = Mathf.Lerp(clearanceRange.x, clearanceRange.y, (float)random.NextDouble());
                var direction = new Vector3((float)random.NextDouble() * 2 - 1, (float)random.NextDouble() * 2 - 1, (float)random.NextDouble() * 2 - 1).normalized;
                if (!TryGround(p, out float height)) continue;
                p.y = height + clearance + size * VerticalHalfExtent;
                Sleep(new Record { position = p, scale = size, direction = direction });
                RecordedCount++;
            }
        }
        void Sleep(Record record)
        {
            var cell = Cell(record.position);
            if (!sleeping.TryGetValue(cell, out var records)) sleeping.Add(cell, records = new List<Record>());
            records.Add(record);
        }
        public void SetMoving(bool value)
        {
            if (Moving == value) return;
            Moving = value;
            foreach (var record in active)
            {
                record.direction = record.motion.Direction;
                record.motion.SetMoving(value);
                record.motion.RestoreDirection(record.direction);
            }
        }
        public void RefreshField()
        {
            if (!poolRoot || !observer) return;
            Vector3 center = observer.position;
            float radius = Mathf.Max(5, spawnRadius), retire = radius + Mathf.Max(1, recycleBuffer);
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var record = active[i];
                if ((record.lamp.transform.position - center).sqrMagnitude <= retire * retire && active.Count <= maxActive) continue;
                record.position = record.lamp.transform.position;
                record.direction = record.motion.Direction;
                record.motion.SetMoving(false);
                record.lamp.gameObject.SetActive(false);
                record.lamp.transform.SetParent(poolRoot, true);
                pool.Push(record.lamp);
                record.lamp = null; record.motion = null;
                active.RemoveAt(i); Sleep(record);
            }
            candidates.Clear();
            var min = Cell(center - Vector3.one * radius); var max = Cell(center + Vector3.one * radius);
            for (int x = min.x; x <= max.x; x++)
                for (int z = min.y; z <= max.y; z++)
                {
                    var cell = new Vector2Int(x, z);
                    Generate(cell);
                    if (!sleeping.TryGetValue(cell, out var records)) continue;
                    foreach (var record in records)
                        if ((record.position - center).sqrMagnitude <= radius * radius) candidates.Add(record);
                }
            candidates.Sort((a, b) => (a.position - center).sqrMagnitude.CompareTo((b.position - center).sqrMagnitude));
            int count = Mathf.Min(spawnBudget, Mathf.Max(0, maxActive - active.Count), candidates.Count);
            for (int i = 0; i < count; i++)
            {
                var record = candidates[i];
                sleeping[Cell(record.position)].Remove(record);
                var lamp = pool.Count > 0 ? pool.Pop() : CreateLamp();
                lamp.transform.SetParent(transform, false);
                lamp.transform.SetPositionAndRotation(record.position, LampRotation);
                lamp.transform.localScale = Vector3.one * record.scale;
                lamp.gameObject.SetActive(true);
                if (playerCollider) Physics.IgnoreCollision(lamp.GetComponent<Collider>(), playerCollider, true);
                record.lamp = lamp; record.motion = lamp.GetComponent<LightMotion>();
                record.motion.speed = moveSpeed;
                record.motion.SetMoving(Moving); record.motion.RestoreDirection(record.direction);
                lamp.SetOn(stateSource.IsOn);
                active.Add(record);
            }
            active.Sort((a, b) => (a.lamp.transform.position - center).sqrMagnitude.CompareTo((b.lamp.transform.position - center).sqrMagnitude));
            for (int i = 0; i < active.Count; i++)
            {
                var record = active[i]; record.motion.speed = moveSpeed;
                record.lamp.illuminate = i < maxPointLights && (record.lamp.transform.position - center).sqrMagnitude < pointLightDistance * pointLightDistance;
            }
            // Lowering the budget in Play also releases unused objects.
            while (pool.Count > Mathf.Max(0, maxActive - active.Count)) Destroy(pool.Pop().gameObject);
        }
        OpeningLight CreateLamp()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Small lamp";
            go.SetActive(false); go.transform.SetParent(poolRoot, false);
            var renderer = go.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var lamp = go.AddComponent<OpeningLight>();
            lamp.surface = renderer; lamp.appearanceSource = stateSource; lamp.illuminate = false;
            var glow = new GameObject("Local glow").AddComponent<Light>();
            glow.transform.SetParent(go.transform, false);
            glow.type = LightType.Point; glow.range = 2.5f; glow.intensity = .7f; glow.shadows = LightShadows.None;
            glow.enabled = false; lamp.glow = glow;
            go.AddComponent<LightMotion>();
            return lamp;
        }
        void OnValidate()
        {
            scaleRange.x = Mathf.Max(.02f, scaleRange.x); scaleRange.y = Mathf.Max(scaleRange.x, scaleRange.y);
            clearanceRange.x = Mathf.Max(0, clearanceRange.x); clearanceRange.y = Mathf.Max(clearanceRange.x, clearanceRange.y);
        }
        void OnDrawGizmosSelected()
        {
            if (!observer) return;
            Gizmos.color = new Color(.8f, .9f, 1, .4f); Gizmos.DrawWireSphere(observer.position, spawnRadius);
            Gizmos.color = new Color(.8f, .9f, 1, .15f); Gizmos.DrawWireSphere(observer.position, spawnRadius + recycleBuffer);
        }
    }
}
