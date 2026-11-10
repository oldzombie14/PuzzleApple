// Native LineRenderer geometry supplies the streak. No texture, UV scrolling or stretching.
Shader "PuzzleApple/V5/Studies/RainStreak"
{
 SubShader {
 Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
 Pass {
 Blend SrcAlpha OneMinusSrcAlpha
 ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct A {float4 vertex:POSITION;half4 color:COLOR;};
 struct V {float4 vertex:SV_POSITION;half4 color:COLOR;};
 V Vert(A i){V o;o.vertex=TransformObjectToHClip(i.vertex.xyz);o.color=i.color;return o;}
 half4 Frag(V i):SV_Target{return i.color;}
 ENDHLSL
 }
 }
}
