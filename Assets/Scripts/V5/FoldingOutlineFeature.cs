using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace PuzzleApple.V5
{
    public sealed class FoldingOutlineFeature : ScriptableRendererFeature
    {
        OutlinePass pass;
        public override void Create()=>pass=new OutlinePass {renderPassEvent=RenderPassEvent.AfterRenderingOpaques};

        public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData renderingData)
        {
            var box=FoldingInterior.Active;
            if(!box||!box.room||box.room.Opened||box.room.Progress<=0||box.surfaces==null||box.surfaces.Length==0)return;
            var surface=box.surfaces[0].renderer;
            if(!surface||(renderingData.cameraData.camera.cullingMask&(1<<surface.gameObject.layer))==0)return;
            pass.material=surface.sharedMaterial;
            renderer.EnqueuePass(pass);
        }

        sealed class OutlinePass : ScriptableRenderPass
        {
            public Material material;
            static readonly ShaderTagId MaskTag=new ShaderTagId("P5FoldingMask");
            sealed class MaskData { public RendererListHandle list; }
            sealed class ContourData { public TextureHandle mask; public Material material; public int passIndex; }

            public override void RecordRenderGraph(RenderGraph graph,ContextContainer frame)
            {
                var resources=frame.Get<UniversalResourceData>();
                var camera=frame.Get<UniversalCameraData>();
                // The screen-space outline uses the intermediate game color and depth pair.
                if(resources.isActiveTargetBackBuffer||camera.camera.cameraType==CameraType.Reflection)return;
                var desc=graph.GetTextureDesc(resources.activeColorTexture);
                desc.name="Visible folding box";
                desc.colorFormat=GraphicsFormat.R8G8B8A8_UNorm;
                desc.clearBuffer=true;
                desc.clearColor=Color.clear;
                desc.filterMode=FilterMode.Point;
                var mask=graph.CreateTexture(desc);
                using(var builder=graph.AddRasterRenderPass<MaskData>("Folding box visible faces",out var data))
                {
                    var rendering=frame.Get<UniversalRenderingData>();
                    var draw=RenderingUtils.CreateDrawingSettings(MaskTag,rendering,camera,frame.Get<UniversalLightData>(),camera.defaultOpaqueSortFlags);
                    var filter=new FilteringSettings(RenderQueueRange.opaque);
                    data.list=graph.CreateRendererList(new RendererListParams(rendering.cullResults,draw,filter));
                    builder.UseRendererList(data.list);
                    builder.SetRenderAttachment(mask,0,AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture,AccessFlags.Read);
                    builder.SetRenderFunc(static (MaskData d,RasterGraphContext ctx)=>ctx.cmd.DrawRendererList(d.list));
                }
                using(var builder=graph.AddRasterRenderPass<ContourData>("Folding box silhouette",out var data))
                {
                    data.mask=mask;
                    data.material=material;
                    data.passIndex=material.FindPass("BoxContour");
                    builder.UseTexture(mask);
                    builder.SetRenderAttachment(resources.activeColorTexture,0,AccessFlags.ReadWrite);
                    builder.SetRenderFunc(static (ContourData d,RasterGraphContext ctx)=>
                        Blitter.BlitTexture(ctx.cmd,d.mask,new Vector4(1,1,0,0),d.material,d.passIndex));
                }
            }
        }
    }
}
