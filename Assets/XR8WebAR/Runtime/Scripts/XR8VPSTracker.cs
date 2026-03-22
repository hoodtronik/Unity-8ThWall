using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;

namespace XR8WebAR
{
    /// <summary>
    /// XR8VPSTracker — Visual Positioning System for location-based WebAR.
    ///
    /// Uses 8th Wall's VPS module to anchor AR content to real-world locations
    /// (buildings, landmarks, storefronts) using visual recognition — entirely
    /// in the browser, no native app required.
    ///
    /// How it works:
    ///   1. Configure wayspot IDs in the Inspector (from 8th Wall Geospatial Browser)
    ///   2. When the user scans a VPS-activated location, the system localizes
    ///   3. AR content is placed at the exact real-world position
    ///   4. Works with the existing XR8GPSTracker for coarse → fine positioning
    ///
    /// Setup:
    ///   1. Add XR8VPSTracker to a root-level GameObject named "XR8VPSTracker"
    ///   2. Add wayspot IDs from the 8th Wall Geospatial Browser
    ///   3. Assign content GameObjects to each wayspot
    ///   4. Build WebGL — VPS runs in the browser via 8th Wall
    ///
    /// Must be on a root-level GameObject to receive SendMessage calls from JS.
    /// </summary>
    public class XR8VPSTracker : MonoBehaviour
    {
        // ━━━ JS Interop ━━━
        [DllImport("__Internal")] private static extern void WebGLStartVPS(string configJson);
        [DllImport("__Internal")] private static extern void WebGLStopVPS();

        // ━━━ Configuration ━━━
        [Header("VPS Settings")]
        [Tooltip("List of wayspot configurations to activate")]
        [SerializeField] private List<WayspotConfig> wayspots = new List<WayspotConfig>();

        [Tooltip("Auto-start VPS when the component starts")]
        [SerializeField] private bool autoStart = true;

        [Header("Camera")]
        [SerializeField] private Camera arCamera;

        [Header("GPS Fallback")]
        [Tooltip("Use GPS for coarse positioning before VPS localizes")]
        [SerializeField] private bool useGPSFallback = true;

        [Tooltip("Reference to XR8GPSTracker for coarse positioning")]
        [SerializeField] private XR8GPSTracker gpsTracker;

        [Header("Localization UI")]
        [Tooltip("Show while scanning for localization (e.g. 'Scan the area...')")]
        [SerializeField] private GameObject scanningPrompt;

        [Tooltip("Show when localized (e.g. 'Located!')")]
        [SerializeField] private GameObject localizedIndicator;

        [Header("Events")]
        [SerializeField] public UnityEvent<string> OnWayspotFound;       // wayspotId
        [SerializeField] public UnityEvent<string> OnWayspotUpdated;     // wayspotId
        [SerializeField] public UnityEvent<string> OnWayspotLost;        // wayspotId
        [SerializeField] public UnityEvent<string> OnLocalizationFailed; // reason
        [SerializeField] public UnityEvent OnVPSReady;

        // ━━━ Data Types ━━━

        [System.Serializable]
        public class WayspotConfig
        {
            [Tooltip("Wayspot ID from 8th Wall Geospatial Browser")]
            public string wayspotId;

            [Tooltip("Display name for this location")]
            public string displayName;

            [Tooltip("Content to activate when this wayspot is localized")]
            public GameObject content;

            [Tooltip("Position offset relative to the wayspot origin")]
            public Vector3 positionOffset;

            [Tooltip("Rotation offset relative to the wayspot origin")]
            public Vector3 rotationOffset;
        }

        // ━━━ Internal State ━━━
        private Dictionary<string, WayspotConfig> wayspotMap = new Dictionary<string, WayspotConfig>();
        private HashSet<string> localizedWayspots = new HashSet<string>();
        private bool isLocalized;
        private bool isScanning;
        private string activeWayspotId;

        // ━━━ Public API ━━━

        /// <summary>Whether VPS has localized to at least one wayspot.</summary>
        public bool IsLocalized => isLocalized;

        /// <summary>Whether VPS is currently scanning for localization.</summary>
        public bool IsScanning => isScanning;

        /// <summary>The currently active wayspot ID (null if not localized).</summary>
        public string ActiveWayspotId => activeWayspotId;

        /// <summary>Number of configured wayspots.</summary>
        public int WayspotCount => wayspots.Count;

        /// <summary>Start VPS scanning.</summary>
        public void StartVPS()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            InitializeVPS();
#else
            Debug.Log("[XR8VPSTracker] VPS requires WebGL build with 8th Wall.");
#endif
        }

