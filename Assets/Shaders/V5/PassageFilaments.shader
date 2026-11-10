Shader "PuzzleApple/V5/PassageFilaments"
{
    Properties
    {
        _Color("Line color",Color)=(1,1,1,1)
        _Visibility("Visibility",Range(0,1))=1
        _FlowSpeed("Straight flow (metres per second)",Float)=0.55
        _RepeatLength("Segment spacing (metres)",Float)=1.2
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Color;float _Visibility,_FlowSpeed,_RepeatLength;
            CBUFFER_END
            struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;return o;}
            half4 frag(V i):SV_Target
            {
                // Fixed parallel lanes; only segment position changes, never its direction.
                float across=i.uv.x*3;
                float lane=floor(across);
                float aa=max(fwidth(across),.001);
                float stroke=1-smoothstep(.11-aa,.11+aa,abs(frac(across)-.5));
                float lengthWS=length(float3(unity_ObjectToWorld._m01,unity_ObjectToWorld._m11,unity_ObjectToWorld._m21));
                float phase=frac((i.uv.y*lengthWS-_Time.y*_FlowSpeed)/max(.1,_RepeatLength)+lane*.31);
                float segment=smoothstep(.02,.06,phase)*(1-smoothstep(.56,.60,phase));
                float ends=smoothstep(0,.018,i.uv.y)*smoothstep(0,.018,1-i.uv.y);
                return half4(_Color.rgb,stroke*(.22+.78*segment)*ends*_Visibility*_Color.a);
            }
            ENDHLSL
        }
    }
}
