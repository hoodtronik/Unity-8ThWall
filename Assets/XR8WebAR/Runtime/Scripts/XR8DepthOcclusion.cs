using UnityEngine;
using UnityEngine.Events;
using System.Globalization;
using System.Runtime.InteropServices;

namespace XR8WebAR
{
    /// <summary>
    /// XR8DepthOcclusion — Real-world depth-based occlusion for WebAR.
    ///
    /// Uses 8th Wall's depth module (available on LiDAR iOS devices and ARCore
    /// depth-capable Android devices) to hide AR content behind real objects.
    ///
    /// Inspired by Lightship ARDK's LightshipOcclusionExtension, adapted for
    /// browser-based AR. Falls back to a simple ground-plane hider on devices
    /// without depth support.
    ///
    /// How it works:
    ///   1. Requests depth frames from 8th Wall's depth pipeline via JS bridge
    ///   2. Uploads depth texture data to Unity via GL texture
    ///   3. A fullscreen depth comparison shader hides AR pixels behind real geometry
    ///   4. On non-depth devices, uses the ground plane as a simple occluder
    ///
    /// Setup:
    ///   1. Add XR8DepthOcclusion to a root-level GameObject named "XR8DepthOcclusion"
    ///   2. Assign the AR Camera
    ///   3. Optionally configure the ground-plane fallback
    ///   4. Build WebGL — occlusion runs automatically on supported devices
    /// </summary>
    public class XR8DepthOcclusion : MonoBehaviour
    {
        // ━━━ JS Interop ━━━
        [DllImport("__Internal")] private static extern void WebGLStartDepthOcclusion(string configJson);
        [DllImport("__Internal")] private static extern void WebGLStopDepthOcclusion();
        [DllImport("__Internal")] private static extern void WebGLSubscribeDepthTexture(int textureId);
        [DllImport("__Internal")] private static extern bool WebGLIsDepthSupported();

        // ━━━ Configuration ━━━
        [Header("Camera")]
        [SerializeField] private Camera arCamera;

        [Header("Occlusion Settings")]
        [Tooltip("Enable depth-based occlusion (requires device with depth sensor)")]
        [SerializeField] private bool enableDepthOcclusion = true;

        [Tooltip("Occlusion quality — lower resolution = better performance")]
        [SerializeField] private OcclusionQuality quality = OcclusionQuality.Medium;

        [Tooltip("Depth edge softness (smooths occlusion boundaries)")]
        [SerializeField][Range(0f, 0.1f)] private float edgeSoftness = 0.02f;

        [Header("Ground Plane Fallback")]
        [Tooltip("Use a simple ground-plane occluder when depth is not available")]
        [SerializeField] private bool groundPlaneFallback = true;

        [Tooltip("Height of the ground plane (usually 0)")]
        [SerializeField] private float groundPlaneHeight = 0f;

        [Tooltip("Material for the ground plane occluder (should write to depth only)")]
        [SerializeField] private Material groundPlaneMaterial;

        [Header("Events")]
        [SerializeField] public UnityEvent OnDepthAvailable;
        [SerializeField] public UnityEvent OnDepthUnavailable;
        [SerializeField] public UnityEvent<float> OnDepthConfidence; // 0-1

        // ━━━ Enums ━━━

        public enum OcclusionQuality
        {
            [Tooltip("64x48 — fastest, coarse occlusion")]
            Low,
            [Tooltip("128x96 — good balance")]
            Medium,
            [Tooltip("256x192 — highest quality, most GPU cost")]
            High
        }

        // ━━━ Internal State ━━━
        private Texture2D depthTexture;
        private bool isDepthAvailable;
        private bool isInitialized;
        private GameObject groundPlane;
        private bool usingFallback;

        // ━━━ Public API ━━━

        /// <summary>Whether real depth data is available from the device.</summary>
        public bool IsDepthAvailable => isDepthAvailable;

        /// <summary>Whether the system is using ground-plane fallback.</summary>
        public bool IsUsingFallback => usingFallback;

        /// <summary>The current depth texture (null if depth unavailable).</summary>
        public Texture2D DepthTexture => depthTexture;

        // ━━━ Lifecycle ━━━

        private void Awake()
        {
            if (arCamera == null)
                arCamera = Camera.main;

            // Create depth texture based on quality
            var (w, h) = GetDepthResolution();
            depthTexture = new Texture2D(w, h, TextureFormat.RFloat, false);
            depthTexture.name = "XR8_DepthMap";
            depthTexture.filterMode = FilterMode.Bilinear;
            depthTexture.wrapMode = TextureWrapMode.Clamp;
        }

