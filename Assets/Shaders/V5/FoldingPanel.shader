Shader "PuzzleApple/V5/Folding Panel"
{
    Properties
    {
        _BaseMap("Surface",2D)="white"{}
        _BaseColor("Surface color",Color)=(1,1,1,1)
        _Smoothness("Smoothness",Range(0,1))=0
        _Metallic("Metallic",Range(0,1))=0
        [HideInInspector] _Cull("Cull",Float)=2
        [HideInInspector] _Cutoff("Cutoff",Float)=.5
        [HideInInspector] _Surface("Surface",Float)=0
        [HideInInspector] _BumpScale("Normal scale",Float)=1
        [HideInInspector] _OcclusionStrength("Occlusion",Float)=1
    }
    SubShader
    {
        Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"}
        Pass
        {
            Name "ForwardLit"
            Tags {"LightMode"="UniversalForwardOnly"}
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitPassVertex
            #pragma fragment PanelFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #define REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
            #define _SPECULARHIGHLIGHTS_OFF
            #define _ENVIRONMENTREFLECTIONS_OFF
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            float4 _InteriorNormal,_InteriorTint;
            float _ClosedWeight,_DarkLevel,_SeamCore,_CoreWidthPixels;

            void PanelFragment(Varyings input,out half4 result:SV_Target)
            {
                SurfaceData surface;
                InitializeStandardLitSurfaceData(input.uv,surface);
                InputData lighting;
                InitializeInputData(input,surface.normalTS,lighting);
                result=UniversalFragmentPBR(lighting,surface);
                float peak=max(result.r,max(result.g,result.b));
                if(peak>.65)result.rgb*=(.65+.3*(1-exp(-(peak-.65)/.3)))/peak;
                result.rgb=MixFog(result.rgb,lighting.fogCoord);

                float3 n=TransformWorldToObjectNormal(input.normalWS);
                float inward=step(.99,dot(normalize(n),_InteriorNormal.xyz));
                // Every quad maps its four real edges to the unit UV rectangle.
                float2 d=min(input.uv,1-input.uv);
                float2 pixel=max(sqrt(ddx(d)*ddx(d)+ddy(d)*ddy(d)),.000001);
                float edge=min(abs(d.x)/pixel.x,abs(d.y)/pixel.y);
                float stroke=saturate(_CoreWidthPixels+.5-edge);
                float closed=inward*_ClosedWeight;
                float3 paper=lerp(result.rgb,_InteriorTint.rgb*_DarkLevel,closed);
                float3 ink=_InteriorTint.rgb*(_DarkLevel+_SeamCore)*closed;
                result.rgb=lerp(paper,ink,stroke);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
