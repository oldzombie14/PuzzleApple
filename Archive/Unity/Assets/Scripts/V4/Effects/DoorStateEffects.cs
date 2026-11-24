using System.Collections.Generic;
using UnityEngine;

namespace PuzzleApple.V4
{
    [DefaultExecutionOrder(-20)]
    public sealed class DoorStateEffects : MonoBehaviour
    {
        public bool IsOn { get; private set; } = true;
        public bool IsOff => !IsOn;
        [Min(0)] public float fadeDuration=1.8f;
        public float Brightness {get;private set;}=1;
        bool transitionEnabled;
        public bool PassageEnabled => IsOn && door && door.LandingCompleted;
        public float MistVisibility => GetComponentInChildren<ThresholdMist>(true)?.Visibility ?? 0;
        ThresholdDoor door;
        ThresholdFish[] fish;
        Light[] lights;
        bool[] lightDefaults;
        float[] lightIntensities;
        struct Surface { public Renderer renderer; public int index; public Color emission; public MaterialPropertyBlock original; }
        readonly List<Surface> surfaces = new List<Surface>();
        bool initialized;
        void Awake()
        {
            door = GetComponent<ThresholdDoor>();
            fish = GetComponentsInChildren<ThresholdFish>(true);
            lights = GetComponentsInChildren<Light>(true);
            lightDefaults = new bool[lights.Length];
            lightIntensities = new float[lights.Length];
            for (int i=0;i<lights.Length;i++){lightDefaults[i]=lights[i].enabled;lightIntensities[i]=lights[i].intensity;}
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.GetComponentInParent<ThresholdMist>()) continue;
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    var material=materials[i];
                    if (!material || !material.HasProperty("_EmissionColor")) continue;
                    var original=new MaterialPropertyBlock(); renderer.GetPropertyBlock(original,i);
                    surfaces.Add(new Surface {renderer=renderer,index=i,emission=material.GetColor("_EmissionColor"),original=original});
                }
            }
            initialized=true;
            ApplyAppearance();
        }
        public void SetState(bool on, bool off)
        {
            var mist=GetComponentInChildren<ThresholdMist>(true);
            if(mist)mist.ResumeStateTransition();
            transitionEnabled=true;
            if (IsOn==on && IsOff==off && initialized) return;
            if(!on&&!off)return;
            IsOn=on&&!off;
            if(initialized) ApplyAppearance();
        }
        public void HoldCurrentState()
        {
            transitionEnabled=false;
            var mist=GetComponentInChildren<ThresholdMist>(true);
            if(mist)mist.HoldCurrentState();
        }
        void Update()
        {
            if(!transitionEnabled)return;
            Brightness=fadeDuration>0?Mathf.MoveTowards(Brightness,IsOn?1:0,Time.unscaledDeltaTime/fadeDuration):(IsOn?1:0);
            ApplyAppearance();
        }
        void ApplyAppearance()
        {
            foreach(var f in fish) f.Paused=IsOff;
            float intensity=Mathf.SmoothStep(0,1,Brightness);
            for(int i=0;i<lights.Length;i++) if(lights[i]){lights[i].enabled=Brightness>0&&lightDefaults[i];lights[i].intensity=lightIntensities[i]*intensity;}
            foreach(var surface in surfaces)
            {
                if(!surface.renderer) continue;
                if (Brightness>=1) { surface.renderer.SetPropertyBlock(surface.original,surface.index); continue; }
                var block=new MaterialPropertyBlock(); surface.renderer.GetPropertyBlock(block,surface.index);
                block.SetColor("_EmissionColor",surface.emission*intensity);
                surface.renderer.SetPropertyBlock(block,surface.index);
            }
        }
    }
}