        private void Start()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            InitializeDepthOcclusion();
#else
            Debug.Log("[XR8DepthOcclusion] Depth occlusion requires WebGL build with 8th Wall. " +
                      "Using ground-plane fallback in editor.");
            if (groundPlaneFallback)
                SetupGroundPlaneFallback();
#endif
        }

        private void OnDestroy()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLStopDepthOcclusion();
#endif
            if (depthTexture != null) Destroy(depthTexture);
            if (groundPlane != null) Destroy(groundPlane);
        }

        // ━━━ Initialization ━━━

        private void InitializeDepthOcclusion()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var (w, h) = GetDepthResolution();
            string config = "{" +
                "\"unityObjectName\":\"" + gameObject.name + "\"," +
                "\"width\":" + w + "," +
                "\"height\":" + h + "," +
                "\"edgeSoftness\":" + edgeSoftness.ToString(CultureInfo.InvariantCulture) +
            "}";

            WebGLStartDepthOcclusion(config);
            WebGLSubscribeDepthTexture((int)depthTexture.GetNativeTexturePtr());
            isInitialized = true;

            Debug.Log("[XR8DepthOcclusion] Initialized at " + w + "x" + h);
#endif
        }

        // ━━━ Ground Plane Fallback ━━━

        private void SetupGroundPlaneFallback()
        {
            if (groundPlane != null) return;

            usingFallback = true;

            // Create a large invisible ground plane that writes to depth buffer
            groundPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            groundPlane.name = "XR8_GroundPlaneOccluder";
            groundPlane.transform.position = new Vector3(0, groundPlaneHeight, 0);
            groundPlane.transform.localScale = Vector3.one * 10f; // 100m x 100m

            // Remove collider (we don't need physics on this)
            var col = groundPlane.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Apply the occlusion material (depth-only rendering)
            var renderer = groundPlane.GetComponent<MeshRenderer>();
            if (groundPlaneMaterial != null)
            {
                renderer.material = groundPlaneMaterial;
            }
            else
            {
                // Use PortalMask-style depth-only shader if available
                var maskShader = Shader.Find("XR8WebAR/PortalMask");
                if (maskShader != null)
                {
                    renderer.material = new Material(maskShader);
                }
                else
                {
                    // Last resort: invisible material that writes depth
                    renderer.material = new Material(Shader.Find("Unlit/Color"));
                    renderer.material.color = new Color(0, 0, 0, 0);
                }
            }

            Debug.Log("[XR8DepthOcclusion] Ground plane fallback created at y=" + groundPlaneHeight);
        }

        // ━━━ Helpers ━━━

        private (int, int) GetDepthResolution()
        {
            return quality switch
            {
                OcclusionQuality.Low => (64, 48),
                OcclusionQuality.High => (256, 192),
                _ => (128, 96), // Medium
            };
        }

        // ━━━ Runtime API ━━━

        /// <summary>Enable or disable depth occlusion at runtime.</summary>
        public void SetEnabled(bool enabled)
        {
            enableDepthOcclusion = enabled;
            if (!enabled && groundPlane != null)
                groundPlane.SetActive(false);
            else if (enabled && usingFallback && groundPlane != null)
                groundPlane.SetActive(true);
        }

        /// <summary>Set the ground plane height for fallback occlusion.</summary>
        public void SetGroundPlaneHeight(float height)
        {
            groundPlaneHeight = height;
            if (groundPlane != null)
            {
                var pos = groundPlane.transform.position;
                pos.y = height;
                groundPlane.transform.position = pos;
            }
        }

        // ━━━ SendMessage callbacks from JS ━━━

        /// <summary>Called when depth data becomes available.</summary>
        void OnDepthStart(string _)
        {
            isDepthAvailable = true;
            usingFallback = false;
            Debug.Log("[XR8DepthOcclusion] Depth data available — using real depth occlusion");
            OnDepthAvailable?.Invoke();

            // Remove ground plane fallback since we have real depth
            if (groundPlane != null)
            {
                Destroy(groundPlane);
                groundPlane = null;
            }
        }

        /// <summary>Called when depth is not supported on this device.</summary>
        void OnDepthNotSupported(string _)
        {
            isDepthAvailable = false;
            Debug.Log("[XR8DepthOcclusion] Depth not supported on this device");

            if (groundPlaneFallback && !usingFallback)
            {
                SetupGroundPlaneFallback();
            }

            OnDepthUnavailable?.Invoke();
        }

        /// <summary>Called with depth confidence value (0-1).</summary>
        void OnDepthConfidenceReceived(string val)
        {
            if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float confidence))
            {
                OnDepthConfidence?.Invoke(confidence);
            }
        }
    }
}
