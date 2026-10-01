using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PuzzleApple.V3
{
    // Visual identity only: the existing player remains the single collision/weight authority.
    public sealed class V3PlayerAppearance : MonoBehaviour
    {
        FirstPersonController player;
        Camera view;
        GameObject apple;
        readonly List<Renderer> renderers=new List<Renderer>();
        public GameObject AppleVisual => apple;
        public void Initialize(FirstPersonController owner,V3Object source,Camera camera)
        {
            player=owner;view=camera;apple=new GameObject("Player apple appearance");
            foreach(var filter in source.GetComponentsInChildren<MeshFilter>())
            {
                var original=filter.GetComponent<MeshRenderer>();if(!original)continue;
                var part=new GameObject(filter.name,typeof(MeshFilter),typeof(MeshRenderer));
                part.transform.SetParent(apple.transform,false);
                part.transform.SetPositionAndRotation(filter.transform.position-source.transform.position,filter.transform.rotation);
                part.transform.localScale=filter.transform.lossyScale;
                part.GetComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
                var renderer=part.GetComponent<MeshRenderer>();renderer.sharedMaterials=original.sharedMaterials;renderers.Add(renderer);
            }
            var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            apple.transform.SetParent(owner.transform,false);
            apple.transform.localPosition=new Vector3(-bounds.center.x,-bounds.min.y+.015f,-bounds.center.z);
            apple.SetActive(false);
        }
        void LateUpdate(){if(apple)apple.SetActive(player.AppleIdentity);}
        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering+=BeforeCamera;
            RenderPipelineManager.endCameraRendering+=AfterCamera;
        }
        void BeforeCamera(ScriptableRenderContext context,Camera camera)
        {
            foreach(var r in renderers)if(r)r.forceRenderingOff=camera==view;
        }
        void AfterCamera(ScriptableRenderContext context,Camera camera)
        {
            foreach(var r in renderers)if(r)r.forceRenderingOff=false;
        }
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering-=BeforeCamera;
            RenderPipelineManager.endCameraRendering-=AfterCamera;
            foreach(var r in renderers)if(r)r.forceRenderingOff=false;
        }
        void OnDestroy(){if(apple)Destroy(apple);}
    }
}
