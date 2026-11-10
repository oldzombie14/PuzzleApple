Shader "PuzzleApple/V5/Studies/Rain Curtain"
{
    Properties
    {
        _Color("Rain",Color)=(.94,.94,.94,1)
        _HeadFade("Lower growing edge / fraction",Range(.05,.65))=.3
        _Spacing("Pulse spacing / volume height",Range(.5,2))=1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Color, _Center, _Area;
            float _StudyTime, _FallSpeed, _SpeedVariation, _Length, _Width, _TailFade,_HeadFade,_Spacing;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION;float2 corner:TEXCOORD0;float2 seed:TEXCOORD1; };
            struct Varyings { float4 positionCS:SV_POSITION;float3 coverage:TEXCOORD0;float4 motion:TEXCOORD1; };
            float Hash(uint n,uint stream)
            {
                uint state=n*747796405u+stream*2891336453u;
                uint word=((state>>((state>>28u)+4u))^state)*277803737u;
                return float((word>>22u)^word)*(1.0/4294967296.0);
            }
            Varyings Vert(Attributes v)
            {
                Varyings o;
                uint id=(uint)v.seed.x;
                float speed=_FallSpeed*lerp(1-_SpeedVariation,1+_SpeedVariation,Hash(id,9u));
                // Fixed vertical paths. Only opacity travels downward: no lateral motion.
                float2 placement=frac(float(id)*float2(.754877666,.569840296));
                float2 jitter=(float2(Hash(id,13u),Hash(id,80u))-.5)*.015;
                float2 origin=(frac(placement+jitter)-.5)*_Area.xz;
                float3 head=_Center.xyz+float3(origin.x,_Area.y*.5,origin.y);
                float3 tail=_Center.xyz+float3(origin.x,-_Area.y*.5,origin.y);
                float4 headCS=TransformObjectToHClip(head),tailCS=TransformObjectToHClip(tail);
                float safeHead=max(headCS.w,.001),safeTail=max(tailCS.w,.001);
                float2 projected=(headCS.xy/safeHead-tailCS.xy/safeTail)*_ScaledScreenParams.xy;
                float2 normal=normalize(float2(-projected.y,projected.x)+float2(.00001,0));
                float halfExtent=max(.15,_Width*.5)+.75;
                o.positionCS=lerp(tailCS,headCS,v.corner.y);
                o.positionCS.xy+=normal*(v.corner.x*halfExtent)*2/_ScaledScreenParams.xy*o.positionCS.w;
                float edge=min(.5-abs(origin.x/_Area.x),.5-abs(origin.y/_Area.z));
                float fade=smoothstep(0,.02,edge);
                fade*=step(.001,min(headCS.w,tailCS.w));
                o.coverage=float3(v.corner.x*halfExtent,v.corner.y,fade);
                o.motion=float4(speed,_Length*lerp(.65,1.35,Hash(id,15u)),_Area.y*_Spacing*lerp(.8,1.2,Hash(id,27u)),Hash(id,41u));
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float widthCoverage=saturate(_Width*.5+.5-abs(i.coverage.x));
                float down=(1-i.coverage.y)*_Area.y;
                float phase=frac((down-_StudyTime*i.motion.x)/i.motion.z+i.motion.w);
                float along=phase*i.motion.z/max(.01,i.motion.y);
                // Upper/trailing region fades out as the lower/leading region grows in.
                // The two edges are independently adjustable, with no texture sampling.
                float envelope=smoothstep(0,max(.05,_TailFade),along)*(1-smoothstep(1-_HeadFade,1,along));
                float birthDeath=smoothstep(0,.025,i.coverage.y)*smoothstep(0,.025,1-i.coverage.y);
                return half4(_Color.rgb,_Color.a*widthCoverage*envelope*birthDeath*i.coverage.z);
            }
            ENDHLSL
        }
    }
}
