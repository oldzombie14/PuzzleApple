Shader "PuzzleApple/V4/ThresholdMist"
{
    Properties { _Color ("Mist tint", Color) = (0.85,0.89,0.95,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float4x4 _MistWorldToLocal;
            float _MistTime, _MistRadius, _MistDensity;
            float3 _MistAxes;
            CBUFFER_END
            float hash(float3 p)
            {
                p=frac(p*.1031); p+=dot(p,p.yzx+33.33);
                return frac((p.x+p.y)*p.z);
            }
            float noise(float3 p)
            {
                float3 a=floor(p),f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(lerp(hash(a),hash(a+float3(1,0,0)),f.x),
                    lerp(hash(a+float3(0,1,0)),hash(a+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(hash(a+float3(0,0,1)),hash(a+float3(1,0,1)),f.x),
                    lerp(hash(a+float3(0,1,1)),hash(a+1),f.x),f.y),f.z);
            }
            float density(float3 p)
            {
                float3 shaped=p/_MistAxes;
                float r=length(shaped)/_MistRadius;
                float3 drift=float3(_MistTime*.043,-_MistTime*.031,_MistTime*.027);
                float3 shapeWarp=float3(noise(p*.46+drift+3),
                    noise(p*.46-drift+17),noise(p*.46+drift.yzx+31))-.5;
                float warpedRadius=length(shaped+shapeWarp*2.5)/_MistRadius;
                // Narrow, shallow envelope with irregular lobes and gaps around the doorway.
                float envelope=exp(-warpedRadius*warpedRadius*2.1)
                    *(1-smoothstep(.5,.96,warpedRadius))*(1-smoothstep(.83,1,r));
                // Rotation around two axes moves texture through the volume, including its centre.
                float angle=_MistTime*.085+length(p)*.24;
                float s=sin(angle),c=cos(angle);
                p.xz=mul(float2x2(c,-s,s,c),p.xz);
                angle=_MistTime*.047;
                s=sin(angle); c=cos(angle);
                p.xy=mul(float2x2(c,-s,s,c),p.xy);
                float3 q=p*.83+float3(8.1,2.7,13.4)+drift;
                float3 warp=float3(noise(q*.65+drift),noise(q*.65-drift+12),noise(q*.65+drift.zxy+29))-.5;
                q+=warp*1.7;
                float broad=noise(q);
                float detail=noise(q*2.13+11-drift*.6)*.7+noise(q*4.3-7+drift*.4)*.3;
                float pockets=smoothstep(.36,.72,broad*.72+detail*.28);
                return envelope*(.008+pockets*3.1);
            }
            Varyings vert(Attributes v)
            {
                Varyings o; o.world=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.world); return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float3 origin=mul(_MistWorldToLocal,float4(_WorldSpaceCameraPos,1)).xyz-float3(0,3,0);
                float3 direction=normalize(mul((float3x3)_MistWorldToLocal,normalize(i.world-_WorldSpaceCameraPos)));
                float3 scaledOrigin=origin/_MistAxes, scaledDirection=direction/_MistAxes;
                float a=dot(scaledDirection,scaledDirection);
                float b=dot(scaledOrigin,scaledDirection);
                float h=b*b-a*(dot(scaledOrigin,scaledOrigin)-_MistRadius*_MistRadius);
                if(h<=0) return 0;
                float start=max(0,(-b-sqrt(h))/a), end=(-b+sqrt(h))/a;
                float2 uv=GetNormalizedScreenSpaceUV(i.positionCS);
                float depth=SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                    depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
                #endif
                float3 sceneWorld=ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP);
                float3 sceneLocal=mul(_MistWorldToLocal,float4(sceneWorld,1)).xyz-float3(0,3,0);
                end=min(end,dot(sceneLocal-origin,direction));
                if(end<=start) return 0;
                float stepSize=(end-start)/48;
                float transmission=1;
                float jitter=hash(float3(floor(i.positionCS.xy),0));
                [loop] for(int k=0;k<48;k++)
                {
                    float3 p=origin+direction*(start+(k+jitter)*stepSize);
                    float floorFade=smoothstep(-3,-2.4,p.y);
                    float extinction=density(p)*floorFade*_MistDensity*stepSize;
                    transmission*=exp(-extinction);
                }
                float alpha=(1-transmission)*_Color.a;
                return half4(_Color.rgb*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
