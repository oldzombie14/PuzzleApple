using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace PuzzleApple
{
    // Same approach as GnomonTrial's HUD: capture world color before the overlay, including transparent objects.
    public sealed class ReticleSceneColorFeature : ScriptableRendererFeature
    {
        sealed class CopyPass : ScriptableRenderPass
        {
            RTHandle output;
            public CopyPass()
            { renderPassEvent = RenderPassEvent.AfterRenderingTransparents; requiresIntermediateTexture = true; }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var camera = frameData.Get<UniversalCameraData>();
                var desc = camera.cameraTargetDescriptor;
                desc.depthBufferBits = 0; desc.msaaSamples = 1;
                RenderingUtils.ReAllocateHandleIfNeeded(ref output, desc, FilterMode.Bilinear,
                    TextureWrapMode.Clamp, name: "Reticle background");
                var destination = graph.ImportTexture(output);
                var parameters = new RenderGraphUtils.BlitMaterialParameters(resources.activeColorTexture, destination,
                    Blitter.GetBlitMaterial(TextureDimension.Tex2D), 0);
                using (var builder = graph.AddBlitPass(parameters, "Copy background for reticle contrast", returnBuilder:true))
                {
                    builder.SetGlobalTextureAfterPass(destination, Shader.PropertyToID("_ReticleSceneColor"));
                    builder.AllowPassCulling(false);
                }
                Shader.SetGlobalVector("_ReticleViewportSize",new Vector4(Screen.width,Screen.height,0,0));
            }
            public void Release() { output?.Release(); output = null; }
        }
        CopyPass copy;
        public override void Create() { copy?.Release(); copy = new CopyPass(); }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            if (data.cameraData.cameraType == CameraType.Game && data.cameraData.camera.CompareTag("MainCamera"))
                renderer.EnqueuePass(copy);
        }
        protected override void Dispose(bool disposing) { copy?.Release(); copy = null; }
    }
}
