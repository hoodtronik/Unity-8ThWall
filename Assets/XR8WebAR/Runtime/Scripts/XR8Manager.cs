using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using System.Runtime.InteropServices;

namespace XR8WebAR
{
    /// <summary>
    /// XR8Manager — Unified command module for 8th Wall AR.
    /// 
    /// Drop this single GameObject into your scene and check the boxes
    /// for which tracking modes you want enabled. XR8Manager handles
    /// engine initialization, camera setup, and coordinates all tracking.
    ///
    /// Usage:
    ///   1. Add XR8Manager to any root-level GameObject
    ///   2. Check the tracking modes you want (Image, World, Face)
    ///   3. Assign the AR Camera
    ///   4. Configure targets/options in the sub-settings
    ///   5. Build WebGL — it just works
    /// </summary>
    public class XR8Manager : MonoBehaviour
    {
        // --- JS Interop ---
        [DllImport("__Internal")] private static extern void WebGLStartXR8WithConfig(string configJson);
        [DllImport("__Internal")] private static extern void WebGLStopXR8();
        [DllImport("__Internal")] private static extern bool WebGLIsXR8Started();

        // === TRACKING MODE CHECKBOXES ===
        [Header("Tracking Modes")]
        [Tooltip("Track images/markers in the camera feed")]
        [SerializeField] private bool enableImageTracking = true;

        [Tooltip("Track world surfaces for placing 3D objects (SLAM)")]
        [SerializeField] private bool enableWorldTracking = false;

        [Tooltip("Track faces for face filters and effects")]
        [SerializeField] private bool enableFaceTracking = false;

        // === CAMERA ===
        [Header("Camera")]
        [SerializeField] private Camera arCamera;
        [SerializeField] private XR8Camera xr8CameraComponent;

        // === IMAGE TRACKING CONFIG ===
        [Header("Image Tracking (if enabled)")]
        [SerializeField] private XR8ImageTracker imageTracker;

        // === WORLD TRACKING CONFIG ===
        [Header("World Tracking (if enabled)")]
        [Tooltip("Enable surface detection and hit testing")]
        [SerializeField] private bool enableSurfaceDetection = true;
        [Tooltip("Show detected surface meshes (debug)")]
        [SerializeField] private bool showSurfaceMeshes = false;

        // === FACE TRACKING CONFIG ===
        [Header("Face Tracking (if enabled)")]
        [Tooltip("Maximum number of faces to track")]
        [SerializeField][Range(1, 3)] private int maxFaces = 1;

        // === ENGINE SETTINGS ===
        [Header("Engine Settings")]
        [Tooltip("Target frame rate (-1 for device max)")]
        [SerializeField] private int targetFrameRate = 30;

        [Tooltip("Enable lighting estimation")]
        [SerializeField] private bool enableLighting = true;

        // === DESKTOP PREVIEW ===
        [Header("Desktop Preview (Editor Only)")]
        [Tooltip("Enable mock tracking in the Unity Editor")]
        [SerializeField] private bool enableDesktopPreview = false;
        [Tooltip("Reference image to simulate as camera input")]
        [SerializeField] private Texture2D previewReferenceImage;

        // === EVENTS ===
        [Header("Events")]
        [SerializeField] public UnityEvent OnEngineReady;
        [SerializeField] public UnityEvent<string> OnEngineError;
        [SerializeField] public UnityEvent OnCameraPermissionGranted;
        [SerializeField] public UnityEvent OnCameraPermissionDenied;

        // === STATE ===
        private bool isInitialized = false;
        public bool IsInitialized => isInitialized;

        public bool ImageTrackingEnabled => enableImageTracking;
        public bool WorldTrackingEnabled => enableWorldTracking;
        public bool FaceTrackingEnabled => enableFaceTracking;

        // Singleton (optional — only one XR8Manager per scene)
        private static XR8Manager _instance;
        public static XR8Manager Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[XR8Manager] Multiple instances detected — destroying duplicate");
                Destroy(gameObject);
                return;
            }
            _instance = this;

            // Auto-find components if not assigned
            if (arCamera == null)
                arCamera = Camera.main;

            if (xr8CameraComponent == null && arCamera != null)
                xr8CameraComponent = arCamera.GetComponent<XR8Camera>();

            if (imageTracker == null && enableImageTracking)
                imageTracker = FindObjectOfType<XR8ImageTracker>();

