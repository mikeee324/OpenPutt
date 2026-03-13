Shader "UI/RoundedRectMaskable"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Radius ("Corner Radius", Range(0, 0.5)) = 0.1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        LOD 100
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "Default"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
                ReadMask 255
                WriteMask 255
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _Color;
            float _Radius;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 dist = min(uv, 1.0 - uv);
                float d = min(dist.x, dist.y);
                float alpha = smoothstep(_Radius - 0.01, _Radius + 0.01, d);
                fixed4 col = _Color;
                col.a *= alpha;
                return col;
            }
            ENDCG
        }
    }
    FallBack "UI/Default"
}