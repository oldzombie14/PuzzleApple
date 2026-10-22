using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PuzzleApple.V4
{
    [ExecuteAlways]
    public sealed class CloudLightCookie : MonoBehaviour
    {
        public Light sun;
        public Material sky;
        [Tooltip("The actual cloud mesh. Set its Transform Position Y to change height above ground.")]
        public Renderer cloudPlane;
        [Tooltip("Alpha is cloud opacity; RGB is light transmission derived from that alpha.")]
        public Texture2D cloudMask;
        public bool cloudsEnabled = true;
        public Vector2 cookieSize = new Vector2(800,800);
        public Vector2 wind = new Vector2(.8f,.32f);
        [System.NonSerialized] public float speedMultiplier = 1;
        Vector3 windTravel;
        public Transform player;
        [Min(.1f)] public float directionDelay = 1.4f;
        [Min(.1f)] public float directionSmoothing = 2.5f;
        public bool FollowFacing { get; set; }
        public Vector3 DriftDirection => driftDirection;
        Vector3 previousPlayerPosition, driftDirection, targetDirection;
        readonly System.Collections.Generic.Queue<Vector4> headings = new System.Collections.Generic.Queue<Vector4>();
        float smoothedSpeed = 1;
        public Vector2 initialOffset = new Vector2(80,110);
        public bool fogEnabled = true;
        [Range(0,.01f)] public float fogDensity = .003f;
        public Color fogColor = new Color(.80f,.84f,.82f);
        bool fogOverride;
        Color overrideColor;
        float overrideDensity;

        public void SetFogOverride(bool active, Color color, float density)
        {
            fogOverride = active; overrideColor = color; overrideDensity = density;
            UpdateProjection();
        }

        void OnEnable()
        {
            driftDirection = targetDirection = new Vector3(wind.x, 0, wind.y).normalized;
            if (player) previousPlayerPosition = player.position;
            UpdateProjection();
        }
        void Update()
        {
            if (Application.isPlaying)
            {
                if (player)
                {
                    var delta = player.position - previousPlayerPosition;
                    previousPlayerPosition = player.position;
                    var heading = FollowFacing ? -player.forward : delta;
                    heading.y = 0;
                    if (heading.sqrMagnitude > .000001f)
                    {
                        heading.Normalize();
                        headings.Enqueue(new Vector4(heading.x,0,heading.z,Time.time + directionDelay));
                    }
                    while (headings.Count > 0 && headings.Peek().w <= Time.time)
                    {
                        var sample = headings.Dequeue(); targetDirection = new Vector3(sample.x,0,sample.z);
                    }
                }
                driftDirection = Vector3.RotateTowards(driftDirection, targetDirection, Time.deltaTime / directionSmoothing, 0).normalized;
                smoothedSpeed = Mathf.Lerp(smoothedSpeed,speedMultiplier,1-Mathf.Exp(-Time.deltaTime / 1.5f));
                windTravel += driftDirection * (wind.magnitude * Time.deltaTime * smoothedSpeed);
            }
            UpdateProjection();
        }

        public void UpdateProjection()
        {
            RenderSettings.fog = fogEnabled;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            float effectiveDensity = fogOverride ? overrideDensity : fogDensity;
            Color effectiveColor = fogOverride ? overrideColor : fogColor;
            RenderSettings.fogDensity = effectiveDensity;
            RenderSettings.fogColor = effectiveColor;
            if (!sun || !cloudPlane || !cloudPlane.sharedMaterial || !cloudMask) return;
            var data = sun.GetComponent<UniversalAdditionalLightData>();
            if (!data) return;
            var travel = sun.transform.InverseTransformVector(Application.isPlaying ? windTravel : Vector3.zero);
            var origin = sun.transform.InverseTransformPoint(cloudPlane.transform.position);
            var offset = initialOffset + new Vector2(origin.x+travel.x,origin.y+travel.y);
            var size = new Vector2(Mathf.Max(1,cookieSize.x),Mathf.Max(1,cookieSize.y));
            cloudPlane.enabled = cloudsEnabled;
            sun.cookie = cloudsEnabled ? cloudMask : null;
            data.lightCookieSize = size;
            data.lightCookieOffset = offset;
            // Matches URP directional cookie UV: light-local XY / size + 0.5.
            // Points along the same sunlight ray share XY, so the alpha above
            // and the light transmission below correspond at every height.
            var worldToUV = Matrix4x4.Scale(new Vector3(1/size.x,1/size.y,1))
                * Matrix4x4.Translate(new Vector3(-offset.x,-offset.y,0))
                * sun.transform.worldToLocalMatrix;
            var material = cloudPlane.sharedMaterial;
            material.SetMatrix("_CloudWorldToUV",worldToUV);
            material.SetTexture("_CloudMask",cloudMask);
            material.SetColor("_MistColor",effectiveColor);
            material.SetFloat("_MistDensity",fogEnabled ? effectiveDensity : 0);
            if (sky) sky.SetVector("_SunDirection",-sun.transform.forward);
        }
    }
}
