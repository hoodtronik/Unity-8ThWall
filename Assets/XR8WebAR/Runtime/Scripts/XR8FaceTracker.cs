using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System.Runtime.InteropServices;

namespace XR8WebAR
{
    /// <summary>
    /// Face attachment point — maps a named face landmark to a Transform.
    /// </summary>
    [System.Serializable]
    public class XR8FaceAttachment
    {
        public enum AttachmentPoint
        {
            Forehead,
            NoseTip,
            NoseBridge,
            LeftEye,
            RightEye,
            LeftEar,
            RightEar,
            LeftCheek,
            RightCheek,
            Chin,
            MouthCenter,
            UpperLip,
            LowerLip,
            LeftEyebrow,
            RightEyebrow
        }

        public AttachmentPoint point;
        public Transform transform;
    }

    /// <summary>
    /// XR8FaceTracker — 8th Wall face tracking for Unity WebGL.
    /// 
    /// Features:
    /// - Face detection events (found/lost)
    /// - Face mesh data (vertices, indices for face mesh rendering)
    /// - Named attachment points (nose, eyes, ears, mouth, etc.)
    /// - Head pose tracking (position + rotation)
    /// - Eye/mouth open state
    /// 
    /// Receives data from xr8-bridge.js via SendMessage.
    /// </summary>
    public class XR8FaceTracker : MonoBehaviour
    {
        // --- JS Interop ---
        [DllImport("__Internal")] private static extern void XR8Face_StartTracking(string configJson, string objName);
        [DllImport("__Internal")] private static extern void XR8Face_StopTracking();

        // --- Configuration ---
        [Header("Face Tracking")]
        [Tooltip("Max faces to track simultaneously (1-3)")]
        [SerializeField][Range(1, 3)] private int maxFaces = 1;

        [Tooltip("Enable face mesh data (vertices/indices)")]
        [SerializeField] private bool enableFaceMesh = false;

        [Tooltip("Enable eye/mouth states (open, closed, etc.)")]
        [SerializeField] private bool enableExpressions = true;

        [Header("Attachment Points")]
        [SerializeField] private List<XR8FaceAttachment> attachments = new List<XR8FaceAttachment>();

        [Header("Events")]
        [SerializeField] public UnityEvent<int> OnFaceFound;
        [SerializeField] public UnityEvent<int> OnFaceLost;
        [SerializeField] public UnityEvent<FaceData> OnFaceUpdated;

        // --- Runtime State ---
        private Dictionary<int, FaceData> activeFaces = new Dictionary<int, FaceData>();
        private bool isTracking = false;

        /// <summary>Face data for a single tracked face.</summary>
        [System.Serializable]
        public class FaceData
        {
            public int faceId;
            public Vector3 position;
            public Quaternion rotation;
            public float mouthOpenness;   // 0 = closed, 1 = fully open
            public float leftEyeOpenness; // 0 = closed, 1 = fully open
            public float rightEyeOpenness;
            public bool isSmiling;

            // Attachment points (world positions)
            public Dictionary<XR8FaceAttachment.AttachmentPoint, Vector3> landmarks 
                = new Dictionary<XR8FaceAttachment.AttachmentPoint, Vector3>();
        }

        public int TrackedFaceCount => activeFaces.Count;
        public bool IsTracking => isTracking;

        public FaceData GetFaceData(int faceId = 0)
        {
            return activeFaces.ContainsKey(faceId) ? activeFaces[faceId] : null;
        }

        private void Start()
        {
#if !UNITY_EDITOR && UNITY_WEBGL
            // Auto-start if not managed by XR8Manager
            if (XR8Manager.Instance == null || !XR8Manager.Instance.FaceTrackingEnabled)
            {
                StartTracking();
            }
#endif
        }

        public void StartTracking()
        {
            if (isTracking) return;

            string config = "{" +
                "\"maxFaces\":" + maxFaces + "," +
                "\"enableMesh\":" + (enableFaceMesh ? "true" : "false") + "," +
                "\"enableExpressions\":" + (enableExpressions ? "true" : "false") +
                "}";

            Debug.Log("[XR8FaceTracker] Starting face tracking: " + config);

#if !UNITY_EDITOR && UNITY_WEBGL
            XR8Face_StartTracking(config, gameObject.name);
#endif
            isTracking = true;
        }

