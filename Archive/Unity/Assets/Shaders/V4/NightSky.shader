Shader "PuzzleApple/V4/NightSky"
{
    Properties
    {
        _DayZenith("Day zenith", Color) = (.48,.61,.67,1)
        _DayHorizon("Day horizon", Color) = (.8,.84,.82,1)
        _Zenith("Night zenith", Color) = (.012,.022,.055,1)
        _Horizon("Night horizon", Color) = (.06,.09,.15,1)
        _SunDirection("Sun direction", Vector) = (.35,.65,.67,0)
        _Night("Night blend", Range(0,1)) = 1
        _StarDensity("Star density", Range(0,.02)) = .0005
        _StarBrightness("Star brightness", Range(0,6)) = 2.3
        _StarSize("Star size", Range(.03,.3)) = .12
        _StarGlow("Soft star halo", Range(0,1)) = .08
        _RayLength("Star ray length", Range(1,10)) = 4.2
        _RayStrength("Star ray strength", Range(0,2)) = .35
        _StarMinHeight("Minimum star height", Range(0,1)) = .309017
        _StarFullHeight("Full star height", Range(0,1)) = .5
        _HazeHeight("Horizon haze height", Range(.02,.4)) = .12
        _HazeEnabled("Horizon haze", Range(0,1)) = 1
        _TwinkleSpeed("Twinkle speed", Range(0,5)) = .55
        _TwinkleAmount("Twinkle amount", Range(0,1)) = .3
        _Clock("Animation time", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f { float4 position:SV_POSITION; float3 direction:TEXCOORD0; };
            float4 _DayZenith, _DayHorizon, _Zenith, _Horizon, _SunDirection;
            float _Night, _StarDensity, _StarBrightness, _StarSize, _StarGlow, _TwinkleSpeed, _TwinkleAmount, _Clock;
            float _StarMinHeight, _StarFullHeight;
            float _HazeHeight, _HazeEnabled;
            float _RayLength, _RayStrength;
            float4x4 _StarRotation;
            float hash(float3 p) { p=frac(p*.1031); p+=dot(p,p.yzx+33.33); return frac((p.x+p.y)*p.z); }
            v2f vert(float4 vertex:POSITION)
            { v2f o; o.position=UnityObjectToClipPos(vertex); o.direction=vertex.xyz; return o; }
            float4 frag(v2f i):SV_Target
            {
                float3 d=normalize(i.direction), a=abs(d);
                float gradient=pow(saturate(d.y),.55);
                float3 day=lerp(_DayHorizon.rgb,_DayZenith.rgb,gradient);
                float sun=dot(d,normalize(_SunDirection.xyz));
                day+=float3(1,.95,.8)*(pow(saturate(sun),32)*.07+smoothstep(.9995,.9998,sun)*.65);
                float3 night=lerp(_Horizon.rgb,_Zenith.rgb,gradient);
                // Match native geometry fog at the horizon, avoiding a bright seam on the ground edge.
                night=lerp(night,unity_FogColor.rgb,exp(-abs(d.y)/max(.01,_HazeHeight))*_HazeEnabled);
                // Fixed directions on the celestial sphere: no screen-space drift or noise animation.
                float elevation=d.y;
                d=mul((float3x3)_StarRotation,d); a=abs(d);
                float2 uv; float face;
                if(a.x>=a.y && a.x>=a.z) { uv=d.yz/a.x; face=d.x>0?0:1; }
                else if(a.y>=a.z) { uv=d.xz/a.y; face=d.y>0?2:3; }
                else { uv=d.xy/a.z; face=d.z>0?4:5; }
                float2 grid=uv*60, cell=floor(grid);
                float footprint=max(length(fwidth(grid))*.5,.012);
                float3 starlight=0;
                // Neighbour cells let long rays extend across cell boundaries without clipping.
                [unroll] for(int sy=-1;sy<=1;sy++)
                [unroll] for(int sx=-1;sx<=1;sx++)
                {
                float2 starCell=cell+float2(sx,sy);
                float3 seed=float3(starCell,face*19+7);
                float selection=hash(seed);
                if(selection>_StarDensity) continue;
                float2 center=.18+.64*float2(hash(seed+13.7),hash(seed+37.1));
                float radius=_StarSize*lerp(.6,1.4,hash(seed+61));
                float2 delta=grid-starCell-center;
                float distanceToStar=length(delta);
                // Derive the footprint from continuous coordinates, never from the hashed cells.
                float variance=radius*radius+footprint*footprint*.35;
                float starDisc=exp(-distanceToStar*distanceToStar/variance)*radius*radius/variance;
                float haloVariance=radius*radius*7+footprint*footprint*.35;
                starDisc+=_StarGlow*exp(-distanceToStar*distanceToStar/haloVariance);
                float phase=hash(seed+91)*6.2831853;
                float beat=_Clock*_TwinkleSpeed*lerp(1.2,3,hash(seed+52))+phase;
                // Gentle, overlapping cycles, rather than sharp, regularly repeated flashes.
                float flash=.5+.32*sin(beat)+.18*sin(beat*.63+phase*1.7);
                float crossFlash=.5+.3*sin(beat*.77+1.9)+.2*sin(beat*1.31+phase);
                float prominence=pow(hash(seed+113),4);
                float angle=phase*.25;
                float2 ray=float2(delta.x*cos(angle)-delta.y*sin(angle),delta.x*sin(angle)+delta.y*cos(angle));
                float rayWidth=radius*.18;
                float rayVariance=rayWidth*rayWidth+footprint*footprint*.22;
                float shapeAmount=saturate(_TwinkleAmount*2);
                float horizontalLength=min(.95,radius*lerp(1,_RayLength,lerp(.25,flash,shapeAmount)));
                float verticalLength=min(.95,radius*lerp(1,_RayLength,lerp(.25,max(flash*.85,crossFlash*.7),shapeAmount)));
                float horizontal=saturate(1-abs(ray.x)/horizontalLength)*exp(-ray.y*ray.y/rayVariance);
                float vertical=saturate(1-abs(ray.y)/verticalLength)*exp(-ray.x*ray.x/rayVariance);
                float rays=(horizontal+vertical)*_RayStrength*lerp(.25,1,max(flash,crossFlash))*lerp(.05,1,prominence);
                float twinkle=lerp(1,.65+.35*flash,_TwinkleAmount);
                float stars=(starDisc*twinkle+rays)*_StarBrightness*lerp(.28,1,prominence);
                float3 tint=lerp(float3(.68,.81,1),float3(1,.94,.82),hash(seed+74));
                starlight+=stars*tint;
                }
                night+=starlight*smoothstep(_StarMinHeight,max(_StarMinHeight+.001,_StarFullHeight),elevation);
                return float4(lerp(day,night,_Night),1);
            }
            ENDCG
        }
    }
}