            Application.targetFrameRate = targetFrameRate;
        }

        private IEnumerator Start()
        {
            if (transform.parent != null)
            {
                Debug.LogError("[XR8Manager] Must be on a root-level GameObject to receive SendMessage calls");
            }

#if UNITY_EDITOR
            if (enableDesktopPreview)
            {
                Debug.Log("[XR8Manager] Desktop Preview mode — skipping XR8 engine start");
                yield return StartCoroutine(StartDesktopPreview());
                yield break;
            }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log("[XR8Manager] Starting XR8 engine...");
            string config = BuildConfigJson();
            Debug.Log("[XR8Manager] Config: " + config);
            WebGLStartXR8WithConfig(config);
#endif
            yield break;
        }

        /// <summary>
        /// Build the JSON config that xr8-bridge.js uses to initialize XR8
        /// </summary>
        private string BuildConfigJson()
        {
            var json = "{";
            json += "\"enableImageTracking\":" + BoolToJson(enableImageTracking) + ",";
            json += "\"enableWorldTracking\":" + BoolToJson(enableWorldTracking) + ",";
            json += "\"enableFaceTracking\":" + BoolToJson(enableFaceTracking) + ",";
            json += "\"enableSurfaceDetection\":" + BoolToJson(enableSurfaceDetection) + ",";
            json += "\"showSurfaceMeshes\":" + BoolToJson(showSurfaceMeshes) + ",";
            json += "\"maxFaces\":" + maxFaces + ",";
            json += "\"enableLighting\":" + BoolToJson(enableLighting) + ",";
            json += "\"targetFrameRate\":" + targetFrameRate;
            json += "}";
            return json;
        }

        private string BoolToJson(bool val) => val ? "true" : "false";

        // --- Called from JS via SendMessage ---

        void OnXR8Ready()
        {
            Debug.Log("[XR8Manager] Engine ready!");
            isInitialized = true;
            OnEngineReady?.Invoke();
        }

        void OnXR8Error(string error)
        {
            Debug.LogError("[XR8Manager] Engine error: " + error);
            OnEngineError?.Invoke(error);
        }

        void OnXR8CameraPermissionGranted()
        {
            Debug.Log("[XR8Manager] Camera permission granted");
            OnCameraPermissionGranted?.Invoke();
        }

        void OnXR8CameraPermissionDenied()
        {
            Debug.LogWarning("[XR8Manager] Camera permission denied!");
            OnCameraPermissionDenied?.Invoke();
        }

        // --- Desktop Preview (Editor Only) ---
#if UNITY_EDITOR
        private string previewActiveTargetId = null;

        private IEnumerator StartDesktopPreview()
        {
            Debug.Log("[XR8Manager] Starting desktop preview...");

            isInitialized = true;
            OnEngineReady?.Invoke();

            // Simulate camera ready
            if (xr8CameraComponent != null)
            {
                xr8CameraComponent.cam.fieldOfView = 60;
            }

            // If image tracking is enabled, simulate tracking found for the first target
            if (enableImageTracking && imageTracker != null)
            {
                // Wait for ImageTracker to finish its Start() and populate targets
                float timeout = 5f;
                while (!imageTracker.IsReady && timeout > 0)
                {
                    yield return null;
                    timeout -= Time.deltaTime;
                }

                if (!imageTracker.IsReady)
                {
                    Debug.LogError("[XR8Manager] Timed out waiting for XR8ImageTracker to initialize!");
                    yield break;
                }

                // Get real target IDs from the image tracker
                var targetIds = imageTracker.GetTargetIds();
                if (targetIds.Count > 0)
                {
                    previewActiveTargetId = targetIds[0];
                    Debug.Log("[XR8Manager] Desktop Preview: Simulating tracking found for '" + previewActiveTargetId + "'");
                    imageTracker.SendMessage("OnTrackingFound", previewActiveTargetId, SendMessageOptions.DontRequireReceiver);

                    // Start continuous pose updates
                    StartCoroutine(DesktopPreviewUpdateLoop());
                }
                else
                {
                    Debug.LogWarning("[XR8Manager] Desktop Preview: No image targets configured on XR8ImageTracker! " +
                        "Click '+ Add' in the XR8ImageTracker inspector to add your targets.");
                }
            }

            yield break;
        }

        private IEnumerator DesktopPreviewUpdateLoop()
        {
            while (enableDesktopPreview && previewActiveTargetId != null && imageTracker != null)
            {
                // Place the target 2 meters in front of the camera, facing it
                var camT = arCamera.transform;
                var targetPos = camT.position + camT.forward * 2f;
                var fwd = -camT.forward;
                var up = Vector3.up;
                var right = Vector3.Cross(up, fwd).normalized;

                // Build CSV: id,px,py,pz,fx,fy,fz,ux,uy,uz,rx,ry,rz
                string csv = previewActiveTargetId + "," +
                    targetPos.x.ToString("F6") + "," +
                    targetPos.y.ToString("F6") + "," +
                    targetPos.z.ToString("F6") + "," +
                    fwd.x.ToString("F6") + "," +
                    fwd.y.ToString("F6") + "," +
                    fwd.z.ToString("F6") + "," +
                    up.x.ToString("F6") + "," +
                    up.y.ToString("F6") + "," +
                    up.z.ToString("F6") + "," +
                    right.x.ToString("F6") + "," +
                    right.y.ToString("F6") + "," +
                    right.z.ToString("F6");

                imageTracker.SendMessage("OnTrack", csv, SendMessageOptions.DontRequireReceiver);

                yield return null;
            }
        }
#endif

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;

#if UNITY_WEBGL && !UNITY_EDITOR
            if (isInitialized)
            {
                try { WebGLStopXR8(); } catch { }
            }
#endif
        }
    }
}
