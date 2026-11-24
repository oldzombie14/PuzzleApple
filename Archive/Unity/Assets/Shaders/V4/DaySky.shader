Shader "PuzzleApple/V4/DaySky"
{
    Properties
    {
        _Zenith("Upper sky", Color) = (.48,.61,.67,1)
        _Horizon("Horizon", Color) = (.80,.84,.82,1)
        _SunDirection("Sun direction", Vector) = (.35,.65,.67,0)
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
            float4 _Zenith, _Horizon, _SunDirection;
            v2f vert(float4 vertex:POSITION)
            {
                v2f o; o.position=UnityObjectToClipPos(vertex); o.direction=vertex.xyz; return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float3 d=normalize(i.direction);
                float3 sky=lerp(_Horizon.rgb,_Zenith.rgb,pow(saturate(d.y),.55));
                float sun=dot(d,normalize(_SunDirection.xyz));
                sky+=float3(1,.95,.80)*(pow(saturate(sun),32)*.07+smoothstep(.9995,.9998,sun)*.65);
                return float4(sky,1);
            }
            ENDCG
        }
    }
}
