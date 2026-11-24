Shader "PuzzleApple/V4/Chapter Corridor Ambient"
{
    Properties
    {
        _BaseMap("Surface", 2D) = "white" {}
        _BaseColor("Color", Color) = (0.86,0.86,0.86,1)
        _Smoothness("Smoothness", Range(0,1)) = 0.2
        _Metallic("Metallic", Range(0,1)) = 0
        _CorridorGradient("World X start / end, bright / dark ambient", Vector) = (-16,29,0.08,0.3)
        [HideInInspector] _BumpScale("Normal scale", Float) = 1
        [HideInInspector] _OcclusionStrength("Occlusion", Float) = 1
        [HideInInspector] _Cutoff("Cutoff", Float) = 0.5
        [HideInInspector] _Cull("Cull", Float) = 2
        [HideInInspector] _Surface("Surface type", Float) = 0
        [HideInInspector] _EmissionColor("Emission", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Cull Back
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
            float4 _CorridorGradient;
            half4 CorridorFragmentPBR(InputData input, SurfaceData surface)
            {
                // An art-directed indirect-light bridge, evaluated per pixel in
                // world space. Native URP still handles direct lights and shadows.
                float t=smoothstep(_CorridorGradient.x,_CorridorGradient.y,input.positionWS.x);
                float ambient = lerp(_CorridorGradient.z, _CorridorGradient.w, t);
                float wallFacing = 1.0 - abs(input.normalWS.y);
                float height = input.positionWS.y;
                // A continuous wall wash falls away toward the floor and ceiling.
                float wash = smoothstep(0.0, 5.0, height) * (1.0 - smoothstep(13.0, 22.0, height));
                ambient += wallFacing * (0.08 + 0.12 * wash);
                // Ground the side walls over a short distance on the gallery floor.
                float wallDistance = abs(abs(input.positionWS.z) - 10.5);
                float floorEdge = saturate(input.normalWS.y) * (1.0 - smoothstep(0.0, 0.55, wallDistance));
                input.bakedGI = (ambient * (1.0 - 0.22 * floorEdge)).xxx;
                return UniversalFragmentPBR(input,surface);
            }
            #define UniversalFragmentPBR CorridorFragmentPBR
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
