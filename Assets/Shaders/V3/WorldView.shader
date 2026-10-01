Shader "PuzzleApple/V3/WorldView"
{
 Properties { _MainTex("World",2D)="black"{} _SoftTex("Soft focus",2D)="black"{} _Open("Eyes open",Range(0,1))=1 _Blur("Blur",Range(0,1))=0 }
 SubShader { Tags { "Queue"="Overlay" "RenderType"="Transparent" } Cull Off ZWrite Off ZTest Always
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
 struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
 sampler2D _MainTex,_SoftTex; float _Open,_Blur;
 v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
 fixed4 frag(v2f i):SV_Target
 {
  float2 uv=i.uv;
  // Broad, curved feathering removes the cut-paper outline of the eyelids.
  float curve=pow(abs((uv.x-.5)*2),2)*.11*(1-_Open);
  float aperture=lerp(-.16,.94,_Open);
  float feather=lerp(.16,.27,_Open);
  float lid=smoothstep(aperture-feather,aperture+feather,abs(uv.y-.5)+curve);
  if(_Open<.001)return fixed4(0,0,0,1);
  float3 c=tex2D(_MainTex,uv).rgb;
  if(_Blur>.001)c=lerp(c,tex2D(_SoftTex,uv).rgb,_Blur);
  float exposure=lerp(.58,1,smoothstep(.12,.94,_Open));
  return fixed4(c*(1-lid)*exposure,1);
 }
 ENDCG }
 }
}
