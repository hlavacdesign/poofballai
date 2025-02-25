Shader "Custom/PixelVideo"
{
    Properties
    {
        // Video Texture A
        _VideoTexA ("Video Texture A", 2D) = "white" {}
        // Video Texture B
        _VideoTexB ("Video Texture B", 2D) = "white" {}
        // Blend parameter (0 = fully A, 1 = fully B)
        _Blend ("Blend", Range(0,1)) = 0.0

        // Pixel grid properties
        _Resolution ("Resolution", Float) = 64
        _MinSize ("Min Size", Float) = 0.01
        _MaxSize ("Max Size", Float) = 0.05
        _SizeSlider ("Size Slider", Range(0,1)) = 0.5
        _Gap ("Gap between quads", Float) = 0.01
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            // Samplers for the two video textures
            sampler2D _VideoTexA;
            sampler2D _VideoTexB;
            float _Blend;

            // Pixel grid parameters
            float _Resolution;
            float _MinSize;
            float _MaxSize;
            float _SizeSlider;
            float _Gap;

            struct appdata
            {
                float4 vertex : POSITION;   // local vertex position (quad corners)
                float2 uv     : TEXCOORD0; // standard UV if needed
                float2 uv2    : TEXCOORD1; // (row, col)
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 vidUV  : TEXCOORD0;  // final UV for sampling the video
            };

            v2f vert (appdata v)
            {
                v2f o;

                // The row/col are stored as floats in v.uv2:
                float row = v.uv2.x;
                float col = v.uv2.y;

                // Interpolate the actual scale based on the slider
                float currentSize = lerp(_MinSize, _MaxSize, _SizeSlider);

                // Offset in x/y to center the entire grid around (0,0).
                float2 offset = float2(
                    col - _Resolution * 0.5, 
                    row - _Resolution * 0.5
                ) * (currentSize + _Gap);

                // Scale the local quad corners
                float2 scaledLocalPos = v.vertex.xy * currentSize;

                // Final position in object space
                float3 finalPos = float3(scaledLocalPos + offset, 0);

                // Transform to clip space
                o.vertex = UnityObjectToClipPos(float4(finalPos, 1.0));

                // For sampling the video:
                // We want normalized UV in [0..1].
                // col in [0..resolution-1], row in [0..resolution-1]
                o.vidUV = float2(col / _Resolution, row / _Resolution);

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Sample from both video textures
                fixed4 colA = tex2D(_VideoTexA, i.vidUV);
                fixed4 colB = tex2D(_VideoTexB, i.vidUV);

                // Blend between them
                fixed4 finalColor = lerp(colA, colB, _Blend);

                return finalColor;
            }
            ENDCG
        }
    }
}
