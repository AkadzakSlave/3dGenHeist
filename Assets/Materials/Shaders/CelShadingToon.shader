Shader "Heist/CelShadingToon"
{
    Properties
    {
        [Header(Base)]
        _MainTex ("Base Texture", 2D) = "white" {}
        _Color ("Base Tint", Color) = (1, 1, 1, 1)
        
        [Header(Cel Lighting)]
        _ShadowColor ("Shadow Tint", Color) = (0.15, 0.15, 0.2, 1.0)
        _ShadowThreshold ("Shadow Threshold", Range(0.0, 1.0)) = 0.45
        _ShadowSmoothness ("Shadow Smoothness", Range(0.001, 0.2)) = 0.03
        
        [Header(Specular Highlight)]
        [Toggle(_SPECULAR_ON)] _EnableSpecular ("Enable Specular", Float) = 1
        _SpecularColor ("Specular Color", Color) = (0.9, 0.9, 0.9, 1.0)
        _SpecularThreshold ("Specular Threshold", Range(0.0, 1.0)) = 0.85
        _Glossiness ("Glossiness / Shininess", Range(0.05, 1.0)) = 0.4
        
        [Header(Rim Lighting)]
        [Toggle(_RIM_ON)] _EnableRim ("Enable Rim Light", Float) = 1
        _RimColor ("Rim Color", Color) = (0.7, 0.7, 0.8, 1.0)
        _RimThreshold ("Rim Threshold", Range(0.0, 1.0)) = 0.65
        
        [Header(Ink Outline)]
        [Toggle(_OUTLINE_ON)] _EnableOutline ("Enable Outline", Float) = 1
        _OutlineColor ("Outline Color", Color) = (0.05, 0.05, 0.05, 1.0)
        _OutlineWidth ("Outline Width", Range(0.0, 0.03)) = 0.006
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        // PASS 1: INVERTED HULL INK OUTLINE (Borderlands / Lethal Company comic line art)
        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode"="Always" }
            Cull Front
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed _EnableOutline;
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
                if (_EnableOutline < 0.5 || _OutlineWidth <= 0.0001)
                {
                    o.pos = float4(0, 0, 0, 0);
                    return o;
                }

                // Extrude vertex along normal in view/clip space for clean perspective scaling
                float4 clipPos = UnityObjectToClipPos(v.vertex);
                float3 clipNormal = mul((float3x3)UNITY_MATRIX_ITMV, v.normal);
                
                // Scale width with distance (clipPos.w) to maintain readable ink lines at distance
                float2 offset = normalize(clipNormal.xy) * _OutlineWidth * clipPos.w;
                clipPos.xy += offset;
                o.pos = clipPos;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }

        // PASS 2: CEL-SHADED SURFACE PASS (With dynamic shadows, spot/point light support)
        Cull Back
        ZWrite On
        
        CGPROGRAM
        #pragma surface surf CelShading fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        fixed4 _ShadowColor;
        half _ShadowThreshold;
        half _ShadowSmoothness;
        
        half _EnableSpecular;
        fixed4 _SpecularColor;
        half _SpecularThreshold;
        half _Glossiness;
        
        half _EnableRim;
        fixed4 _RimColor;
        half _RimThreshold;

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;
        };

        // Custom Toon / Cel Lighting Model
        half4 LightingCelShading(SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)
        {
            half NdotL = dot(s.Normal, lightDir);
            
            // Half-Lambert wrapping + stepped Cel Ramp
            half halfLambert = NdotL * 0.5 + 0.5;
            half lightIntensity = halfLambert * atten;
            half celRamp = smoothstep(_ShadowThreshold - _ShadowSmoothness, _ShadowThreshold + _ShadowSmoothness, lightIntensity);
            
            // Diffuse Cel color
            half3 lightCol = _LightColor0.rgb;
            half3 baseDiffuse = lerp(_ShadowColor.rgb * s.Albedo, s.Albedo * lightCol, celRamp);
            
            // Specular Highlight (Cel stepped)
            half3 specularFinal = half3(0, 0, 0);
            if (_EnableSpecular > 0.5)
            {
                half3 h = normalize(lightDir + viewDir);
                half NdotH = max(0, dot(s.Normal, h));
                half specPower = pow(NdotH, _Glossiness * 128.0);
                half specStep = smoothstep(_SpecularThreshold - 0.02, _SpecularThreshold + 0.02, specPower * atten);
                specularFinal = _SpecularColor.rgb * specStep * lightCol;
            }
            
            // Rim Light (Fresnel silhouette glow)
            half3 rimFinal = half3(0, 0, 0);
            if (_EnableRim > 0.5)
            {
                half rim = 1.0 - max(0, dot(s.Normal, viewDir));
                half rimStep = smoothstep(_RimThreshold - 0.05, _RimThreshold + 0.05, rim) * max(0, NdotL + 0.2);
                rimFinal = _RimColor.rgb * rimStep * lightCol;
            }

            half4 c;
            c.rgb = baseDiffuse + specularFinal + rimFinal;
            c.a = s.Alpha;
            return c;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed4 col = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = col.rgb;
            o.Alpha = col.a;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
