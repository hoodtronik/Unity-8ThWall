using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace XR8WebAR
{
    /// <summary>
    /// XR8WorldTracker — SLAM-based world tracking for surface detection and object placement.
    /// 
    /// Receives 6DOF camera pose and surface events from xr8-bridge.js.
    /// Works alongside XR8ImageTracker when both are enabled in XR8Manager.
    /// 
    /// Must be on a root-level GameObject named "XR8WorldTracker" to receive SendMessage calls.
    /// </summary>
    public class XR8WorldTracker : MonoBehaviour
    {
        [DllImport("__Internal")] private static extern string WebGLXR8HitTest(float screenX, float screenY);

        [Header("Settings")]
        [SerializeField] private Camera trackerCam;
        [Tooltip("Visualize detected surfaces (debug)")]
        [SerializeField] private bool showSurfaceVisuals = false;
        [Tooltip("Prefab to instantiate at tap position")]
        [SerializeField] private GameObject placementPrefab;

        [Header("Events")]
        [SerializeField] public UnityEvent<Vector3, Vector3> OnSurfaceDetected;
        [SerializeField] public UnityEvent<Vector3> OnObjectPlaced;
        [SerializeField] public UnityEvent<Vector3, Quaternion> OnCameraPoseUpdated;

        // --- State ---
        private Dictionary<string, Vector3> activeSurfaces = new Dictionary<string, Vector3>();
        private List<GameObject> placedObjects = new List<GameObject>();

        public Dictionary<string, Vector3> ActiveSurfaces => activeSurfaces;
        public int PlacedObjectCount => placedObjects.Count;

        private void Awake()
        {
            if (trackerCam == null)
                trackerCam = Camera.main;
        }

        // --- Called from JS via SendMessage ---

        /// <summary>
        /// Receives 6DOF camera pose from XR8 SLAM.
        /// CSV format: posX,posY,posZ,rotX,rotY,rotZ,rotW
        /// </summary>
        void OnCameraPose(string csv)
        {
            var parts = csv.Split(',');
            if (parts.Length < 7) return;

            var pos = new Vector3(
                float.Parse(parts[0]),
                float.Parse(parts[1]),
                -float.Parse(parts[2]) // XR8 Z → Unity -Z
            );

            var rot = new Quaternion(
                -float.Parse(parts[3]),
                -float.Parse(parts[4]),
                float.Parse(parts[5]),
                float.Parse(parts[6])
            );

            // Update camera transform
            if (trackerCam != null)
            {
                trackerCam.transform.localPosition = pos;
                trackerCam.transform.localRotation = rot;
            }

            OnCameraPoseUpdated?.Invoke(pos, rot);
        }

        /// <summary>
        /// Surface found event from XR8.
        /// CSV format: id,posX,posY,posZ
        /// </summary>
        void OnSurfaceFound(string csv)
        {
            var parts = csv.Split(',');
            if (parts.Length < 4) return;

            string id = parts[0];
            var pos = new Vector3(
                float.Parse(parts[1]),
                float.Parse(parts[2]),
                -float.Parse(parts[3])
            );

            activeSurfaces[id] = pos;
            Debug.Log("[XR8WorldTracker] Surface found: " + id + " at " + pos);

            OnSurfaceDetected?.Invoke(pos, Vector3.up);
        }

        /// <summary>
        /// Surface updated event from XR8.
        /// </summary>
        void OnSurfaceUpdated(string csv)
        {
            var parts = csv.Split(',');
            if (parts.Length < 4) return;

            string id = parts[0];
            var pos = new Vector3(
                float.Parse(parts[1]),
                float.Parse(parts[2]),
                -float.Parse(parts[3])
            );

            activeSurfaces[id] = pos;
        }

        /// <summary>
        /// Surface lost event from XR8.
        /// </summary>
        void OnSurfaceLost(string id)
        {
            activeSurfaces.Remove(id);
            Debug.Log("[XR8WorldTracker] Surface lost: " + id);
        }

        /// <summary>
        /// Hit test result from XR8.
        /// CSV format: posX,posY,posZ,normalX,normalY,normalZ
        /// </summary>
        void OnHitTestResult(string csv)
        {
            var parts = csv.Split(',');
            if (parts.Length < 6) return;

            var pos = new Vector3(
                float.Parse(parts[0]),
                float.Parse(parts[1]),
                -float.Parse(parts[2])
            );

            var normal = new Vector3(
                float.Parse(parts[3]),
                float.Parse(parts[4]),
                -float.Parse(parts[5])
            );

            Debug.Log("[XR8WorldTracker] Hit test: " + pos);

            // Place object if prefab is assigned
            if (placementPrefab != null)
            {
                var obj = Instantiate(placementPrefab, pos, Quaternion.LookRotation(Vector3.forward, normal));
                placedObjects.Add(obj);
                OnObjectPlaced?.Invoke(pos);
            }
        }

        /// <summary>
        /// Perform a hit test at the screen center (tap-to-place).
        /// Call this from a UI button or input handler.
        /// </summary>
        public void TapToPlace()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLXR8HitTest(0.5f, 0.5f); // Normalized screen center
#else
            Debug.Log("[XR8WorldTracker] TapToPlace only works in WebGL builds");
#endif
        }

        /// <summary>
        /// Perform a hit test at a specific screen position.
        /// </summary>
        public void HitTestAt(Vector2 normalizedScreenPos)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLXR8HitTest(normalizedScreenPos.x, normalizedScreenPos.y);
#endif
        }

        /// <summary>
        /// Clear all placed objects.
        /// </summary>
        public void ClearPlacedObjects()
        {
            foreach (var obj in placedObjects)
            {
                if (obj != null) Destroy(obj);
            }
            placedObjects.Clear();
        }
    }
}
