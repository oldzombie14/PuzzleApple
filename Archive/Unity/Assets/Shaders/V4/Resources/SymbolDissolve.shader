Shader "PuzzleApple/V4/SymbolDissolve"
{
    Properties
    {
        [PerRendererData] _MainTex("Symbol", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _Progress("Dissolve", Range(0,1)) = 0
        _InkSize("Ink size", Vector) = (64,64,0,0)
        _Seed("Seed", Float) = 0
        _StencilComp("Stencil Comparison", Float) = 8
        _Stencil("Stencil ID", Float) = 0
        _StencilOp("Stencil Operation", Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask("Stencil Read Mask", Float) = 255
        _ColorMask("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            sampler2D _MainTex;
            fixed4 _Color, _TextureSampleAdd;
            float4 _ClipRect, _InkSize;
            float _Progress, _Seed;
            struct appdata { float4 vertex:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float4 local:TEXCOORD1; };
            v2f vert(appdata i)
            {
                v2f o; o.local=i.vertex; o.vertex=UnityObjectToClipPos(i.vertex);
                o.color=i.color*_Color; o.uv=i.uv; return o;
            }
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453);
            }
            float noise(float2 p)
            {
                float2 cell=floor(p), f=frac(p);
                f=f*f*(3-2*f);
                return lerp(lerp(hash(cell),hash(cell+float2(1,0)),f.x),
                    lerp(hash(cell+float2(0,1)),hash(cell+float2(1,1)),f.x),f.y);
            }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 color=(tex2D(_MainTex,i.uv)+_TextureSampleAdd)*i.color;
                // Local glyph coordinates keep broad regions consistent even in sprite atlases.
                float2 p=i.local.xy/max(_InkSize.xy,float2(1,1))*3.4+_Seed;
                float field=noise(p)*.8+noise(p*1.9+13.7)*.2;
                float threshold=lerp(-.22,1.22,_Progress);
                float remaining=smoothstep(threshold-.16,threshold+.16,field);
                color.a*=remaining*(1-.25*_Progress);
                #ifdef UNITY_UI_CLIP_RECT
                color.a*=UnityGet2DClipping(i.local.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a-.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
