Shader "Heist/SoftDustParticle"
{
    Properties
    {
        _Color ("Tint Color", Color) = (1, 1, 1, 1)
        _FalloffPower ("Softness Falloff", Range(0.5, 3.0)) = 1.4
    }

    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent+10" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask RGB
        Cull Off 
        Lighting Off 
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_particles
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _FalloffPower;

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.uv = v.texcoord;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Procedural soft circle with smooth exponential falloff
                float2 centerVec = i.uv - 0.5;
                float distSq = dot(centerVec, centerVec) * 4.0; // 0 at center, 1 at boundary
                
                if (distSq >= 1.0) discard;

                float falloff = saturate(1.0 - distSq);
                falloff = pow(falloff, _FalloffPower);

                return fixed4(i.color.rgb, i.color.a * falloff);
            }
            ENDCG
        }
    }
}
