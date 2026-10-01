Shader "PuzzleApple/V3/SoftFocus"
{
 Properties { _MainTex("Source",2D)="black"{} }
 SubShader
 {
  Cull Off ZWrite Off ZTest Always
  Pass
  {
   CGPROGRAM
   #pragma vertex vert_img
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex;float4 _MainTex_TexelSize;float2 _Axis;
   fixed4 frag(v2f_img i):SV_Target
   {
    float2 d=_MainTex_TexelSize.xy*_Axis;
    float3 c=tex2D(_MainTex,i.uv).rgb*.227027;
    c+=(tex2D(_MainTex,i.uv+d*1.384615).rgb+tex2D(_MainTex,i.uv-d*1.384615).rgb)*.316216;
    c+=(tex2D(_MainTex,i.uv+d*3.230769).rgb+tex2D(_MainTex,i.uv-d*3.230769).rgb)*.070270;
    return fixed4(c,1);
   }
   ENDCG
  }
 }
}
