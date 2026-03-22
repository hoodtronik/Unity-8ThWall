using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using System.Runtime.InteropServices;

namespace XR8WebAR
{
    /// <summary>
    /// XR8TextureExtractor — Extracts the de-warped (perspective-corrected) texture
    /// from a tracked image target into a RenderTexture.
    ///
    /// Ported from Imaginary Labs' TextureExtractor_WarpedImage for XR8 engine.
    ///
    /// Use Cases:
    ///   - AR business card scanning: extract the card image for OCR/display
    ///   - AR product label reading: capture the label for AI processing
    ///   - Live poster tracking: show the tracked poster content in a UI panel
    ///
    /// Setup:
    ///   1. Add this component to any GameObject
    ///   2. Set the targetId to match an XR8ImageTracker target
    ///   3. Assign an output RenderTexture (create via Assets > Create > Render Texture)
    ///   4. Choose mode: EVERY_FRAME (continuous) or MANUAL (call ExtractTexture() yourself)
    /// </summary>
    public class XR8TextureExtractor : MonoBehaviour
    {
        [DllImport("__Internal")] private static extern bool IsXR8TrackerReady();
        [DllImport("__Internal")] private static extern void GetXR8WarpedTexture(string targetId, int textureId, int resolution);

        // === Configuration ===
        [Header("Target")]
        [Tooltip("The image target ID to extract texture from (must match XR8ImageTracker)")]
        [SerializeField] private string targetId;

        [Header("Output")]
        [Tooltip("RenderTexture to write the extracted image into")]
        [SerializeField] private RenderTexture outputTexture;

        [Header("Mode")]
        [SerializeField] private ExtractionMode mode = ExtractionMode.EveryFrame;

        [Header("Visibility Check (Optional)")]
        [Tooltip("Only extract when the target is fully visible in camera")]
        [SerializeField] private bool checkVisibility = false;
        [SerializeField] private Camera visibilityCamera;
        [SerializeField] private MeshRenderer visibilityRenderer;

        [Header("Events")]
        [SerializeField] private UnityEvent OnBecameFullyVisible;
        [SerializeField] private UnityEvent OnBecameObscured;

        // === Enums ===
        public enum ExtractionMode
        {
            [Tooltip("Extract texture every frame (continuous)")]
            EveryFrame,
            [Tooltip("Only extract when ExtractTexture() is called manually")]
            Manual
        }

        // === Private State ===
        private Texture2D warpedTexture;
        private int warpedTextureId;
        private bool isFullyVisibleLastFrame = false;
        private bool isInitializing = false;
        private bool isInitialized = false;

        /// <summary>True when the extractor has been initialized and is ready to extract.</summary>
        public bool IsReady => isInitialized;

        /// <summary>True when the target is fully visible in the camera (only valid if checkVisibility is enabled).</summary>
        public bool IsFullyVisible => isFullyVisibleLastFrame;

        private void Start()
        {
#if !UNITY_EDITOR && UNITY_WEBGL
            StartCoroutine(Initialize());
#else
            Debug.Log("[XR8TextureExtractor] Texture extraction only works in WebGL builds.");
#endif
        }

        private IEnumerator Initialize()
        {
            isInitializing = true;

            // Wait for the XR8 tracker to be ready
            while (!IsXR8TrackerReady())
            {
                yield return null;
            }

            if (outputTexture == null)
            {
                Debug.LogError("[XR8TextureExtractor] outputTexture is not assigned!");
                isInitializing = false;
                yield break;
            }

            warpedTexture = new Texture2D(outputTexture.width, outputTexture.height);
            warpedTextureId = (int)warpedTexture.GetNativeTexturePtr();

            isInitialized = true;
            isInitializing = false;

            Debug.Log("[XR8TextureExtractor] Initialized for target '" + targetId +
                      "' (resolution: " + outputTexture.width + "x" + outputTexture.height + ")");
        }

        private void OnDisable()
        {
            isFullyVisibleLastFrame = false;
            isInitializing = false;
            OnBecameObscured?.Invoke();
        }

        private void Update()
        {
            if (mode == ExtractionMode.EveryFrame)
            {
                ExtractTexture();
            }

            if (checkVisibility && visibilityCamera != null && visibilityRenderer != null)
            {
                if (!visibilityRenderer.gameObject.activeInHierarchy)
                {
                    if (isFullyVisibleLastFrame)
                    {
                        isFullyVisibleLastFrame = false;
                        OnBecameObscured?.Invoke();
                    }
                    return;
                }

                bool isFullyVisible = IsFullyVisibleInCamera();

                if (isFullyVisible && !isFullyVisibleLastFrame)
                {
                    OnBecameFullyVisible?.Invoke();
                }
                else if (!isFullyVisible && isFullyVisibleLastFrame)
                {
                    OnBecameObscured?.Invoke();
                }

                isFullyVisibleLastFrame = isFullyVisible;
            }
        }

        /// <summary>
        /// Manually trigger a texture extraction. Call this when mode is Manual.
        /// Can also be called at any time regardless of mode.
        /// </summary>
        public void ExtractTexture()
        {
#if !UNITY_EDITOR && UNITY_WEBGL
            if (warpedTexture == null)
            {
                if (!isInitializing)
                {
                    StartCoroutine(Initialize());
                }
                return;
            }

            GetXR8WarpedTexture(targetId, warpedTextureId, outputTexture.width);
            Graphics.Blit(warpedTexture, outputTexture);
#endif
        }

        private bool IsFullyVisibleInCamera()
        {
            if (visibilityRenderer == null || visibilityCamera == null)
                return false;

            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(visibilityCamera);
            Bounds bounds = visibilityRenderer.bounds;

            // Check all 8 corners of the bounding box
            Vector3[] corners = new Vector3[8];
            var ext = bounds.extents;
            var cen = bounds.center;
            corners[0] = cen + new Vector3(ext.x, ext.y, ext.z);
            corners[1] = cen + new Vector3(-ext.x, ext.y, ext.z);
            corners[2] = cen + new Vector3(ext.x, -ext.y, ext.z);
            corners[3] = cen + new Vector3(-ext.x, -ext.y, ext.z);
            corners[4] = cen + new Vector3(ext.x, ext.y, -ext.z);
            corners[5] = cen + new Vector3(-ext.x, ext.y, -ext.z);
            corners[6] = cen + new Vector3(ext.x, -ext.y, -ext.z);
            corners[7] = cen + new Vector3(-ext.x, -ext.y, -ext.z);

            for (int i = 0; i < 8; i++)
            {
                if (!GeometryUtility.TestPlanesAABB(frustumPlanes, new Bounds(corners[i], Vector3.zero)))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
