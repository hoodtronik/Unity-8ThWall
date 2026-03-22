// Depth Occlusion Shader — Hides AR content behind real-world geometry.
//
// Uses the depth texture from XR8DepthOcclusion to compare real-world
// depth against virtual object depth. Where real objects are closer,
// the AR content is hidden (occluded).
//
// This shader is applied as a post-processing image effect OR used
// on a fullscreen quad in front of the AR camera.
//
// Works with: XR8DepthOcclusion.cs + XR8DepthLib.jslib
// WebGL 2.0 / OpenGL ES 3.0 compatible.

Shader "XR8WebAR/DepthOcclusion"
{
    Properties
    {
        _DepthTex ("Depth Texture (from 8th Wall)", 2D) = "white" {}
        _NearClip ("Near Clip", Float) = 0.1
        _FarClip ("Far Clip", Float) = 30.0
        _EdgeSoftness ("Edge Softness", Range(0, 0.1)) = 0.02
        _DepthBias ("Depth Bias", Range(-0.5, 0.5)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Overlay-1"
        }

        // This shader writes to depth buffer to occlude AR objects
        ZWrite On
        ZTest Always
        ColorMask 0  // Don't write color — depth only
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _DepthTex;
            float _NearClip;
            float _FarClip;
            float _EdgeSoftness;
            float _DepthBias;

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

            float frag(v2f i) : SV_Depth
            {
                // Sample depth from 8th Wall (0 = near, 1 = far, encoded in R channel)
                float rawDepth = tex2D(_DepthTex, i.uv).r;

                // Convert to linear depth
                float linearDepth = lerp(_NearClip, _FarClip, rawDepth) + _DepthBias;

                // Convert linear depth to Unity clip-space depth
                // This is the inverse of Unity's depth linearization
                float ndcDepth = (1.0 / linearDepth - 1.0 / _FarClip) /
                                 (1.0 / _NearClip - 1.0 / _FarClip);

                return ndcDepth;
            }
            ENDCG
        }
    }
}
