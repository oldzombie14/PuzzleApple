Shader "PuzzleApple/V5/Wall Vocabulary"
{
    Properties { [PerRendererData] _MainTex("Symbol",2D)="white"{} _Tint("Tint",Color)=(1,1,1,0) }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            half4 _Tint;
            V Vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;o.color=i.color;return o;}
            half4 Frag(V i):SV_Target{return half4(_Tint.rgb,SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a*_Tint.a);}
            ENDHLSL
        }
    }
}
