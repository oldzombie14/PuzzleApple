Shader "PuzzleApple/V4/ThresholdStarlight"
{
    Properties { [HDR] _Color("Glow", Color) = (3,3,3,1) }
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
            Varyings vert(Attributes i) { Varyings o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=i.uv; o.color=i.color; return o; }
            half4 frag(Varyings i):SV_Target
            {
                float2 p=(i.uv-.5)*2;
                float r=dot(p,p);
                float core=exp(-r*18);
                float halo=exp(-r*5)*.23;
                float rays=(exp(-abs(p.x)*45-abs(p.y)*5)+exp(-abs(p.y)*45-abs(p.x)*5))*.28;
                // Tail particles use a low red vertex channel: soft dust without the core or cross rays.
                float star=step(.5, i.color.r);
                float dust=exp(-r*6)*.65;
                float a=lerp(dust,core+halo+rays,star)*saturate((1-r)*3);
                return half4(_Color.rgb*i.color.rgb,a*i.color.a*_Color.a);
            }
            ENDHLSL
        }
    }
}
