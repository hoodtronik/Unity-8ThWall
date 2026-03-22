using UnityEngine;
using UnityEngine.Events;
using System.Globalization;
using System.Runtime.InteropServices;

namespace XR8WebAR
{
    /// <summary>
    /// XR8SemanticLayer — Real-time sky & person segmentation for WebAR.
    ///
    /// Uses 8th Wall's LayersController to provide segmentation masks that enable:
    ///   - Sky replacement (swap real sky with custom skybox/gradient/video)
    ///   - Person occlusion (real people walk in front of AR content)
    ///   - Enhanced lighting estimation (directional light + ambient color)
    ///
    /// Inspired by Lightship ARDK's ARSemanticSegmentationManager, adapted for
    /// browser-based AR via 8th Wall's JS engine.
    ///
    /// Setup:
    ///   1. Add XR8SemanticLayer to a root-level GameObject named "XR8SemanticLayer"
    ///   2. Assign the AR Camera
    ///   3. Enable the layers you want (Sky, Person)
    ///   4. Optionally assign a sky replacement material
    ///   5. Build WebGL — segmentation runs in the browser
    ///
    /// Must be on a root-level GameObject to receive SendMessage calls from JS.
    /// </summary>
    public class XR8SemanticLayer : MonoBehaviour
    {
        // ━━━ JS Interop ━━━
        [DllImport("__Internal")] private static extern void WebGLStartSemanticSegmentation(string configJson);
        [DllImport("__Internal")] private static extern void WebGLStopSemanticSegmentation();
        [DllImport("__Internal")] private static extern void WebGLEnableSemanticLayer(string layerName, bool enable);
        [DllImport("__Internal")] private static extern void WebGLSubscribeSemanticTexture(string layerName, int textureId);

        // ━━━ Configuration ━━━
        [Header("Segmentation Layers")]
        [Tooltip("Enable sky segmentation (for sky replacement effects)")]
        [SerializeField] private bool enableSkySegmentation = true;

        [Tooltip("Enable person segmentation (for person occlusion)")]
        [SerializeField] private bool enablePersonSegmentation = false;

        [Header("Camera")]
        [SerializeField] private Camera arCamera;

        [Header("Sky Replacement")]
        [Tooltip("Material used for sky replacement (applied to a fullscreen quad behind AR content)")]
        [SerializeField] private Material skyReplacementMaterial;

        [Tooltip("Custom sky texture (gradient, skybox, etc.) — set on the sky replacement material")]
        [SerializeField] private Texture2D customSkyTexture;

        [Tooltip("Sky replacement color (used if no custom texture is assigned)")]
        [SerializeField] private Color skyColor = new Color(0.2f, 0.5f, 0.9f, 1f);

        [Header("Person Occlusion")]
        [Tooltip("Material used for person occlusion (renders person mask into depth)")]
        [SerializeField] private Material personOcclusionMaterial;

        [Header("Enhanced Lighting")]
        [Tooltip("Auto-adjust scene directional light based on real-world lighting")]
        [SerializeField] private bool enhancedLighting = false;

        [Tooltip("The directional light to adjust")]
        [SerializeField] private Light directionalLight;

        [Tooltip("Smoothing speed for lighting changes")]
        [SerializeField] private float lightingSmoothSpeed = 3f;

        [Header("Events")]
        [SerializeField] public UnityEvent OnSkyMaskAvailable;
        [SerializeField] public UnityEvent OnPersonMaskAvailable;
        [SerializeField] public UnityEvent<float, float> OnLightingUpdated; // intensity, colorTemp

        // ━━━ Internal State ━━━
        private Texture2D skyMaskTexture;
        private Texture2D personMaskTexture;
        private bool skyMaskReady;
        private bool personMaskReady;
        private bool isInitialized;

        // Lighting estimation
        private float targetLightIntensity = 1f;
        private float targetColorTemperature = 6500f;
        private Vector3 targetLightDirection = Vector3.down;

        // Sky replacement quad
        private GameObject skyQuad;
        private MeshRenderer skyQuadRenderer;

