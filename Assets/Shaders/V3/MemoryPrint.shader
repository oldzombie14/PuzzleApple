Shader "PuzzleApple/V3/MemoryPrint"
{
 Properties
 {
  [PerRendererData] _MainTex("Snapshot",2D)="white"{}
  _Saturation("Saturation",Range(0,1))=.22
  _Grain("Fine grain",Range(0,.05))=.012
 }
 SubShader
 {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
  Blend SrcAlpha OneMinusSrcAlpha
  Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct appdata { float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR; };
   struct v2f { float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR; };
   sampler2D _MainTex;float _Saturation,_Grain;
   v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
   fixed4 frag(v2f i):SV_Target
   {
    float3 source=tex2D(_MainTex,i.uv).rgb;
    float luma=dot(source,float3(.2126,.7152,.0722));
    float3 c=lerp(luma.xxx,source,_Saturation);
    c=lerp(float3(.05,.05,.05),float3(.83,.83,.83),saturate(c*.94+.025));
    float2 p=i.uv*2-1;
    c*=1-.12*smoothstep(.25,1.5,dot(p,p));
    float grain=frac(sin(dot(floor(i.uv*float2(960,540)),float2(12.9898,78.233)))*43758.5453)-.5;
    c+=grain*_Grain;
    return fixed4(saturate(c)*i.color.rgb,i.color.a);
   }
   ENDCG
  }
 }
}
