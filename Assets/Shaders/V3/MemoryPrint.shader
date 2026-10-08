Shader "PuzzleApple/V3/MemoryPrint"
{
 Properties
 {
  [PerRendererData] _MainTex("Snapshot",2D)="white"{}
  _Saturation("Saturation",Range(0,1))=.5
  _Grain("Fine grain",Range(0,.05))=.003
  _BlackPoint("Black point",Range(0,.2))=.025
  _WhitePoint("White point",Range(.3,1))=.85
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
   #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
   #include "UnityCG.cginc"
   #include "UnityUI.cginc"
   struct appdata { float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR; };
   struct v2f { float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;float2 localPosition:TEXCOORD1; };
   sampler2D _MainTex;float _Saturation,_Grain,_BlackPoint,_WhitePoint;float4 _ClipRect;
   v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;o.localPosition=v.vertex.xy;return o;}
   fixed4 frag(v2f i):SV_Target
   {
    float3 source=tex2D(_MainTex,i.uv).rgb;
    float luma=dot(source,float3(.2126,.7152,.0722));
    float3 c=lerp(luma.xxx,source,_Saturation);
    // Map perceptual brightness without lifting black objects into gray fog.
    c=LinearToGammaSpace(c);
    c=saturate((c-_BlackPoint)/max(.01,_WhitePoint-_BlackPoint));
    c=GammaToLinearSpace(c);
    float2 p=i.uv*2-1;
    c*=1-.12*smoothstep(.25,1.5,dot(p,p));
    float grain=frac(sin(dot(floor(i.uv*float2(960,540)),float2(12.9898,78.233)))*43758.5453)-.5;
    c+=grain*_Grain;
    float alpha=i.color.a;
    #ifdef UNITY_UI_CLIP_RECT
    alpha*=UnityGet2DClipping(i.localPosition,_ClipRect);
    #endif
    return fixed4(saturate(c)*i.color.rgb,alpha);
   }
   ENDCG
  }
 }
}
