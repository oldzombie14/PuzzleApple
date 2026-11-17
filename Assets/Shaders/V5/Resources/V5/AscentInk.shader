Shader "PuzzleApple/V5/Ascent Ink"
{
    Properties { [HDR]_Face("Stone",Color)=(1,1,1,1) _Edge("Edge",Color)=(.035,.035,.035,1) _Mask("Symbol contour",2D)="white"{} _MaskScale("Symbol mask scale",Float)=0 }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_Mask); SAMPLER(sampler_Mask);
            float4 _Face,_Edge; float _MaskScale;
            struct A {float4 pos:POSITION;float3 normal:NORMAL;};
            struct V {float4 pos:SV_POSITION;float3 normal:TEXCOORD0;float3 view:TEXCOORD1;float2 uv:TEXCOORD2;};
            V Vert(A i){V o;float3 world=TransformObjectToWorld(i.pos.xyz);o.pos=TransformWorldToHClip(world);o.view=GetWorldSpaceViewDir(world);o.normal=TransformObjectToWorldNormal(i.normal);o.uv=i.pos.xy*_MaskScale+.5;return o;}
            half4 Frag(V i):SV_Target {float facing=abs(dot(normalize(i.normal),normalize(i.view)));float2 w=max(float2(.009,.009),fwidth(i.uv)*1.3);
                float inside=min(min(SAMPLE_TEXTURE2D(_Mask,sampler_Mask,i.uv+float2(w.x,0)).a,SAMPLE_TEXTURE2D(_Mask,sampler_Mask,i.uv-float2(w.x,0)).a),
                                 min(SAMPLE_TEXTURE2D(_Mask,sampler_Mask,i.uv+float2(0,w.y)).a,SAMPLE_TEXTURE2D(_Mask,sampler_Mask,i.uv-float2(0,w.y)).a));
                return lerp(_Edge,_Face,smoothstep(.16,.56,facing)*inside);}
            ENDHLSL
        }
    }
}
