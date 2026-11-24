using UnityEngine;
using PuzzleApple.V5.Cognition;

namespace PuzzleApple.V5
{
    public sealed class ChapterPaintingLight : MonoBehaviour
    {
        public Light spot;
        public Renderer lens;
        public bool swing;
        [Range(0,45)] public float amplitude=20;
        [Min(.1f)] public float halfPeriod=3;
        Quaternion resting;
        float phase, initialIntensity;
        Color emission;
        MaterialPropertyBlock ink;
        public bool Lit => spot && spot.intensity>0;

        void Awake()
        {
            resting=transform.localRotation;
            initialIntensity=spot.intensity;
            emission=lens.sharedMaterial.GetColor("_EmissionColor");
            ink=new MaterialPropertyBlock();
        }
        internal float Phase => phase;
        internal void RestorePhase(float value)
        {
            phase = value;
            if (swing) transform.localRotation = resting * Quaternion.Euler(amplitude * Mathf.Sin(phase), 0, 0);
        }
        public void Tick(bool moving)
        {
            if(swing && moving && Lit){
                RestorePhase(phase + Mathf.PI * Time.deltaTime / halfPeriod);
            }
            lens.GetPropertyBlock(ink);
            ink.SetColor("_EmissionColor",emission*Mathf.Clamp01(spot.intensity/Mathf.Max(.001f,initialIntensity)));
            lens.SetPropertyBlock(ink);
        }
    }
}
