Shader "Custom/SpriteOutline"
{
    Properties
    {
        _MainTex ("Sprite Sheet", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _OutlineColor ("Outline Color", Color) = (0.05, 0.05, 0.08, 1)
        _OutlineThickness ("Outline Thickness", Range(0, 0.015)) = 0.004
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.1
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        LOD 100

        Pass
        {
            Name "SPRITE_OUTLINE"
            Cull Off
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _OutlineColor;
            float _OutlineThickness;
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
                float4 tileBounds : TEXCOORD1; // xy = tileMin, zw = tileMax
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);

                // Apply tiling & offset (SpriteAnimator sets this via MaterialPropertyBlock)
                o.uv = v.uv * _MainTex_ST.xy + _MainTex_ST.zw;

                // Pass tile bounds so fragment shader can clamp neighbor samples
                float2 margin = float2(0.001, 0.001);
                o.tileBounds = float4(
                    _MainTex_ST.zw + margin,
                    _MainTex_ST.zw + _MainTex_ST.xy - margin
                );

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Sample center pixel
                fixed4 center = tex2D(_MainTex, i.uv);

                // If center is visible, render the sprite with tint
                if (center.a >= _Cutoff)
                {
                    return center * _Color;
                }

                // Center is transparent — check if any neighbor is visible
                float t = _OutlineThickness;
                float2 bMin = i.tileBounds.xy;
                float2 bMax = i.tileBounds.zw;

                float a1 = tex2D(_MainTex, clamp(i.uv + float2( t, 0), bMin, bMax)).a;
                float a2 = tex2D(_MainTex, clamp(i.uv + float2(-t, 0), bMin, bMax)).a;
                float a3 = tex2D(_MainTex, clamp(i.uv + float2(0,  t), bMin, bMax)).a;
                float a4 = tex2D(_MainTex, clamp(i.uv + float2(0, -t), bMin, bMax)).a;

                float maxNeighbor = max(max(a1, a2), max(a3, a4));

                // If any neighbor is visible, this is an outline pixel
                if (maxNeighbor >= _Cutoff)
                {
                    return _OutlineColor;
                }

                // Fully transparent — discard
                clip(-1);
                return fixed4(0, 0, 0, 0);
            }
            ENDCG
        }
    }

    Fallback "Unlit/Transparent Cutout"
}
