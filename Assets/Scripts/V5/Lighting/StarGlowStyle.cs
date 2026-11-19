using UnityEngine;

namespace PuzzleApple.V5
{
    [CreateAssetMenu(menuName="PuzzleApple/V5/Star glow")]
    public sealed class StarGlowStyle : ScriptableObject
    {
        [Min(0)] public float emission=1f;
        [Min(1)] public float haloRadius=1.35f;
        [Range(0,1)] public float haloOpacity=0;
        public static StarGlowStyle Load()=>Resources.Load<StarGlowStyle>("V5/StarGlowStyle");
        public static Renderer CreateHalo(Transform star,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name="Star halo";go.transform.SetParent(star,false);
            var collider=go.GetComponent<Collider>();collider.enabled=false;Object.Destroy(collider);
            var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows=false;renderer.enabled=false;
            return renderer;
        }
        public void DrawHalo(Renderer halo,bool visible,MaterialPropertyBlock properties)
        {
            halo.enabled=visible&&haloOpacity>0;halo.transform.localScale=Vector3.one*haloRadius;
            properties.SetFloat("_Opacity",haloOpacity);halo.SetPropertyBlock(properties);
        }
    }
}
