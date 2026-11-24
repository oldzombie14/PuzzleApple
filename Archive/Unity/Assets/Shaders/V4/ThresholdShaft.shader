Shader "PuzzleApple/V4/ThresholdShaft"
{
    Properties { _Color("Color",Color)=(.45,.65,.8,.12) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            CBUFFER_END
            Varyings vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.uv=v.uv; return o; }
            half4 frag(Varyings i):SV_Target
            {
                float edge=pow(saturate(sin(i.uv.x*3.14159265)),2);
                float end=smoothstep(0,.15,i.uv.y)*smoothstep(1,.8,i.uv.y);
                return half4(_Color.rgb,_Color.a*edge*end);
            }
            ENDHLSL
        }
    }
}