        // ━━━ Public API ━━━

        /// <summary>Whether sky segmentation data is available.</summary>
        public bool IsSkySegmentationReady => skyMaskReady;

        /// <summary>Whether person segmentation data is available.</summary>
        public bool IsPersonSegmentationReady => personMaskReady;

        /// <summary>The current sky segmentation mask texture.</summary>
        public Texture2D SkyMaskTexture => skyMaskTexture;

        /// <summary>The current person segmentation mask texture.</summary>
        public Texture2D PersonMaskTexture => personMaskTexture;

        /// <summary>Current estimated ambient light intensity (0-1).</summary>
        public float AmbientIntensity => targetLightIntensity;

        /// <summary>Current estimated color temperature (Kelvin).</summary>
        public float ColorTemperature => targetColorTemperature;

        // ━━━ Lifecycle ━━━

        private void Awake()
        {
            if (arCamera == null)
                arCamera = Camera.main;

            // Create mask textures (will be populated by JS bridge)
            skyMaskTexture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            skyMaskTexture.name = "XR8_SkyMask";
            skyMaskTexture.filterMode = FilterMode.Bilinear;
            skyMaskTexture.wrapMode = TextureWrapMode.Clamp;

            personMaskTexture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            personMaskTexture.name = "XR8_PersonMask";
            personMaskTexture.filterMode = FilterMode.Bilinear;
            personMaskTexture.wrapMode = TextureWrapMode.Clamp;
        }

        private void Start()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            InitializeSegmentation();
#else
            Debug.Log("[XR8SemanticLayer] Segmentation only runs in WebGL builds. " +
                      "In editor, sky replacement uses the assigned custom sky texture.");

            // In editor: set up sky replacement with static texture for preview
            if (enableSkySegmentation && skyReplacementMaterial != null)
            {
                SetupSkyQuad();
            }
#endif
        }

        private void Update()
        {
            // Smooth lighting transitions
            if (enhancedLighting && directionalLight != null)
            {
                directionalLight.intensity = Mathf.Lerp(
                    directionalLight.intensity, targetLightIntensity,
                    Time.deltaTime * lightingSmoothSpeed);

                directionalLight.colorTemperature = Mathf.Lerp(
                    directionalLight.colorTemperature, targetColorTemperature,
                    Time.deltaTime * lightingSmoothSpeed);

                var currentDir = directionalLight.transform.forward;
                var newDir = Vector3.Lerp(currentDir, targetLightDirection,
                    Time.deltaTime * lightingSmoothSpeed);
                if (newDir.sqrMagnitude > 0.001f)
                    directionalLight.transform.forward = newDir;
            }
        }

        private void OnDestroy()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLStopSemanticSegmentation();
#endif
            if (skyMaskTexture != null) Destroy(skyMaskTexture);
            if (personMaskTexture != null) Destroy(personMaskTexture);
            if (skyQuad != null) Destroy(skyQuad);
        }

        // ━━━ Initialization ━━━

        private void InitializeSegmentation()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string config = "{" +
                "\"unityObjectName\":\"" + gameObject.name + "\"," +
                "\"enableSky\":" + (enableSkySegmentation ? "true" : "false") + "," +
                "\"enablePerson\":" + (enablePersonSegmentation ? "true" : "false") +
            "}";

            WebGLStartSemanticSegmentation(config);

            // Enable requested layers
            if (enableSkySegmentation)
            {
                WebGLEnableSemanticLayer("sky", true);
                WebGLSubscribeSemanticTexture("sky", (int)skyMaskTexture.GetNativeTexturePtr());
            }

            if (enablePersonSegmentation)
            {
                WebGLEnableSemanticLayer("person", true);
                WebGLSubscribeSemanticTexture("person", (int)personMaskTexture.GetNativeTexturePtr());
            }

            if (enableSkySegmentation && skyReplacementMaterial != null)
            {
                SetupSkyQuad();
            }

            isInitialized = true;
            Debug.Log("[XR8SemanticLayer] Initialized — sky=" + enableSkySegmentation +
                      " person=" + enablePersonSegmentation);
