Shader "PuzzleApple/V5/Corridor Star"
{
    Properties
    {
        _BaseColor("Tint and opacity", Color)=(0,0,0,1)
        [HDR] _EmissionColor("Emission", Color)=(1,1,1,1)
        _FogInfluence("Fog influence",Range(0,1))=1
        _FacetStrength("Facet shading", Range(0,1))=1
        [HideInInspector] _CorridorAxis("Corridor depth plane", Vector)=(0,0,0,0)
        [HideInInspector] _CorridorLength("Visible corridor length", Float)=1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            ZWrite Off ZTest LEqual Cull Back
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor, _EmissionColor, _CorridorAxis;
                float _CorridorLength, _FacetStrength, _FogInfluence;
            CBUFFER_END
            struct Varyings { float4 positionCS:SV_POSITION; float depth:TEXCOORD0; half fog:TEXCOORD1; half tone:TEXCOORD2; };
            Varyings vert(float4 positionOS:POSITION, float3 normalOS:NORMAL)
            {
                Varyings o;
                float3 world=TransformObjectToWorld(positionOS.xyz);
                o.positionCS=TransformWorldToHClip(world);
                o.depth=dot(world,_CorridorAxis.xyz)+_CorridorAxis.w;
                o.fog=ComputeFogFactor(o.positionCS.z);
                float3 normal = TransformObjectToWorldNormal(normalOS);
                float light = 1.55*normal.y + dot(normal.xz,float2(-.42,-.25));
                float glow = dot(_EmissionColor.rgb,float3(.2126,.7152,.0722)) * _BaseColor.a;
                float contrast = lerp(.26,.025,smoothstep(.35,1.4,glow)) * _FacetStrength;
                o.tone = lerp(1,lerp(.12,1.25,smoothstep(-.7,.8,light)),contrast);
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                clip(min(i.depth,_CorridorLength-i.depth));
                half3 color=_BaseColor.rgb+_EmissionColor.rgb*i.tone;
                return half4(lerp(color,MixFog(color,i.fog),_FogInfluence),_BaseColor.a);
            }
            ENDHLSL
        }
    }
}
