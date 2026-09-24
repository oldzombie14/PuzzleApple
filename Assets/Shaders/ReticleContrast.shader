Shader "PuzzleApple/Reticle Contrast"
{
    Properties
    {
        [PerRendererData] _MainTex ("Icon alpha", 2D) = "white" {}
        _ContrastThreshold ("Background brightness threshold", Range(0,1)) = 0.45
        _AlphaCutoff ("Ignore faint source pixels", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D_X(_ReticleSceneColor); SAMPLER(sampler_ReticleSceneColor);
            float4 _ReticleViewportSize;
            float _ContrastThreshold;
            float _AlphaCutoff;
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv; o.color = v.color;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float2 screenUV = i.positionCS.xy / max(_ReticleViewportSize.xy, float2(1,1));
                TransformNormalizedScreenUV(screenUV);
                // The screen-space overlay projection has the opposite vertical orientation on D3D.
                #if UNITY_UV_STARTS_AT_TOP
                screenUV.y = 1.0 - screenUV.y;
                #endif
                float3 background = SAMPLE_TEXTURE2D_X(_ReticleSceneColor,sampler_ReticleSceneColor,screenUV).rgb;
                float ink = 1.0 - step(_ContrastThreshold,dot(background,float3(0.2126,0.7152,0.0722)));
                // The supplied PNG's RGB is dark; only its alpha defines the icon silhouette.
                half alpha = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a * i.color.a;
                clip(alpha - _AlphaCutoff);
                return half4(ink,ink,ink,alpha);
            }
            ENDHLSL
        }
    }
}
