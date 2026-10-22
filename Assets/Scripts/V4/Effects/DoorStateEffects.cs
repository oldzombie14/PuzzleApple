using System.Collections.Generic;
using UnityEngine;

namespace PuzzleApple.V4
{
    [DefaultExecutionOrder(-20)]
    public sealed class DoorStateEffects : MonoBehaviour
    {
        public bool IsOn { get; private set; }
        public bool IsOff { get; private set; }
        public bool PassageEnabled => IsOn && door && door.LandingCompleted;
        ThresholdDoor door;
        ThresholdFish[] fish;
        Light[] lights;
        bool[] lightDefaults;
        struct Surface { public Renderer renderer; public int index; public Color emission; public MaterialPropertyBlock original; }
        readonly List<Surface> surfaces = new List<Surface>();
        bool initialized;
        void Awake()
        {
            door = GetComponent<ThresholdDoor>();
            fish = GetComponentsInChildren<ThresholdFish>(true);
            lights = GetComponentsInChildren<Light>(true);
            lightDefaults = new bool[lights.Length];
            for (int i=0;i<lights.Length;i++) lightDefaults[i]=lights[i].enabled;
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
            if (IsOn==on && IsOff==off && initialized) return;
            IsOn=on; IsOff=off;
            if(initialized) ApplyAppearance();
        }
        void ApplyAppearance()
        {
            foreach(var f in fish) f.Paused=IsOff;
            for(int i=0;i<lights.Length;i++) if(lights[i]) lights[i].enabled=!IsOff && lightDefaults[i];
            foreach(var surface in surfaces)
            {
                if(!surface.renderer) continue;
                if (!IsOff) { surface.renderer.SetPropertyBlock(surface.original,surface.index); continue; }
                var block=new MaterialPropertyBlock(); surface.renderer.GetPropertyBlock(block,surface.index);
                block.SetColor("_EmissionColor",Color.black);
                surface.renderer.SetPropertyBlock(block,surface.index);
            }
        }
    }
}
