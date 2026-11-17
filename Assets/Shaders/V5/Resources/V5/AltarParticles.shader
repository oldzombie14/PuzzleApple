Shader "PuzzleApple/V5/Altar Particles"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+56" "RenderType"="Transparent" }
        Pass
        {
            ZWrite Off Cull Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Varyings { float4 clip:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            Varyings Vert(float4 p:POSITION,float2 uv:TEXCOORD0,half4 color:COLOR)
            {
                Varyings o;o.clip=TransformObjectToHClip(p.xyz);o.uv=2*uv-1;o.color=color;return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 q=abs(i.uv);
                float d=q.x/.58+q.y;
                float aa=max(.015,fwidth(d));
                return half4(.008,.008,.008,(1-smoothstep(1-aa,1+aa,d))*i.color.a);
            }
            ENDHLSL
        }
    }
}
