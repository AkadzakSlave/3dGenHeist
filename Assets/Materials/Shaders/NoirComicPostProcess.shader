Shader "Hidden/Heist/NoirComicPostProcess"
{
    Properties
    {
        _MainTex ("Source Texture", 2D) = "white" {}
        
        [Header(Ink Outlines)]
        _EnableOutline ("Enable Outlines", Float) = 1
        _EdgeThreshold ("Outline Sensitivity", Range(0.01, 1.0)) = 0.22
        _EdgeThickness ("Outline Thickness", Range(0.5, 3.0)) = 1.0
        _OutlineColor ("Outline Color", Color) = (0.05, 0.05, 0.05, 1.0)
        
        [Header(Brightness and Contrast)]
        _Brightness ("Brightness Boost", Range(0.5, 2.5)) = 1.0
        _Contrast ("Contrast", Range(0.5, 1.8)) = 1.0
        _Saturation ("Saturation", Range(0.0, 1.5)) = 0.95
        _ShadowCrush ("Shadow Crush", Range(0.0, 0.5)) = 0.0
        
        [Header(Cel Shading Banding)]
        _EnableCelBanding ("Enable Cel Steps", Float) = 1
        _PosterizeSteps ("Cel Light Steps", Range(2, 16)) = 6
        
        [Header(Atmosphere)]
        _VignetteIntensity ("Vignette Intensity", Range(0.0, 0.8)) = 0.15
        _VignetteSmoothness ("Vignette Smoothness", Range(0.1, 1.0)) = 0.6
    }

    SubShader
    {
        Cull Off 
        ZWrite Off 
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            sampler2D _CameraDepthTexture;

            float _EnableOutline;
            float _EdgeThreshold;
            float _EdgeThickness;
            fixed4 _OutlineColor;

            float _Brightness;
            float _Contrast;
            float _Saturation;
            float _ShadowCrush;

            float _EnableCelBanding;
            float _PosterizeSteps;
            float _VignetteIntensity;
            float _VignetteSmoothness;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float GetLuminance(float3 col)
            {
                return dot(col, float3(0.299, 0.587, 0.114));
            }

            float SampleLinearDepth(float2 uv)
            {
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
                return Linear01Depth(rawDepth);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                fixed4 sourceColor = tex2D(_MainTex, uv);
                float3 col = sourceColor.rgb;

                // 1. Brightness pre-adjust
                col *= _Brightness;

                // 2. SOBEL INK OUTLINE DETECTION
                float isOutline = 0.0;
                if (_EnableOutline > 0.5)
                {
                    float2 t = _MainTex_TexelSize.xy * _EdgeThickness;

                    // Sample Depth at cross neighbors
                    float dCenter = SampleLinearDepth(uv);
                    float dUp     = SampleLinearDepth(uv + float2(0, t.y));
                    float dDown   = SampleLinearDepth(uv - float2(0, t.y));
                    float dLeft   = SampleLinearDepth(uv - float2(t.x, 0));
                    float dRight  = SampleLinearDepth(uv + float2(t.x, 0));

                    float depthScale = 1.0 / max(dCenter, 0.02);
                    float depthDelta = (abs(dCenter - dUp) + abs(dCenter - dDown) + 
                                        abs(dCenter - dLeft) + abs(dCenter - dRight)) * depthScale * 25.0;

                    // Sample Luminance at cross neighbors for inner edge creases
                    float lCenter = GetLuminance(col);
                    float lUp     = GetLuminance(tex2D(_MainTex, uv + float2(0, t.y)).rgb * _Brightness);
                    float lDown   = GetLuminance(tex2D(_MainTex, uv - float2(0, t.y)).rgb * _Brightness);
                    float lLeft   = GetLuminance(tex2D(_MainTex, uv - float2(t.x, 0)).rgb * _Brightness);
                    float lRight  = GetLuminance(tex2D(_MainTex, uv + float2(t.x, 0)).rgb * _Brightness);

                    float lumDelta = (abs(lCenter - lUp) + abs(lCenter - lDown) + 
                                      abs(lCenter - lLeft) + abs(lCenter - lRight)) * 1.5;

                    float totalEdge = depthDelta + lumDelta;
                    isOutline = smoothstep(_EdgeThreshold, _EdgeThreshold + 0.12, totalEdge);
                }

                // 3. CEL POSTERIZATION / LIGHT BANDING (Безопасная квантовка без ухода в черноту)
                if (_EnableCelBanding > 0.5 && _PosterizeSteps > 1.5)
                {
                    float lum = GetLuminance(col);
                    if (lum > 0.01)
                    {
                        // Ступенчатый расчет с сохранением базовой освещенности
                        float steppedLum = floor(lum * _PosterizeSteps + 0.5) / _PosterizeSteps;
                        steppedLum = max(steppedLum, lum * 0.4); // Защита: полутени никогда не падают в абсолютный ноль
                        col = col * (steppedLum / lum);
                    }
                }

                // 4. SHADOW CRUSH (Мягкое управление глубиной теней)
                if (_ShadowCrush > 0.005)
                {
                    col = max(float3(0, 0, 0), col - _ShadowCrush) / (1.0 - _ShadowCrush + 0.0001);
                }

                // 5. CONTRAST (Линейный контраст с опорной точкой 0.5)
                if (abs(_Contrast - 1.0) > 0.01)
                {
                    col = (col - 0.5) * _Contrast + 0.5;
                    col = saturate(col);
                }

                // 6. SATURATION
                if (abs(_Saturation - 1.0) > 0.01)
                {
                    float finalLum = GetLuminance(col);
                    col = lerp(float3(finalLum, finalLum, finalLum), col, _Saturation);
                }

                // 7. INK OUTLINE OVERLAY
                col = lerp(col, _OutlineColor.rgb, isOutline);

                // 8. CINEMATIC VIGNETTE
                if (_VignetteIntensity > 0.005)
                {
                    float2 coord = (uv - 0.5) * 2.0;
                    float dist = dot(coord, coord);
                    float vignette = 1.0 - smoothstep(_VignetteSmoothness, 1.4, dist) * _VignetteIntensity;
                    col *= saturate(vignette);
                }

                return fixed4(col, sourceColor.a);
            }
            ENDCG
        }
    }
}
