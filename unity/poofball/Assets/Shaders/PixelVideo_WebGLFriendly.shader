Shader "Custom/PixelVideo_WebGLFriendly"
{
    Properties
    {
        // The two video textures
        _VideoTexA ("Video Texture A", 2D) = "white" {}
        _VideoTexB ("Video Texture B", 2D) = "white" {}
        // Blend: 0 = A, 1 = B
        _Blend ("Blend", Range(0,1)) = 0.0

        // Number of "pixels" along each axis
        _Resolution ("Resolution", Float) = 64

        // Fraction of the pixel to discard along each edge
        // e.g. 0.05 = 5% of each edge is cut out.
        _Gap ("Gap between pixel blocks", Range(0,0.5)) = 0.05
    }

    SubShader
    {
        // Use cutout tags so that discard actually creates holes
        // (rather than trying to blend them).
        Tags
        {
            "Queue"="AlphaTest"
            "RenderType"="TransparentCutout"
        }
        LOD 100

        Pass
        {
            // We can optionally disable shadow casting if you like, e.g. 
            // "LightMode"="ShadowCaster" pass.  
            // But for simplicity, let's do just a basic pass.

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _VideoTexA;
            sampler2D _VideoTexB;
            float     _Blend;

            float     _Resolution;
            float     _Gap;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Determine which pixel block (col, row) we belong to
                float2 uvScaled   = i.uv * _Resolution;
                float2 pixelIndex = floor(uvScaled); // e.g. (col, row)

                // Center of that pixel in UV
                float2 discreteUV = (pixelIndex + 0.5) / _Resolution;

                // Sample both textures
                fixed4 colA = tex2D(_VideoTexA, discreteUV);
                fixed4 colB = tex2D(_VideoTexB, discreteUV);

                // Blend
                fixed4 finalColor = lerp(colA, colB, _Blend);

                // Gaps: measure how far within the pixel we are
                float2 fracInPixel = frac(uvScaled); 
                // fracInPixel in [0..1] within that pixel.

                // If near the edge < _Gap or > (1 - _Gap), discard
                if (fracInPixel.x < _Gap || fracInPixel.x > 1 - _Gap ||
                    fracInPixel.y < _Gap || fracInPixel.y > 1 - _Gap)
                {
                    // "clip" is basically the same as "discard" in Unity. 
                    // If the argument is < 0, the pixel is rejected.
                    clip(-1);
                }

                return finalColor;
            }
            ENDCG
        }
    }
}
