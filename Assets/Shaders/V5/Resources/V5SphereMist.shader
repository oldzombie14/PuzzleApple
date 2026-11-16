Shader "PuzzleApple/V5/Sphere Mist"
{
    Properties { _Glow("Local scattering", Float) = .42 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "DisableBatching"="True" }
        Pass
        {
            Blend One One
            ColorMask RGB
            Cull Front
            ZWrite Off
            ZTest Always
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            float _Glow;
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionOS : TEXCOORD0; };
            Varyings vert(float3 positionOS : POSITION)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(positionOS);
                o.positionOS = positionOS;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                // Integrate scattering through a unit-diameter volume up to the first opaque surface.
                float3 eye = TransformWorldToObject(GetCameraPositionWS());
                float3 ray = normalize(i.positionOS-eye);
                float mid = -dot(eye,ray);
                float3 closest = eye + ray*mid;
                float radiusSq = dot(closest,closest);
                float chord = sqrt(max(0,.25-radiusSq));
                float start = max(0,mid-chord);
                float end = mid+chord;
                float2 uv = GetNormalizedScreenSpaceUV(i.positionCS);
                float depth = SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                    depth = lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
                #endif
                float3 hit = TransformWorldToObject(ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP));
                end = min(end,dot(hit-eye,ray));
                float stepSize = max(0,end-start)/8;
                float sum = 0;
                [unroll] for(int k=0;k<8;k++)
                {
                    float3 p = eye + ray*(start+(k+.5)*stepSize);
                    float density = saturate(1-4*dot(p,p));
                    sum += density*density*density;
                }
                return half4((_Glow*stepSize*sum).xxx,0);
            }
            ENDHLSL
        }
    }
}
