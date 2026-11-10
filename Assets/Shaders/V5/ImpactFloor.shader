Shader "PuzzleApple/V5/Impact Floor"
{
    Properties
    {
        _FloorColor("Lit surface",Color)=(1,1,1,1)
        _Brightness("Brightness",Range(0,1))=0
        _Plane("Plane: floor XZ / wall XY / wall ZY",Float)=0
        [NoScaleOffset] _MuralNormal("Carving normal RGB / cavity A (linear)",2D)="bump"{}
        _MuralEnabled("Mural enabled",Float)=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
        Pass
        {
            Tags {"LightMode"="UniversalForwardOnly"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A{float4 positionOS:POSITION;};
            struct V{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;};
            float4 _FloorColor,_Impacts[16],_Hole;float4x4 _WorldToFloor;
            float _Brightness,_RippleDuration,_RippleSpeed,_RingCount,_RingSpacing,_RingWidth,_RippleStrength,_Plane;
            TEXTURE2D(_MuralNormal);SAMPLER(sampler_MuralNormal);
            float4 _MuralRect;float3 _MuralLight;
            float _MuralEnabled,_MuralRelief,_MuralCavity;
            V Vert(A i){V o;o.positionWS=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);return o;}
            half4 Frag(V i):SV_Target
            {
                float3 local=mul(_WorldToFloor,float4(i.positionWS,1)).xyz;
                float2 p=_Plane<.5?local.xz:(_Plane<1.5?local.xy:local.zy);
                if(_Hole.w>.5)clip(max(abs(p.x-_Hole.x),abs(p.y-_Hole.y))-_Hole.z);
                float ink=0;
                for(int n=0;n<16;n++)
                {
                    float age=_Time.y-_Impacts[n].z;
                    if(_Impacts[n].w<.5||age<0||age>_RippleDuration)continue;
                    float distance=length(p-_Impacts[n].xy);
                    for(int ring=0;ring<4;ring++)
                    {
                        if(ring>=_RingCount)break;
                        float radius=age*_RippleSpeed-ring*_RingSpacing;
                        if(radius<0)continue;
                        float edge=abs(distance-radius);
                        float band=1-smoothstep(_RingWidth*.5,_RingWidth+fwidth(distance),edge);
                        ink=max(ink,band*(1-smoothstep(.35*_RippleDuration,_RippleDuration,age)));
                    }
                }
                float carving=1;
                if(_MuralEnabled>.5)
                {
                    float2 uv=(p-_MuralRect.xy)/_MuralRect.zw+.5;
                    float inside=step(0,uv.x)*step(uv.x,1)*step(0,uv.y)*step(uv.y,1);
                    float4 relief=SAMPLE_TEXTURE2D(_MuralNormal,sampler_MuralNormal,saturate(uv));
                    // Raw linear RGB preserves cavity in alpha; do not use Unity normal compression.
                    float3 normal=normalize(relief.rgb*2-1);
                    // Neutral wall remains exactly white. Only the sunken relief receives shading.
                    float shade=saturate(1+(dot(normal,_MuralLight)-_MuralLight.z)*_MuralRelief-relief.a*_MuralCavity);
                    carving=lerp(1,shade,inside);
                }
                return half4(_FloorColor.rgb*_Brightness*carving*(1-ink*_RippleStrength),1);
            }
            ENDHLSL
        }
    }
}
