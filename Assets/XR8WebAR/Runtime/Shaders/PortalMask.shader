// Portal Mask Shader — The "invisible wall" that creates the portal effect.
//
// This shader writes to the STENCIL BUFFER only — it renders NO color.
// Place this on the portal doorframe/window geometry. Objects behind it
// disappear because the stencil test blocks the default rendering.
//
// Works with: PortalInterior.shader (which only renders WHERE this mask wrote)
// WebGL 2.0 / OpenGL ES 3.0 compatible.

Shader "XR8WebAR/PortalMask"
{
    Properties
    {
        [IntRange] _StencilRef ("Stencil Reference", Range(0, 255)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry-1"  // Render BEFORE normal geometry
        }

        // === STENCIL PASS ===
        // Write to stencil buffer but render nothing visible
        Pass
        {
            // Don't write any color
            ColorMask 0
            // Don't write to depth buffer (portal is invisible)
            ZWrite Off

            Stencil
            {
                Ref [_StencilRef]
                Comp Always       // Always write
                Pass Replace      // Replace stencil value with Ref
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return fixed4(0, 0, 0, 0); // Nothing rendered
            }
            ENDCG
        }
    }
}