        /// <summary>Stop VPS scanning.</summary>
        public void StopVPS()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLStopVPS();
#endif
            isScanning = false;
        }

        /// <summary>Get the config for a specific wayspot.</summary>
        public WayspotConfig GetWayspotConfig(string wayspotId) =>
            wayspotMap.ContainsKey(wayspotId) ? wayspotMap[wayspotId] : null;

        /// <summary>Manually activate content for a wayspot (e.g. for testing).</summary>
        public void SimulateLocalization(string wayspotId, Vector3 position, Quaternion rotation)
        {
            if (!wayspotMap.ContainsKey(wayspotId)) return;

            var config = wayspotMap[wayspotId];
            ActivateWayspotContent(config, position, rotation);
            isLocalized = true;
            activeWayspotId = wayspotId;
            OnWayspotFound?.Invoke(wayspotId);
        }

        // ━━━ Lifecycle ━━━

        private void Awake()
        {
            if (arCamera == null)
                arCamera = Camera.main;

            if (gpsTracker == null && useGPSFallback)
                gpsTracker = FindFirstObjectByType<XR8GPSTracker>();

            // Build wayspot lookup
            foreach (var ws in wayspots)
            {
                if (!string.IsNullOrEmpty(ws.wayspotId))
                {
                    wayspotMap[ws.wayspotId] = ws;
                    // Hide content initially
                    if (ws.content != null)
                        ws.content.SetActive(false);
                }
            }

            // Initial UI state
            if (scanningPrompt != null) scanningPrompt.SetActive(false);
            if (localizedIndicator != null) localizedIndicator.SetActive(false);
        }

        private void Start()
        {
            if (autoStart)
                StartVPS();
        }

        private void OnDestroy()
        {
            StopVPS();
        }

        // ━━━ Initialization ━━━

        private void InitializeVPS()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // Build wayspot ID list
            var idList = new List<string>();
            foreach (var ws in wayspots)
            {
                if (!string.IsNullOrEmpty(ws.wayspotId))
                    idList.Add("\"" + ws.wayspotId + "\"");
            }

            string config = "{" +
                "\"unityObjectName\":\"" + gameObject.name + "\"," +
                "\"wayspotIds\":[" + string.Join(",", idList) + "]" +
            "}";

            WebGLStartVPS(config);
            isScanning = true;

            if (scanningPrompt != null)
                scanningPrompt.SetActive(true);

            Debug.Log("[XR8VPSTracker] VPS started — scanning for " + wayspots.Count + " wayspot(s)");
#endif
        }

        // ━━━ Content Activation ━━━

        private void ActivateWayspotContent(WayspotConfig config, Vector3 position, Quaternion rotation)
        {
            if (config.content == null) return;

            config.content.SetActive(true);
            config.content.transform.position = position + config.positionOffset;
            config.content.transform.rotation = rotation * Quaternion.Euler(config.rotationOffset);

            Debug.Log("[XR8VPSTracker] Content activated: " +
                      (config.displayName ?? config.wayspotId) + " at " + position);
        }

        // ━━━ SendMessage callbacks from JS ━━━

        /// <summary>VPS pipeline is ready to localize.</summary>
        void OnVPSStarted(string _)
        {
            Debug.Log("[XR8VPSTracker] VPS ready — scanning for wayspots...");
            OnVPSReady?.Invoke();
        }

        /// <summary>
        /// Wayspot localized. CSV: wayspotId,px,py,pz,rx,ry,rz,rw
        /// </summary>
        void OnWayspotLocalized(string csv)
        {
            var parts = csv.Split(',');
            if (parts.Length < 8) return;

            string wayspotId = parts[0];

            var pos = new Vector3(
                float.Parse(parts[1], CultureInfo.InvariantCulture),
                float.Parse(parts[2], CultureInfo.InvariantCulture),
                -float.Parse(parts[3], CultureInfo.InvariantCulture)
            );

            var rot = new Quaternion(
                -float.Parse(parts[4], CultureInfo.InvariantCulture),
                -float.Parse(parts[5], CultureInfo.InvariantCulture),
                float.Parse(parts[6], CultureInfo.InvariantCulture),
                float.Parse(parts[7], CultureInfo.InvariantCulture)
            );

            isLocalized = true;
            isScanning = false;
            activeWayspotId = wayspotId;
            localizedWayspots.Add(wayspotId);

            // UI updates
            if (scanningPrompt != null) scanningPrompt.SetActive(false);
            if (localizedIndicator != null) localizedIndicator.SetActive(true);

            // Activate content
            if (wayspotMap.ContainsKey(wayspotId))
            {
                ActivateWayspotContent(wayspotMap[wayspotId], pos, rot);
            }

            Debug.Log("[XR8VPSTracker] Localized: " + wayspotId + " at " + pos);
            OnWayspotFound?.Invoke(wayspotId);
        }

        /// <summary>Wayspot pose updated (continuous tracking).</summary>
        void OnWayspotPoseUpdated(string csv)
        {
            var parts = csv.Split(',');
            if (parts.Length < 8) return;

            string wayspotId = parts[0];

            if (wayspotMap.ContainsKey(wayspotId) && wayspotMap[wayspotId].content != null)
            {
                var pos = new Vector3(
                    float.Parse(parts[1], CultureInfo.InvariantCulture),
                    float.Parse(parts[2], CultureInfo.InvariantCulture),
                    -float.Parse(parts[3], CultureInfo.InvariantCulture)
                );

                var rot = new Quaternion(
                    -float.Parse(parts[4], CultureInfo.InvariantCulture),
                    -float.Parse(parts[5], CultureInfo.InvariantCulture),
                    float.Parse(parts[6], CultureInfo.InvariantCulture),
                    float.Parse(parts[7], CultureInfo.InvariantCulture)
                );

                var config = wayspotMap[wayspotId];
                config.content.transform.position = pos + config.positionOffset;
                config.content.transform.rotation = rot * Quaternion.Euler(config.rotationOffset);
            }

            OnWayspotUpdated?.Invoke(wayspotId);
        }

        /// <summary>Wayspot lost (tracking lost).</summary>
        void OnWayspotLostFromJS(string wayspotId)
        {
            localizedWayspots.Remove(wayspotId);

            if (localizedWayspots.Count == 0)
            {
                isLocalized = false;
                activeWayspotId = null;
                if (scanningPrompt != null) scanningPrompt.SetActive(true);
                if (localizedIndicator != null) localizedIndicator.SetActive(false);
            }

            Debug.Log("[XR8VPSTracker] Wayspot lost: " + wayspotId);
            OnWayspotLost?.Invoke(wayspotId);
        }

        /// <summary>Localization failed.</summary>
        void OnLocalizationFailedFromJS(string reason)
        {
            Debug.LogWarning("[XR8VPSTracker] Localization failed: " + reason);
            OnLocalizationFailed?.Invoke(reason);
        }
    }
}
