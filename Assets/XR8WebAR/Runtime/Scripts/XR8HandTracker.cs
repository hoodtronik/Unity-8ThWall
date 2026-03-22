using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;

namespace XR8WebAR
{
    /// <summary>
    /// XR8HandTracker — Real-time hand tracking for WebAR.
    ///
    /// Uses 8th Wall's hand tracking module to detect hand landmarks (21 points
    /// per hand) directly in the browser. Provides hand position, gesture detection,
    /// and optional visual debug rendering.
    ///
    /// Supports common gestures: Pinch, Point, Open Palm, Fist, Thumbs Up.
    ///
    /// Setup:
    ///   1. Add XR8HandTracker to a root-level GameObject named "XR8HandTracker"
    ///   2. Assign the AR Camera
    ///   3. Optionally assign a prefab for landmark visualization
    ///   4. Build WebGL — hand tracking runs in the browser via 8th Wall
    ///
    /// Must be on a root-level GameObject to receive SendMessage calls from JS.
    /// </summary>
    public class XR8HandTracker : MonoBehaviour
    {
        // ━━━ JS Interop ━━━
        [DllImport("__Internal")] private static extern void WebGLStartHandTracking(string configJson);
        [DllImport("__Internal")] private static extern void WebGLStopHandTracking();

        // ━━━ Configuration ━━━
        [Header("Camera")]
        [SerializeField] private Camera arCamera;

        [Header("Hand Tracking Settings")]
        [Tooltip("Maximum number of hands to track (1 or 2)")]
        [SerializeField][Range(1, 2)] private int maxHands = 1;

        [Tooltip("Minimum detection confidence (0-1)")]
        [SerializeField][Range(0f, 1f)] private float minConfidence = 0.7f;

        [Header("Gesture Detection")]
        [Tooltip("Enable gesture recognition (pinch, point, open palm, etc.)")]
        [SerializeField] private bool enableGestures = true;

        [Tooltip("Pinch threshold — distance between thumb tip and index tip (normalized)")]
        [SerializeField][Range(0f, 0.15f)] private float pinchThreshold = 0.05f;

        [Header("Debug Visualization")]
        [Tooltip("Show hand landmark spheres in the scene")]
        [SerializeField] private bool showLandmarks = false;

        [Tooltip("Prefab to instantiate for each landmark point")]
        [SerializeField] private GameObject landmarkPrefab;

        [Tooltip("Landmark sphere size")]
        [SerializeField] private float landmarkSize = 0.008f;

        [Tooltip("Color for landmark spheres")]
        [SerializeField] private Color landmarkColor = Color.cyan;

        [Header("Events")]
        [SerializeField] public UnityEvent<int> OnHandFound;          // handIndex
        [SerializeField] public UnityEvent<int> OnHandLost;           // handIndex
        [SerializeField] public UnityEvent<int, Vector3> OnPinch;     // handIndex, worldPos
        [SerializeField] public UnityEvent<int> OnPinchRelease;       // handIndex
        [SerializeField] public UnityEvent<int, string> OnGesture;    // handIndex, gestureName
        [SerializeField] public UnityEvent<int, Vector3[]> OnHandLandmarks; // handIndex, 21 points

        // ━━━ Hand Landmark Indices (MediaPipe standard) ━━━
        public static class Landmark
        {
            public const int Wrist = 0;
            public const int ThumbCMC = 1, ThumbMCP = 2, ThumbIP = 3, ThumbTip = 4;
            public const int IndexMCP = 5, IndexPIP = 6, IndexDIP = 7, IndexTip = 8;
            public const int MiddleMCP = 9, MiddlePIP = 10, MiddleDIP = 11, MiddleTip = 12;
            public const int RingMCP = 13, RingPIP = 14, RingDIP = 15, RingTip = 16;
            public const int PinkyMCP = 17, PinkyPIP = 18, PinkyDIP = 19, PinkyTip = 20;
            public const int Count = 21;
        }

        // ━━━ Internal State ━━━
        private Vector3[][] handLandmarks;   // [handIndex][landmarkIndex]
        private bool[] handTracked;
        private string[] currentGesture;
        private bool[] isPinching;
        private float[] handConfidence;

        // Debug visualization
        private GameObject[][] landmarkObjects;

        // ━━━ Public API ━━━

        /// <summary>Whether the specified hand is currently tracked.</summary>
        public bool IsHandTracked(int handIndex = 0) =>
            handIndex >= 0 && handIndex < maxHands && handTracked[handIndex];

        /// <summary>Get all 21 landmark positions for a hand (world space).</summary>
        public Vector3[] GetLandmarks(int handIndex = 0) =>
            handIndex >= 0 && handIndex < maxHands ? handLandmarks[handIndex] : null;

        /// <summary>Get a specific landmark position (world space).</summary>
        public Vector3 GetLandmark(int landmarkIndex, int handIndex = 0)
        {
            if (handIndex < 0 || handIndex >= maxHands) return Vector3.zero;
            if (landmarkIndex < 0 || landmarkIndex >= Landmark.Count) return Vector3.zero;
            return handLandmarks[handIndex][landmarkIndex];
        }

