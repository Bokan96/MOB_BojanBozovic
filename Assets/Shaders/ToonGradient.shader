Shader "Custom/ToonGradient"
{
    Properties
    {
        [Header(Gradient)]
        _ColorTop ("Top Color", Color) = (0.85, 0.9, 0.95, 1)
        _ColorBottom ("Bottom Color", Color) = (0.55, 0.6, 0.7, 1)
        _GradientScale ("Gradient Scale", Range(0.1, 10.0)) = 1.5
        _GradientOffset ("Gradient Y Offset", Range(-10, 3)) = 0.5

        [Header(Toon Shading)]
        _MidTint ("Mid-Tone Tint", Color) = (0.75, 0.75, 0.75, 1)
        _ShadowTint ("Shadow Tint", Color) = (0.5, 0.5, 0.55, 1)
        _HighlightThreshold ("Highlight Threshold", Range(-1, 1)) = 0.5
        _ShadowThreshold ("Shadow Threshold", Range(-1, 1)) = 0.0

        [Header(Rim Highlight)]
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimIntensity ("Rim Intensity", Range(0, 0.6)) = 0.2

        [Header(Outline)]
        _OutlineWidth ("Outline Width", Range(0, 0.15)) = 0.025
        _OutlineColor ("Outline Color", Color) = (0.08, 0.08, 0.12, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        // ===========================================
        // PASS 1: Main Toon Gradient
        // ===========================================
        Pass
        {
            Name "TOON_MAIN"
            Tags { "LightMode"="ForwardBase" }
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            fixed4 _ColorTop;
            fixed4 _ColorBottom;
            float _GradientScale;
            float _GradientOffset;
            fixed4 _MidTint;
            fixed4 _ShadowTint;
            float _HighlightThreshold;
            float _ShadowThreshold;
            fixed4 _RimColor;
            float _RimPower;
            float _RimIntensity;

            // Light from Top-Left-Front (perfect for 3/4 perspective cubes)
            static const float3 LIGHT_DIR = normalize(float3(-0.4, 0.8, -0.4));

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed3 color : COLOR0;
                fixed rim : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);

                // --- Gradient (object-space Y) ---
                float gradT = saturate(v.vertex.y * _GradientScale + _GradientOffset);
                fixed3 gradientCol = lerp(_ColorBottom.rgb, _ColorTop.rgb, gradT);

                // --- Toon shading (fake directional light, 3-step) ---
                float3 wNormal = normalize(UnityObjectToWorldNormal(v.normal));
                float NdotL = dot(wNormal, LIGHT_DIR);
                
                // 3 discrete light bands
                float toonMid = smoothstep(_ShadowThreshold - 0.05, _ShadowThreshold + 0.05, NdotL);
                float toonHigh = smoothstep(_HighlightThreshold - 0.05, _HighlightThreshold + 0.05, NdotL);

                fixed3 midColor = lerp(_ShadowTint.rgb, _MidTint.rgb, toonMid);
                fixed3 finalLight = lerp(midColor, fixed3(1,1,1), toonHigh);

                o.color = gradientCol * finalLight;

                // --- Rim highlight ---
                float3 wPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 viewDir = normalize(_WorldSpaceCameraPos - wPos);
                float rimDot = 1.0 - saturate(dot(viewDir, wNormal));
                o.rim = pow(rimDot, _RimPower) * _RimIntensity;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed3 col = i.color + _RimColor.rgb * i.rim;
                return fixed4(col, 1);
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

            float _OutlineWidth;
            fixed4 _OutlineColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 expanded = v.vertex.xyz + v.normal * _OutlineWidth;
                o.pos = UnityObjectToClipPos(float4(expanded, 1.0));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }
    }

    Fallback "Unlit/Color"
}
