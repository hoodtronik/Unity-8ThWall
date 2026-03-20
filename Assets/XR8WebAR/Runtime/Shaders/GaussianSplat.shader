Shader "XR8WebAR/GaussianSplat"
{
    Properties
    {
        _SplatScale ("Splat Scale", Range(0.1, 5.0)) = 1.0
        _Opacity ("Global Opacity", Range(0, 1)) = 1.0
        _CutoffAlpha ("Alpha Cutoff", Range(0, 0.5)) = 0.02
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            Name "GaussianSplatPass"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.0

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 splatColor : TEXCOORD1;
                float2 splatAxes : TEXCOORD2; // ellipse semi-axes in screen px
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // Per-instance data via MaterialPropertyBlock
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _SplatPosition)  // xyz = world pos, w = scale
                UNITY_DEFINE_INSTANCED_PROP(float4, _SplatColor)     // rgba
                UNITY_DEFINE_INSTANCED_PROP(float4, _SplatCov2D)     // xx, xy, yy, unused (2D covariance)
            UNITY_INSTANCING_BUFFER_END(Props)

            float _SplatScale;
            float _Opacity;
            float _CutoffAlpha;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                // Get per-instance data
                float4 splatPos = UNITY_ACCESS_INSTANCED_PROP(Props, _SplatPosition);
                float4 splatCol = UNITY_ACCESS_INSTANCED_PROP(Props, _SplatColor);
                float4 cov2d = UNITY_ACCESS_INSTANCED_PROP(Props, _SplatCov2D);

                // World position of this splat
                float3 worldPos = splatPos.xyz;
                float splatBaseScale = splatPos.w * _SplatScale;

                // Project center to clip space
                float4 clipPos = UnityWorldToClipPos(worldPos);

                // Compute 2D covariance eigenvalues for ellipse size
                // cov2d = (a, b, c, _) where covariance matrix is [[a,b],[b,c]]
                float a = cov2d.x;
                float b = cov2d.y;
                float c = cov2d.z;

                // Eigenvalue decomposition for 2x2 symmetric matrix
                float trace = a + c;
                float det = a * c - b * b;
                float disc = sqrt(max(trace * trace * 0.25 - det, 0.0001));
                float lambda1 = trace * 0.5 + disc;
                float lambda2 = max(trace * 0.5 - disc, 0.0001);

                // Semi-axes (sqrt of eigenvalues) * scale * 3 sigma
                float r1 = sqrt(lambda1) * splatBaseScale * 3.0;
                float r2 = sqrt(lambda2) * splatBaseScale * 3.0;
                
                o.splatAxes = float2(r1, r2);

                // Eigenvector for orientation
                float2 eigVec = normalize(float2(b, lambda1 - a) + 0.0001);

                // Build 2D rotation from eigenvector
                float2x2 rot = float2x2(eigVec.x, -eigVec.y, eigVec.y, eigVec.x);

                // Offset this vertex (v.uv is -1..1 quad corners)
                float2 localOffset = v.uv * float2(r1, r2);
                float2 rotatedOffset = mul(rot, localOffset);

                // Convert from pixel-ish units to NDC
                float2 screenSize = _ScreenParams.xy;
                float2 ndcOffset = rotatedOffset / screenSize * 2.0;

                // Apply offset in clip space
                o.pos = clipPos;
                o.pos.xy += ndcOffset * clipPos.w;

                o.uv = v.uv;
                o.splatColor = splatCol * float4(1, 1, 1, _Opacity);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                // Gaussian falloff: e^(-0.5 * (x^2 + y^2))
                // uv is in [-1, 1], so distance squared from center
                float distSq = dot(i.uv, i.uv);
                
                // Gaussian with sigma = 1/3 (so 3-sigma = edge of quad)
                float gaussian = exp(-4.5 * distSq);

                // Discard nearly-invisible pixels
                float alpha = i.splatColor.a * gaussian;
                if (alpha < _CutoffAlpha) discard;

                return float4(i.splatColor.rgb, alpha);
            }
            ENDCG
        }
    }

    FallBack Off
}
