Shader "PuzzleApple/V5/Studies/Architectural Ink"
{
    Properties
    {
        _Paper("Light tone",Color)=(.88,.88,.88,1)
        _Ink("Dark tone",Color)=(.075,.075,.075,1)
        _EdgeWidth("Edge pixels",Range(.25,2))=.65
        _FaceValue("Front value",Range(0,1))=.12
        _SideValue("Side value",Range(0,1))=.035
        _TopValue("Top value",Range(0,1))=.21
        _EdgeStrength("Edge strength",Range(0,1))=.6
        _PulseSpeed("Pulse speed",Range(-1,1))=.12
        _PulseAmount("Pulse amount",Range(0,1))=.2
        _DepthFade("Distance fade",Range(0,.05))=.012
        _StudyTime("Study clock",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Paper,_Ink;
            float _EdgeWidth,_FaceValue,_SideValue,_TopValue,_EdgeStrength,_PulseSpeed,_PulseAmount,_DepthFade,_StudyTime;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
            struct V {float4 positionCS:SV_POSITION;float3 p:TEXCOORD0;float3 n:TEXCOORD1;float distance:TEXCOORD2;float seed:TEXCOORD3;};
            V Vert(A v)
            {
                V o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.p=v.positionOS.xyz;o.n=v.normalOS;
                float3 world=TransformObjectToWorld(v.positionOS.xyz);o.distance=length(world-_WorldSpaceCameraPos);
                o.seed=frac(dot(TransformObjectToWorld(float3(0,0,0)),float3(.123,.37,.71)));return o;
            }
            half4 Frag(V i):SV_Target
            {
                float3 n=abs(normalize(i.n));
                float2 p=n.x>.5?i.p.zy:n.y>.5?i.p.xz:i.p.xy;
                float2 edge=(.5-abs(p))/max(fwidth(p),.00001);
                float edgeMask=1-smoothstep(_EdgeWidth-.5,_EdgeWidth+.5,min(edge.x,edge.y));
                float value=dot(n,float3(_SideValue,_TopValue,_FaceValue));
                float perimeter=(edge.x<edge.y?p.y*sign(p.x):-p.x*sign(p.y))+.5;
                float phase=frac(perimeter*.5-_StudyTime*_PulseSpeed+i.seed);
                float pulse=smoothstep(.7,.88,phase)*(1-smoothstep(.94,1,phase));
                float3 tone=lerp(_Ink.rgb,_Paper.rgb,value);
                tone=lerp(tone,_Paper.rgb,edgeMask*saturate(_EdgeStrength+_PulseAmount*pulse));
                tone=lerp(tone,_Ink.rgb,1-exp(-i.distance*_DepthFade));
                return half4(tone,1);
            }
            ENDHLSL
        }
    }
}
