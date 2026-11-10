Shader "PuzzleApple/V5/Toon Passage"
{
    Properties
    {
        _BaseColor("Flat white",Color)=(1,1,1,1)
        _ShadeColor("Secondary face",Color)=(0.72,0.72,0.72,1)
        _ShadowColor("Dark face",Color)=(0.42,0.42,0.42,1)
        _InkColor("Edge ink",Color)=(0.035,0.035,0.035,1)
        _EdgeWidth("Edge width in pixels",Range(0,3))=0.8
        _FrontWhite("White front faces",Range(0,1))=1
        _Dissolve("Door dissolve",Range(0,1))=0
        _DoorInset("Door outline clearance",Range(0,.1))=0
        _DoorBackBrightness("Room-side brightness",Range(0,1))=0
        _DoorBackColor("Room-side wall color",Color)=(1,1,1,1)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor,_ShadeColor,_ShadowColor,_InkColor,_DoorBackColor;
            float _EdgeWidth,_FrontWhite,_Dissolve,_DoorInset,_DoorBackBrightness;
            CBUFFER_END
            struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;};
            struct V{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 local:TEXCOORD1;float3 normalOS:TEXCOORD2;float3 normalWS:TEXCOORD3;float fog:TEXCOORD4;};
            V Vert(A v){V o;o.positionWS=TransformObjectToWorld(v.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);o.local=v.positionOS.xyz;o.normalOS=v.normalOS;o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.fog=ComputeFogFactor(o.positionCS.z);return o;}
            float Hash(float3 p)
            {
                p=frac(p*.1031);p+=dot(p,p.yzx+33.33);
                return frac((p.x+p.y)*p.z);
            }
            float Noise(float3 p)
            {
                float3 c=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(lerp(Hash(c),Hash(c+float3(1,0,0)),f.x),
                                 lerp(Hash(c+float3(0,1,0)),Hash(c+float3(1,1,0)),f.x),f.y),
                            lerp(lerp(Hash(c+float3(0,0,1)),Hash(c+float3(1,0,1)),f.x),
                                 lerp(Hash(c+float3(0,1,1)),Hash(c+float3(1,1,1)),f.x),f.y),f.z);
            }
            half4 Frag(V i):SV_Target
            {
                // The room face fills the entire flush opening and matches Impact Floor walls.
                // Only the corridor face has clearance around its visible fine-line border.
                bool roomFace=_DoorInset>0&&i.normalOS.z>.5;
                if(_DoorInset>0&&!roomFace)clip(.5-max(abs(i.local.x),abs(i.local.y))-_DoorInset);
                // Clip only the door's property block. Path defaults remain fully opaque.
                // Progress drives the entire pattern, so split/resume and reversal retain it exactly.
                if(_Dissolve>0)
                {
                    float3 p=i.local*float3(14,15,1.2);
                    float n=Noise(p)*.7+Noise(p*2.13+7.1)*.3;
                    clip(n-lerp(-.02,1.02,_Dissolve));
                }
                if(roomFace)return half4(_DoorBackColor.rgb*_DoorBackBrightness,1);
                // Art-directed local key light: stable on all four random wall orientations
                // and independent of camera position. Three matte, discrete face values.
                float light=dot(normalize(i.normalOS),normalize(float3(-.35,.8,-.48)));
                float3 color=lerp(_ShadowColor.rgb,_ShadeColor.rgb,step(.18,light));
                // Upward and front-facing planes stay white; depth is carried by side faces.
                color=lerp(color,_BaseColor.rgb,max(step(.65,light),_FrontWhite*step(.45,light)));
                float3 a=abs(i.normalOS);float2 q=a.y>.5?i.local.xz:(a.x>.5?i.local.zy:i.local.xy);
                float d=min(.5-abs(q.x),.5-abs(q.y));
                float edge=1-smoothstep(0,max(fwidth(d)*_EdgeWidth,.000001),d);
                return half4(MixFog(lerp(color,_InkColor.rgb,edge),i.fog),1);
            }
            ENDHLSL
        }
    }
}
