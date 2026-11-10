// ColorMap © NullTale - https://x.com/NullTale
// Adapted from umbrella_game/Assets/VolFx/VolFx/Runtime/Passes/Lib/ColorMap/ColorMap.shader.
// Shared local evaluation shader; source package license must be checked before release.
// Retains the luminance -> gradient -> intensity blend. Removes palette/animation,
// clamps instead of frac, and uses Unity's native Graphics.Blit vertex path.
Shader "Hidden/PuzzleApple/V5/ColorMapTrial"
{
    Properties { _MainTex ("Source", 2D) = "white" {} }
    SubShader
    {
        ZTest Always ZWrite Off Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            sampler2D _GradientTex;
            float _Intensity, _BlackPoint, _WhitePoint, _Gamma;
            half4 frag(v2f_img i) : SV_Target
            {
                half4 initial = tex2D(_MainTex, i.uv);
                float val = dot(max(initial.rgb, 0), float3(.299, .587, .114));
                val = pow(saturate((val - _BlackPoint) / max(.001, _WhitePoint - _BlackPoint)), _Gamma);
                // Sample texel centres: no white/HDR wrap to black, no cyclic offset.
                half4 grad = tex2D(_GradientTex, float2((val * 255.0 + .5) / 256.0, .5));
                return half4(lerp(initial.rgb, grad.rgb, _Intensity * grad.a), initial.a);
            }
            ENDHLSL
        }
    }
}
