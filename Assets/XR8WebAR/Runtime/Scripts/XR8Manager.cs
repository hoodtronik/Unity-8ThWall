using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using System.Runtime.InteropServices;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace XR8WebAR
{
    /// <summary>
    /// Desktop preview simulation modes.
    /// </summary>
    public enum DesktopPreviewMode
    {
        /// <summary>Preview disabled — XR8 engine runs normally.</summary>
        None,
        /// <summary>Current default: target locked in front of camera with mouse/keyboard controls.</summary>
        Static,
        /// <summary>WASD + mouse look free-fly through the scene (Lightship-style).</summary>
        FlyThrough,
        /// <summary>Replay a recorded pose sequence from a CSV TextAsset.</summary>
        RecordedPlayback,
        /// <summary>Static mode + configurable jitter, drift, and random tracking loss.</summary>
        SimulatedNoise
    }

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
        [SerializeField] private XR8WorldTracker worldTracker;
        [Tooltip("Enable surface detection and hit testing")]
        [SerializeField] private bool enableSurfaceDetection = true;
        [Tooltip("Show detected surface meshes (debug)")]
        [SerializeField] private bool showSurfaceMeshes = false;

        // === COMBINED TRACKING ===
        [Header("Combined Tracking (Image + World)")]
        [Tooltip("Combined tracker for image-to-floor content placement")]
        [SerializeField] private XR8CombinedTracker combinedTracker;

        // === FACE TRACKING CONFIG ===
        [Header("Face Tracking (if enabled)")]
        [SerializeField] private XR8FaceTracker faceTracker;
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
        [Tooltip("Select a preview simulation mode for in-editor testing")]
        [SerializeField] private DesktopPreviewMode previewMode = DesktopPreviewMode.None;

        [Tooltip("Reference image to simulate as camera input")]
        [SerializeField] private Texture2D previewReferenceImage;

        // --- FlyThrough settings ---
        [Tooltip("Movement speed in m/s")]
        [SerializeField] private float flySpeed = 5f;
        [Tooltip("Mouse look sensitivity")]
        [SerializeField] private float flyLookSensitivity = 2f;
        [Tooltip("Auto-fire tracking for nearest image target")]
        [SerializeField] private bool autoTrackNearest = true;

        // --- RecordedPlayback settings ---
        [Tooltip("CSV file with recorded pose data (frame,id,px,py,pz,fx,fy,fz,ux,uy,uz,rx,ry,rz)")]
        [SerializeField] private TextAsset playbackDataFile;
        [Tooltip("Loop the playback recording")]
        [SerializeField] private bool playbackLoop = true;
        [Tooltip("Playback speed multiplier")]
        [SerializeField] private float playbackSpeed = 1f;

        // --- SimulatedNoise settings ---
        [Tooltip("Position jitter magnitude (meters)")]
        [SerializeField] private float positionJitter = 0.005f;
        [Tooltip("Rotation jitter magnitude (degrees)")]
        [SerializeField] private float rotationJitter = 0.5f;
        [Tooltip("Chance per frame of losing tracking (0-1)")]
        [SerializeField][Range(0f, 0.1f)] private float trackingLossChance = 0.002f;
        [Tooltip("Min/max seconds tracking stays lost")]
        [SerializeField] private Vector2 trackingLossDuration = new Vector2(0.5f, 2f);

        // Backward compatibility
        /// <summary>True if any preview mode is active.</summary>
        public bool DesktopPreviewEnabled => previewMode != DesktopPreviewMode.None;
        public DesktopPreviewMode PreviewMode => previewMode;

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
                imageTracker = FindFirstObjectByType<XR8ImageTracker>();

            if (worldTracker == null && enableWorldTracking)
                worldTracker = FindFirstObjectByType<XR8WorldTracker>();

            if (faceTracker == null && enableFaceTracking)
                faceTracker = FindFirstObjectByType<XR8FaceTracker>();

            if (combinedTracker == null && enableImageTracking && enableWorldTracking)
                combinedTracker = FindFirstObjectByType<XR8CombinedTracker>();

            Application.targetFrameRate = targetFrameRate;
        }

        private IEnumerator Start()
        {
            if (transform.parent != null)
            {
                Debug.LogError("[XR8Manager] Must be on a root-level GameObject to receive SendMessage calls");
            }

#if UNITY_EDITOR
            if (DesktopPreviewEnabled)
            {
                Debug.Log("[XR8Manager] Desktop Preview mode (" + previewMode + ") — skipping XR8 engine start");
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
        private int previewTargetIndex = 0;
        private float previewTargetDistance = 2f;
        private Vector2 previewTargetOffset = Vector2.zero;
        private bool previewIsTracking = false;
        private bool previewUseMouse = true;

        // FlyThrough state
        private float flyYaw = 0f;
        private float flyPitch = 0f;
        private float currentFlySpeed;

        // RecordedPlayback state
        private string[][] playbackFrames;
        private int playbackFrameIndex = 0;
        private float playbackTimer = 0f;

        // SimulatedNoise state
        private float noiseTime = 0f;
        private float trackingLostTimer = 0f;
        private bool noiseTrackingLost = false;

        [Header("Desktop Preview Controls")]
        [Tooltip("Use webcam feed as background (if available)")]
        [SerializeField] private bool previewUseWebcam = false;
        private WebCamTexture previewWebcam;

        private IEnumerator StartDesktopPreview()
        {
            Debug.Log("[XR8Manager] ===== Desktop Preview Mode: " + previewMode + " =====");

            switch (previewMode)
            {
                case DesktopPreviewMode.Static:
                case DesktopPreviewMode.SimulatedNoise:
                    LogStaticControls();
                    break;
                case DesktopPreviewMode.FlyThrough:
                    LogFlyThroughControls();
                    break;
                case DesktopPreviewMode.RecordedPlayback:
                    LogPlaybackControls();
                    break;
            }

            isInitialized = true;
            OnEngineReady?.Invoke();

            // Set camera FOV to match typical phone
            if (xr8CameraComponent != null)
            {
                xr8CameraComponent.cam.fieldOfView = 60;
            }

            // Optional: start webcam for background
            if (previewUseWebcam)
            {
                StartPreviewWebcam();
            }

            // Mode-specific init
            if (previewMode == DesktopPreviewMode.FlyThrough)
            {
                currentFlySpeed = flySpeed;
                if (arCamera != null)
                {
                    var euler = arCamera.transform.eulerAngles;
                    flyYaw = euler.y;
                    flyPitch = euler.x;
                }
            }

            if (previewMode == DesktopPreviewMode.RecordedPlayback)
            {
                if (!LoadPlaybackData())
                {
                    Debug.LogError("[XR8Manager] Playback: No data file assigned or file is empty!");
                    yield break;
                }
            }

            // Wait for ImageTracker to be ready (all modes except pure FlyThrough without auto-track)
            if (enableImageTracking && imageTracker != null)
            {
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

                var targetIds = imageTracker.GetTargetIds();
                if (targetIds.Count > 0)
                {
                    previewActiveTargetId = targetIds[0];
                    previewTargetIndex = 0;
                    Debug.Log("[XR8Manager] Preview: Ready with " + targetIds.Count + " target(s).");

                    // Auto-start tracking for immediate preview (Static/Noise modes)
                    if (previewMode != DesktopPreviewMode.RecordedPlayback)
                    {
                        yield return new WaitForSeconds(0.5f);
                        PreviewStartTracking();
                    }
                }
                else
                {
                    Debug.LogWarning("[XR8Manager] Desktop Preview: No image targets configured! " +
                        "Click '+ Add' in the XR8ImageTracker inspector to add your targets.");
                }
            }

            // Start face tracking preview if enabled
            if (enableFaceTracking && faceTracker != null)
            {
                faceTracker.StartDesktopPreview();
            }

            // Start recorded playback loop
            if (previewMode == DesktopPreviewMode.RecordedPlayback)
            {
                StartCoroutine(PlaybackUpdateLoop());
            }

            yield break;
        }

        // ---- Control help text ----

        private void LogStaticControls()
        {
            Debug.Log("[XR8Manager] Controls:");
            Debug.Log("  [T] Toggle tracking on/off");
            Debug.Log("  [Tab] Cycle through image targets");
            Debug.Log("  [Mouse Drag] Move target position");
            Debug.Log("  [Scroll] Adjust target distance");
            Debug.Log("  [R] Reset target position");
            Debug.Log("  [Esc] Lose tracking");
            if (previewMode == DesktopPreviewMode.SimulatedNoise)
                Debug.Log("  (Noise: jitter=" + positionJitter + "m, loss=" + (trackingLossChance*100f).ToString("F1") + "%)");
            Debug.Log("========================================");
        }

        private void LogFlyThroughControls()
        {
            Debug.Log("[XR8Manager] Controls:");
            Debug.Log("  [WASD] Move forward/back/strafe");
            Debug.Log("  [Q/E] Move down/up");
            Debug.Log("  [Right-click + Mouse] Look around");
            Debug.Log("  [Scroll] Adjust speed");
            Debug.Log("  [Shift] Sprint (2x speed)");
            Debug.Log("  [T] Toggle tracking on/off");
            Debug.Log("  [Tab] Cycle through image targets");
            Debug.Log("========================================");
        }

        private void LogPlaybackControls()
        {
            Debug.Log("[XR8Manager] Controls:");
            Debug.Log("  [Space] Pause/resume playback");
            Debug.Log("  [Left/Right] Step frame by frame");
            Debug.Log("  [R] Restart from beginning");
            Debug.Log("========================================");
        }

        // ---- Tracking controls (shared) ----

        private void PreviewStartTracking()
        {
            if (previewIsTracking || previewActiveTargetId == null) return;

            previewIsTracking = true;
            previewTargetDistance = 2f;
            previewTargetOffset = Vector2.zero;

            Debug.Log("[XR8Manager] Preview: Tracking FOUND -> '" + previewActiveTargetId + "'");

            // Fire tracking found event
            imageTracker.SendMessage("OnTrackingFound", previewActiveTargetId, SendMessageOptions.DontRequireReceiver);

            // Start continuous pose updates (Static/Noise modes)
            if (previewMode == DesktopPreviewMode.Static || previewMode == DesktopPreviewMode.SimulatedNoise
                || previewMode == DesktopPreviewMode.FlyThrough)
            {
                StartCoroutine(DesktopPreviewUpdateLoop());
            }
        }

        private void PreviewStopTracking()
        {
            if (!previewIsTracking || previewActiveTargetId == null) return;

            previewIsTracking = false;
            Debug.Log("[XR8Manager] Preview: Tracking LOST -> '" + previewActiveTargetId + "'");
            imageTracker.SendMessage("OnTrackingLost", previewActiveTargetId, SendMessageOptions.DontRequireReceiver);
        }

        private void PreviewCycleTarget()
        {
            if (imageTracker == null) return;
            var targetIds = imageTracker.GetTargetIds();
            if (targetIds.Count <= 1) return;

            bool wasTracking = previewIsTracking;
            if (wasTracking) PreviewStopTracking();

            previewTargetIndex = (previewTargetIndex + 1) % targetIds.Count;
            previewActiveTargetId = targetIds[previewTargetIndex];
            Debug.Log("[XR8Manager] Preview: Switched to target '" + previewActiveTargetId + "' (" + (previewTargetIndex + 1) + "/" + targetIds.Count + ")");

            if (wasTracking) PreviewStartTracking();
        }

        private void StartPreviewWebcam()
        {
            var devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                Debug.LogWarning("[XR8Manager] Preview: No webcam found for background");
                return;
            }

            previewWebcam = new WebCamTexture(devices[0].name, 1280, 720, 30);
            previewWebcam.Play();
            Debug.Log("[XR8Manager] Preview: Webcam started -> " + devices[0].name);

            if (arCamera != null)
            {
                arCamera.clearFlags = CameraClearFlags.SolidColor;
                arCamera.backgroundColor = Color.black;
            }
        }

        // ==================================================================
        // STATIC / NOISE MODE UPDATE LOOP
        // ==================================================================

        private IEnumerator DesktopPreviewUpdateLoop()
        {
            while (DesktopPreviewEnabled && previewIsTracking && previewActiveTargetId != null && imageTracker != null)
            {
                // SimulatedNoise: random tracking loss
                if (previewMode == DesktopPreviewMode.SimulatedNoise)
                {
                    if (noiseTrackingLost)
                    {
                        trackingLostTimer -= Time.deltaTime;
                        if (trackingLostTimer <= 0)
                        {
                            noiseTrackingLost = false;
                            Debug.Log("[XR8Manager] Noise: Tracking recovered");
                            imageTracker.SendMessage("OnTrackingFound", previewActiveTargetId, SendMessageOptions.DontRequireReceiver);
                        }
                        else
                        {
                            yield return null;
                            continue;
                        }
                    }
                    else if (Random.value < trackingLossChance)
                    {
                        noiseTrackingLost = true;
                        trackingLostTimer = Random.Range(trackingLossDuration.x, trackingLossDuration.y);
                        Debug.Log("[XR8Manager] Noise: Tracking lost for " + trackingLostTimer.ToString("F1") + "s");
                        imageTracker.SendMessage("OnTrackingLost", previewActiveTargetId, SendMessageOptions.DontRequireReceiver);
                        yield return null;
                        continue;
                    }
                }

                // Handle mouse drag for target offset (Static/Noise only)
                if (previewUseMouse && previewMode != DesktopPreviewMode.FlyThrough)
                {
#if ENABLE_INPUT_SYSTEM
                    var mouse = Mouse.current;
                    if (mouse != null && mouse.leftButton.isPressed)
                    {
                        var delta = mouse.delta.ReadValue();
                        previewTargetOffset.x += delta.x * 0.002f;
                        previewTargetOffset.y += delta.y * 0.002f;
                    }

                    float scroll = mouse != null ? mouse.scroll.ReadValue().y / 120f : 0f;
                    if (Mathf.Abs(scroll) > 0.001f)
                    {
                        previewTargetDistance = Mathf.Clamp(previewTargetDistance - scroll * 2f, 0.3f, 10f);
                    }
#else
                    if (Input.GetMouseButton(0))
                    {
                        previewTargetOffset.x += Input.GetAxis("Mouse X") * 0.05f;
                        previewTargetOffset.y += Input.GetAxis("Mouse Y") * 0.05f;
                    }

                    float scroll = Input.GetAxis("Mouse ScrollWheel");
                    if (Mathf.Abs(scroll) > 0.001f)
                    {
                        previewTargetDistance = Mathf.Clamp(previewTargetDistance - scroll * 2f, 0.3f, 10f);
                    }
#endif
                }

                // Compute target pose relative to camera
                var camT = arCamera.transform;
                var targetPos = camT.position
                    + camT.forward * previewTargetDistance
                    + camT.right * previewTargetOffset.x
                    + camT.up * previewTargetOffset.y;

                // SimulatedNoise: apply Perlin jitter
                if (previewMode == DesktopPreviewMode.SimulatedNoise)
                {
                    noiseTime += Time.deltaTime * 3f;
                    targetPos += new Vector3(
                        (Mathf.PerlinNoise(noiseTime, 0f) - 0.5f) * 2f * positionJitter,
                        (Mathf.PerlinNoise(0f, noiseTime) - 0.5f) * 2f * positionJitter,
                        (Mathf.PerlinNoise(noiseTime, noiseTime) - 0.5f) * 2f * positionJitter
                    );
                }

                var fwd = -camT.forward;
                var up = Vector3.up;
                var right = Vector3.Cross(up, fwd).normalized;

                // SimulatedNoise: apply rotation jitter
                if (previewMode == DesktopPreviewMode.SimulatedNoise && rotationJitter > 0f)
                {
                    var jitterRot = Quaternion.Euler(
                        (Mathf.PerlinNoise(noiseTime * 1.3f, 0f) - 0.5f) * 2f * rotationJitter,
                        (Mathf.PerlinNoise(0f, noiseTime * 1.7f) - 0.5f) * 2f * rotationJitter,
                        0f
                    );
                    fwd = jitterRot * fwd;
                    up = jitterRot * up;
                    right = jitterRot * right;
                }

                // Convert to camera-local space because XR8 tracking data is relative to the camera
                var localPos = camT.InverseTransformPoint(targetPos);
                var localFwd = camT.InverseTransformDirection(fwd);
                var localUp = camT.InverseTransformDirection(up);
                var localRight = camT.InverseTransformDirection(right);

                SendTrackingCSV(previewActiveTargetId, localPos, localFwd, localUp, localRight);

                yield return null;
            }
        }

        // ==================================================================
        // FLY-THROUGH MODE
        // ==================================================================

        private void FlyThroughUpdate()
        {
            if (arCamera == null) return;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null) return;

            // Right-click look
            if (mouse != null && mouse.rightButton.isPressed)
            {
                var delta = mouse.delta.ReadValue();
                flyYaw += delta.x * flyLookSensitivity * 0.1f;
                flyPitch -= delta.y * flyLookSensitivity * 0.1f;
                flyPitch = Mathf.Clamp(flyPitch, -89f, 89f);
            }

            // Scroll to adjust speed
            if (mouse != null)
            {
                float scrollDelta = mouse.scroll.ReadValue().y / 120f;
                if (Mathf.Abs(scrollDelta) > 0.01f)
                    currentFlySpeed = Mathf.Clamp(currentFlySpeed + scrollDelta * 0.5f, 0.5f, 50f);
            }

            float speed = currentFlySpeed * Time.deltaTime;
            if (kb[Key.LeftShift].isPressed) speed *= 2f;

            var move = Vector3.zero;
            if (kb[Key.W].isPressed) move += Vector3.forward;
            if (kb[Key.S].isPressed) move += Vector3.back;
            if (kb[Key.A].isPressed) move += Vector3.left;
            if (kb[Key.D].isPressed) move += Vector3.right;
            if (kb[Key.E].isPressed) move += Vector3.up;
            if (kb[Key.Q].isPressed) move += Vector3.down;
#else
            // Right-click look
            if (Input.GetMouseButton(1))
            {
                flyYaw += Input.GetAxis("Mouse X") * flyLookSensitivity;
                flyPitch -= Input.GetAxis("Mouse Y") * flyLookSensitivity;
                flyPitch = Mathf.Clamp(flyPitch, -89f, 89f);
            }

            float scrollDelta = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scrollDelta) > 0.001f)
                currentFlySpeed = Mathf.Clamp(currentFlySpeed + scrollDelta * 5f, 0.5f, 50f);

            float speed = currentFlySpeed * Time.deltaTime;
            if (Input.GetKey(KeyCode.LeftShift)) speed *= 2f;

            var move = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) move += Vector3.forward;
            if (Input.GetKey(KeyCode.S)) move += Vector3.back;
            if (Input.GetKey(KeyCode.A)) move += Vector3.left;
            if (Input.GetKey(KeyCode.D)) move += Vector3.right;
            if (Input.GetKey(KeyCode.E)) move += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) move += Vector3.down;
