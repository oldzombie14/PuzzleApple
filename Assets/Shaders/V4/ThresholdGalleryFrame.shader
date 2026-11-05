Shader "PuzzleApple/V4/ThresholdGalleryFrame"
{
    Properties
    {
        [HDR] _EmissionColor ("Glow multiplier", Color) = (1,1,1,1)
        [HideInInspector] _SolidProgress ("Solid formation", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _EmissionColor;
                float _SolidProgress;
            CBUFFER_END
            struct Varyings { float4 clip : SV_POSITION; float3 local : TEXCOORD0; };
            Varyings Vert(float3 position : POSITION)
            {
                Varyings output;
                output.clip = TransformObjectToHClip(position);
                output.local = position;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                if (_SolidProgress < 1)
                {
                    float noise = frac(52.9829189 * frac(dot(floor(input.clip.xy), float2(.06711056, .00583715))));
                    clip(_SolidProgress - max(noise, .00001));
                }
                float2 q = input.local.xz;
                float edge = min(.13363 - abs(q.x), .18079 - abs(q.y));
                float along = abs(q.x) > .123 ? q.y : q.x;
                float chip = sin(along * 73) + .75 * sin(along * 191) + .45 * sin(along * 337);
                float dark = step(input.local.y, .00122);
                if (edge > .0015 && edge < .0021 && chip >= -.25) dark = 1;
                if (edge > .0109 && edge < .0115 && chip >= -.25) dark = 0;
                half3 color = lerp(half3(1.15,.65,.32), half3(.62,.205,.052), dark);
                return half4(color * _EmissionColor.rgb, 1);
            }
            ENDHLSL
        }
    }
}
