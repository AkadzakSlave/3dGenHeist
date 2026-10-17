Shader "Heist/InteractablePinkGlow"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (0.2, 0.85, 1.0, 0.9)
        _OutlineWidth("Outline Width", Range(0.001, 0.05)) = 0.015
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent+150" 
            "RenderPipeline"="UniversalPipeline" 
        }

        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite Off
            ZTest LEqual
            Offset 1, 1
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(input.normalOS);
                
                posWS += normWS * _OutlineWidth;

                output.positionCS = TransformWorldToHClip(posWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
