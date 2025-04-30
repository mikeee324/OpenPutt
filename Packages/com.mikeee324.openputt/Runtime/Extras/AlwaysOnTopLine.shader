Shader "Custom/AlwaysOnTopLine"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1) // The color of the line
    }
    SubShader
    {
        // Set the render queue to a value higher than typical opaque geometry
        // This ensures the line is rendered after most other objects.
        // "Transparent+1" is a common choice for overlay effects.
        Tags { "RenderType"="Transparent" "Queue"="Transparent+1" }
        LOD 100

        // Disable depth writing so the line doesn't block other objects behind it
        ZWrite Off
        // Always pass the depth test, ensuring the line is drawn regardless of depth
        ZTest Always
        // Enable blending for transparency if needed
        Blend SrcAlpha OneMinusSrcAlpha

        // Disable backface culling so both sides of the line geometry are rendered
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0; // Not strictly needed for a simple color line
                // Removed float4 color : COLOR; // We will ignore the vertex color
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR; // Pass the desired color to fragment shader
            };

            fixed4 _Color; // Shader property color

            v2f vert (appdata v)
            {
                v2f o;
                // Transform vertex position from object space to clip space
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                // Pass only the shader property color to the fragment shader
                o.color = _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Return the calculated color (which is just the material's _Color)
                return i.color;
            }
            ENDCG
        }
    }
}
