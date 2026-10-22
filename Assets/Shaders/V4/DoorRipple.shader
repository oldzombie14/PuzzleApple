Shader "PuzzleApple/V4/DoorRipple"
{
    Properties { [HDR] _Color ("Glow", Color) = (2,2,2,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            CBUFFER_END
            Varyings vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.uv=v.uv; o.color=v.color; return o; }
            half4 frag(Varyings i):SV_Target
            {
                float d = abs(i.uv.y-.5)*2;
                float variance = .012 + pow(fwidth(d), 2);
                float core = exp(-d*d/variance) * sqrt(.012/variance);
                float glow = exp(-d*d*6)*.16 + core*.84;
                return half4(_Color.rgb, glow*i.color.a*_Color.a);
            }
            ENDHLSL
        }
    }
}
