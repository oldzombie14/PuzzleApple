Shader "PuzzleApple/V5/Ascent Treads"
{
    Properties
    {
        _InkPixels("Total stroke width at 720p",Range(.5,4))=1.8
        [HideInInspector] _TreadNeighbours("Previous and next tread",Vector)=(0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _InkPixels;
                float4 _TreadNeighbours;
            CBUFFER_END
            struct Varyings { float4 clip:SV_POSITION; float3 pos:TEXCOORD0; float3 normal:TEXCOORD1; float railContrast:TEXCOORD2; };
            Varyings Vert(float3 p:POSITION,float3 normal:NORMAL)
            {
                Varyings o;o.clip=TransformObjectToHClip(p);o.pos=p;o.normal=normal;
                float4 nose=TransformObjectToHClip(float3(p.x,.5,-.5));
                float4 next=TransformObjectToHClip(float3(p.x,1.5,.5));
                float2 spacing=(next.xy/max(next.w,.001)-nose.xy/max(nose.w,.001))*_ScaledScreenParams.xy*.5;
                // At grazing angles several risers can occupy one pixel despite their individual height.
                float width=_InkPixels*(_ScaledScreenParams.y/720.0);
                o.railContrast=any(_TreadNeighbours.xy)?lerp(.16,1,smoothstep(.7,3.5,length(spacing)/width)):1;
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float3 eye=TransformWorldToObject(GetCameraPositionWS());
                float3 n=i.normal;
                bool cap=abs(n.y)>.5,side=abs(n.x)>.5;
                float2 q=cap?i.pos.xz:(side?i.pos.zy:i.pos.xy);
                float2 eyeAxes=cap?eye.xz:(side?eye.zy:eye.xy);
                float2 neighbourVisible=step(.5,sign(q)*eyeAxes);
                // The upper rear and lower front rails join another step at a concave crease.
                bool joinsNext=_TreadNeighbours.y>.5 && ((n.y>.5 && q.y>0)||(n.z>.5 && q.y>0));
                bool joinsPrevious=_TreadNeighbours.x>.5 && ((n.y<-.5 && q.y<0)||(n.z<-.5 && q.y<0));
                if(joinsNext || joinsPrevious)neighbourVisible.y=1;
                // Each visible face contributes half a stroke. A silhouette owns its full width.
                float2 stroke=_InkPixels*(_ScaledScreenParams.y/720.0)*(1-.5*neighbourVisible);
                float2 dx=ddx(q),dy=ddy(q);
                float2 pixelSize=max(sqrt(dx*dx+dy*dy),1e-6);
                float2 distance=(.5-abs(q))/pixelSize;
                float2 filteredStroke=min(stroke,.20/pixelSize);
                float2 ink=1-smoothstep(-.5,.5,distance-filteredStroke);
                // Below pixel resolution a pair of adjacent rails averages into the white face.
                float2 resolved=smoothstep(stroke*1.2,stroke*2.4,1/pixelSize);
                ink*=lerp(.28,1,resolved);
                if(!side)ink.y*=i.railContrast;
                half value=1-max(ink.x,ink.y);
                return half4(value,value,value,1);
            }
            ENDHLSL
        }
    }
}
