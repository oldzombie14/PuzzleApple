Shader "PuzzleApple/V4/WorldPicture"
{
 Properties
 {
  _MainTex("World",2D)="white"{}
  _Strength("Style strength",Range(0,1))=.65
  _Levels("Tonal steps",Range(6,32))=14
  _Saturation("Saturation",Range(0,1.5))=.90
  _ShadowTint("Cool shadows",Color)=(.78,.91,.91,1)
  _LightTint("Warm highlights",Color)=(1,.99,.92,1)
 }
 SubShader
 {
  Tags { "Queue"="Overlay" "RenderType"="Opaque" }
  Cull Off ZWrite Off ZTest Always
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
   struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
   sampler2D _MainTex;
   float4 _MainTex_TexelSize,_ShadowTint,_LightTint;
   float _Strength,_Levels,_Saturation;
   v2f vert(appdata v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o; }
   float4 frag(v2f i):SV_Target
   {
    float3 original=tex2D(_MainTex,i.uv).rgb;
    // Quantize perceived brightness, preserving hue instead of RGB banding.
    float3 c=LinearToGammaSpace(original);
    float luma=dot(c,float3(.2126,.7152,.0722));
    float stepped=floor(luma*_Levels+.5)/_Levels;
    float tone=lerp(luma,stepped,_Strength);
    c*=tone/max(luma,.001);
    c=lerp(tone.xxx,c,_Saturation);
    float3 tint=lerp(_ShadowTint.rgb,_LightTint.rgb,smoothstep(.22,.80,luma));
    c*=lerp(float3(1,1,1),LinearToGammaSpace(tint),.55);
    return float4(GammaToLinearSpace(saturate(c)),1);
   }
   ENDCG
  }
 }
}
