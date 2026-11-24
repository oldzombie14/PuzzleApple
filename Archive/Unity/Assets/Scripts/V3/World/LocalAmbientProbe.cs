using UnityEngine;
using System.Linq;
using UnityEngine.Rendering;

namespace PuzzleApple.V3
{
    // Native per-renderer SH lighting keeps a dark room dark from either side
    // of its doorway. Camera volumes cannot provide this spatial separation.
    [ExecuteAlways, RequireComponent(typeof(Renderer)), DefaultExecutionOrder(450)]
    public sealed class LocalAmbientProbe : MonoBehaviour
    {
        public BoxCollider roomVolume;
        [Tooltip("Disable for corridor objects, such as both leaves of the entrance gate, that must not follow the hall blackout.")]
        public bool followRoomLighting=true;
        [ColorUsage(false,true)] public Color ambient=new Color(.004f,.004f,.004f);
        [Min(.01f)] public float transition=1;
        Renderer surface;
        MaterialPropertyBlock properties;
        RoomLighting lighting;
        Material[] originalMaterials,darkMaterials;
        bool environmentSuppressed;
        readonly SphericalHarmonicsL2[] probe=new SphericalHarmonicsL2[1];
        void OnEnable()=>Apply();
        void OnValidate()=>Apply();
        void LateUpdate()=>Apply();
        public void Apply()
        {
            if(!roomVolume)return;
            if(!surface)surface=GetComponent<Renderer>();
            if(properties==null)properties=new MaterialPropertyBlock();
            var point=roomVolume.transform.InverseTransformPoint(surface.bounds.center)-roomVolume.center;
            var half=roomVolume.size*.5f;
            float inside=Mathf.Min(half.x-Mathf.Abs(point.x),half.y-Mathf.Abs(point.y),half.z-Mathf.Abs(point.z));
            float weight=Mathf.SmoothStep(0,1,Mathf.Clamp01(inside/transition));
            var local=new SphericalHarmonicsL2();local.AddAmbientLight(ambient);
            var outside=RenderSettings.ambientProbe;
            if(!lighting)lighting=roomVolume.GetComponentInParent<RoomLighting>();
            float illumination=Application.isPlaying&&lighting&&followRoomLighting?lighting.AmbientAt(surface.bounds.center):1;
            if(Application.isPlaying&&lighting&&originalMaterials==null)
            {
                var current=surface.sharedMaterials;
                originalMaterials=current.Select(lighting.Original).ToArray();
                if(!current.SequenceEqual(originalMaterials))surface.sharedMaterials=originalMaterials;
            }
            // URP can fall back to the sky reflection even when a local probe's
            // intensity is zero. Use its native material switch while this room is dark.
            bool suppress=illumination<.999f;
            if(suppress!=environmentSuppressed)
            {
                if(suppress&&darkMaterials==null)darkMaterials=originalMaterials.Select(lighting.WithoutEnvironment).ToArray();
                surface.sharedMaterials=suppress?darkMaterials:originalMaterials;environmentSuppressed=suppress;
            }
            for(int channel=0;channel<3;channel++)for(int coefficient=0;coefficient<9;coefficient++)
                probe[0][channel,coefficient]=Mathf.Lerp(outside[channel,coefficient],local[channel,coefficient],weight)*illumination;
            surface.lightProbeUsage=LightProbeUsage.CustomProvided;
            surface.GetPropertyBlock(properties);properties.CopySHCoefficientArraysFrom(probe);
            properties.SetVector("_GlossyEnvironmentColor",Vector4.Lerp(Shader.GetGlobalVector("_GlossyEnvironmentColor"),(Vector4)ambient,weight)*illumination);
            surface.SetPropertyBlock(properties);
        }
        void OnDisable()
        {
            if(!surface)return;
            if(environmentSuppressed&&originalMaterials!=null){surface.sharedMaterials=originalMaterials;environmentSuppressed=false;}
            probe[0]=RenderSettings.ambientProbe;
            surface.GetPropertyBlock(properties);properties.CopySHCoefficientArraysFrom(probe);
            properties.SetVector("_GlossyEnvironmentColor",Shader.GetGlobalVector("_GlossyEnvironmentColor"));surface.SetPropertyBlock(properties);
            surface.lightProbeUsage=LightProbeUsage.BlendProbes;
        }
    }
}
