using UnityEngine;
using UnityEngine.Events;

namespace PuzzleApple.V4
{
    public enum LightLevelState { Off, InBetween, On }
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
        public float Brightness => appearanceSource ? appearanceSource.Brightness : brightness;
        public bool IsOn => Brightness >= 1;
        public bool IsOff => Brightness <= 0;
        public LightLevelState CurrentState => IsOn ? LightLevelState.On : IsOff ? LightLevelState.Off : LightLevelState.InBetween;
        public bool TargetOn => appearanceSource ? appearanceSource.TargetOn : targetOn;
        public bool TransitionEnabled => appearanceSource ? appearanceSource.TransitionEnabled : transitionEnabled;
        bool targetOn = true, transitionEnabled;
        Material runtimeMaterial;
        Color emission, onColor;
        float brightness = 1, onLightIntensity;
        NightEnvironment environment;
        MaterialPropertyBlock revealProperties;
        float revealStart, revealDuration;
        bool revealing;
        public float RevealAmount { get; private set; } = 1;
        public void BeginReveal(Material material, float start, float duration)
        {
            revealStart=start; revealDuration=Mathf.Max(.01f,duration); revealing=true;
            RevealAmount=0;
            surface.sharedMaterial=material;
            if(revealProperties==null)revealProperties=new MaterialPropertyBlock();
            ApplyReveal();
            if(glow)glow.enabled=false;
        }
        void ApplyReveal()
        {
            var source=appearanceSource.AppearanceMaterial;
            var color=source.GetColor("_BaseColor");color.a=RevealAmount;
            revealProperties.SetColor("_BaseColor",color);
            revealProperties.SetColor("_EmissionColor",source.GetColor("_EmissionColor")*RevealAmount);
            revealProperties.SetColor("_SpecColor",source.GetColor("_SpecColor")*RevealAmount);
            surface.SetPropertyBlock(revealProperties);
        }
        void Awake()
        {
            if (!surface) surface = GetComponent<Renderer>();
            if (appearanceSource)
            {
                if(!revealing)surface.sharedMaterial = appearanceSource.AppearanceMaterial;
                return;
            }
            runtimeMaterial = surface.material;
            environment=FindFirstObjectByType<NightEnvironment>();
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
            if (appearanceSource) return;
            if (targetOn == value && transitionEnabled) return;
            targetOn = value;
            transitionEnabled = true;
            if (transitionDuration <= 0) { brightness = value ? 1 : 0; ApplyAppearance(); }
            stateChanged.Invoke(value);
        }
        public void HoldCurrentState() { transitionEnabled = false; }
        void Update()
        {
            if (appearanceSource)
            {
                if(revealing)
                {
                    RevealAmount=Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.unscaledTime-revealStart)/revealDuration));
                    ApplyReveal();
                    if(RevealAmount>=1)
                    {
                        revealing=false;surface.SetPropertyBlock(null);
                        surface.sharedMaterial=appearanceSource.AppearanceMaterial;
                    }
                }
                if (glow)
                {
                    glow.enabled = illuminate && appearanceSource.brightness > 0 && RevealAmount>0;
                    glow.intensity = Mathf.SmoothStep(0, 1, appearanceSource.brightness) * .7f * RevealAmount;
                }
                return;
            }
            if (!transitionEnabled) return;
            // The sky leads both directions; the lamp then overlaps it with a slower fade.
            if(environment&&environment.lamp==this&&transitionDuration>0&&
                (targetOn?environment.NightBlend>.8f:environment.NightBlend<.2f))return;
            float target = targetOn ? 1 : 0;
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
