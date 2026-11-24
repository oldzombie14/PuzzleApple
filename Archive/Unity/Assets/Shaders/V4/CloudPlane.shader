Shader "PuzzleApple/V4/CloudPlane"
{
    Properties
    {
        _CloudMask("Cloud opacity (alpha)",2D)="black"{}
        _CloudColor("Cloud colour",Color)=(.95,.95,.91,1)
        _MistColor("Mist colour",Color)=(.80,.84,.82,1)
        _MistDensity("Mist density",Float)=.003
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_CloudMask); SAMPLER(sampler_CloudMask);
            CBUFFER_START(UnityPerMaterial)
                float4 _CloudColor;
                float4 _MistColor;
                float _MistDensity;
                float4x4 _CloudWorldToUV;
            CBUFFER_END
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; };
            Varyings vert(float4 positionOS:POSITION)
            {
                Varyings o;
                o.positionWS=TransformObjectToWorld(positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float2 uv=mul(_CloudWorldToUV,float4(i.positionWS,1)).xy+.5;
                float alpha=SAMPLE_TEXTURE2D(_CloudMask,sampler_CloudMask,uv).a;
                // Fade only in the far distance, before the finite mesh edge.
                float distanceToCamera=distance(i.positionWS,_WorldSpaceCameraPos);
                alpha*=1-smoothstep(3000,5000,distanceToCamera);
                // Softer aerial haze for the high layer, without a separate grey sheet.
                float mist=1-exp(-pow(distanceToCamera*_MistDensity*.25,2));
                return half4(lerp(_CloudColor.rgb,_MistColor.rgb,mist),alpha*_CloudColor.a*(1-mist));
            }
            ENDHLSL
        }
    }
}
