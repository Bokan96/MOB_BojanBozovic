Shader "Custom/TireGrid"
{
    Properties
    {
        _BaseColor ("Base Color",      Color) = (0.12, 0.12, 0.13, 1)
        _GridColor ("Grid Line Color", Color) = (0.22, 0.22, 0.24, 1)

        _GridScale ("Grid Scale (lines per UV unit)", Range(1, 64)) = 20
        _LineWidth ("Line Width",                     Range(0.01, 0.5)) = 0.15

        [Header(Outline)]
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.02
        _OutlineColor ("Outline Color", Color) = (0.06, 0.06, 0.07, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        // ===========================================
        // PASS 1: Tread Grid
        // ===========================================
        Pass
        {
            Name "TIRE_MAIN"
            Tags { "LightMode"="Always" }
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            fixed4 _BaseColor;
            fixed4 _GridColor;
            float  _GridScale;
            float  _LineWidth;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Scale UVs to set grid density
                float2 scaled = i.uv * _GridScale;

                // Distance to nearest grid line (0 = on line, 0.5 = tile centre)
                float2 grid      = frac(scaled);
                float2 distToEdge = min(grid, 1.0 - grid);
                float  minDist   = min(distToEdge.x, distToEdge.y);

                // Anti-aliased line using fwidth for stable thickness at any zoom
                float fw = fwidth(minDist);
                float lineMask = 1.0 - smoothstep(_LineWidth - fw, _LineWidth + fw, minDist);

                fixed3 col = lerp(_BaseColor.rgb, _GridColor.rgb, lineMask);
                return fixed4(col, 1.0);
            }
            ENDCG
        }

        // ===========================================
        // PASS 2: Inverted Hull Outline
        // ===========================================
        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode"="Always" }
            Cull Front
            ZWrite On
            Offset 1, 1

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            float  _OutlineWidth;
            fixed4 _OutlineColor;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f     { float4 pos    : SV_POSITION; };

            v2f vert(appdata v)
            {
                v2f o;
                float3 expanded = v.vertex.xyz + v.normal * _OutlineWidth;
                o.pos = UnityObjectToClipPos(float4(expanded, 1.0));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target { return _OutlineColor; }
            ENDCG
        }
    }

    Fallback "Unlit/Color"
}
