using UnityEngine;
using UnityEngine.Events;

namespace PuzzleApple.V4
{
    public sealed class OpeningLight : MonoBehaviour
    {
        public Renderer surface;
        public Light glow;
        [Tooltip("动态灯共用全局灯的材质与亮度，避免每盏灯复制材质。")]
        public OpeningLight appearanceSource;
        [System.NonSerialized] public bool illuminate = true;
        public Material AppearanceMaterial => runtimeMaterial;
        public Color offColor = Color.black;
        [Min(0)] public float transitionDuration = 1.2f;
        public UnityEvent<bool> stateChanged = new UnityEvent<bool>();
        public bool IsOn { get; private set; } = true;
        Material runtimeMaterial;
        Color emission, onColor;
        float brightness = 1, onLightIntensity;
        void Awake()
        {
            if (!surface) surface = GetComponent<Renderer>();
            if (appearanceSource)
            {
                surface.sharedMaterial = appearanceSource.AppearanceMaterial;
                return;
            }
            runtimeMaterial = surface.material;
            emission = runtimeMaterial.GetColor("_EmissionColor");
            onColor = runtimeMaterial.GetColor("_BaseColor");
            // Explicit specular control lets reflections fade with the lamp as well.
            runtimeMaterial.SetFloat("_WorkflowMode", 0);
            runtimeMaterial.EnableKeyword("_SPECULAR_SETUP");
            runtimeMaterial.SetColor("_SpecColor", new Color(.04f, .04f, .04f, 1));
            if (glow) onLightIntensity = glow.intensity;
        }
        public void SetOn(bool value)
        {
            if (IsOn == value) return;
            IsOn = value;
            if (transitionDuration <= 0) { brightness = value ? 1 : 0; ApplyAppearance(); }
            stateChanged.Invoke(value);
        }
        void Update()
        {
            if (appearanceSource)
            {
                IsOn = appearanceSource.IsOn;
                if (glow)
                {
                    glow.enabled = illuminate && appearanceSource.brightness > 0;
                    glow.intensity = Mathf.SmoothStep(0, 1, appearanceSource.brightness) * .7f;
                }
                return;
            }
            float target = IsOn ? 1 : 0;
            if (brightness == target) return;
            brightness = transitionDuration > 0
                ? Mathf.MoveTowards(brightness, target, Time.unscaledDeltaTime / transitionDuration) : target;
            ApplyAppearance();
        }
        void ApplyAppearance()
        {
            float eased = Mathf.SmoothStep(0, 1, brightness);
            runtimeMaterial.SetColor("_EmissionColor", Color.Lerp(Color.black, emission, eased));
            runtimeMaterial.SetColor("_BaseColor", Color.Lerp(offColor, onColor, eased));
            runtimeMaterial.SetColor("_SpecColor", new Color(.04f, .04f, .04f, 1) * eased);
            if (glow) { glow.intensity = onLightIntensity * eased; glow.enabled = brightness > 0; }
        }
        void OnDestroy() { if (runtimeMaterial) Destroy(runtimeMaterial); }
    }
}
