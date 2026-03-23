// Person Occlusion Shader — Makes real people appear IN FRONT of AR content.
//
// Uses the person segmentation mask from XR8SemanticLayer to write
// depth values for person-occupied pixels, causing AR objects to
// render behind real people.
//
// Works with: XR8SemanticLayer.cs + XR8SemanticLib.jslib
// WebGL 2.0 / OpenGL ES 3.0 compatible.

Shader "XR8WebAR/PersonOcclusion"
{
    Properties
    {
        _PersonMask ("Person Mask (from SemanticLayer)", 2D) = "black" {}
        _OcclusionDepth ("Occlusion Depth", Range(0.1, 10.0)) = 0.5
        _EdgeSoftness ("Edge Feather", Range(0, 0.15)) = 0.03
        _MaskThreshold ("Mask Threshold", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Overlay-2"
        }

        // Depth-only pass: write person depth to occlude AR objects behind people
        ZWrite On
        ZTest Always
        ColorMask 0
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _PersonMask;
            float _OcclusionDepth;
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

            float frag(v2f i) : SV_Depth
            {
                // Sample person mask (1 = person, 0 = background)
                float mask = tex2D(_PersonMask, i.uv).r;

                // Soft edge: smoothstep around threshold
                float personAlpha = smoothstep(
                    _MaskThreshold - _EdgeSoftness,
                    _MaskThreshold + _EdgeSoftness,
                    mask);

                // Discard non-person pixels (no depth write)
                clip(personAlpha - 0.01);

                // Convert occlusion depth to clip-space depth
                // Person is assumed to be at _OcclusionDepth meters from camera
                float near = _ProjectionParams.y; // near clip
                float far = _ProjectionParams.z;  // far clip
                float ndcDepth = (1.0 / _OcclusionDepth - 1.0 / far) /
                                 (1.0 / near - 1.0 / far);

                return ndcDepth;
            }
            ENDCG
        }
    }
}