        /// <summary>Whether the specified hand is pinching.</summary>
        public bool IsPinching(int handIndex = 0) =>
            handIndex >= 0 && handIndex < maxHands && isPinching[handIndex];

        /// <summary>Current gesture name for the specified hand.</summary>
        public string GetGesture(int handIndex = 0) =>
            handIndex >= 0 && handIndex < maxHands ? currentGesture[handIndex] : "none";

        /// <summary>Tracking confidence for the specified hand (0-1).</summary>
        public float GetConfidence(int handIndex = 0) =>
            handIndex >= 0 && handIndex < maxHands ? handConfidence[handIndex] : 0f;

        // ━━━ Lifecycle ━━━

        private void Awake()
        {
            if (arCamera == null)
                arCamera = Camera.main;

            // Initialize arrays
            handLandmarks = new Vector3[maxHands][];
            handTracked = new bool[maxHands];
            currentGesture = new string[maxHands];
            isPinching = new bool[maxHands];
            handConfidence = new float[maxHands];

            for (int i = 0; i < maxHands; i++)
            {
                handLandmarks[i] = new Vector3[Landmark.Count];
                currentGesture[i] = "none";
            }

            if (showLandmarks)
                CreateLandmarkVisuals();
        }

        private void Start()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            InitializeHandTracking();
#else
            Debug.Log("[XR8HandTracker] Hand tracking requires WebGL build with 8th Wall.");
#endif
        }

        private void OnDestroy()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLStopHandTracking();
#endif
            DestroyLandmarkVisuals();
        }

        // ━━━ Initialization ━━━

        private void InitializeHandTracking()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string config = "{" +
                "\"unityObjectName\":\"" + gameObject.name + "\"," +
                "\"maxHands\":" + maxHands + "," +
                "\"minConfidence\":" + minConfidence.ToString(CultureInfo.InvariantCulture) +
            "}";

            WebGLStartHandTracking(config);
            Debug.Log("[XR8HandTracker] Initialized — maxHands=" + maxHands);
