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
            public bool preserveMeshDepth;
            [Range(0,1)] public float closedShade;
        }
        public FoldingRoom room;
        public Surface[] surfaces;
        public Color tint=Color.white;
        public bool useSceneLighting;
        [Range(0,1)] public float closedLightingBlend;
        [Min(0)] public float darkLevel=.0025f;
        [Min(0)] public float coreBrightness=.18f;
        [Tooltip("Total apparent seam width across the two adjoining faces, in screen pixels.")]
        [Range(.5f,3)] public float edgeWidthPixels=1.5f;
        [Range(0,1)] public float handoffStart=.05f,handoffEnd=.8f;
        public float ClosedWeight=>1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(handoffStart,Mathf.Max(handoffStart+.001f,handoffEnd),room?room.Progress:0));
        MaterialPropertyBlock properties;
        internal static FoldingInterior Active { get; private set; }
        sealed class Face
        {
            public readonly Vector3[] corners=new Vector3[4], world=new Vector3[4];
            public Vector3 normal;
            public Vector4 seams;
        }
        Face[] faces;
        static readonly int[] edgeStart={0,1,0,2},edgeEnd={2,3,1,3};
        void OnEnable(){Active=this;faces=null;Apply();}
        void OnValidate()=>faces=null;

        void UpdateSeams()
        {
            if(faces==null||faces.Length!=surfaces.Length)
            {
                faces=new Face[surfaces.Length];
                for(int i=0;i<surfaces.Length;i++)
                {
                    var s=surfaces[i];
                    if(!s.renderer||!s.renderer.TryGetComponent<MeshFilter>(out var filter)||!filter.sharedMesh)continue;
                    var mesh=filter.sharedMesh;
                    var vertices=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;
                    var face=faces[i]=new Face();
                    for(int k=0;k<vertices.Length;k++)
                        if(Vector3.Dot(normals[k],s.inwardNormal)>.99f)
                            face.corners[(uv[k].x>.5f?1:0)+(uv[k].y>.5f?2:0)]=vertices[k];
                }
            }
            for(int i=0;i<faces.Length;i++)
            {
                var face=faces[i];
                if(face==null||!surfaces[i].renderer)continue;
                var tr=surfaces[i].renderer.transform;
                face.normal=tr.TransformDirection(surfaces[i].inwardNormal);
                face.seams=Vector4.zero;
                for(int k=0;k<4;k++)face.world[k]=tr.TransformPoint(face.corners[k]);
            }
            // Only coincident edges with a real dihedral angle are creases.
            for(int i=0;i<faces.Length;i++)for(int j=i+1;j<faces.Length;j++)
            {
                var a=faces[i];var b=faces[j];
                if(a==null||b==null)continue;
                float strength=Mathf.InverseLerp(.978f,.86f,Vector3.Dot(a.normal,b.normal));
                if(strength<=0)continue;
                for(int u=0;u<4;u++)for(int v=0;v<4;v++)
                {
                    var p=a.world[edgeStart[u]];var q=a.world[edgeEnd[u]];
                    var r=b.world[edgeStart[v]];var s=b.world[edgeEnd[v]];
                    if(((p-r).sqrMagnitude<1e-6f&&(q-s).sqrMagnitude<1e-6f)||
                       ((p-s).sqrMagnitude<1e-6f&&(q-r).sqrMagnitude<1e-6f))
                    {a.seams[u]=strength;b.seams[v]=strength;}
                }
            }
        }
        void LateUpdate()=>Apply();
        public void Apply()
        {
            if(surfaces==null)return;
            if(properties==null)properties=new MaterialPropertyBlock();

            UpdateSeams();
            float weight=ClosedWeight;
            float opacity=room?room.PanelCoverage:1;
            Vector4 sink=Vector4.zero;
            if(room && opacity<1)
            {
                Vector3 feet=room.SinkCentre;
                float farthestSquared=0;
                float floorSquared=0;
                foreach(var surface in surfaces)
                {
                    if(!surface.renderer)continue;
                    var bounds=surface.renderer.bounds;
                    float dx=Mathf.Abs(bounds.center.x-feet.x)+bounds.extents.x;
                    float dz=Mathf.Abs(bounds.center.z-feet.z)+bounds.extents.z;
                    float distanceSquared=dx*dx+dz*dz;
                    farthestSquared=Mathf.Max(farthestSquared,distanceSquared);
                    if(surface.renderer.transform==room.floorPanel)floorSquared=distanceSquared;
                }
                float reach=Mathf.Sqrt(farthestSquared);
                float immersion=1-opacity;
                float radius=reach*opacity;
                if(floorSquared>0 && floorSquared<farthestSquared)
                {
                    // One continuous front crosses the far edge of the base midway through immersion,
                    // even when the player's feet are off centre or the lid extends farther.
                    const float contact=8f/15f; // Fraction of the immersion timeline.
                    float ratio=Mathf.Sqrt(floorSquared/farthestSquared);
                    float k=(1-contact-ratio)/(contact*ratio);
                    radius=reach*opacity/(1+k*immersion);
                }
                sink=new Vector4(feet.x,feet.z,reach,radius);
            }
            if(room&&room.floorPanel)
                weight=Mathf.Max(weight,.3f*(1-Mathf.SmoothStep(0,1,room.FloorFinishProgress*2)));
            Shader.SetGlobalColor("_P5OutlineInk",tint*((darkLevel+coreBrightness)*weight));
            Shader.SetGlobalVector("_P5OutlineStyle",new Vector4(edgeWidthPixels,0,0,0));
            float groundY=room&&room.floorPanel?room.floorPanel.position.y:transform.position.y;
            if(Application.isPlaying)
            {
                Shader.SetGlobalVector("_P5BoxWave",sink);
                Shader.SetGlobalVector("_P5BoxWavePhase",new Vector4(1-opacity,groundY,0,0));
            }
            for(int i=0;i<surfaces.Length;i++)
            {
                var surface=surfaces[i];
                var r=surface.renderer;if(!r)continue;
                r.GetPropertyBlock(properties);
                properties.SetVector("_ContactSeams",faces[i]?.seams??Vector4.zero);
                properties.SetVector("_InteriorNormal",surface.inwardNormal);
                var face=faces[i];
                properties.SetVector("_InteriorFaceCentre",face==null?Vector3.zero:
                    (face.corners[0]+face.corners[1]+face.corners[2]+face.corners[3])*.25f);
                properties.SetColor("_InteriorTint",tint*(1-surface.closedShade));
                properties.SetFloat("_ClosedWeight",useSceneLighting?0:weight*(1-closedLightingBlend));
                properties.SetFloat("_PreserveMeshDepth",surface.preserveMeshDepth?1:0);
                properties.SetFloat("_PanelOpacity",opacity);
                properties.SetVector("_PanelSink",sink);
                properties.SetFloat("_PanelGroundY",groundY);
                properties.SetFloat("_DarkLevel",darkLevel);
                properties.SetFloat("_SeamCore",coreBrightness);
                properties.SetFloat("_CoreWidthPixels",edgeWidthPixels*.5f);
                r.SetPropertyBlock(properties);
            }
        }
        void OnDisable()
        {
            if(Active==this)Active=null;
            if(Application.isPlaying)Shader.SetGlobalVector("_P5BoxWave",Vector4.zero);
            if(surfaces==null)return;
            if(properties==null)properties=new MaterialPropertyBlock();
            foreach(var s in surfaces)if(s.renderer){s.renderer.GetPropertyBlock(properties);properties.SetFloat("_ClosedWeight",0);properties.SetFloat("_PanelOpacity",1);s.renderer.SetPropertyBlock(properties);}
        }
    }
}
