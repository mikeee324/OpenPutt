// Rounded-rect UI shader with border, drawn from a signed distance field.
// Image Type = Simple, fill from vertex colour, border from _BorderColor.
// Source Image is optional and multiplies the fill only - it must not be atlased.
Shader "OpenPutt/UI/RoundedRect"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        // Local units, ordered TL, TR, BR, BL. 0 = square corner.
        _CornerRadius ("Corner Radius (TL,TR,BR,BL)", Vector) = (0.0533,0.0533,0.0533,0.0533)
        _BorderWidth ("Border Width (local units)", Float) = 0.0067
        _BorderColor ("Border Colour", Color) = (0.29803922,0.3372549,0.41568628,1)
        [Toggle(_SUPERSAMPLE)] _Supersample ("Supersample Edges (2x2 RGSS)", Float) = 1
        // LEqual matches what a world space Canvas would set, Always draws over solid
        // geometry. Leave the render queue alone - overriding it on a material inside a
        // Canvas breaks hierarchy draw order and the element paints over its own children.
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth Test", Float) = 4

        // Diagonal highlight sweep, ported from OpenPutt/AlwaysOnTopUI. Property names
        // match that shader so values carry across. _Color0 alpha is unused, as there.
        [Toggle(_SHINEENABLE_ON)] _ShineEnable ("Shine Enable", Float) = 0
        [HDR] _Color0 ("Shine Colour", Color) = (1,1,1,0)
        _ShineWidth ("Shine Width", Float) = 0.1
        _ShineSpeed ("Shine Speed", Float) = 1
        _Float0 ("Shine Sweep Scale", Float) = 10
        _Float1 ("Shine Sweep Offset", Float) = -4.5

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [_ZTest]
        Blend SrcAlpha OneMinusSrcAlpha

        CGINCLUDE
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color  : COLOR;
                float2 uv     : TEXCOORD0;
                // Same uv, centroid sampled - safe to shade at under MSAA, useless to differentiate.
                centroid float2 uvShade : TEXCOORD1;
                float2 objPos : TEXCOORD2;
                float4 mask   : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _BorderColor;
            float4 _ClipRect;
            float4 _CornerRadius;
            float _BorderWidth;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            fixed4 _Color0;
            float _ShineWidth;
            float _ShineSpeed;
            float _Float0;
            float _Float1;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float4 clipPos = UnityObjectToClipPos(v.vertex);
                o.vertex = clipPos;
                o.color  = v.color * _Color;
                o.uv     = o.uvShade = v.uv;
                o.objPos = v.vertex.xy;

                // Softened RectMask2D edge, same form as UI-Default.
                float2 pixelSize = clipPos.w;
                pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                o.mask = float4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw,
                                0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));
                return o;
            }

            // Negative inside, zero on the edge.
            float RoundedRectSDF(float2 p, float2 halfSize)
            {
                // Radius picked per quadrant; uv (0,0) is bottom-left.
                float2 rr = (p.x < 0) ? _CornerRadius.xw : _CornerRadius.yz;
                float r = clamp((p.y > 0) ? rr.x : rr.y, 0, min(halfSize.x, halfSize.y));

                float2 q = abs(p) - halfSize + r;
                return length(max(q, 0)) + min(max(q.x, q.y), 0) - r;
            }

            // x = coverage of the shape, y = coverage eroded by the border width.
            float2 ShapeCoverage(v2f i)
            {
                // Rect size from the inverted screen-space Jacobian, so rotation and
                // grazing angles still give the right local units.
                float2 dpdx = ddx(i.objPos), dpdy = ddy(i.objPos);
                float2 dudx = ddx(i.uv), dudy = ddy(i.uv);
                float det = max(abs(dudx.x * dudy.y - dudy.x * dudx.y), 1e-12);
                float2 size = abs(float2(dpdx.x * dudy.y - dpdy.x * dudx.y,
                                         dpdy.y * dudx.x - dpdx.y * dudy.x)) / det;

                float2 halfSize = size * 0.5;
                float2 p = (i.uvShade - 0.5) * size;

                // One pixel of distance, so edges stay 1px wide at any zoom.
                float aa = max(fwidth(RoundedRectSDF((i.uv - 0.5) * size, halfSize)), 1e-8);

            #ifdef _SUPERSAMPLE
                // 2x2 rotated grid - fixes crawling in tight corners and sub-pixel border bands.
                float2 pdx = dudx * size, pdy = dudy * size;
                float2 s0 = 0.125 * pdx + 0.375 * pdy;
                float2 s1 = 0.375 * pdx - 0.125 * pdy;

                float4 d = float4(RoundedRectSDF(p + s0, halfSize), RoundedRectSDF(p - s0, halfSize),
                                  RoundedRectSDF(p + s1, halfSize), RoundedRectSDF(p - s1, halfSize));
                float4 quarter = 0.25;
                return float2(dot(saturate(0.5 - d / aa), quarter),
                              dot(saturate(0.5 - (d + _BorderWidth) / aa), quarter));
            #else
                float d = RoundedRectSDF(p, halfSize);
                return saturate(0.5 - float2(d, d + _BorderWidth) / aa);
            #endif
            }

            // Band travelling along the uv diagonal. The sweep runs well past 0..1 either
            // side, so the highlight only crosses the rect for part of each cycle.
            float ShineBand(float2 uv)
            {
                float d = (uv.x + uv.y) * 0.5;
                float sweep = frac(_Time.y * _ShineSpeed) * _Float0 + _Float1;
                return 1 - smoothstep(0, _ShineWidth, abs(d - sweep));
            }

            float RectClip(v2f i)
            {
            #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(i.mask.xy)) * i.mask.zw);
                return m.x * m.y;
            #else
                return 1;
            #endif
            }
        ENDCG

        // Colour pass - draws fill and border, never writes the stencil.
        Pass
        {
            Name "Default"

            Stencil
            {
                Ref [_Stencil]
                Comp [_StencilComp]
                Pass Keep
                ReadMask [_StencilReadMask]
                WriteMask 0
            }

            ColorMask [_ColorMask]

        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #pragma shader_feature_local _SUPERSAMPLE
            // multi_compile, not shader_feature: OpenPuttCalloutBox toggles this with
            // EnableKeyword at runtime, and shader_feature variants no material ships
            // with enabled get stripped from the build.
            #pragma multi_compile_local _ _SHINEENABLE_ON

            fixed4 frag(v2f i) : SV_Target
            {
                // Sprite folded into the vertex colour so it only reaches the fill.
                i.color *= tex2Dgrad(_MainTex, i.uvShade, ddx(i.uv), ddy(i.uv));

                // Snap alpha to 1/255 steps - the low bits band against an HDR scene.
                const half alphaPrecision = half(0xff);
                i.color.a = round(i.color.a * alphaPrecision) / alphaPrecision;

                float2 cov = ShapeCoverage(i);

                // Composited by coverage, not lerped, so a zero-width border leaves no fringe.
                float fillCov = cov.y * i.color.a;
                float bordCov = max(cov.x - cov.y, 0) * _BorderColor.a;
                float total   = fillCov + bordCov;

                fixed4 col;
                col.rgb = (i.color.rgb * fillCov + _BorderColor.rgb * bordCov) / max(total, 1e-6);
                col.a   = total * RectClip(i);

                #ifdef _SHINEENABLE_ON
                // Gated on solid coverage, so the sweep can't crawl along the antialiased
                // edge. Additive on rgb only - alpha stays the shape's, as in AlwaysOnTopUI.
                col.rgb += ShineBand(i.uvShade) * _Color0.rgb * step(0.9, total);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
        ENDCG
        }

        // Stencil pass - marks only the fill, so children stop at the inside of the border.
        Pass
        {
            Name "MaskStencil"

            ColorMask 0

        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma shader_feature_local _SUPERSAMPLE

            fixed4 frag(v2f i) : SV_Target
            {
                clip(ShapeCoverage(i).y * RectClip(i) - 0.5);
                return 0;
            }
        ENDCG
        }
    }
}