        public void StopTracking()
        {
            if (!isTracking) return;
            Debug.Log("[XR8FaceTracker] Stopping face tracking");
#if !UNITY_EDITOR && UNITY_WEBGL
            XR8Face_StopTracking();
#endif
            isTracking = false;
        }

        // =====================================================================
        // Called from JS via SendMessage
        // =====================================================================

        /// <summary>Called when a face is detected. Format: "faceId"</summary>
        void OnFaceTrackingFound(string data)
        {
            int faceId = int.Parse(data);
            Debug.Log("[XR8FaceTracker] Face found: " + faceId);

            if (!activeFaces.ContainsKey(faceId))
            {
                activeFaces[faceId] = new FaceData { faceId = faceId };
            }

            OnFaceFound?.Invoke(faceId);
        }

        /// <summary>Called when a face is lost. Format: "faceId"</summary>
        void OnFaceTrackingLost(string data)
        {
            int faceId = int.Parse(data);
            Debug.Log("[XR8FaceTracker] Face lost: " + faceId);

            activeFaces.Remove(faceId);
            OnFaceLost?.Invoke(faceId);
        }

        /// <summary>
        /// Called every frame with face pose data.
        /// Format: "faceId,px,py,pz,rx,ry,rz,rw,mouthOpen,leftEyeOpen,rightEyeOpen,isSmiling"
        /// </summary>
        void OnFacePose(string data)
        {
            string[] v = data.Split(',');
            if (v.Length < 12) return;

            var ci = System.Globalization.CultureInfo.InvariantCulture;
            int faceId = int.Parse(v[0]);

            if (!activeFaces.ContainsKey(faceId))
            {
                activeFaces[faceId] = new FaceData { faceId = faceId };
            }

            var face = activeFaces[faceId];
            face.position = new Vector3(
                float.Parse(v[1], ci), float.Parse(v[2], ci), float.Parse(v[3], ci));
            face.rotation = new Quaternion(
                float.Parse(v[4], ci), float.Parse(v[5], ci),
                float.Parse(v[6], ci), float.Parse(v[7], ci));
            face.mouthOpenness = float.Parse(v[8], ci);
            face.leftEyeOpenness = float.Parse(v[9], ci);
            face.rightEyeOpenness = float.Parse(v[10], ci);
            face.isSmiling = v[11] == "1";

            OnFaceUpdated?.Invoke(face);
        }

        /// <summary>
        /// Called with attachment point positions.
        /// Format: "faceId,pointName,px,py,pz|pointName,px,py,pz|..."
        /// </summary>
        void OnFaceLandmarks(string data)
        {
            string[] parts = data.Split('|');
            if (parts.Length < 1) return;

            // First part has the faceId
            string[] header = parts[0].Split(',');
            int faceId = int.Parse(header[0]);

            if (!activeFaces.ContainsKey(faceId)) return;
            var face = activeFaces[faceId];

            var ci = System.Globalization.CultureInfo.InvariantCulture;

            // Parse each landmark: "pointName,px,py,pz"
            for (int i = 0; i < parts.Length; i++)
            {
                string[] lm = parts[i].Split(',');
                // First part has faceId prepended, others don't
                int offset = (i == 0) ? 1 : 0;
                if (lm.Length < offset + 4) continue;

                string pointName = lm[offset];
                var pos = new Vector3(
                    float.Parse(lm[offset + 1], ci),
                    float.Parse(lm[offset + 2], ci),
                    float.Parse(lm[offset + 3], ci));

                // Try to map to enum
                if (System.Enum.TryParse<XR8FaceAttachment.AttachmentPoint>(pointName, true, out var point))
                {
                    face.landmarks[point] = pos;
                }
            }

            // Update attachment transforms
            UpdateAttachmentTransforms(faceId);
        }

        private void UpdateAttachmentTransforms(int faceId)
        {
            if (!activeFaces.ContainsKey(faceId)) return;
            var face = activeFaces[faceId];

            foreach (var attachment in attachments)
            {
                if (attachment.transform == null) continue;
                if (face.landmarks.ContainsKey(attachment.point))
                {
                    attachment.transform.position = face.landmarks[attachment.point];
                    // Orient attachment to face the camera
                    if (Camera.main != null)
                    {
                        attachment.transform.LookAt(Camera.main.transform);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (isTracking) StopTracking();
        }
    }
}
