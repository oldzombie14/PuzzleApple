using UnityEngine;
using UnityEngine.Rendering;

namespace PuzzleApple.V4
{
    [ExecuteAlways, DefaultExecutionOrder(400)]
    public sealed class NightEnvironment : MonoBehaviour
    {
        [Header("引用")]
        public OpeningLight lamp;
        public Light sun;
        public CloudLightCookie clouds;
        public Material daySky, nightSky;
        [Header("编辑模式预览（运行时按 X）")]
        public bool previewNight;
        public bool WorldMoving { get; set; }
        [Min(1)] public float movingCloudSpeed = 12;
        public float movingStarDegreesPerSecond = 3;
        Quaternion starRotation = Quaternion.identity;
        [Min(0)] public float transitionDuration = 1.2f;
        [Header("夜晚天空")]
        public Color nightZenith = new Color(.012f,.022f,.055f);
        public Color nightHorizon = new Color(.06f,.09f,.15f);
        [Range(0, .02f)] public float starDensity = .0005f;
        [Range(0, 6)] public float starBrightness = 2.3f;
        [Range(.03f, .3f)] public float starSize = .12f;
        [Range(0, 1)] public float starGlow = .08f;
        [Range(1, 10)] public float starRayLength = 4.2f;
        [Range(0, 2)] public float starRayStrength = .35f;
        [Tooltip("星星开始出现的仰角；地平线为 0 度。")]
        [Range(0, 75)] public float starMinimumElevation = 18;
        [Tooltip("从无星到完整亮度的仰角过渡范围。")]
        [Range(1, 30)] public float starElevationFade = 12;
        [Range(0, 5)] public float twinkleSpeed = .55f;
        [Range(0, 1)] public float twinkleAmount = .3f;
        [Header("夜晚环境光")]
        public Color nightAmbientSky = new Color(.10f,.14f,.24f);
        public Color nightAmbientEquator = new Color(.06f,.085f,.13f);
        public Color nightAmbientGround = new Color(.035f,.045f,.065f);
        public Color nightLightColor = new Color(.45f,.55f,.8f);
        [Min(0)] public float nightLightIntensity = .035f;
        [Header("夜晚雾与云")]
        public Color nightFogColor = new Color(.18f,.22f,.30f);
        [Range(0,.01f)] public float nightFogDensity = .004f;
        [Tooltip("夜空地平线薄雾向上散开的高度。")]
        [Range(.02f,.4f)] public float nightHazeHeight = .12f;
        public Color nightCloudColor = new Color(.08f,.105f,.16f,.65f);
        [Header("白天基线（保留当前场景值）")]
        public Color dayAmbientSky, dayAmbientEquator, dayAmbientGround, dayLightColor, dayCloudColor;
        public float dayLightIntensity;
        public AmbientMode dayAmbientMode = AmbientMode.Trilight;
        public float NightBlend => blend;
        float blend;
        Material skyInstance;
        MaterialPropertyBlock cloudProperties;
        void OnEnable() { blend = !Application.isPlaying && previewNight ? 1 : 0; }
        void LateUpdate()
        {
            if (!sun || !daySky || !nightSky || !clouds) return;
            bool night = Application.isPlaying ? lamp && !lamp.IsOn : previewNight;
            blend = Application.isPlaying && transitionDuration > 0
                ? Mathf.MoveTowards(blend, night ? 1 : 0, Time.unscaledDeltaTime / transitionDuration) : night ? 1 : 0;
            Apply(blend);
            clouds.cloudsEnabled = blend < .98f;
            clouds.FollowFacing = WorldMoving;
            clouds.speedMultiplier = WorldMoving && !night ? movingCloudSpeed : 1;
            if (Application.isPlaying && WorldMoving && night)
            {
                var axis = Vector3.Cross(Vector3.up, clouds.DriftDirection);
                starRotation = Quaternion.AngleAxis(movingStarDegreesPerSecond * Time.deltaTime, axis) * starRotation;
            }
            skyInstance.SetMatrix("_StarRotation", Matrix4x4.Rotate(Quaternion.Inverse(starRotation)));
        }
        void Apply(float t)
        {
            if (!skyInstance) skyInstance = new Material(nightSky) { name = "V4 night preview", hideFlags = HideFlags.HideAndDontSave };
            skyInstance.SetColor("_DayZenith", daySky.GetColor("_Zenith"));
            skyInstance.SetColor("_DayHorizon", daySky.GetColor("_Horizon"));
            skyInstance.SetVector("_SunDirection", -sun.transform.forward);
            skyInstance.SetColor("_Zenith", nightZenith); skyInstance.SetColor("_Horizon", nightHorizon);
            skyInstance.SetFloat("_Night", t); skyInstance.SetFloat("_StarDensity", starDensity);
            skyInstance.SetFloat("_StarBrightness", starBrightness); skyInstance.SetFloat("_StarSize", starSize);
            skyInstance.SetFloat("_StarGlow", starGlow);
            skyInstance.SetFloat("_RayLength", starRayLength);
            skyInstance.SetFloat("_RayStrength", starRayStrength);
            skyInstance.SetFloat("_StarMinHeight", Mathf.Sin(starMinimumElevation * Mathf.Deg2Rad));
            skyInstance.SetFloat("_StarFullHeight", Mathf.Sin(Mathf.Min(89, starMinimumElevation + starElevationFade) * Mathf.Deg2Rad));
            skyInstance.SetFloat("_HazeHeight", nightHazeHeight);
            skyInstance.SetFloat("_HazeEnabled", clouds.fogEnabled ? 1 : 0);
            skyInstance.SetFloat("_TwinkleSpeed", twinkleSpeed); skyInstance.SetFloat("_TwinkleAmount", twinkleAmount);
            skyInstance.SetFloat("_Clock", Application.isPlaying ? Time.unscaledTime : Time.realtimeSinceStartup);
            RenderSettings.skybox = t > 0 ? skyInstance : daySky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(dayAmbientSky, nightAmbientSky, t);
            RenderSettings.ambientEquatorColor = Color.Lerp(dayAmbientEquator, nightAmbientEquator, t);
            RenderSettings.ambientGroundColor = Color.Lerp(dayAmbientGround, nightAmbientGround, t);
            sun.color = Color.Lerp(dayLightColor, nightLightColor, t);
            sun.intensity = Mathf.Lerp(dayLightIntensity, nightLightIntensity, t);
            clouds.SetFogOverride(t > 0, Color.Lerp(clouds.fogColor, nightFogColor, t), Mathf.Lerp(clouds.fogDensity, nightFogDensity, t));
            if (cloudProperties == null) cloudProperties = new MaterialPropertyBlock();
            clouds.cloudPlane.GetPropertyBlock(cloudProperties);
            cloudProperties.SetColor("_CloudColor", Color.Lerp(dayCloudColor, nightCloudColor, t));
            clouds.cloudPlane.SetPropertyBlock(cloudProperties);
        }
        void OnDisable()
        {
            if (daySky && sun && clouds && nightSky) Apply(0);
            RenderSettings.ambientMode = dayAmbientMode;
            if (skyInstance)
            {
                if (Application.isPlaying) Destroy(skyInstance); else DestroyImmediate(skyInstance);
                skyInstance = null;
            }
        }
    }
}
