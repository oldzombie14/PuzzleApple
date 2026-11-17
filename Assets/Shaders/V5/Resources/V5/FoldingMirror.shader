Shader "PuzzleApple/V5/Folding Mirror"
{
    Properties
    {
        _ReflectionTex("Reflection",2D)="white"{}
        _ReflectionReady("Ready",Float)=0
        _Tint("Reflection tint",Color)=(1,1,1,1)
        _BaseColor("Frame",Color)=(.15,.16,.16,1)
        _MirrorGlass("Reflecting surface",Float)=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest"}
        Pass
        {
            Tags {"LightMode"="UniversalForwardOnly"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float4 _PanelSink, _Tint, _BaseColor;
            float _PanelOpacity, _PanelGroundY, _PreserveMeshDepth, _MirrorGlass, _ReflectionReady;
            #include "../../PanelCut.hlsl"
            TEXTURE2D(_ReflectionTex); SAMPLER(sampler_ReflectionTex);
            struct Varyings { float4 cs:SV_POSITION; float3 ws:TEXCOORD0; float4 screen:TEXCOORD1; float3 normal:TEXCOORD2; };
            Varyings Vert(float4 position:POSITION,float3 normal:NORMAL){
                Varyings o;o.ws=TransformObjectToWorld(position.xyz);o.cs=GroundDepth(TransformWorldToHClip(o.ws),o.ws);
                o.screen=ComputeScreenPos(o.cs);o.normal=TransformObjectToWorldNormal(normal);return o;
            }
            half4 Frag(Varyings i):SV_Target{
                ClipPanel(i.ws.xz);
                if(_MirrorGlass>.5){
                    half3 reflection=SAMPLE_TEXTURE2D(_ReflectionTex,sampler_ReflectionTex,i.screen.xy/i.screen.w).rgb;
                    return half4(lerp(half3(.72,.74,.74),reflection*_Tint.rgb,_ReflectionReady),1);
                }
                Light key=GetMainLight();
                half3 normal=normalize(i.normal);
                return half4(_BaseColor.rgb*(SampleSH(normal)+key.color*saturate(dot(normal,key.direction))),1);
            }
            ENDHLSL
        }
    }
}
