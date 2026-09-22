Shader "PuzzleApple/Two Tone"
{
    Properties
    {
        _LitColor("Sunlit Color", Color) = (0.82,0.81,0.78,1)
        _ShadowColor("Shadow Color", Color) = (0.51,0.53,0.56,1)
        _Threshold("Light Threshold", Range(-1,1)) = 0.2
        _CastShadowStrength("Received Shadow Strength", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        half4 _LitColor, _ShadowColor;
        float _Threshold, _CastShadowStrength;
        CBUFFER_END
        ENDHLSL
        Pass
        {
            Name "TwoTone"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct V { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; };
            V Vert(A i) { V o; o.positionWS=TransformObjectToWorld(i.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.positionWS); o.normalWS=TransformObjectToWorldNormal(i.normalOS); return o; }
            half4 Frag(V i):SV_Target
            {
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                float4 sc=ComputeScreenPos(TransformWorldToHClip(i.positionWS));
                #else
                float4 sc=TransformWorldToShadowCoord(i.positionWS);
                #endif
                Light light=GetMainLight(sc);
                float lit=step(_Threshold,dot(normalize(i.normalWS),light.direction));
                lit*=step(0.5,lerp(1,light.shadowAttenuation,_CastShadowStrength));
                return lerp(_ShadowColor,_LitColor,lit);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex VertDepth
            #pragma fragment FragDepth
            float4 VertDepth(float4 p:POSITION):SV_POSITION { return TransformObjectToHClip(p.xyz); }
            half4 FragDepth():SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex VertN
            #pragma fragment FragN
            struct A { float4 p:POSITION; float3 n:NORMAL; };
            struct V { float4 p:SV_POSITION; float3 n:TEXCOORD0; };
            V VertN(A i) { V o; o.p=TransformObjectToHClip(i.p.xyz); o.n=TransformObjectToWorldNormal(i.n); return o; }
            half4 FragN(V i):SV_Target { return half4(normalize(i.n),0); }
            ENDHLSL
        }
    }
}
