Shader "PuzzleApple/V4/SkyInscription"
{
    Properties { [PerRendererData] _MainTex ("Symbol alpha", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            Varyings Vert(Attributes v)
            {
                Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;o.color=v.color;return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                return half4(i.color.rgb,SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a*i.color.a);
            }
            ENDHLSL
        }
    }
}
