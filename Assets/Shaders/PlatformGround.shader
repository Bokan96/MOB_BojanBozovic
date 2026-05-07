Shader "Custom/PlatformGround"
{
    Properties
    {
        [Header(Gradient)]
        _ColorTop    ("Top Color",    Color) = (0.55, 0.62, 0.45, 1)
        _ColorBottom ("Bottom Color", Color) = (0.35, 0.40, 0.28, 1)
        _GradientScale  ("Gradient Scale",    Range(0.1, 10)) = 2.0
        _GradientOffset ("Gradient Y Offset", Range(-3,  3))  = 0.5

        [Header(Grid Lines)]
        _GridColor  ("Grid Line Color",  Color)        = (0.25, 0.30, 0.20, 1)
        _GridScale  ("Grid Scale (tiles per unit)", Range(0.1, 4.0)) = 1.0
        _GridWidth  ("Line Width",       Range(0.01, 0.5)) = 0.06
        _GridTop    ("Top-Face Only",    Range(0, 1)) = 1  // 1 = only draw grid on upward-facing faces

        [Header(Toon Shading)]
        _ShadowStrength  ("Shadow Strength",  Range(0, 1))   = 0.35
        _ShadowThreshold ("Shadow Threshold", Range(-1, 1))  = 0.1

        [Header(Outline)]
        _OutlineWidth ("Outline Width", Range(0, 0.15)) = 0.03
        _OutlineColor ("Outline Color", Color) = (0.08, 0.08, 0.12, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        // ===========================================
        // PASS 1: Platform + Grid
        // ===========================================
        Pass
        {
            Name "PLATFORM_MAIN"
            Tags { "LightMode"="ForwardBase" }
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            fixed4 _ColorTop;
            fixed4 _ColorBottom;
            float  _GradientScale;
            float  _GradientOffset;
            fixed4 _GridColor;
            float  _GridScale;
            float  _GridWidth;
            float  _GridTop;
            float  _ShadowStrength;
            float  _ShadowThreshold;

            static const float3 LIGHT_DIR = normalize(float3(0.4, 0.9, 0.3));

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos     : SV_POSITION;
                float3 wPos    : TEXCOORD0;  // world position for grid
                float3 wNormal : TEXCOORD1;
                float  gradT   : TEXCOORD2;
                float  toon    : TEXCOORD3;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos     = UnityObjectToClipPos(v.vertex);
                o.wPos    = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.wNormal = normalize(UnityObjectToWorldNormal(v.normal));

                // Gradient using object-space Y
                o.gradT = saturate(v.vertex.y * _GradientScale + _GradientOffset);

                // Toon shading
                float NdotL = dot(o.wNormal, LIGHT_DIR);
                o.toon = smoothstep(_ShadowThreshold - 0.05, _ShadowThreshold + 0.05, NdotL);

                return o;
            }

            // Returns 1.0 when worldPos is on a grid line, 0.0 otherwise
            float GridMask(float3 wPos)
            {
                // Use world XZ for the grid pattern
                float2 grid = frac(wPos.xz * _GridScale);

                // Distance to nearest edge (0 = on line, 0.5 = midpoint of tile)
                float2 distToLine = min(grid, 1.0 - grid);
                float  minDist    = min(distToLine.x, distToLine.y);

                // Anti-aliased line using fwidth for stable thickness at any camera distance
                float  fw = fwidth(minDist);
                return 1.0 - smoothstep(_GridWidth - fw, _GridWidth + fw, minDist);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // --- Gradient colour ---
                fixed3 col = lerp(_ColorBottom.rgb, _ColorTop.rgb, i.gradT);

                // --- Grid overlay ---
                // Optionally restrict to top-facing surface (NdotUp > 0.5)
                float upFace = saturate(dot(i.wNormal, float3(0,1,0)) - 0.4);
                float doGrid = lerp(1.0, upFace, _GridTop);

                float gridM = GridMask(i.wPos) * doGrid;
                col = lerp(col, _GridColor.rgb, gridM);

                // --- Toon shadow ---
                float shadow = lerp(1.0 - _ShadowStrength, 1.0, i.toon);
                col *= shadow;

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
