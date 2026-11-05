Shader "PuzzleApple/V4/DoorWeave"
{
    Properties
    {
        _BaseColor("Surface", Color) = (.12,.14,.17,1)
        [HDR] _EmissionColor("Surface emission", Color) = (.045,.055,.075,1)
        [HDR] _ThreadColor("Thread glow", Color) = (2,2.8,3.6,1)
        _SolidProgress("Solid formation", Range(0,1)) = 0
        _StitchSize("Stitch size", Float) = .22
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 door:TEXCOORD0; float3 normal:TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _EmissionColor, _ThreadColor;
            float _SolidProgress, _StitchSize;
            float4x4 _WorldToDoor;
            CBUFFER_END
            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 world=TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(world);
                o.door=mul(_WorldToDoor,float4(world,1)).xyz;
                o.normal=mul((float3x3)_WorldToDoor,TransformObjectToWorldNormal(i.normalOS));
                return o;
            }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            half4 frag(Varyings i):SV_Target
            {
                // Actual incoming strands remain on the frame; only fade in the solid here.
                float solid=_SolidProgress;
                float3 surface=_BaseColor.rgb*.55+_EmissionColor.rgb;
                return half4(surface,solid);
            }
            ENDHLSL
        }
    }
}
