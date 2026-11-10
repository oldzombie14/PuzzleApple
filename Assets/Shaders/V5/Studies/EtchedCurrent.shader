Shader "PuzzleApple/V5/Studies/Etched Current"
{
    Properties
    {
        _Paper("Line tone",Color)=(.9,.9,.9,1)
        _Ink("Solid tone",Color)=(.045,.045,.045,1)
        _LineWidth("Line pixels",Range(.3,2))=.7
        _Frequency("Contour count",Range(3,24))=10
        _Speed("Evolution speed",Range(-1,1))=.13
        _Curvature("Flow curvature",Range(0,1))=.3
        _Coverage("Local coverage",Range(.1,1))=.65
        _StudyTime("Study clock",Float)=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
        Pass
        {
            Tags {"LightMode"="SRPDefaultUnlit"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "StudyFlowNoise.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Paper,_Ink;
            float _LineWidth,_Frequency,_Speed,_Curvature,_Coverage,_StudyTime;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
            struct V {float4 positionCS:SV_POSITION;float3 p:TEXCOORD0;float3 n:TEXCOORD1;};
            V Vert(A v){V o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.p=v.positionOS.xyz;o.n=v.normalOS;return o;}
            half4 Frag(V i):SV_Target
            {
                float3 n=abs(normalize(i.n));float2 p=n.x>.5?i.p.zy:n.y>.5?i.p.xz:i.p.xy;
                float2 q=p*1.65,gradient;
                float time=_StudyTime*_Speed;
                // Analytic gradients rotate over time, then drive a short curl backtrace.
                // This changes the field itself; no sampled image or stretched texture.
                [unroll]for(int k=0;k<3;k++)
                {
                    StudyFlowNoise(q+float2(k*.13,0),time-k*.08,gradient);
                    q-=float2(gradient.y,-gradient.x)*(_Curvature*.055);
                }
                float noise=StudyFlowNoise(q,time,gradient);
                float coordinate=(q.y*.7+q.x*.24+noise*_Curvature*.3)*_Frequency-time*.16;
                float pixels=abs(frac(coordinate+.5)-.5)/max(fwidth(coordinate),.0001);
                float strokes=1-smoothstep(_LineWidth*.5-.35,_LineWidth*.5+.65,pixels);
                float margin=min(.5-abs(p.x),.5-abs(p.y));
                float crop=smoothstep(.045,.09,margin);
                float diagonal=p.x+p.y*.35;
                float region=1-smoothstep(_Coverage*.5-.025,_Coverage*.5+.025,abs(diagonal));
                float envelope=.5+.5*sin(q.x*2.3-q.y*.6+time*.6);
                float field=strokes*crop*region*lerp(.3,.85,envelope);
                float2 e=(.5-abs(p))/max(fwidth(p),.00001);
                float rim=(1-smoothstep(.1,1.1,min(e.x,e.y)))*.48;
                return half4(lerp(_Ink.rgb,_Paper.rgb,max(field,rim)),1);
            }
            ENDHLSL
        }
    }
}