#endif
        }

        // ━━━ SendMessage callbacks from JS ━━━

        /// <summary>
        /// Receives hand landmark data from JS bridge.
        /// CSV format: handIndex,confidence,x0,y0,z0,x1,y1,z1,...,x20,y20,z20
        /// (1 + 1 + 21*3 = 65 values)
        /// </summary>
        void OnHandData(string csv)
        {
            var parts = csv.Split(',');
            if (parts.Length < 65) return;

            int handIndex = int.Parse(parts[0]);
            if (handIndex < 0 || handIndex >= maxHands) return;

            float confidence = float.Parse(parts[1], CultureInfo.InvariantCulture);
            handConfidence[handIndex] = confidence;

            if (confidence < minConfidence)
            {
                if (handTracked[handIndex])
                {
                    handTracked[handIndex] = false;
                    OnHandLost?.Invoke(handIndex);
                }
                return;
            }

            bool wasTracked = handTracked[handIndex];
            handTracked[handIndex] = true;

            // Parse 21 landmark positions
            for (int i = 0; i < Landmark.Count; i++)
            {
                int offset = 2 + i * 3;
                handLandmarks[handIndex][i] = new Vector3(
                    float.Parse(parts[offset], CultureInfo.InvariantCulture),
                    float.Parse(parts[offset + 1], CultureInfo.InvariantCulture),
                    -float.Parse(parts[offset + 2], CultureInfo.InvariantCulture) // XR8 Z → Unity -Z
                );
            }

            if (!wasTracked)
                OnHandFound?.Invoke(handIndex);

            OnHandLandmarks?.Invoke(handIndex, handLandmarks[handIndex]);

            // Gesture detection
            if (enableGestures)
                DetectGestures(handIndex);

            // Update debug visuals
            if (showLandmarks)
                UpdateLandmarkVisuals(handIndex);
        }

        /// <summary>Called when a hand is lost.</summary>
        void OnHandLostFromJS(string handIndexStr)
        {
            int handIndex = int.Parse(handIndexStr);
            if (handIndex < 0 || handIndex >= maxHands) return;

            if (handTracked[handIndex])
            {
                handTracked[handIndex] = false;
                isPinching[handIndex] = false;
                currentGesture[handIndex] = "none";
                OnHandLost?.Invoke(handIndex);
            }

            if (showLandmarks)
                HideLandmarkVisuals(handIndex);
        }

        // ━━━ Gesture Detection ━━━

        private void DetectGestures(int handIndex)
        {
            var lm = handLandmarks[handIndex];

            // Pinch detection (thumb tip ↔ index tip distance)
            float pinchDist = Vector3.Distance(lm[Landmark.ThumbTip], lm[Landmark.IndexTip]);
            float handSize = Vector3.Distance(lm[Landmark.Wrist], lm[Landmark.MiddleMCP]);
            float normalizedPinch = handSize > 0.001f ? pinchDist / handSize : 1f;

            bool wasPinching = isPinching[handIndex];
            isPinching[handIndex] = normalizedPinch < pinchThreshold;

            if (isPinching[handIndex] && !wasPinching)
            {
                Vector3 pinchPos = (lm[Landmark.ThumbTip] + lm[Landmark.IndexTip]) * 0.5f;
                OnPinch?.Invoke(handIndex, pinchPos);
            }
            else if (!isPinching[handIndex] && wasPinching)
            {
                OnPinchRelease?.Invoke(handIndex);
            }

            // Simple gesture classification
            string gesture = ClassifyGesture(lm, handSize);
            if (gesture != currentGesture[handIndex])
            {
                currentGesture[handIndex] = gesture;
                OnGesture?.Invoke(handIndex, gesture);
            }
        }

        private string ClassifyGesture(Vector3[] lm, float handSize)
        {
            if (handSize < 0.001f) return "none";

            // Finger extension check (is fingertip above MCP joint?)
            bool indexExtended = lm[Landmark.IndexTip].y > lm[Landmark.IndexMCP].y;
            bool middleExtended = lm[Landmark.MiddleTip].y > lm[Landmark.MiddleMCP].y;
            bool ringExtended = lm[Landmark.RingTip].y > lm[Landmark.RingMCP].y;
            bool pinkyExtended = lm[Landmark.PinkyTip].y > lm[Landmark.PinkyMCP].y;
            bool thumbExtended = Vector3.Distance(lm[Landmark.ThumbTip], lm[Landmark.Wrist]) >
                                 Vector3.Distance(lm[Landmark.ThumbMCP], lm[Landmark.Wrist]) * 1.2f;

            int extendedCount = (indexExtended ? 1 : 0) + (middleExtended ? 1 : 0) +
                                (ringExtended ? 1 : 0) + (pinkyExtended ? 1 : 0);

            // Open palm — all fingers extended
            if (extendedCount >= 4 && thumbExtended)
                return "open_palm";

            // Fist — no fingers extended
            if (extendedCount == 0 && !thumbExtended)
                return "fist";

            // Point — only index extended
            if (indexExtended && !middleExtended && !ringExtended && !pinkyExtended)
                return "point";

            // Peace / V sign — index + middle extended
            if (indexExtended && middleExtended && !ringExtended && !pinkyExtended)
                return "peace";

            // Thumbs up — only thumb extended
            if (thumbExtended && extendedCount == 0)
                return "thumbs_up";

            return "unknown";
        }

        // ━━━ Debug Landmark Visuals ━━━

        private void CreateLandmarkVisuals()
        {
            landmarkObjects = new GameObject[maxHands][];
            for (int h = 0; h < maxHands; h++)
            {
                landmarkObjects[h] = new GameObject[Landmark.Count];
                for (int i = 0; i < Landmark.Count; i++)
                {
                    GameObject lm;
                    if (landmarkPrefab != null)
                    {
                        lm = Instantiate(landmarkPrefab, transform);
                    }
                    else
                    {
                        lm = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        lm.transform.SetParent(transform);
                        lm.transform.localScale = Vector3.one * landmarkSize;

                        var col = lm.GetComponent<Collider>();
                        if (col != null) Destroy(col);

                        var r = lm.GetComponent<MeshRenderer>();
                        r.material = new Material(Shader.Find("Unlit/Color"));
                        r.material.color = landmarkColor;
                    }

                    lm.name = "Hand" + h + "_LM" + i;
                    lm.SetActive(false);
                    landmarkObjects[h][i] = lm;
                }
            }
        }

        private void UpdateLandmarkVisuals(int handIndex)
        {
            if (landmarkObjects == null || handIndex >= landmarkObjects.Length) return;

            for (int i = 0; i < Landmark.Count; i++)
            {
                if (landmarkObjects[handIndex][i] != null)
                {
                    landmarkObjects[handIndex][i].SetActive(true);
                    landmarkObjects[handIndex][i].transform.position = handLandmarks[handIndex][i];
                }
            }
        }

        private void HideLandmarkVisuals(int handIndex)
        {
            if (landmarkObjects == null || handIndex >= landmarkObjects.Length) return;

            for (int i = 0; i < Landmark.Count; i++)
            {
                if (landmarkObjects[handIndex][i] != null)
                    landmarkObjects[handIndex][i].SetActive(false);
            }
        }

        private void DestroyLandmarkVisuals()
        {
            if (landmarkObjects == null) return;
            for (int h = 0; h < landmarkObjects.Length; h++)
            {
                if (landmarkObjects[h] == null) continue;
                for (int i = 0; i < landmarkObjects[h].Length; i++)
                {
                    if (landmarkObjects[h][i] != null) Destroy(landmarkObjects[h][i]);
                }
            }
        }
    }
}
