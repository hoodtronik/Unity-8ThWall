// Sky Replacement Shader — Replaces real-world sky with custom content.
//
// Uses the sky segmentation mask from XR8SemanticLayer to selectively
// replace sky pixels with a custom texture or solid color.
//
// Works with: XR8SemanticLayer.cs + XR8SemanticLib.jslib
// WebGL 2.0 / OpenGL ES 3.0 compatible.
//
// Inspired by Lightship ARDK's sky segmentation rendering approach,
// adapted for 8th Wall WebAR.

Shader "XR8WebAR/SkyReplacement"
{
    Properties
    {
        _SkyMask ("Sky Segmentation Mask", 2D) = "black" {}
        _CustomSky ("Custom Sky Texture", 2D) = "white" {}
        _SkyColor ("Sky Color (fallback)", Color) = (0.2, 0.5, 0.9, 1.0)
        _EdgeSoftness ("Edge Softness", Range(0, 0.3)) = 0.05
        _MaskThreshold ("Mask Threshold", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Background+1"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _SkyMask;
            sampler2D _CustomSky;
            fixed4 _SkyColor;
            float _EdgeSoftness;
            float _MaskThreshold;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Sample sky mask (white = sky, black = not sky)
                float mask = tex2D(_SkyMask, i.uv).r;

                // Smooth threshold with edge softness
                float skyAlpha = smoothstep(_MaskThreshold - _EdgeSoftness,
                                            _MaskThreshold + _EdgeSoftness, mask);

                // Sample custom sky or use solid color
                fixed4 skyTex = tex2D(_CustomSky, i.uv);
                // If custom sky texture is default white, use the color instead
                fixed4 skyOutput = lerp(_SkyColor, skyTex,
                    step(0.01, abs(skyTex.r - 1.0) + abs(skyTex.g - 1.0) + abs(skyTex.b - 1.0)));

                skyOutput.a = skyAlpha;
                return skyOutput;
            }
            ENDCG
        }
    }
}
