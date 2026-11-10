// Procedural geometry in object space. No texture sampling or texture stretching.
Shader "PuzzleApple/V5/Studies/CircuitStreams"
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
float seed=Hash(cell);float turn=floor(seed*4);
if(turn<1)p=p;else if(turn<2)p=float2(p.y,-p.x);else if(turn<3)p=-p;else p=float2(-p.y,p.x);
float width=_LineWidth;
float wire=min(Segment(p,float2(-.5,0),float2(0,0)),Segment(p,float2(0,0),float2(0,.5)));
float phase=frac(_StudyTime*_Speed*.6+seed);
float2 head=phase<.5?float2(phase-.5,0):float2(0,phase-.5);
float2 delta=abs(p-head);
float packet=Stroke(max(delta.x,delta.y),width*(1.6+seed));
float gate=step(seed,lerp(.18,1,_Density));
float junction=Stroke(abs(max(abs(p.x),abs(p.y))-.075),width*.6);
return Ink(gate*(Stroke(wire,width)*.32+packet+junction*.25));
 }
 ENDHLSL
 }
 }
}

