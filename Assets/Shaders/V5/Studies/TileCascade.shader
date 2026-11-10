// Procedural geometry in object space. No texture sampling or texture stretching.
Shader "PuzzleApple/V5/Studies/TileCascade"
{
 Properties {
  _Scale("Cells per mesh unit",Range(1,12))=5
  _LineWidth("Stroke width",Range(.005,.15))=.025
  _Speed("Flow speed",Range(-2,2))=1
  _Density("Activity",Range(.1,1))=.7
  _Invert("Invert black / white",Range(0,1))=0
  [HideInInspector] _StudyTime("Study clock",Float)=0
 }
 SubShader {
 Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
 Pass {
 Tags {"LightMode"="UniversalForward"}
 Cull Back ZWrite On
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #include "StudyCommon.hlsl"
 half4 Frag(Varyings i):SV_Target {
 float2 uv=FaceUV(i)*_Scale;float2 cell=floor(uv),p=frac(uv)-.5;
float seed=Hash(cell);
float phase=frac(_StudyTime*_Speed*.28-(cell.x+cell.y)*.11-seed*.2);
float pulse=smoothstep(.03,.22,phase)*(1-smoothstep(.55,.88,phase));
float size=lerp(.06,.45,pulse)*lerp(.45,1,seed);
float square=max(abs(p.x),abs(p.y));
float border=Stroke(abs(square-size),_LineWidth);
float filled=Stroke(square,size)*step(seed,_Density*.5)*pulse;
float tick=Stroke(Segment(p,float2(-.5,-.5),float2(-.32,-.5)),_LineWidth*.6);
return Ink(max(max(border*pulse,filled),tick*.22));
 }
 ENDHLSL
 }
 }
}

