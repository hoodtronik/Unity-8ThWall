// Portal Interior Shader — Renders ONLY where the PortalMask wrote to stencil.
//
// Place this on all objects INSIDE the portal. They will only be visible
// when viewed through the portal mask, creating the "window to another world" effect.
//
// Supports: color tint, texture, basic lighting.
// WebGL 2.0 / OpenGL ES 3.0 compatible.

Shader "XR8WebAR/PortalInterior"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Texture", 2D) = "white" {}
        [IntRange] _StencilRef ("Stencil Reference", Range(0, 255)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"  // Normal render order
        }

        Pass
        {
            Stencil
            {
                Ref [_StencilRef]
                Comp Equal        // Only render where stencil == Ref
                Pass Keep         // Don't modify stencil
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 texCol = tex2D(_MainTex, i.uv) * _Color;

                // Simple directional lighting
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float ndl = saturate(dot(normalize(i.worldNormal), lightDir));
                float3 lighting = lerp(float3(0.3, 0.3, 0.35), float3(1, 1, 1), ndl);

                texCol.rgb *= lighting;
                UNITY_APPLY_FOG(i.fogCoord, texCol);
                return texCol;
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}
