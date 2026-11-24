Shader "PuzzleApple/V5/Planar Mirror"
{
    Properties
    {
        _ReflectionTex ("Reflection", 2D) = "white" {}
        _Tint ("Glass tint", Color) = (0.96, 0.98, 0.98, 1)
        _ReflectionReady ("Reflection ready", Float) = 0
        _LessonVisibility ("Visibility", Range(0,1)) = 1
        [HideInInspector] _DepthClip ("Depth clipping", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "Mirror"
            Tags { "LightMode"="UniversalForward" }
            Cull Back ZWrite On
            ZClip [_DepthClip]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_ReflectionTex); SAMPLER(sampler_ReflectionTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _ReflectionReady;
                float _LessonVisibility;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 screen : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screen = ComputeScreenPos(output.positionCS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.screen.xy / input.screen.w;
                float grain = frac(dot(floor(input.positionCS.xy), float2(.75487766,.56984029)));
                clip(_LessonVisibility-grain-.00001);
                half3 reflected = SAMPLE_TEXTURE2D(_ReflectionTex, sampler_ReflectionTex, uv).rgb;
                return half4(lerp(half3(.72,.74,.74), reflected * _Tint.rgb, _ReflectionReady), 1);
            }
            ENDHLSL
        }
    }
}
