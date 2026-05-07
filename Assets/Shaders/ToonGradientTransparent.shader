Shader "Custom/ToonGradientTransparent"
{
    Properties
    {
        [Header(Gradient)]
        _ColorTop ("Top Color", Color) = (0.85, 0.9, 0.95, 1)
        _ColorBottom ("Bottom Color", Color) = (0.55, 0.6, 0.7, 1)
        _Alpha ("Transparency", Range(0, 1)) = 0.5
        _GradientScale ("Gradient Scale", Range(0.1, 10.0)) = 1.5
        _GradientOffset ("Gradient Y Offset", Range(-3, 3)) = 0.5

        [Header(Toon Shading)]
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.35
        _ShadowThreshold ("Shadow Threshold", Range(-1, 1)) = 0.1

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
        Tags { "RenderType"="Transparent" "Queue"="Transparent+50" }
        LOD 100
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

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
            float _Alpha;
            float _GradientScale;
            float _GradientOffset;
            float _ShadowStrength;
            float _ShadowThreshold;
            fixed4 _RimColor;
            float _RimPower;
            float _RimIntensity;

            static const float3 LIGHT_DIR = normalize(float3(0.4, 0.9, 0.3));

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

                float gradT = saturate(v.vertex.y * _GradientScale + _GradientOffset);
                fixed3 gradientCol = lerp(_ColorBottom.rgb, _ColorTop.rgb, gradT);

                float3 wNormal = normalize(UnityObjectToWorldNormal(v.normal));
                float NdotL = dot(wNormal, LIGHT_DIR);
                float toon = smoothstep(_ShadowThreshold - 0.05, _ShadowThreshold + 0.05, NdotL);
                fixed3 shadowTint = fixed3(1.0 - _ShadowStrength, 1.0 - _ShadowStrength, 1.0 - _ShadowStrength);
                o.color = gradientCol * lerp(shadowTint, fixed3(1,1,1), toon);

                float3 wPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 viewDir = normalize(_WorldSpaceCameraPos - wPos);
                float rimDot = 1.0 - saturate(dot(viewDir, wNormal));
                o.rim = pow(rimDot, _RimPower) * _RimIntensity;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed3 col = i.color + _RimColor.rgb * i.rim;
                return fixed4(col, _Alpha);
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
            ZWrite Off
            Offset 1, 1

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            float _OutlineWidth;
            fixed4 _OutlineColor;
            float _Alpha;

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
                return fixed4(_OutlineColor.rgb, _Alpha * _OutlineColor.a);
            }
            ENDCG
        }
    }

    Fallback "Transparent/VertexLit"
}
