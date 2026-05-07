Shader "Custom/SpriteToon"
{
    Properties
    {
        _MainTex ("Sprite Sheet", 2D) = "white" {}

        [Header(Tint and Gradient)]
        _ColorTop ("Top Tint", Color) = (1, 1, 1, 1)
        _ColorBottom ("Bottom Tint", Color) = (0.7, 0.7, 0.8, 1)
        _GradientShift ("Gradient Shift", Range(-1, 1)) = 0.0

        [Header(Shading)]
        _ShadowStrength ("Shadow Strength", Range(0, 0.6)) = 0.25
        _ShadowOffset ("Shadow X Offset", Range(-1, 1)) = 0.3

        [Header(Alpha)]
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.1
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        LOD 100

        Pass
        {
            Name "SPRITE_TOON"
            Cull Off
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _ColorTop;
            fixed4 _ColorBottom;
            float _GradientShift;
            float _ShadowStrength;
            float _ShadowOffset;
            float _Cutoff;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 localUV : TEXCOORD1; // raw 0-1 UV for gradient
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);

                // Transformed UV for sprite-sheet sampling (SpriteAnimator sets _MainTex_ST)
                o.uv = v.uv * _MainTex_ST.xy + _MainTex_ST.zw;

                // Raw UV for gradient calculation (always 0-1 across the quad)
                o.localUV = v.uv;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Sample sprite texture
                fixed4 tex = tex2D(_MainTex, i.uv);

                // Alpha test
                clip(tex.a - _Cutoff);

                // --- Vertical gradient tint ---
                // localUV.y goes 0 (bottom) to 1 (top) across the quad
                float gradT = saturate(i.localUV.y + _GradientShift);
                fixed4 tint = lerp(_ColorBottom, _ColorTop, gradT);

                // --- Fake side shadow ---
                // Uses horizontal UV to darken one side, simulating directional light
                // _ShadowOffset > 0 means light comes from the right (left side darker)
                float shadowT = saturate(i.localUV.x * sign(_ShadowOffset) +
                                         (1.0 - abs(_ShadowOffset)));
                // Smoothstep for a soft transition
                float shadow = lerp(1.0 - _ShadowStrength, 1.0, smoothstep(0.0, 0.6, shadowT));

                // Combine: texture × gradient tint × shadow
                fixed4 col;
                col.rgb = tex.rgb * tint.rgb * shadow;
                col.a = tex.a;

                return col;
            }
            ENDCG
        }
    }

    Fallback "Unlit/Transparent Cutout"
}
