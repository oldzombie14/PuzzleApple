Shader "PuzzleApple/V5/Chamber Lamp"
{
    Properties
    {
        _BaseColor("Surface tint", Color)=(1,1,1,1)
        [HDR] _EmissionColor("Soft neutral emission", Color)=(1,1,1,1)
        _FaceLevels("Local X / Y / Z face brightness", Vector)=(0.52,0.78,1.1,0)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            Name "ChamberLamp"
            Tags {"LightMode"="UniversalForward"}
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor, _EmissionColor, _FaceLevels;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
            struct V {float4 positionCS:SV_POSITION;float3 normalOS:TEXCOORD0;};
            V vert(A i) {V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.normalOS=i.normalOS;return o;}
            half4 frag(V i):SV_Target
            {
                // A luminous solid, not six equally clipped white faces. Object-space
                // values keep the existing cube readable as the player moves around it.
                float3 n=abs(normalize(i.normalOS));
                float face=dot(n,_FaceLevels.xyz)/max(dot(n,float3(1,1,1)),.001);
                return half4(_EmissionColor.rgb*face+_BaseColor.rgb*.025,1);
            }
            ENDHLSL
        }
    }
}
