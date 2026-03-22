using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace XR8WebAR
{
    /// <summary>
    /// XR8WorldTracker — SLAM-based world tracking with multiple tracking modes.
    /// 
    /// Supports 3 modes (inspired by Imaginary Labs' WorldTracker architecture):
    ///   - SixDOF: Full SLAM-based 6DOF tracking (default)
    ///   - ThreeDOF: Rotation-only (device orientation), good for skybox/panorama
    ///   - Orbit: Camera orbits a center object (product viewers / configurators)
    /// 
    /// Integrates with XR8PlacementIndicator for visual placement workflow.
    /// Receives camera pose and surface events from xr8-bridge.js via SendMessage.
    /// Must be on a root-level GameObject named "XR8WorldTracker".
    /// </summary>
    public class XR8WorldTracker : MonoBehaviour
    {
        [DllImport("__Internal")] private static extern void WebGLXR8HitTest(float screenX, float screenY);
        [DllImport("__Internal")] private static extern void WebGLPlaceOrigin(string camPosStr);
        [DllImport("__Internal")] private static extern void WebGLResetOrigin();
        [DllImport("__Internal")] private static extern void WebGLSetViewportPos(string vStr);
        [DllImport("__Internal")] private static extern void SetWebGLWorldTrackerSettings(string settings);

        // ━━━ Tracking Mode ━━━
        public enum TrackingMode
        {
            [Tooltip("Full 6DOF SLAM tracking")]
            SixDOF,
            [Tooltip("3DOF rotation-only (gyro/orientation)")]
            ThreeDOF,
            [Tooltip("Camera orbits around a center point (product viewers)")]
            Orbit
        }

        public enum PlaneMode
        {
            [Tooltip("Detect horizontal surfaces (floors, tables)")]
            Horizontal,
            [Tooltip("Detect vertical surfaces (walls) — experimental")]
            Vertical
        }

        [Header("Tracking Mode")]
        [SerializeField] private TrackingMode mode = TrackingMode.SixDOF;
        [Tooltip("Which surface orientation to detect (horizontal = floors, vertical = walls)")]
        [SerializeField] private PlaneMode planeMode = PlaneMode.Horizontal;

        [Header("Camera")]
        [SerializeField] private Camera trackerCam;
        [Tooltip("Camera starting height in meters")]
        [SerializeField] private float cameraStartHeight = 1.25f;

        // ━━━ Content ━━━
        [Header("Content")]
        [Tooltip("The main AR content root — shown/hidden by placement workflow")]
        [SerializeField] private GameObject mainContent;
        [Tooltip("Prefab to instantiate at hit-test position")]
        [SerializeField] private GameObject placementPrefab;

        // ━━━ 3DOF Settings ━━━
        [Header("3DOF Settings")]
        [Tooltip("Virtual arm length for 3DOF depth simulation")]
        [SerializeField] private float armLength = 0.4f;
        [Tooltip("Extra smoothing (lerp camera pose each frame)")]
        [SerializeField] private bool useSmoothing = false;
        [Range(1f, 50f)]
        [SerializeField] private float smoothFactor = 10f;
        [Tooltip("Angle smooth factor sent to XR8 JS engine (lower = smoother gyro)")]
        [Range(0.0001f, 0.1f)]
        [SerializeField] private float angleSmoothFactor = 0.001f;
        [Tooltip("Angle drift threshold — ignore gyro changes below this (reduces jitter)")]
        [Range(0f, 0.05f)]
        [SerializeField] private float angleDriftThreshold = 0.025f;
        [Tooltip("Use device compass for heading (important for GPS-based AR)")]
        [SerializeField] private bool useCompass = false;

        // ━━━ Orbit Settings ━━━
        [Header("Orbit Mode Settings")]
        [Tooltip("The transform to orbit around")]
        [SerializeField] private Transform orbitCenter;
        [SerializeField] private float orbitDistance = 0.4f;
        [SerializeField] private float minOrbitDistance = 0.25f;
        [SerializeField] private float maxOrbitDistance = 2.5f;
        [Tooltip("Enable swipe-to-rotate in orbit mode")]
        [SerializeField] private bool orbitSwipeToRotate = true;
        [SerializeField] private float orbitSwipeSensitivity = 0.25f;
        [Tooltip("Enable pinch-to-zoom in orbit mode")]
        [SerializeField] private bool orbitPinchToZoom = true;

        // ━━━ Placement ━━━
        [Header("Placement")]
        [Tooltip("Use a placement indicator before showing content")]
        [SerializeField] private bool usePlacementIndicator = false;
        [SerializeField] private XR8PlacementIndicator placementIndicator;
        [Tooltip("Visualize detected surfaces (debug)")]
        [SerializeField] private bool showSurfaceVisuals = false;

        [Header("6DOF Resilience")]
        [Tooltip("When in 6DOF mode and tracking is lost, fall back to 3DOF (gyro-only) instead of freezing")]
        [SerializeField] private bool fallbackToThreeDOFOnLost = false;
        [Tooltip("Frames without surfaces before triggering 3DOF fallback")]
        [SerializeField] private int fallbackFrameThreshold = 30;

        // ━━━ Events ━━━
        [Header("Events")]
        [SerializeField] public UnityEvent<Vector3, Vector3> OnSurfaceDetected;
        [SerializeField] public UnityEvent<Vector3> OnObjectPlaced;
        [SerializeField] public UnityEvent<Vector3, Quaternion> OnCameraPoseUpdated;
        [SerializeField] public UnityEvent OnOriginPlaced;
        [SerializeField] public UnityEvent OnOriginReset;
        [Tooltip("Fires when XR8 reports tracking confidence (0 = lost, 1 = excellent)")]
        [SerializeField] public UnityEvent<float> OnTrackingConfidence;

        [Header("Show/Hide on Place/Reset")]
        [Tooltip("GameObjects to show when content is placed (e.g. UI buttons, overlays)")]
        [SerializeField] private List<GameObject> showOnPlaced = new List<GameObject>();
        [Tooltip("GameObjects to show when origin is reset (e.g. 'Scan your area' prompt)")]
        [SerializeField] private List<GameObject> showOnReset = new List<GameObject>();

        // ━━━ Internal State ━━━
        private Dictionary<string, Vector3> activeSurfaces = new Dictionary<string, Vector3>();
        private List<GameObject> placedObjects = new List<GameObject>();

        private Vector3 targetPos;
        private Quaternion targetRot;
        private Vector3 origCamPos;
        private Quaternion origCamRot;

        // Orbit state
        private Quaternion orbitSwipeOffset = Quaternion.identity;
        private Quaternion orbitLastSwipeOffset = Quaternion.identity;
        private bool orbitIsDragging = false;
        private Vector2 orbitDragStart;
        private bool orbitIsPinching = false;
        private float orbitStartDist;
        private Vector2 pinchTouch0Start, pinchTouch1Start;

        // 3DOF fallback state
        private int framesSinceLastSurface = 0;
        private bool isInFallback3DOF = false;
        private float lastConfidence = 1f;

        // ━━━ Public API ━━━
        public TrackingMode Mode => mode;
        public Dictionary<string, Vector3> ActiveSurfaces => activeSurfaces;
        public int PlacedObjectCount => placedObjects.Count;

        private void Awake()
        {
            if (trackerCam == null)
                trackerCam = Camera.main;

            // Set camera starting height
            var camPos = trackerCam.transform.position;
            camPos.y = cameraStartHeight;
            trackerCam.transform.position = camPos;

            origCamPos = trackerCam.transform.position;
            origCamRot = trackerCam.transform.rotation;
            targetPos = origCamPos;
            targetRot = origCamRot;

            // Hide content if using placement workflow
            if (usePlacementIndicator && mainContent != null)
                mainContent.SetActive(false);

            // Initial show/hide state for convenience lists
            foreach (var go in showOnPlaced) if (go != null) go.SetActive(false);
            foreach (var go in showOnReset) if (go != null) go.SetActive(true);
        }

        private void Start()
        {
            // Send world tracker settings to JS engine
            SendSettingsToJS();
        }

        /// <summary>Serialize and send all world tracker settings to the JS engine.</summary>
        private void SendSettingsToJS()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string modeStr = mode switch
            {
                TrackingMode.ThreeDOF => "3DOF",
                TrackingMode.Orbit => "ORBIT",
                _ => "6DOF"
            };

            // Build JSON matching Imaginary Labs' format for JS engine compatibility
            var json = "{";
            json += "\"MODE\":\"" + modeStr + "\",";
            json += "\"CAM_START_HEIGHT\":" + cameraStartHeight.ToString(CultureInfo.InvariantCulture) + ",";
            json += "\"ARM_LENGTH\":" + armLength.ToString(CultureInfo.InvariantCulture) + ",";
            json += "\"ANGLE_SMOOTH_FACTOR\":" + angleSmoothFactor.ToString(CultureInfo.InvariantCulture) + ",";
            json += "\"ANGLE_DRIFT_THRESHOLD\":" + angleDriftThreshold.ToString(CultureInfo.InvariantCulture) + ",";
            json += "\"USE_SMOOTHING\":" + (useSmoothing ? "true" : "false") + ",";
            json += "\"SMOOTH_FACTOR\":" + smoothFactor.ToString(CultureInfo.InvariantCulture) + ",";
            json += "\"USE_COMPASS\":" + (useCompass ? "true" : "false") + ",";
            json += "\"PLANE_MODE\":\"" + (planeMode == PlaneMode.Vertical ? "VERTICAL" : "HORIZONTAL") + "\"";
            json += "}";

            SetWebGLWorldTrackerSettings(json);
            Debug.Log("[XR8WorldTracker] Settings sent to JS: " + json);
#endif
        }

        private void Update()
        {
            switch (mode)
            {
                case TrackingMode.ThreeDOF:
                    Update_ThreeDOF();
                    break;
                case TrackingMode.Orbit:
                    Update_Orbit();
                    break;
                case TrackingMode.SixDOF:
                default:
                    // Camera pose is set directly in OnCameraPose
                    // 3DOF fallback: if no surfaces detected for N frames, use gyro smoothing
                    if (fallbackToThreeDOFOnLost)
                    {
                        if (activeSurfaces.Count == 0)
                        {
                            framesSinceLastSurface++;
                            if (framesSinceLastSurface >= fallbackFrameThreshold && !isInFallback3DOF)
                            {
                                isInFallback3DOF = true;
                                Debug.Log("[XR8WorldTracker] 3DOF fallback activated — no surfaces for " + fallbackFrameThreshold + " frames");
                            }
                        }
                        else
                        {
                            if (isInFallback3DOF)
                            {
                                Debug.Log("[XR8WorldTracker] 3DOF fallback deactivated — surfaces found");
                            }
                            framesSinceLastSurface = 0;
                            isInFallback3DOF = false;
                        }

                        if (isInFallback3DOF) Update_ThreeDOF();
                    }
                    break;
            }

#if UNITY_EDITOR
            Update_EditorDebug();
#endif
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        //  3DOF Mode
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Update_ThreeDOF()
        {
            if (!useSmoothing) return;

            // Smoothly lerp camera toward target pose
            trackerCam.transform.position = Vector3.Lerp(
                trackerCam.transform.position, targetPos,
                Time.deltaTime * smoothFactor
            );
            trackerCam.transform.rotation = Quaternion.Slerp(
                trackerCam.transform.rotation, targetRot,
                Time.deltaTime * smoothFactor
            );
        }

        private void UpdateCameraTransform_ThreeDOF(Vector3 pos, Quaternion rot)
        {
            if (!useSmoothing)
            {
                trackerCam.transform.localPosition = pos;
                trackerCam.transform.localRotation = rot;
            }
            else
            {
                targetPos = pos;
                targetRot = rot;
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        //  Orbit Mode
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Update_Orbit()
        {
            // Smooth camera toward target
            trackerCam.transform.position = Vector3.Lerp(
                trackerCam.transform.position, targetPos,
                Time.deltaTime * smoothFactor
            );
            trackerCam.transform.rotation = Quaternion.Slerp(
                trackerCam.transform.rotation, targetRot,
                Time.deltaTime * smoothFactor
            );

            if (orbitSwipeToRotate) Update_Orbit_Swipe();
            if (orbitPinchToZoom) Update_Orbit_Pinch();
        }

        private void UpdateCameraTransform_Orbit(Quaternion rawRot)
        {
            if (orbitCenter == null) return;

            Quaternion finalRot = orbitSwipeOffset * rawRot;
            Vector3 pos = orbitCenter.position - (finalRot * Vector3.forward) * orbitDistance;

            targetPos = pos;
            targetRot = finalRot;
        }

        private void Update_Orbit_Swipe()
        {
#if ENABLE_INPUT_SYSTEM
            // InputSystem path
            if (Touchscreen.current != null)
            {
                int activeTouches = 0;
                for (int i = 0; i < Touchscreen.current.touches.Count; i++)
                {
                    var tp = Touchscreen.current.touches[i].phase.ReadValue();
                    if (tp == UnityEngine.InputSystem.TouchPhase.Began ||
                        tp == UnityEngine.InputSystem.TouchPhase.Moved ||
                        tp == UnityEngine.InputSystem.TouchPhase.Stationary)
                        activeTouches++;
                }
                if (activeTouches > 1) { orbitIsDragging = false; return; }
            }

            bool pressed = (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                           (Touchscreen.current != null && Touchscreen.current.primaryTouch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began);
            bool released = (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame) ||
                            (Touchscreen.current != null && Touchscreen.current.primaryTouch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Ended);

            if (pressed)
            {
                orbitDragStart = GetPointerPosition();
                orbitIsDragging = true;
            }
            else if (released)
            {
                orbitIsDragging = false;
                orbitLastSwipeOffset = orbitSwipeOffset;
            }
            else if (orbitIsDragging)
            {
                Vector2 current = GetPointerPosition();
                float deltaX = current.x - orbitDragStart.x;
                orbitSwipeOffset = orbitLastSwipeOffset * Quaternion.Euler(0f, deltaX * orbitSwipeSensitivity, 0f);
            }
#else
            // Legacy Input path
            if (Input.touchCount > 1)
            {
                orbitIsDragging = false;
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                orbitDragStart = GetPointerPosition();
                orbitIsDragging = true;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                orbitIsDragging = false;
                orbitLastSwipeOffset = orbitSwipeOffset;
            }
            else if (orbitIsDragging)
            {
                Vector2 current = GetPointerPosition();
                float deltaX = current.x - orbitDragStart.x;
                orbitSwipeOffset = orbitLastSwipeOffset * Quaternion.Euler(0f, deltaX * orbitSwipeSensitivity, 0f);
            }
#endif
        }

        private void Update_Orbit_Pinch()
        {
#if ENABLE_INPUT_SYSTEM
            // InputSystem path
            if (Touchscreen.current == null) { orbitIsPinching = false; return; }

            int activeTouches = 0;
            for (int i = 0; i < Touchscreen.current.touches.Count; i++)
            {
                var tp = Touchscreen.current.touches[i].phase.ReadValue();
                if (tp == UnityEngine.InputSystem.TouchPhase.Began ||
                    tp == UnityEngine.InputSystem.TouchPhase.Moved ||
                    tp == UnityEngine.InputSystem.TouchPhase.Stationary)
                    activeTouches++;
            }
            if (activeTouches < 2) { orbitIsPinching = false; return; }

            var t0 = Touchscreen.current.touches[0];
            var t1 = Touchscreen.current.touches[1];

            if (t0.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began ||
                t1.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
            {
                pinchTouch0Start = t0.position.ReadValue();
                pinchTouch1Start = t1.position.ReadValue();
                orbitStartDist = orbitDistance;
                orbitIsPinching = true;
            }
            else if (t0.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Ended ||
                     t1.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Ended ||
                     t0.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled ||
                     t1.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                orbitIsPinching = false;
            }

            if (orbitIsPinching)
            {
                float startDist = (pinchTouch1Start - pinchTouch0Start).magnitude;
                if (startDist < 0.01f) return;
                float currentDist = Vector2.Distance(t0.position.ReadValue(), t1.position.ReadValue());
                orbitDistance = Mathf.Clamp(orbitStartDist / (currentDist / startDist), minOrbitDistance, maxOrbitDistance);
            }
#else
            // Legacy Input path
            if (Input.touchCount < 2)
            {
                orbitIsPinching = false;
                return;
            }

            var touch0 = Input.GetTouch(0);
            var touch1 = Input.GetTouch(1);

            if (touch0.phase == TouchPhase.Began || touch1.phase == TouchPhase.Began)
            {
                pinchTouch0Start = touch0.position;
                pinchTouch1Start = touch1.position;
                orbitStartDist = orbitDistance;
                orbitIsPinching = true;
            }
            else if (touch0.phase == TouchPhase.Ended || touch1.phase == TouchPhase.Ended ||
                     touch0.phase == TouchPhase.Canceled || touch1.phase == TouchPhase.Canceled)
            {
                orbitIsPinching = false;
            }

            if (orbitIsPinching)
            {
                float startDist = (pinchTouch1Start - pinchTouch0Start).magnitude;
                if (startDist < 0.01f) return;

                float currentDist = Vector2.Distance(touch0.position, touch1.position);
                orbitDistance = Mathf.Clamp(orbitStartDist / (currentDist / startDist), minOrbitDistance, maxOrbitDistance);
            }
#endif
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        //  JS Bridge — Called via SendMessage
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// Receives 6DOF camera pose from XR8 SLAM.
        /// CSV format: posX,posY,posZ,rotX,rotY,rotZ,rotW
        /// </summary>
        void OnCameraPose(string csv)
        {
            var parts = csv.Split(',');
            if (parts.Length < 7) return;

            var pos = new Vector3(
                float.Parse(parts[0], CultureInfo.InvariantCulture),
                float.Parse(parts[1], CultureInfo.InvariantCulture),
                -float.Parse(parts[2], CultureInfo.InvariantCulture) // XR8 Z → Unity -Z
            );

            var rot = new Quaternion(
                -float.Parse(parts[3], CultureInfo.InvariantCulture),
                -float.Parse(parts[4], CultureInfo.InvariantCulture),
                float.Parse(parts[5], CultureInfo.InvariantCulture),
                float.Parse(parts[6], CultureInfo.InvariantCulture)
            );

            switch (mode)
            {
                case TrackingMode.ThreeDOF:
                    UpdateCameraTransform_ThreeDOF(pos, rot);
                    break;
                case TrackingMode.Orbit:
                    UpdateCameraTransform_Orbit(rot);
                    break;
                case TrackingMode.SixDOF:
                default:
                    trackerCam.transform.localPosition = pos;
                    trackerCam.transform.localRotation = rot;
                    break;
            }

            OnCameraPoseUpdated?.Invoke(pos, rot);
        }

        /// <summary>Surface found event from XR8. CSV: id,posX,posY,posZ</summary>
        void OnSurfaceFound(string csv)
        {
            var parts = csv.Split(',');
            if (parts.Length < 4) return;

            string id = parts[0];
            var pos = new Vector3(
                float.Parse(parts[1], CultureInfo.InvariantCulture),
                float.Parse(parts[2], CultureInfo.InvariantCulture),
                -float.Parse(parts[3], CultureInfo.InvariantCulture)
            );

            activeSurfaces[id] = pos;
            Debug.Log("[XR8WorldTracker] Surface found: " + id + " at " + pos);
            OnSurfaceDetected?.Invoke(pos, Vector3.up);
        }

        /// <summary>Surface updated event from XR8.</summary>
        void OnSurfaceUpdated(string csv)
        {
            var parts = csv.Split(',');
            if (parts.Length < 4) return;

            string id = parts[0];
            var pos = new Vector3(
                float.Parse(parts[1], CultureInfo.InvariantCulture),
                float.Parse(parts[2], CultureInfo.InvariantCulture),
                -float.Parse(parts[3], CultureInfo.InvariantCulture)
            );

            activeSurfaces[id] = pos;
        }

        /// <summary>Surface lost event from XR8.</summary>
        void OnSurfaceLost(string id)
        {
            activeSurfaces.Remove(id);
            Debug.Log("[XR8WorldTracker] Surface lost: " + id);
        }

        /// <summary>Tracking confidence from XR8 (0 = lost, 1 = excellent). Called via SendMessage from JS.</summary>
        void OnTrackingConfidenceReceived(string val)
        {
            if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float confidence))
            {
                lastConfidence = confidence;
                OnTrackingConfidence?.Invoke(confidence);
            }
        }

        /// <summary>Hit test result. CSV: posX,posY,posZ,normalX,normalY,normalZ</summary>
        void OnHitTestResult(string csv)
        {
            var parts = csv.Split(',');
            if (parts.Length < 6) return;

            var pos = new Vector3(
                float.Parse(parts[0], CultureInfo.InvariantCulture),
                float.Parse(parts[1], CultureInfo.InvariantCulture),
                -float.Parse(parts[2], CultureInfo.InvariantCulture)
            );

            var normal = new Vector3(
                float.Parse(parts[3], CultureInfo.InvariantCulture),
                float.Parse(parts[4], CultureInfo.InvariantCulture),
                -float.Parse(parts[5], CultureInfo.InvariantCulture)
            );

            Debug.Log("[XR8WorldTracker] Hit test: " + pos);

            if (placementPrefab != null)
            {
                var obj = Instantiate(placementPrefab, pos, Quaternion.LookRotation(Vector3.forward, normal));
                placedObjects.Add(obj);
                OnObjectPlaced?.Invoke(pos);
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        //  Public API
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>Place content (via placement indicator or directly).</summary>
        public void PlaceOrigin()
        {
            if (placementIndicator != null)
            {
                placementIndicator.PlaceContent();
            }
            else if (mainContent != null)
            {
                mainContent.SetActive(true);
                // Face the camera
                Vector3 lookAt = trackerCam.transform.position;
                lookAt.y = 0f;
                mainContent.transform.LookAt(lookAt, Vector3.up);
            }

            // Show/hide convenience lists
            foreach (var go in showOnPlaced) if (go != null) go.SetActive(true);
            foreach (var go in showOnReset) if (go != null) go.SetActive(false);

            // Notify JS engine of origin placement
#if UNITY_WEBGL && !UNITY_EDITOR
            var camPos = trackerCam.transform.position;
            WebGLPlaceOrigin(camPos.x.ToString(CultureInfo.InvariantCulture) + "," +
                             camPos.y.ToString(CultureInfo.InvariantCulture) + "," +
                             camPos.z.ToString(CultureInfo.InvariantCulture));
#endif

            OnOriginPlaced?.Invoke();
        }

        /// <summary>Reset placement — hide content, show indicator, and reset JS engine origin.</summary>
        public void ResetOrigin()
        {
            if (placementIndicator != null)
            {
                placementIndicator.ResetPlacement();
            }
            else if (mainContent != null && usePlacementIndicator)
            {
                mainContent.SetActive(false);
            }

            // Show/hide convenience lists
            foreach (var go in showOnPlaced) if (go != null) go.SetActive(false);
            foreach (var go in showOnReset) if (go != null) go.SetActive(true);

            // Reset camera to original pose
            trackerCam.transform.position = origCamPos;
            trackerCam.transform.rotation = origCamRot;

            // Reset orbit state
            orbitSwipeOffset = Quaternion.identity;
            orbitLastSwipeOffset = Quaternion.identity;

            // Tell the JS engine to reset its origin — prevents SLAM drift accumulation
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLResetOrigin();
#endif

            OnOriginReset?.Invoke();
        }

        /// <summary>Perform a hit test at screen center (tap-to-place).</summary>
        public void TapToPlace()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLXR8HitTest(0.5f, 0.5f);
#else
            if (XR8Manager.Instance != null && XR8Manager.Instance.DesktopPreviewEnabled)
            {
                XR8Manager.Instance.SimulateHitTest(new Vector2(0.5f, 0.5f));
            }
            else
            {
                Debug.Log("[XR8WorldTracker] TapToPlace requires WebGL or an active XR8Manager Desktop Preview mode");
            }
#endif
        }

        /// <summary>Perform a hit test at a specific screen position.</summary>
        public void HitTestAt(Vector2 normalizedScreenPos)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLXR8HitTest(normalizedScreenPos.x, normalizedScreenPos.y);
#else
            if (XR8Manager.Instance != null && XR8Manager.Instance.DesktopPreviewEnabled)
            {
                XR8Manager.Instance.SimulateHitTest(normalizedScreenPos);
            }
#endif
        }

        /// <summary>Clear all placed objects.</summary>
        public void ClearPlacedObjects()
        {
            foreach (var obj in placedObjects)
            {
                if (obj != null) Destroy(obj);
            }
            placedObjects.Clear();
        }

        /// <summary>Get the tracker camera.</summary>
        public Camera TrackerCamera => trackerCam;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        //  Editor Debug
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Update_EditorDebug()
        {
            float speed = 2.5f * Time.deltaTime;
            float dx = 0f, dy = 0f, dz = 0f;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb[Key.A].isPressed) dx = -speed;
            if (kb[Key.D].isPressed) dx = speed;
            if (kb[Key.W].isPressed) dz = speed;
            if (kb[Key.S].isPressed) dz = -speed;
            if (kb[Key.R].isPressed) dy = speed;
            if (kb[Key.F].isPressed) dy = -speed;

            float angularSpeed = 60f * Time.deltaTime;
            float dRotX = 0f, dRotY = 0f;

            if (kb[Key.UpArrow].isPressed) dRotX = angularSpeed;
            if (kb[Key.DownArrow].isPressed) dRotX = -angularSpeed;
            if (kb[Key.LeftArrow].isPressed) dRotY = -angularSpeed;
            if (kb[Key.RightArrow].isPressed) dRotY = angularSpeed;
#else
            if (Input.GetKey(KeyCode.A)) dx = -speed;
            if (Input.GetKey(KeyCode.D)) dx = speed;
            if (Input.GetKey(KeyCode.W)) dz = speed;
            if (Input.GetKey(KeyCode.S)) dz = -speed;
            if (Input.GetKey(KeyCode.R)) dy = speed;
            if (Input.GetKey(KeyCode.F)) dy = -speed;

            float angularSpeed = 60f * Time.deltaTime;
            float dRotX = 0f, dRotY = 0f;

            if (Input.GetKey(KeyCode.UpArrow)) dRotX = angularSpeed;
            if (Input.GetKey(KeyCode.DownArrow)) dRotX = -angularSpeed;
            if (Input.GetKey(KeyCode.LeftArrow)) dRotY = -angularSpeed;
            if (Input.GetKey(KeyCode.RightArrow)) dRotY = angularSpeed;
#endif

            if (Mathf.Abs(dx) + Mathf.Abs(dy) + Mathf.Abs(dz) > 0.001f)
            {
                trackerCam.transform.position +=
                    trackerCam.transform.right * dx +
                    trackerCam.transform.up * dy +
                    trackerCam.transform.forward * dz;
            }

            if (Mathf.Abs(dRotX) + Mathf.Abs(dRotY) > 0.001f)
            {
                trackerCam.transform.rotation *= Quaternion.Euler(dRotX, dRotY, 0f);
            }
        }

        private Vector2 GetPointerPosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null &&
                Touchscreen.current.primaryTouch.phase.ReadValue() != UnityEngine.InputSystem.TouchPhase.None)
                return Touchscreen.current.primaryTouch.position.ReadValue();
            if (Mouse.current != null)
                return Mouse.current.position.ReadValue();
            return Vector2.zero;
#else
            if (Input.touchCount > 0) return Input.GetTouch(0).position;
            return (Vector2)Input.mousePosition;
#endif
        }

        /// <summary>Current tracking confidence (0-1). Only updated in WebGL builds.</summary>
        public float TrackingConfidence => lastConfidence;

        /// <summary>Whether the tracker is currently in 3DOF fallback mode.</summary>
        public bool IsInFallbackMode => isInFallback3DOF;

        /// <summary>Current plane detection mode.</summary>
        public PlaneMode CurrentPlaneMode => planeMode;
    }
}