#endif
        }

        // ━━━ Sky Replacement Setup ━━━

        private void SetupSkyQuad()
        {
            if (skyQuad != null) return;

            // Create a fullscreen quad behind all AR content
            skyQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            skyQuad.name = "XR8_SkyReplacementQuad";
            skyQuad.layer = gameObject.layer;

            // Remove collider
            var col = skyQuad.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Position far behind camera
            skyQuad.transform.SetParent(arCamera.transform, false);
            skyQuad.transform.localPosition = new Vector3(0, 0, arCamera.farClipPlane * 0.9f);
            skyQuad.transform.localRotation = Quaternion.identity;

            // Scale to fill the view
            float h = 2f * arCamera.farClipPlane * 0.9f * Mathf.Tan(arCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float w = h * arCamera.aspect;
            skyQuad.transform.localScale = new Vector3(w, h, 1f);

            // Apply material
            skyQuadRenderer = skyQuad.GetComponent<MeshRenderer>();
            if (skyReplacementMaterial != null)
            {
                skyQuadRenderer.material = new Material(skyReplacementMaterial);
                skyQuadRenderer.material.SetTexture("_SkyMask", skyMaskTexture);

                if (customSkyTexture != null)
                    skyQuadRenderer.material.SetTexture("_CustomSky", customSkyTexture);
                else
                    skyQuadRenderer.material.SetColor("_SkyColor", skyColor);
            }

            Debug.Log("[XR8SemanticLayer] Sky replacement quad created");
        }

        // ━━━ Runtime API ━━━

        /// <summary>Enable or disable sky segmentation at runtime.</summary>
        public void SetSkySegmentation(bool enabled)
        {
            enableSkySegmentation = enabled;
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLEnableSemanticLayer("sky", enabled);
#endif
            if (skyQuad != null)
                skyQuad.SetActive(enabled);
        }

        /// <summary>Enable or disable person segmentation at runtime.</summary>
        public void SetPersonSegmentation(bool enabled)
        {
            enablePersonSegmentation = enabled;
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLEnableSemanticLayer("person", enabled);
#endif
        }

        /// <summary>Set a custom sky replacement color at runtime.</summary>
        public void SetSkyColor(Color color)
        {
            skyColor = color;
            if (skyQuadRenderer != null && skyQuadRenderer.material != null)
                skyQuadRenderer.material.SetColor("_SkyColor", color);
        }

        /// <summary>Set a custom sky replacement texture at runtime.</summary>
        public void SetSkyTexture(Texture2D texture)
        {
            customSkyTexture = texture;
            if (skyQuadRenderer != null && skyQuadRenderer.material != null)
                skyQuadRenderer.material.SetTexture("_CustomSky", texture);
        }

        // ━━━ SendMessage callbacks from JS ━━━

        /// <summary>Called when a new sky segmentation mask is ready.</summary>
        void OnSkyMaskReady(string _)
        {
            skyMaskReady = true;
            OnSkyMaskAvailable?.Invoke();
        }

        /// <summary>Called when a new person segmentation mask is ready.</summary>
        void OnPersonMaskReady(string _)
        {
            personMaskReady = true;
            OnPersonMaskAvailable?.Invoke();
        }

        /// <summary>Called with enhanced lighting data: intensity,colorTemp,dirX,dirY,dirZ</summary>
        void OnLightingEstimate(string csv)
        {
            var parts = csv.Split(',');
            if (parts.Length < 5) return;

            targetLightIntensity = float.Parse(parts[0], CultureInfo.InvariantCulture);
            targetColorTemperature = float.Parse(parts[1], CultureInfo.InvariantCulture);
            targetLightDirection = new Vector3(
                float.Parse(parts[2], CultureInfo.InvariantCulture),
                float.Parse(parts[3], CultureInfo.InvariantCulture),
                float.Parse(parts[4], CultureInfo.InvariantCulture)
            );

            OnLightingUpdated?.Invoke(targetLightIntensity, targetColorTemperature);
        }
    }
}
