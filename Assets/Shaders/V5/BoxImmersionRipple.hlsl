#ifndef P5_BOX_IMMERSION_RIPPLE_INCLUDED
#define P5_BOX_IMMERSION_RIPPLE_INCLUDED
float4 _P5BoxWave;       // feet XZ, outer reach, current contact radius
float4 _P5BoxWavePhase;  // immersion progress, floor height

half3 BoxImmersionRipple(half3 color,float2 xz)
{
    float t=_P5BoxWavePhase.x;
    if(_P5BoxWave.z<=0 || t<=0 || t>=1)return color;
    float2 p=xz-_P5BoxWave.xy;
    float radius=length(p)+.16*sin(dot(p,float2(.68,.29)));
    float fromContact=radius-_P5BoxWave.w;
    float envelope=smoothstep(-.85,-.15,fromContact)*(1-smoothstep(.65,2.5,fromContact));
    float phase=radius/1.05-6*t;
    float dist=abs(frac(phase+.5)-.5);
    float aa=clamp(fwidth(phase),.009,.12);
    float ring=1-smoothstep(.038,.038+aa,dist);
    float strength=envelope*ring*smoothstep(0,.06,t)*(1-smoothstep(.91,1,t));
    float luminance=dot(color,float3(.2126,.7152,.0722));
    return lerp(color,luminance>.12?color*.42:color+.12,strength);
}
#endif
