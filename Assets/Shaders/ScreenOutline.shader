Shader "PuzzleApple/Screen Outline"
{
    Properties
    {
        _OutlineColor("Outline Color",Color)=(0,0,0,1)
        _OutlineWidth("Outline Width (pixels)",Range(0,8))=2
        _NormalThreshold("Normal Edge Threshold",Range(0.05,1))=0.25
        _DepthThreshold("Relative Depth Threshold",Range(0.001,0.2))=0.025
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _OutlineColor;
            float _OutlineWidth,_NormalThreshold,_DepthThreshold;
            CBUFFER_END
            half4 Frag(Varyings i):SV_Target
            {
                float2 uv=i.texcoord;
                half4 color=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv);
                if(_OutlineWidth<=0) return color;
                float2 offset=_OutlineWidth*.5/_ScaledScreenParams.xy;
                float2 dirs[4]={float2(1,0),float2(0,1),float2(.707,.707),float2(.707,-.707)};
                float edge=0;
                [unroll] for(int k=0;k<4;k++)
                {
                    float2 a=saturate(uv+dirs[k]*offset), b=saturate(uv-dirs[k]*offset);
                    float da=LinearEyeDepth(SampleSceneDepth(a),_ZBufferParams);
                    float db=LinearEyeDepth(SampleSceneDepth(b),_ZBufferParams);
                    float3 na=SampleSceneNormals(a),nb=SampleSceneNormals(b);
                    float normalEdge=step(_NormalThreshold,1-dot(na,nb));
                    // Scale by view angle to avoid drawing lines on continuous grazing surfaces.
                    float3 view=normalize(GetCameraPositionWS()-ComputeWorldSpacePosition(uv,SampleSceneDepth(uv),UNITY_MATRIX_I_VP));
                    float grazing=max(.1,abs(dot(na,view)));
                    float depthEdge=step(_DepthThreshold,abs(da-db)*grazing/max(min(da,db),.01));
                    edge=max(edge,max(normalEdge,depthEdge));
                }
                return lerp(color,_OutlineColor,edge*_OutlineColor.a);
            }
            ENDHLSL
        }
    }
}
