Shader "PuzzleApple/V5/Star Halo"
{
    Properties {
        _Opacity("Opacity",Range(0,1))=.06
        _BaseColor("Visibility",Color)=(0,0,0,1)
        _EmissionColor("Emission",Color)=(1,1,1,1)
        _CorridorAxis("Depth plane",Vector)=(0,0,0,0)
        _CorridorLength("Length",Float)=1
    }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass {
            ZWrite Off Cull Off ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor,_EmissionColor,_CorridorAxis;
                float _Opacity,_CorridorLength;
            CBUFFER_END
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float depth:TEXCOORD1;};
            Varyings vert(float4 p:POSITION,float2 uv:TEXCOORD0) {
                Varyings o;float3 world=TransformObjectToWorld(p.xyz);
                o.positionCS=TransformWorldToHClip(world);o.uv=uv*2-1;
                o.depth=dot(world,_CorridorAxis.xyz)+_CorridorAxis.w;return o;
            }
            half4 frag(Varyings i):SV_Target {
                clip(min(i.depth,_CorridorLength-i.depth));
                float r=length(i.uv);float a=pow(saturate(1-r*r),3);
                return half4(_EmissionColor.rgb,_BaseColor.a*_Opacity*a);
            }
            ENDHLSL
        }
    }
}
