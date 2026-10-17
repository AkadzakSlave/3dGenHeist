Shader "Heist/FortniteTrajectory"
{
    Properties
    {
        _Color("Trajectory Color", Color) = (0.0, 0.85, 1.0, 0.95)
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent+200" 
            "IgnoreProjector"="True"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
            };

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }
    FallBack Off
}
