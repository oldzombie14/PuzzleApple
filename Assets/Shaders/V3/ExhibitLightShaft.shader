Shader "PuzzleApple/V3/Exhibit Light Shaft"
{
    Properties
    {
        [HDR] _BeamColor("Scattered light",Color)=(1,1,1,1)
        _Density("Scattering density",Range(0,1))=.12
        _Radius("Outer radius at floor (metres)",Float)=3.8
        _SourceRadius("Source radius (metres)",Float)=.16
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "Local scattering"
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Front
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BeamColor;
                float _Density, _Radius, _SourceRadius;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(output.positionWS);return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 originWS=GetCameraPositionWS();
                float3 directionWS=normalize(input.positionWS-originWS);
                float3 originOS=TransformWorldToObject(originWS);
                float3 directionOS=mul((float3x3)unity_WorldToObject,directionWS);
                // The native cube bounds the volume; distances remain in world metres.
                float3 signDir=step(0,directionOS)*2-1;
                float3 inverse=signDir/max(abs(directionOS),1e-6);
                float3 a=(-.5-originOS)*inverse,b=(.5-originOS)*inverse;
                float3 lo=min(a,b),hi=max(a,b);
                float start=max(0,max(lo.x,max(lo.y,lo.z)));
                float end=min(hi.x,min(hi.y,hi.z));
                float2 uv=GetNormalizedScreenSpaceUV(input.positionCS);
                float depth=SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                    depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
                #endif
                float3 surface=ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP);
                end=min(end,dot(surface-originWS,directionWS));
                if(end<=start)return 0;
                const int steps=32;
                float stride=(end-start)/steps;
                float light=0;
                [unroll]for(int i=0;i<steps;i++)
                {
                    float3 p=originOS+directionOS*(start+(i+.5)*stride);
                    float height=saturate(p.z+.5);
                    float radius=lerp(_SourceRadius,_Radius,height);
                    float radial=length(p.xy)*(_Radius*2)/max(.01,radius);
                    float edge=exp(-3.5*radial*radial)*(1-smoothstep(.75,1,radial));
                    // Subtle low-frequency variation, with no animated glitter or dust sprites.
                    float haze=.96+.04*sin(p.x*13+p.z*23)*sin(p.y*11-p.z*17);
                    float cap=smoothstep(0,.018,height)*(1-smoothstep(.94,1,height));
                    light+=edge*haze*cap*stride/(1+height*height*2);
                }
                float scattering=1-exp(-light*_Density);
                return half4(_BeamColor.rgb*scattering,0);
            }
            ENDHLSL
        }
    }
}
