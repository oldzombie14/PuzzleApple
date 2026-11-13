Shader "PuzzleApple/V5/World Surface"
{
    Properties
    {
        _BaseMap("Surface",2D)="white"{}
        [HideInInspector] _SrcBlend("Source blend",Float)=1
        [HideInInspector] _DstBlend("Destination blend",Float)=0
        [HideInInspector] _ZWrite("Depth write",Float)=1
        _BaseColor("Surface color",Color)=(1,1,1,1)
        _Smoothness("Smoothness",Range(0,1))=0
        _Metallic("Metallic",Range(0,1))=0
        _EdgeWidth("Black edge width in pixels",Range(0,3))=0
        [HideInInspector] _UseInkRoomBounds("Shared inner ink boundary",Float)=0
        [HideInInspector] _InkRoomMin("Inner ink minimum",Vector)=(0,0,0,0)
        [HideInInspector] _InkRoomMax("Inner ink maximum",Vector)=(0,0,0,0)
        _InkColor("Edge ink multiplier",Color)=(0,0,0,1)
        [Enum(Scene surface,0,Corridor surface,1,Room effects,2)] _SurfaceMode("Surface effects",Float)=0
        _CorridorFill("Corridor visibility fill",Range(0,1))=.35
        _CorridorLight("Corridor lighting",Range(0,1))=1
        _CorridorPureWhite("Pure white corridor trial",Range(0,1))=0
        _LightSteps("Room lighting bands",Range(2,8))=4
        _LightStepBlend("Room lighting band strength",Range(0,1))=0
        _LightBandEdge("Outer light band limit",Range(.1,.8))=.45
        _ShadeColor("Secondary face",Color)=(.72,.72,.72,1)
        _ShadowColor("Dark face",Color)=(.42,.42,.42,1)
        _FrontWhite("White front faces",Range(0,1))=1
        _FloorColor("Room surface",Color)=(1,1,1,1)
        _Brightness("Room brightness",Range(0,1))=0
        _Plane("Room plane XZ / XY / ZY",Float)=0
        [NoScaleOffset] _MuralNormal("Carving normal RGB / cavity A",2D)="bump"{}
        [NoScaleOffset] _HintLight("Light hint",2D)="black"{}
        [NoScaleOffset] _HintFloor("Floor hint",2D)="black"{}
        [NoScaleOffset] _HintMove("Move hint",2D)="black"{}
        _MuralEnabled("Mural enabled",Float)=0
        [HideInInspector] _Dissolve("Door dissolve",Range(0,1))=0
        [HideInInspector] _IsDoorLeaf("Door leaf",Float)=0
        [HideInInspector] _DoorBackBrightness("Room-side brightness",Float)=0
        [HideInInspector] _DoorBackColor("Room-side wall color",Color)=(1,1,1,1)
        [HideInInspector] _SpecColor("Specular",Color)=(.2,.2,.2,1)
        [HideInInspector] _Cull("Cull",Float)=2
        [HideInInspector] _Cutoff("Cutoff",Float)=.5
        [HideInInspector] _Surface("Surface type",Float)=0
        [HideInInspector] _BumpScale("Normal scale",Float)=1
        [HideInInspector] _OcclusionStrength("Occlusion",Float)=1
        [HideInInspector] _LiningShade("Passage lining shade",Range(0,1))=0
        [HideInInspector] _RoomShellInward("Room interior face",Vector)=(0,0,0,0)
        [HideInInspector] _PassageCover("Fading aperture cover",Float)=0
        [HideInInspector] _PassageOpacity("Aperture opacity",Range(0,1))=1
        [HideInInspector] _InteriorNormal("Local inward face",Vector)=(0,1,0,0)
        [HideInInspector] _PanelSize("Panel size",Vector)=(6,.1,6,0)
        [HideInInspector] _InteriorEdgeMin("Visible inner edge minimum",Vector)=(-.5,-.5,-.5,0)
        [HideInInspector] _InteriorEdgeMax("Visible inner edge maximum",Vector)=(.5,.5,.5,0)
        [HideInInspector] _ClosedWeight("Interior override",Float)=0
        [HideInInspector] _DarkLevel("Dark level",Float)=.006
        [HideInInspector] _SeamCore("Core brightness",Float)=.09
        [HideInInspector] _CoreWidthPixels("Inner edge half-width in pixels",Float)=.75
        [HideInInspector] _InteriorTint("Interior tint",Color)=(1,1,1,1)
    }
    SubShader
    {
        Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry"}
        Pass
        {
            Name "ForwardLit"
            Tags {"LightMode"="UniversalForwardOnly"}
            Cull Back ZWrite [_ZWrite]
            Blend [_SrcBlend] [_DstBlend]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitPassVertex
            #pragma fragment InteriorFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #define REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
            #define _SPECULARHIGHLIGHTS_OFF
            #define _ENVIRONMENTREFLECTIONS_OFF
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            float4 _RoomShellInward,_InteriorNormal,_PanelSize,_InteriorTint,_InteriorEdgeMin,_InteriorEdgeMax;
            float _ClosedWeight,_DarkLevel,_SeamCore,_CoreWidthPixels;
            float _P5FloorGlow,_P5DoorGlow,_LiningShade,_PlayableFloor;
            float _EdgeWidth;
            float _UseInkRoomBounds;
            float _PassageCover,_PassageOpacity,_CorridorLight;
            float4 _PassageOrigin,_PassageRight,_PassageUp,_PassageSize;
            float4 _InkRoomMin,_InkRoomMax;
            float4 _InkColor;
            #include "WorldSurfaceEffects.hlsl"
            #include "BoxImmersionRipple.hlsl"
            void InteriorFragment(Varyings input,out half4 result:SV_Target)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                if(_PassageCover>.5)
                {
                    float3 offset=input.positionWS-_PassageOrigin.xyz;
                    float x=dot(offset,_PassageRight.xyz),y=dot(offset,_PassageUp.xyz);
                    clip(min(_PassageSize.x*.5-abs(x),min(y,_PassageSize.y-y)));
                }
                float3 p=TransformWorldToObject(input.positionWS);
                float3 normalOS=TransformWorldToObjectNormal(input.normalWS);
                // Exterior and thickness faces occlude the interior just like the black corridor surround.
                if(dot(_RoomShellInward.xyz,_RoomShellInward.xyz)>.5 && dot(normalOS,_RoomShellInward.xyz)<.5)
                {
                    result=half4(0,0,0,_PassageCover>.5?_PassageOpacity:1);
                    return;
                }
                SurfaceData surface;
                InitializeStandardLitSurfaceData(input.uv,surface);
                bool roomFace=_SurfaceMode==1&&_IsDoorLeaf>0&&normalOS.z>.5;
                if(_SurfaceMode==1)
                {

                    if(roomFace)surface.albedo=_DoorBackColor.rgb*_DoorBackBrightness;
                    else
                    {
                        // A restrained visibility floor, with real lights still added by PBR.
                        // The inward door face has no fill and matches the adjacent dark wall.
                        float light=dot(normalize(normalOS),normalize(float3(-.35,.8,-.48)));
                        float3 tone=lerp(_ShadowColor.rgb,_ShadeColor.rgb,step(.18,light));
                        tone=lerp(tone,_BaseColor.rgb,max(step(.65,light),_FrontWhite*step(.45,light)));
                        surface.emission=tone*_CorridorFill;
                    }
                }
                else if(_SurfaceMode==2)surface.albedo*=_FloorColor.rgb*_Brightness;
                InputData lighting;
                InitializeInputData(input,surface.normalTS,lighting);
                InitializeBakedGIData(input,lighting);
                result=UniversalFragmentPBR(lighting,surface);
                // Roll off close point-light hotspots on matte surfaces. Keep HDR bloom
                // on the dedicated lamp shaders rather than turning white walls into lamps.
                float peak=max(result.r,max(result.g,result.b));
                if(peak>.65)result.rgb*=(.65+.3*(1-exp(-(peak-.65)/.3)))/peak;
                if(_LiningShade>0)result.rgb=(result.rgb+_P5FloorGlow)*_LiningShade;
                if((_RoomShellInward.y>.9&&normalOS.y>.9)||(_PlayableFloor>.5&&input.normalWS.y>.9))result.rgb+=_P5FloorGlow;
                if(_SurfaceMode==2||roomFace)
                {
                    float luminance=dot(result.rgb,float3(.2126,.7152,.0722));
                    float limit=max(.1,_LightBandEdge);
                    float steps=max(2,_LightSteps), scaled=saturate(luminance/limit)*steps;
                    float aa=max(.008,fwidth(scaled));
                    float band=(floor(scaled)+smoothstep(.5-aa,.5+aa,frac(scaled)))*limit/steps;
                    // Keep the broad bright core smooth; banding fades in only at its dim rim.
                    float edgeWeight=1-smoothstep(limit*.65,limit,luminance);
                    result.rgb=lerp(result.rgb,result.rgb*(band/max(.0001,luminance)),_LightStepBlend*edgeWeight);
                    // Ink and carving stay crisp instead of being swallowed by lighting bands.
                    if(_SurfaceMode==2)result.rgb*=RoomSurfaceEffects(input.positionWS);
                }
                if(_SurfaceMode==1&&!roomFace&&_CorridorPureWhite>.5)result.rgb=1;
                if(_RoundOutline>.5)
                {
                    float key=dot(normalize(normalOS),normalize(float3(-.55,.75,-.38)));
                    // The handle has exactly two tones with a hard light/shadow boundary.
                    result.rgb=step(0,key);
                }
                if(_SurfaceMode==1)
                {
                    if(_IsDoorLeaf>.5)result.rgb+=_P5DoorGlow;
                    else if(normalOS.y>.9)result.rgb+=_P5FloorGlow;
                }
                float3 inward=TransformObjectToWorldNormal(_InteriorNormal.xyz);
                float interior=step(.99,dot(normalize(input.normalWS),normalize(inward)));
                // Cube object coordinates travel with each folding panel. Distances are metres,
                // so nonuniform panel scaling does not widen the light into a blurry wash.
                // Reuse Toon Passage's cube-face ink edge while retaining native Lit lighting.
                // Disabled by default so folding panels keep their existing seam treatment.
                float3 face=abs(normalOS);
                float2 q=face.y>.5?p.xz:(face.x>.5?p.zy:p.xy);
                float d=min(.5-abs(q.x),.5-abs(q.y));
                if(_RoundOutline>.5)
                {
                    float facing=saturate(dot(normalize(input.normalWS),GetWorldSpaceNormalizeViewDir(input.positionWS)));
                    d=facing*facing;
                }
                // Use the whole room's perimeter, not each doorway collision section.
                // This avoids drawing false seams that reveal the concealed room-side door.
                if(_SurfaceMode==2)d=RoomPerimeterDistance(input.positionWS);
                if(_UseInkRoomBounds>.5)
                {
                    if(_RippleStrength>0)result.rgb*=ContactRipple(input.positionWS);
                    float3 boundary=min(input.positionWS-_InkRoomMin.xyz,_InkRoomMax.xyz-input.positionWS);
                    float3 n=abs(normalize(input.normalWS));
                    d=n.y>.5?min(boundary.x,boundary.z):(n.x>.5?min(boundary.y,boundary.z):min(boundary.x,boundary.y));
                }
                float inkPixel=max(length(float2(ddx(d),ddy(d))),.000001);
                // Each adjacent face supplies half the total screen-space stroke.
                float ink=(_UseInkRoomBounds>.5?saturate(_EdgeWidth*.5+.5-abs(d)/inkPixel):
                    1-smoothstep(0,max(fwidth(d)*_EdgeWidth,.000001),d))*step(.001,_EdgeWidth);
                if(!roomFace)result.rgb*=lerp(float3(1,1,1),_InkColor.rgb,ink);
                if(_RoundOutline<.5&&!(_SurfaceMode==1&&!roomFace&&_CorridorPureWhite>.5))result.rgb=MixFog(result.rgb,lighting.fogCoord);
                float3 edges=min(p-_InteriorEdgeMin.xyz,_InteriorEdgeMax.xyz-p)*_PanelSize.xyz;
                float3 axis=abs(_InteriorNormal.xyz);
                float distance=axis.x>.5?min(edges.y,edges.z):(axis.y>.5?min(edges.x,edges.z):min(edges.x,edges.y));
                // Uniform narrow light edge, with pixel coverage only at its boundary.
                // No surface-space gradient or artificial glow tail.
                // Isotropic screen-space width: perspective and diagonal edges must not
                // turn the same seam into a broad strip. Each face supplies half the line.
                float pixelWidth=max(.000001,length(float2(ddx(distance),ddy(distance))));
                float core=saturate(_CoreWidthPixels+.5-abs(distance)/pixelWidth);
                float3 localLight=_InteriorTint.rgb*(_DarkLevel+_SeamCore*core);
                // Replace interior scene lighting while closed, rather than adding emission
                // over its existing hard shadow boundaries. The exterior always stays Lit.
                result.rgb=lerp(result.rgb,localLight,saturate(_ClosedWeight)*interior);
                if(input.normalWS.y>.9 && abs(input.positionWS.y-_P5BoxWavePhase.y)<.18)
                    result.rgb=BoxImmersionRipple(result.rgb,input.positionWS.xz);
                if(_SurfaceMode==1&&!roomFace)result.rgb*=_CorridorLight;
                result.a=_SurfaceMode==1?saturate(1-_Dissolve):1;
                if(_PassageCover>.5)result.a*=_PassageOpacity;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags {"LightMode"="ShadowCaster"}
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex SurfaceShadowVertex
            #pragma fragment SurfaceShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            #include "WorldSurfaceEffects.hlsl"
            struct SurfaceShadowVaryings
            {
                float4 positionCS:SV_POSITION;
                float3 local:TEXCOORD0;
                float3 world:TEXCOORD1;
                float3 normalOS:TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            SurfaceShadowVaryings SurfaceShadowVertex(Attributes input)
            {
                SurfaceShadowVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input,output);
                output.positionCS=GetShadowPositionHClip(input);
                output.local=input.positionOS.xyz;
                output.world=TransformObjectToWorld(input.positionOS.xyz);
                output.normalOS=input.normalOS;
                return output;
            }
            half4 SurfaceShadowFragment(SurfaceShadowVaryings input):SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);

                if(_SurfaceMode==2&&_Hole.w>.5)
                {
                    float3 local=mul(_WorldToFloor,float4(input.world,1)).xyz;
                    float2 p=_Plane<.5?local.xz:(_Plane<1.5?local.xy:local.zy);
                    clip(max(abs(p.x-_Hole.x),abs(p.y-_Hole.y))-_Hole.z);
                }
                return 0;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
