Shader "PuzzleApple/V5/Carved Door Inscription"
{
    Properties { [HDR] _GlyphEmission("Charged inscription", Color)=(0,0,0,0) _MainTex("Word mask",2D)="white"{} [HideInInspector] _ExposureGain("Stone exposure compensation",Float)=1 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+1" }
        Pass
        {
            Cull Off ZWrite Off
            // Add the lit engraving while retaining the uncharged cut in the stone.
            Blend One SrcAlpha, Zero One
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float _ExposureGain;
            float4 _GlyphEmission;
            struct Input { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Output { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Output Vert(Input i){Output o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;return o;}
            half4 Frag(Output i):SV_Target
            {
                half mask=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a;
                return half4(_GlyphEmission.rgb*mask,lerp(1,.34h*_ExposureGain,mask));
            }
            ENDHLSL
        }
    }
}
