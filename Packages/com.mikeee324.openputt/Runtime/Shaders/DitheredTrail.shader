Shader "Custom/DitheredTrailCutout"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _DitherFade ("Dither Fade", Range(0, 1)) = 0.5
        _DitherStrength ("Dither Strength", Range(1, 100)) = 20
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="Opaque" }
        Cull Off
        ZWrite On
        AlphaToMask On // Enables MSAA-based transparency
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _DitherFade;
            float _DitherStrength;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;

                // Trail age (0 = new, 1 = old)
                float trailAge = i.uv.x;

                // Dithering fade calculation
                float ditherThreshold = saturate((trailAge - (1.0 - _DitherFade)) / _DitherFade);

                // Dither pattern (adjustable strength)
                float dither = frac(sin(dot(i.uv, float2(12.9898, 78.233) * _DitherStrength)) * 43758.5453);

                // Hard cutout (clip pixels based on dither threshold)
                clip(dither - ditherThreshold);

                return col;
            }
            ENDCG
        }
    }
}
