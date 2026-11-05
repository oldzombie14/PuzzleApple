Shader "PuzzleApple/V4/Garden Velvet"
{
    Properties
    {
        [HideInInspector] _BaseMap("Surface", 2D) = "white" {}
        _BaseColor("Velvet dye", Color) = (0.25, 0.145, 0.105, 1)
        _Smoothness("Smoothness", Range(0,1)) = 0.14
        [HideInInspector] _Cull("Cull", Float) = 2
        [HideInInspector] _Surface("Surface type", Float) = 0
        [HideInInspector] _Cutoff("Cutoff", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #define _ENVIRONMENTREFLECTIONS_OFF 1
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            half4 VelvetLighting(InputData input, SurfaceData surface)
            {
                // Project the nap onto each room face in metres, independent of cube scale.
                float3 n = abs(input.normalWS);
                float2 uv = n.y > 0.5 ? input.positionWS.xz :
                            (n.x > 0.5 ? input.positionWS.zy : input.positionWS.xy);
                float phase = uv.x * 275.0 + sin(uv.y * 17.0 + uv.x * 8.0);
                half fibre = sin(phase) * (1.0 - smoothstep(0.5, 3.0, fwidth(phase)));
                half nap = pow(1.0h - saturate(dot(input.normalWS, input.viewDirectionWS)), 2.5h);
                surface.albedo *= (1.0h + 0.045h * fibre + 0.50h * nap);
                input.bakedGI = max(input.bakedGI, half3(0.12h, 0.11h, 0.10h));
                return UniversalFragmentPBR(input, surface);
            }
            #define UniversalFragmentPBR VelvetLighting
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
