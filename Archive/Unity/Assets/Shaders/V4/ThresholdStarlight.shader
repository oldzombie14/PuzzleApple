Shader "PuzzleApple/V4/ThresholdStarlight"
{
    Properties { [HDR] _Color("Glow", Color) = (3,3,3,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 tangentOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 offset : TEXCOORD1;
                float4 signal : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 signal : TEXCOORD1;
            };
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            CBUFFER_END

            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 view = TransformWorldToView(TransformObjectToWorld(i.positionOS.xyz));
                float2 offset = i.offset;
                float2 travel = mul((float3x3)UNITY_MATRIX_MV, i.tangentOS).xy;
                if (i.signal.r < .5)
                {
                    float2 across = float2(-travel.y, travel.x) * rsqrt(max(dot(travel, travel), 1e-9));
                    offset = across * i.offset.x;
                }
                else
                {
                    // Lean with the projected path. Opposite directions share a cross axis;
                    // this bounded tilt also tends to zero when the star moves into the camera.
                    float tilt = .44 * (2 * travel.x * travel.y) / (dot(travel, travel) + .015);
                    float sn, cs;
                    sincos(tilt, sn, cs);
                    offset = float2(cs * offset.x - sn * offset.y, sn * offset.x + cs * offset.y);
                }
                view.xy += offset;
                o.positionCS = mul(UNITY_MATRIX_P, float4(view,1));
                o.uv = i.uv;
                o.signal = i.signal;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 p = i.uv * 2 - 1;
                float light;
                if (i.signal.r > .5)
                {
                    float flare = i.signal.g;
                    float2 q = abs(p);
                    float along = max(q.x,q.y);
                    float across = min(q.x,q.y);
                    float length = lerp(.115,.78,flare);
                    float width = lerp(.06,.21,flare) / (1 + 13 * along);
                    float spike = pow(saturate(1 - across / width - along / length), 1.4);
                    float r2 = dot(p,p);
                    light = exp(-r2 * lerp(115, 680, flare)) * (.27 + .04 * flare)
                        + spike * flare + exp(-r2 * 22) * .012 * flare;
                }
                else
                {
                    float age = i.uv.y;
                    float h = i.signal.b;
                    float gaps = smoothstep(.36,.79,abs(sin(h * 6.4 + .85 * sin(h * 3.6))));
                    light = exp(-p.x * p.x * 3.8) * smoothstep(.09,.26,age)
                        * (1 - smoothstep(.62,1,age)) * (.09 + .91 * gaps) * .19;
                }
                return half4(_Color.rgb * (.124 * _Color.a * light * i.signal.a), 0);
            }
            ENDHLSL
        }
    }
}
