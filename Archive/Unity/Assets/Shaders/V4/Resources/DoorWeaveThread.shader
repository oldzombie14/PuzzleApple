Shader "PuzzleApple/V4/DoorWeaveThread"
{
    Properties { [HDR] _Color("Thread glow", Color) = (2,2.8,3.6,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            CBUFFER_END
            Varyings vert(Attributes i)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=i.uv; o.color=i.color; return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float edge=abs(i.uv.y*2-1);
                float feather=1-smoothstep(.12,1,edge);
                return half4(_Color.rgb*i.color.rgb,feather*i.color.a*_Color.a);
            }
            ENDHLSL
        }
    }
}
