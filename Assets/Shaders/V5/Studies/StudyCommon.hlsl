#ifndef PUZZLE_APPLE_STUDY_COMMON
#define PUZZLE_APPLE_STUDY_COMMON
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
CBUFFER_START(UnityPerMaterial)
float _Scale, _LineWidth, _Speed, _Density, _StudyTime, _Invert;
CBUFFER_END
struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
struct Varyings { float4 positionCS:SV_POSITION; float3 local:TEXCOORD0; float3 normal:TEXCOORD1; };
Varyings Vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.local=v.positionOS.xyz; o.normal=v.normalOS; return o; }
float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
float2 FaceUV(Varyings i) { float3 n=abs(normalize(i.normal)); return n.x>max(n.y,n.z)?i.local.zy:(n.y>n.z?i.local.xz:i.local.xy); }
float Stroke(float d,float width) { float aa=max(fwidth(d),.001); return 1-smoothstep(width-aa,width+aa,d); }
float Segment(float2 p,float2 a,float2 b) { float2 ab=b-a; return length(p-a-ab*saturate(dot(p-a,ab)/max(.0001,dot(ab,ab)))); }
half4 Ink(float value) { value=lerp(saturate(value),1-saturate(value),_Invert); return half4(value,value,value,1); }
#endif

