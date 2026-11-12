Shader "PuzzleApple/V5/Floor Portal"
{
    Properties
    {
        _BaseMap("Outdoor view", 2D) = "black" {}
        _Shimmer("Air refraction", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float _Shimmer;
            struct Varyings { float4 positionCS : SV_POSITION; float4 screen : TEXCOORD0; float2 aperture : TEXCOORD1; };
            Varyings Vert(float3 positionOS : POSITION, float2 uv : TEXCOORD0)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(positionOS);
                o.screen = ComputeScreenPos(o.positionCS);
                o.aperture = uv;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.screen.xy / i.screen.w;
                float2 p = i.aperture * 2 - 1;
                float rim = smoothstep(.5, .95, max(abs(p.x), abs(p.y)));
                uv += .002 * _Shimmer * rim * sin(p.yx * 9 + _Time.y * 7);
                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, saturate(uv));
            }
            ENDHLSL
        }
    }
}
