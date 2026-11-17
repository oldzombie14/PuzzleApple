Shader "PuzzleApple/V5/Ascent Veil"
{
    Properties { _Amount("Whiteness",Range(0,1))=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+50" "RenderType"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _Amount;
            float4 Vert(float4 positionOS:POSITION):SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half4 Frag():SV_Target { return half4(1,1,1,_Amount); }
            ENDHLSL
        }
    }
}
