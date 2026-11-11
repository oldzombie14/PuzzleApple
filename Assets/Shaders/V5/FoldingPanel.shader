Shader "PuzzleApple/V5/Folding Panel"
{
    Properties
    {
        _BaseMap("Surface",2D)="white"{}
        _BaseColor("Surface color",Color)=(1,1,1,1)
        [HideInInspector] _PanelOpacity("Panel opacity",Range(0,1))=1
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
        Tags {"Queue"="AlphaTest" "RenderType"="TransparentCutout" "RenderPipeline"="UniversalPipeline"}
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        float _PanelOpacity;
        // Shared by the entire net: feet (XZ), farthest corner, remaining radius.
        float4 _PanelSink;
        float _PanelGroundY;
        float4 _InteriorNormal;
        float4 _InteriorFaceCentre;
        float4 VisibleFace(float4 cs,float3 normalOS)
        {
            // Once flat, the inward face is the exposed sheet. The bevels and
            // backs meet below it and would otherwise cross at a reversing hinge.
            if(_PanelOpacity<1 && dot(normalOS,_InteriorNormal.xyz)<.99)
                return float4(0,0,0,1);
            return cs;
        }
        float4 CameraVisibleFace(float4 cs,float3 normalOS)
        {
            // Seen from the inner side, the sheet's true edge is its inner quad.
            // A mitred solid extends beyond that quad as soon as the hinge turns;
            // its exposed bevel/back otherwise becomes a wide black wedge.
            float3 eyeOS=TransformWorldToObject(GetCameraPositionWS());
            if(dot(eyeOS-_InteriorFaceCentre.xyz,_InteriorNormal.xyz)>0 &&
               dot(normalOS,_InteriorNormal.xyz)<.99)
                return float4(0,0,0,1);
            return VisibleFace(cs,normalOS);
        }
        // Keep the part descending through the opening visible against the ground.
        // Preserve its projected silhouette and its occlusion by objects above ground.
        float4 GroundDepth(float4 cs,float3 ws)
        {
            float below=_PanelGroundY+.075-ws.y;
            if(below>0)
            {
                float3 view=GetWorldSpaceNormalizeViewDir(ws);
                if(view.y>.001)
                {
                    // Compress the depth below grade into a thin layer. Its ordering
                    // stays strictly monotonic, including across adjacent panel faces.
                    float lift=.985*below;
                    float3 contact=ws+view*min(lift/view.y,distance(ws,GetCameraPositionWS())*.99);
                    float4 groundCS=TransformWorldToHClip(contact);
                    cs.z=cs.w*groundCS.z/groundCS.w;
                }
            }
            return cs;
        }
        float CutDepth(float2 xz)
        {
            float2 p=xz-_PanelSink.xy;
            float radius=length(p);
            float reach=max(_PanelSink.z,.001);
            float t=1-_PanelOpacity;
            float swell=.34*sin(dot(p,float2(.68,.29))+2*t)
                       +.17*sin(dot(p,float2(-.35,.84))-1.4*t);
            float u=saturate(radius/reach);
            return radius + 4*u*(1-u)*swell - _PanelSink.w;
        }
        void ClipPanel(float2 xz)
        {
            if(_PanelOpacity<1)clip(min(_PanelOpacity-.000001,-CutDepth(xz)));
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags {"LightMode"="UniversalForwardOnly"}
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex PanelVertex
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
            float4 _InteriorTint,_ContactSeams;
            float _P5FloorGlow;
            float _ClosedWeight,_DarkLevel,_SeamCore,_CoreWidthPixels;
            Varyings PanelVertex(Attributes input)
            {
                Varyings output=LitPassVertex(input);
                output.positionCS=CameraVisibleFace(GroundDepth(output.positionCS,output.positionWS),input.normalOS);
                return output;
            }

            void PanelFragment(Varyings input,out half4 result:SV_Target)
            {
                ClipPanel(input.positionWS.xz);
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
                float closed=inward*_ClosedWeight;
                result.rgb=lerp(result.rgb,_InteriorTint.rgb*_DarkLevel,closed);
                if(_InteriorNormal.y>.9)result.rgb+=_P5FloorGlow*inward;
                float2 width=max(sqrt(ddx(input.uv)*ddx(input.uv)+ddy(input.uv)*ddy(input.uv)),.000001);
                float4 dist=float4(input.uv.x,1-input.uv.x,input.uv.y,1-input.uv.y)/width.xxyy;
                float4 bands=saturate(_CoreWidthPixels+.5-dist)*_ContactSeams;
                float seam=max(max(bands.x,bands.y),max(bands.z,bands.w))*inward;
                result.rgb=lerp(result.rgb,_InteriorTint.rgb*(_DarkLevel+_SeamCore)*_ClosedWeight,seam);
                result.a=1;
            }
            ENDHLSL
        }
        Pass
        {
            Name "BoxMask"
            Tags {"LightMode"="P5FoldingMask"}
            Cull Back ZWrite Off ZTest LEqual Offset -1,-1
            HLSLPROGRAM
            #pragma vertex MaskVert
            #pragma fragment MaskFrag

            struct MaskVaryings {float4 positionCS:SV_POSITION;float3 ws:TEXCOORD0;};
            MaskVaryings MaskVert(float3 position:POSITION,float3 normal:NORMAL)
            {
                MaskVaryings o;
                o.ws=TransformObjectToWorld(position);
                o.positionCS=CameraVisibleFace(GroundDepth(TransformWorldToHClip(o.ws),o.ws),normal);
                return o;
            }
            half4 MaskFrag(MaskVaryings i):SV_Target
            {
                ClipPanel(i.ws.xz);
                return 1;
            }
            ENDHLSL
        }
        Pass
        {
            Name "BoxContour"
            Tags {"LightMode"="P5FoldingContour"}
            Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment ContourFrag
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float4 _P5OutlineInk,_P5OutlineStyle;
            float Boundary(float2 uv)
            {
                return 1-SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv).r;


            }
            half4 ContourFrag(Varyings i):SV_Target
            {
                float2 uv=i.texcoord;
                float2 centre=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv).rg;
                clip(centre.r-.5);
                float2 px=abs(_BlitTexture_TexelSize.xy);
                float stroke=0;
                [unroll] for(int radius=1;radius<=3;radius++)
                {
                    float2 d=px*radius;
                    float edge=max(max(Boundary(uv+float2(d.x,0)),Boundary(uv-float2(d.x,0))),
                                   max(Boundary(uv+float2(0,d.y)),Boundary(uv-float2(0,d.y))));
                    float2 diagonal=d*.70710678;
                    edge=max(edge,max(max(Boundary(uv+diagonal),Boundary(uv-diagonal)),
                                      max(Boundary(uv+float2(diagonal.x,-diagonal.y)),Boundary(uv+float2(-diagonal.x,diagonal.y)))));
                    // Silhouettes have only one visible side; creases share the
                    // configured width between two faces.
                    stroke=max(stroke,edge*saturate(_P5OutlineStyle.x+1-radius));
                }
                return half4(_P5OutlineInk.rgb,stroke);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags {"LightMode"="ShadowCaster"}
            ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex PanelShadowVertex
            #pragma fragment PanelShadow
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #define _ALPHATEST_ON 1
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            Varyings PanelShadowVertex(Attributes input)
            {
                Varyings output=ShadowPassVertex(input);
                output.positionCS=VisibleFace(output.positionCS,input.normalOS);
                output.uv=TransformObjectToWorld(input.positionOS.xyz).xz;
                return output;
            }
            half4 PanelShadow(Varyings input):SV_Target
            {
                ClipPanel(input.uv);
                return 0;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags {"LightMode"="DepthOnly"}
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex PanelDepthVertex
            #pragma fragment PanelDepth
            #define _ALPHATEST_ON 1
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            Varyings PanelDepthVertex(Attributes input,float3 normalOS:NORMAL)
            {
                Varyings output=DepthOnlyVertex(input);
                float3 ws=TransformObjectToWorld(input.position.xyz);
                output.positionCS=CameraVisibleFace(GroundDepth(output.positionCS,ws),normalOS);
                output.uv=ws.xz;
                return output;
            }
            half PanelDepth(Varyings input):SV_Target
            {
                ClipPanel(input.uv);
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
}
