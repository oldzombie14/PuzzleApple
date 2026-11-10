using UnityEngine;

namespace PuzzleApple.V5
{
    [ExecuteAlways]
    public sealed class FoldingInterior : MonoBehaviour
    {
        [System.Serializable] public struct Surface
        {
            public Renderer renderer;
            [Tooltip("Local normal of the inward-facing Cube surface, authored in the closed pose.")]
            public Vector3 inwardNormal;
        }
        public FoldingRoom room;
        public Surface[] surfaces;
        public Color tint=Color.white;
        [Min(0)] public float darkLevel=.0025f;
        [Min(0)] public float coreBrightness=.18f;
        [Tooltip("Total apparent seam width across the two adjoining faces, in screen pixels.")]
        [Range(.5f,3)] public float edgeWidthPixels=1.5f;
        [Range(0,1)] public float handoffStart=.05f,handoffEnd=.8f;
        public float ClosedWeight=>1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(handoffStart,Mathf.Max(handoffStart+.001f,handoffEnd),room?room.Progress:0));
        MaterialPropertyBlock properties;
        void OnEnable()=>Apply();
        void LateUpdate()=>Apply();
        public void Apply()
        {
            if(surfaces==null)return;
            if(properties==null)properties=new MaterialPropertyBlock();
            foreach(var surface in surfaces)
            {
                var r=surface.renderer;if(!r)continue;
                r.GetPropertyBlock(properties);
                properties.SetVector("_InteriorNormal",surface.inwardNormal);
                properties.SetColor("_InteriorTint",tint);
                properties.SetFloat("_ClosedWeight",ClosedWeight);
                properties.SetFloat("_DarkLevel",darkLevel);
                properties.SetFloat("_SeamCore",coreBrightness);
                properties.SetFloat("_CoreWidthPixels",edgeWidthPixels*.5f);
                r.SetPropertyBlock(properties);
            }
        }
        void OnDisable()
        {
            if(surfaces==null)return;
            if(properties==null)properties=new MaterialPropertyBlock();
            foreach(var s in surfaces)if(s.renderer){s.renderer.GetPropertyBlock(properties);properties.SetFloat("_ClosedWeight",0);s.renderer.SetPropertyBlock(properties);}
        }
    }
}