#endif

            arCamera.transform.rotation = Quaternion.Euler(flyPitch, flyYaw, 0f);
            arCamera.transform.position += arCamera.transform.TransformDirection(move) * speed;
        }

        // ==================================================================
        // RECORDED PLAYBACK MODE
        // ==================================================================

        private bool playbackPaused = false;

        private bool LoadPlaybackData()
        {
            if (playbackDataFile == null) return false;

            var lines = playbackDataFile.text.Split('\n');
            var frames = new System.Collections.Generic.List<string[]>();
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith("//"))
                    continue;
                var cols = trimmed.Split(',');
                if (cols.Length >= 13) // id + 12 floats
                    frames.Add(cols);
            }
            playbackFrames = frames.ToArray();
            playbackFrameIndex = 0;
            playbackTimer = 0f;
            Debug.Log("[XR8Manager] Playback: Loaded " + playbackFrames.Length + " frames from " + playbackDataFile.name);
            return playbackFrames.Length > 0;
        }

        private IEnumerator PlaybackUpdateLoop()
        {
            if (playbackFrames == null || playbackFrames.Length == 0) yield break;

            // Fire initial tracking found
            string firstId = playbackFrames[0][0].Trim();
            previewActiveTargetId = firstId;
            previewIsTracking = true;
            if (imageTracker != null)
                imageTracker.SendMessage("OnTrackingFound", firstId, SendMessageOptions.DontRequireReceiver);

            float frameInterval = 1f / 30f; // Assume 30fps recording

            while (DesktopPreviewEnabled && previewMode == DesktopPreviewMode.RecordedPlayback)
            {
                if (!playbackPaused)
                {
                    playbackTimer += Time.deltaTime * playbackSpeed;

                    if (playbackTimer >= frameInterval)
                    {
                        playbackTimer -= frameInterval;
                        playbackFrameIndex++;

                        if (playbackFrameIndex >= playbackFrames.Length)
                        {
                            if (playbackLoop)
                            {
                                playbackFrameIndex = 0;
                                Debug.Log("[XR8Manager] Playback: Looping...");
                            }
                            else
                            {
                                Debug.Log("[XR8Manager] Playback: Finished (" + playbackFrames.Length + " frames)");
                                PreviewStopTracking();
                                yield break;
                            }
                        }

                        // Send frame data
                        var cols = playbackFrames[playbackFrameIndex];
                        string id = cols[0].Trim();

                        // Handle target changes
                        if (id != previewActiveTargetId)
                        {
                            if (previewIsTracking)
                            {
                                imageTracker.SendMessage("OnTrackingLost", previewActiveTargetId, SendMessageOptions.DontRequireReceiver);
                            }
                            previewActiveTargetId = id;
                            imageTracker.SendMessage("OnTrackingFound", id, SendMessageOptions.DontRequireReceiver);
                        }

                        // Build CSV from recorded data
                        string csv = string.Join(",", cols);
                        imageTracker.SendMessage("OnTrack", csv, SendMessageOptions.DontRequireReceiver);
                    }
                }

                yield return null;
            }
        }

        // ==================================================================
        // INPUT HANDLING
        // ==================================================================

        private void DesktopPreviewHandleInput()
        {
            if (!DesktopPreviewEnabled) return;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;

            // Common: [T] Toggle tracking, [Tab] Cycle targets
            if (kb[Key.T].wasPressedThisFrame)
            {
                if (previewIsTracking) PreviewStopTracking();
                else PreviewStartTracking();
            }
            if (kb[Key.Tab].wasPressedThisFrame)
                PreviewCycleTarget();

            switch (previewMode)
            {
                case DesktopPreviewMode.Static:
                case DesktopPreviewMode.SimulatedNoise:
                    if (kb[Key.R].wasPressedThisFrame)
                    {
                        previewTargetDistance = 2f;
                        previewTargetOffset = Vector2.zero;
                        Debug.Log("[XR8Manager] Preview: Position reset");
                    }
                    if (kb[Key.Escape].wasPressedThisFrame)
                        PreviewStopTracking();
                    break;

                case DesktopPreviewMode.RecordedPlayback:
                    if (kb[Key.Space].wasPressedThisFrame)
                    {
                        playbackPaused = !playbackPaused;
                        Debug.Log("[XR8Manager] Playback: " + (playbackPaused ? "Paused" : "Resumed") +
                            " (frame " + playbackFrameIndex + "/" + (playbackFrames?.Length ?? 0) + ")");
                    }
                    if (kb[Key.RightArrow].wasPressedThisFrame && playbackFrames != null)
                    {
                        playbackPaused = true;
                        playbackFrameIndex = Mathf.Min(playbackFrameIndex + 1, playbackFrames.Length - 1);
                    }
                    if (kb[Key.LeftArrow].wasPressedThisFrame && playbackFrames != null)
                    {
                        playbackPaused = true;
                        playbackFrameIndex = Mathf.Max(playbackFrameIndex - 1, 0);
                    }
                    if (kb[Key.R].wasPressedThisFrame)
                    {
                        playbackFrameIndex = 0;
                        playbackTimer = 0f;
                        Debug.Log("[XR8Manager] Playback: Restarted");
                    }
                    break;
            }
#else
            // Common: [T] Toggle tracking, [Tab] Cycle targets
            if (Input.GetKeyDown(KeyCode.T))
            {
                if (previewIsTracking) PreviewStopTracking();
                else PreviewStartTracking();
            }
            if (Input.GetKeyDown(KeyCode.Tab))
                PreviewCycleTarget();

            switch (previewMode)
            {
                case DesktopPreviewMode.Static:
                case DesktopPreviewMode.SimulatedNoise:
                    if (Input.GetKeyDown(KeyCode.R))
                    {
                        previewTargetDistance = 2f;
                        previewTargetOffset = Vector2.zero;
                        Debug.Log("[XR8Manager] Preview: Position reset");
                    }
                    if (Input.GetKeyDown(KeyCode.Escape))
                        PreviewStopTracking();
                    break;

                case DesktopPreviewMode.RecordedPlayback:
                    if (Input.GetKeyDown(KeyCode.Space))
                    {
                        playbackPaused = !playbackPaused;
                        Debug.Log("[XR8Manager] Playback: " + (playbackPaused ? "Paused" : "Resumed"));
                    }
                    if (Input.GetKeyDown(KeyCode.RightArrow) && playbackFrames != null)
                    {
                        playbackPaused = true;
                        playbackFrameIndex = Mathf.Min(playbackFrameIndex + 1, playbackFrames.Length - 1);
                    }
                    if (Input.GetKeyDown(KeyCode.LeftArrow) && playbackFrames != null)
                    {
                        playbackPaused = true;
                        playbackFrameIndex = Mathf.Max(playbackFrameIndex - 1, 0);
                    }
                    if (Input.GetKeyDown(KeyCode.R))
                    {
                        playbackFrameIndex = 0;
                        playbackTimer = 0f;
                        Debug.Log("[XR8Manager] Playback: Restarted");
                    }
                    break;
            }
#endif
        }

        // ==================================================================
        // UTILITIES
        // ==================================================================

        private void SendTrackingCSV(string id, Vector3 pos, Vector3 fwd, Vector3 up, Vector3 right)
        {
            string csv = id + "," +
                pos.x.ToString("F6") + "," + pos.y.ToString("F6") + "," + pos.z.ToString("F6") + "," +
                fwd.x.ToString("F6") + "," + fwd.y.ToString("F6") + "," + fwd.z.ToString("F6") + "," +
                up.x.ToString("F6") + "," + up.y.ToString("F6") + "," + up.z.ToString("F6") + "," +
                right.x.ToString("F6") + "," + right.y.ToString("F6") + "," + right.z.ToString("F6");
            imageTracker.SendMessage("OnTrack", csv, SendMessageOptions.DontRequireReceiver);
        }

        private void Update()
        {
            DesktopPreviewHandleInput();
            if (previewMode == DesktopPreviewMode.FlyThrough)
                FlyThroughUpdate();
        }

        private void CleanupPreviewWebcam()
        {
            if (previewWebcam != null && previewWebcam.isPlaying)
            {
                previewWebcam.Stop();
            }
        }
#endif

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;

#if UNITY_EDITOR
            CleanupPreviewWebcam();
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
            if (isInitialized)
            {
                try { WebGLStopXR8(); } catch { }
            }
#endif
        }
    }
}
