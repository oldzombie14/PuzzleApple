// Existing Toon Passage door and Impact Floor effects, kept on the shared surface.
// Their coordinates, timing and property names remain compatible with the controllers.
float _SurfaceMode,_FrontWhite,_Dissolve,_IsDoorLeaf,_DoorBackBrightness;
float _CorridorFill;
float4 _ShadeColor,_ShadowColor,_DoorBackColor;
float4 _FloorColor,_Impacts[16],_Hole,_RoomBounds;
float4x4 _WorldToFloor;
float _Brightness,_RippleDuration,_RippleSpeed,_RingCount,_RingSpacing,_RingWidth,_RippleStrength,_Plane;
float _RoomOpeningWidth;
float4 _RippleTaper;
float _LightSteps,_LightStepBlend,_LightBandEdge,_CorridorPureWhite;
TEXTURE2D(_MuralNormal);SAMPLER(sampler_MuralNormal);
TEXTURE2D(_HintLight);SAMPLER(sampler_HintLight);
TEXTURE2D(_HintFloor);TEXTURE2D(_HintMove);
float _HintFloorUVOffset;
float _RoundOutline;
float4 _MuralRect;float3 _MuralLight;
float _MuralEnabled,_MuralRelief,_MuralCavity;

float RoomPerimeterDistance(float3 positionWS)
{
    float3 local=mul(_WorldToFloor,float4(positionWS,1)).xyz;
    float3 edge=float3(_RoomBounds.x-abs(local.x),min(local.y,_RoomBounds.y-local.y),
        min(local.z-_RoomBounds.z,_RoomBounds.z+_RoomBounds.w-local.z));
    if(_Plane<.5&&local.y<.5)
    {
        // Distance to the two front-wall segments, including their doorway endpoints.
        float front=length(float2(max(0,_RoomOpeningWidth*.5-abs(local.x)),local.z-_RoomBounds.z));
        edge.z=min(front,_RoomBounds.z+_RoomBounds.w-local.z);
    }
    return _Plane<.5?min(edge.x,edge.z):(_Plane<1.5?min(edge.x,edge.y):min(edge.z,edge.y));
}
float HintDepth(float slot,float2 uv)
{
    if(any(uv<0)||any(uv>1))return 0;
    if(slot<0)return SAMPLE_TEXTURE2D(_HintLight,sampler_HintLight,uv).a;
    if(slot==0)return SAMPLE_TEXTURE2D(_HintMove,sampler_HintLight,uv).a;
    return SAMPLE_TEXTURE2D(_HintFloor,sampler_HintLight,uv).a;
}
float ContactRipple(float3 local)
{
    float ink=0;
    for(int n=0;n<16;n++)
    {
        float age=_Time.y-_Impacts[n].w;
        if(age<0||age>_RippleDuration)continue;
        float distance=length(local-_Impacts[n].xyz);
        float pixelWidth=max(.0001,fwidth(distance));
        float fade=1-smoothstep(.35*_RippleDuration,_RippleDuration,age);
        for(int ring=0;ring<4;ring++)
        {
            if(ring>=_RingCount)break;
            float radius=age*_RippleSpeed-ring*_RingSpacing;
            if(radius<0)continue;
            float taper=smoothstep(_RippleTaper.x,_RippleTaper.x+max(.01,_RippleTaper.y),radius);
            float innerWeight=_RingCount>1 ? ring/(_RingCount-1) : 1;
            float width=_RingWidth*lerp(.72,1,innerWeight)*lerp(1,_RippleTaper.z,taper);
            // Fraction of the radial pixel footprint covered by this stroke.
            float offset=distance-radius;
            float outer=saturate(.5+(.5*width-offset)/pixelWidth);
            float inner=saturate(.5+(-.5*width-offset)/pixelWidth);
            float onset=smoothstep(0,.07,radius);
            ink=max(ink,(outer-inner)*onset*fade*lerp(.42,1,innerWeight));
        }
    }
    return 1-ink*_RippleStrength;
}
float3 RoomSurfaceEffects(float3 positionWS)
{
    float3 local=mul(_WorldToFloor,float4(positionWS,1)).xyz;
    float2 p=_Plane<.5?local.xz:(_Plane<1.5?local.xy:local.zy);
    if(_Hole.w>.5)clip(max(abs(p.x-_Hole.x),abs(p.y-_Hole.y))-_Hole.z);
    float carving=1;
    if(_MuralEnabled>.5)
    {
        float2 uv=(p-_MuralRect.xy)/_MuralRect.zw+.5;
        float inside=step(0,uv.x)*step(uv.x,1)*step(0,uv.y)*step(uv.y,1);
        float4 relief=SAMPLE_TEXTURE2D(_MuralNormal,sampler_MuralNormal,saturate(uv));
        float3 normal=normalize(relief.rgb*2-1);
        float shade=saturate(1+(dot(normal,_MuralLight)-_MuralLight.z)*_MuralRelief-relief.a*_MuralCavity);
        carving=lerp(1,shade,inside);
        float size=min(.9,_MuralRect.z*.12);
        float2 glyph=(p-float2(_MuralRect.x,_MuralRect.y-_MuralRect.w*.5-size*.8))/size;
        float slot=clamp(round(glyph.x/1.2),-1,1);
        float2 symbolUV=glyph-float2(slot*1.2,0)+.5;
        if(slot>0)symbolUV.y+=_HintFloorUVOffset;
        if(all(abs(symbolUV-.5)<.53))
        {
            float bevel=.018;
            float depth=HintDepth(slot,symbolUV);
            float2 slope=float2(HintDepth(slot,symbolUV+float2(bevel,0))-HintDepth(slot,symbolUV-float2(bevel,0)),
                                HintDepth(slot,symbolUV+float2(0,bevel))-HintDepth(slot,symbolUV-float2(0,bevel)));
            float3 glyphNormal=normalize(float3(slope*1.4,1));
            carving*=saturate(1+(dot(glyphNormal,_MuralLight)-_MuralLight.z)*_MuralRelief-depth*.55*_MuralCavity);
        }
    }
    return carving*ContactRipple(local);
}
