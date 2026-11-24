Shader "PuzzleApple/V5/Lesson Ink"
{
    Properties
    {
        _BaseColor("Light",Color)=(1,1,1,1)
        _BaseMap("Symbol",2D)="white"{}
        _DrawWake("Draw star trail",Float)=0
        _StrokeSeed("Trail variation",Float)=0
        _WakeEnd("Trail mask limit",Range(0,1))=1
        _Intensity("Relative brightness",Float)=1
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST, _BaseColor;
            float _DrawWake,_StrokeSeed,_WakeEnd,_Intensity;
            CBUFFER_END
            struct Input { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Output { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float worldHeight:TEXCOORD1; half4 color:COLOR; };
            Output Vert(Input v){
                Output o;
                float3 positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(positionWS);
                o.worldHeight=GetAbsolutePositionWS(positionWS).y;
                o.uv=v.uv;o.color=v.color;return o;
            }
            float Noise(float seed){
                return frac(sin(seed*12.9898)*4375.3);
            }
            float Wake(float2 uv,float worldHeight){
                float down=1-uv.y;
                float aa=max(.002,fwidth(uv.x)*.55);
                float light=0;
                [unroll] for(int n=0;n<4;n++){
                    float seed=_StrokeSeed+n*17.3;
                    float side=n==0?0:(n-2.2)*.22+.08*(Noise(seed)-.5);
                    float end=_WakeEnd*(n==0?1:lerp(.36,.96,Noise(seed+2)));
                    float start=n==0?0:.035+.16*Noise(seed+6);
                    float axis=.5+side*(.22+.78*smoothstep(0,.52,down));
                    float width=(n==0?.056:.029)*lerp(1,.53,down);
                    float filament=(1-smoothstep(max(0,width-aa),width+aa,abs(uv.x-axis)))*saturate(width/aa);
                    // The star carries the silhouette. Its filaments reveal bands at fixed
                    // heights in the sky as the tip passes, even when the camera rides with it.
                    float along=worldHeight*lerp(.85,1.85,Noise(seed+3))+seed;
                    along+=.26*sin(along*1.37+seed);
                    float bandSeed=seed+floor(along)*4.7;
                    float dash=frac(along);
                    float softness=clamp(fwidth(along)*.65,.025,.14);
                    float begin=lerp(.03,.24,Noise(bandSeed));
                    float finish=lerp(.53,.96,Noise(bandSeed+1.9));
                    float broken=smoothstep(begin-softness,begin+softness,dash)*
                        (1-smoothstep(finish-softness,finish+softness,dash));
                    float fade=(1-smoothstep(end*.48,end,down))*smoothstep(start,start+.018,down);
                    light+=filament*broken*fade*(n==0?.78:.39);
                }
                return saturate(light);
            }
            half4 Frag(Output i):SV_Target {
                half4 ink=_BaseColor*i.color;
                ink.rgb*=_Intensity;
                if(_DrawWake>.5)ink.a*=Wake(i.uv,i.worldHeight);
                else ink.a*=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv*_BaseMap_ST.xy+_BaseMap_ST.zw).a;
                return ink;
            }
            ENDHLSL
        }
    }
}
