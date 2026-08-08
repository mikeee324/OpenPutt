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
        Tags { "Queue"="Transparent" "RenderType"="Opaque" }
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

            float Bayer2x2(int2 p)
            {
                int x = p.x & 1;
                int y = p.y & 1;

                if (y == 0)
                {
                    if (x == 0) return 0.0;
                    return 2.0;
                }

                if (x == 0) return 3.0;
                return 1.0;
            }

            float Bayer4x4(int2 p)
            {
                int x = p.x & 3;
                int y = p.y & 3;

                // 4x4 Bayer matrix normalized to 0..1.
                if (y == 0)
                {
                    if (x == 0) return (0.0 + 0.5) / 16.0;
                    if (x == 1) return (8.0 + 0.5) / 16.0;
                    if (x == 2) return (2.0 + 0.5) / 16.0;
                    return (10.0 + 0.5) / 16.0;
                }
                if (y == 1)
                {
                    if (x == 0) return (12.0 + 0.5) / 16.0;
                    if (x == 1) return (4.0 + 0.5) / 16.0;
                    if (x == 2) return (14.0 + 0.5) / 16.0;
                    return (6.0 + 0.5) / 16.0;
                }
                if (y == 2)
                {
                    if (x == 0) return (3.0 + 0.5) / 16.0;
                    if (x == 1) return (11.0 + 0.5) / 16.0;
                    if (x == 2) return (1.0 + 0.5) / 16.0;
                    return (9.0 + 0.5) / 16.0;
                }

                if (x == 0) return (15.0 + 0.5) / 16.0;
                if (x == 1) return (7.0 + 0.5) / 16.0;
                if (x == 2) return (13.0 + 0.5) / 16.0;
                return (5.0 + 0.5) / 16.0;
            }

            float Bayer8x8(int2 p)
            {
                int2 low = p & 3;
                int2 high = (p >> 2) & 1;

                // Build an 8x8 Bayer index from 4x4 + 2x2 components (0..63).
                float b4 = Bayer4x4(low) * 16.0 - 0.5;
                float b2 = Bayer2x2(high);
                float idx = b4 * 4.0 + b2;

                return (idx + 0.5) / 64.0;
            }

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

                // Ordered Bayer dithering in screen space; strength scales pattern density.
                float bayerScale = max(_DitherStrength, 1.0) * 0.25;
                int2 bayerCoord = int2(floor(i.vertex.xy * bayerScale));
                float dither = Bayer8x8(bayerCoord);

                // Hard cutout (clip pixels based on dither threshold)
                clip(dither - ditherThreshold);

                return col;
            }
            ENDCG
        }
    }
}
