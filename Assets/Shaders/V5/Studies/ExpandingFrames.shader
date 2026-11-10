// Procedural geometry in object space. No texture sampling or texture stretching.
Shader "PuzzleApple/V5/Studies/ExpandingFrames"
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
float seed=Hash(cell),radius=max(abs(p.x),abs(p.y));
float phase=frac(_StudyTime*_Speed*.35+seed);
float outer=phase*.62;
float frame=Stroke(abs(radius-outer),_LineWidth)*smoothstep(0,.12,phase)*(1-smoothstep(.65,1,phase));
float second=frac(phase+.5)*.62;
frame=max(frame,Stroke(abs(radius-second),_LineWidth*.5)*.4);
float perimeter=abs(p.x)>abs(p.y)?(p.x>0?p.y+.5:2.5-p.y):(p.y>0?1.5-p.x:3.5+p.x);
float travel=frac(perimeter*.25-_StudyTime*_Speed*.2-seed);
float edge=Stroke(abs(radius-.43),_LineWidth*.6)*step(travel,_Density*.28);
return Ink(max(frame,edge));
 }
 ENDHLSL
 }
 }
}

