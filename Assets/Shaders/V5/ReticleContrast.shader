Shader "PuzzleApple/V5/Reticle Contrast"
{
    Properties
    {
        [PerRendererData] _MainTex ("Icon alpha", 2D) = "white" {}
        [HideInInspector] _ReticleSceneColor ("World image", 2D) = "black" {}
        _ContrastThreshold ("Background brightness threshold", Range(0,1)) = 0.45
        _AlphaCutoff ("Ignore faint source pixels", Range(0,1)) = 0
        [HideInInspector] _SrcBlend ("Source blend", Float) = 5
        [HideInInspector] _ScreenPointer ("Draw screen pointer", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        Blend [_SrcBlend] OneMinusSrcAlpha
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
            float _ScreenPointer;
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv; o.color = v.color;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                if (_ScreenPointer > .5)
                {
                    // Premultiplied coverage with OneMinusDstColor inverts the final UI pixel.
                    float distance = length(i.uv - .5);
                    float pixel = max(fwidth(distance), .0001);
                    float coverage = saturate((.5 - distance) / pixel + .5);
                    if (_ScreenPointer > 1.5)
                    {
                        float fill = saturate((.5 - distance) / pixel - .5);
                        return half4(fill,fill,fill,coverage)*i.color.a;
                    }
                    return half4(coverage,coverage,coverage,coverage)*i.color.a;
                }
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
