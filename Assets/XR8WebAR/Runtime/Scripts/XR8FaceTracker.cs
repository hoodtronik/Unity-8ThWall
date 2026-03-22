using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System.Runtime.InteropServices;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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

        [Tooltip("Offset from the landmark position (local space)")]
        public Vector3 positionOffset;

        [Tooltip("Rotation offset from face orientation (local space)")]
        public Vector3 rotationOffset;
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
    /// - Desktop preview mode with animated face simulation
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

        [Header("Rotation")]
        [Tooltip("Attachments inherit the face rotation instead of just looking at the camera")]
        [SerializeField] private bool inheritFaceRotation = true;

        [Tooltip("Smoothing factor for attachment movement (higher = snappier)")]
        [SerializeField][Range(1f, 30f)] private float smoothingFactor = 15f;

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
                if (!face.landmarks.ContainsKey(attachment.point)) continue;

                Vector3 worldPos = face.landmarks[attachment.point];

                // Apply position offset in face-local space
                if (attachment.positionOffset != Vector3.zero)
                {
                    worldPos += face.rotation * attachment.positionOffset;
                }

                // Smooth position
                attachment.transform.position = Vector3.Lerp(
                    attachment.transform.position, worldPos,
                    Time.deltaTime * smoothingFactor);

                // Rotation: inherit face orientation or face camera
                if (inheritFaceRotation)
                {
                    Quaternion targetRot = face.rotation * Quaternion.Euler(attachment.rotationOffset);
                    attachment.transform.rotation = Quaternion.Slerp(
                        attachment.transform.rotation, targetRot,
                        Time.deltaTime * smoothingFactor);
                }
                else
                {
                    if (Camera.main != null)
                    {
                        attachment.transform.LookAt(Camera.main.transform);
                    }
                }
            }
        }

        // =====================================================================
        // Desktop Preview — Simulated face in the editor
        // =====================================================================
#if UNITY_EDITOR
        [Header("Desktop Preview")]
        [Tooltip("Enable simulated face tracking in editor")]
        [SerializeField] private bool enableDesktopPreview = false;

        private float previewTime = 0f;
        private bool previewFaceActive = false;

        /// <summary>Start editor preview — called from XR8Manager or manually.</summary>
        public void StartDesktopPreview()
        {
            if (!enableDesktopPreview) return;

            Debug.Log("[XR8FaceTracker] ===== Face Desktop Preview =====");
            Debug.Log("  [F] Toggle face found/lost");
            Debug.Log("  [Mouse] Move face position");
            Debug.Log("  [1-5] Expression presets (neutral/smile/surprise/wink/talk)");
            Debug.Log("==========================================");

            isTracking = true;

            // Auto-find face after short delay
            StartCoroutine(PreviewAutoStart());
        }

        private IEnumerator PreviewAutoStart()
        {
            yield return new WaitForSeconds(0.3f);
            PreviewFaceFound();
        }

        private void PreviewFaceFound()
        {
            previewFaceActive = true;
            OnFaceTrackingFound("0");
        }

        private void PreviewFaceLost()
        {
            previewFaceActive = false;
            OnFaceTrackingLost("0");
        }

        private void Update()
        {
            if (!enableDesktopPreview || !isTracking) return;

            PreviewHandleInput();

            if (previewFaceActive)
            {
                PreviewUpdateFace();
            }
        }

        private void PreviewHandleInput()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb[Key.F].wasPressedThisFrame)
            {
                if (previewFaceActive) PreviewFaceLost();
                else PreviewFaceFound();
            }

            // Expression presets
            if (kb[Key.Digit1].wasPressedThisFrame) SetPreviewExpression(0.0f, 1.0f, 1.0f, false); // Neutral
            if (kb[Key.Digit2].wasPressedThisFrame) SetPreviewExpression(0.1f, 0.8f, 0.8f, true);  // Smile
            if (kb[Key.Digit3].wasPressedThisFrame) SetPreviewExpression(0.8f, 0.9f, 0.9f, false); // Surprise
            if (kb[Key.Digit4].wasPressedThisFrame) SetPreviewExpression(0.0f, 0.0f, 1.0f, true);  // Wink
            if (kb[Key.Digit5].wasPressedThisFrame) SetPreviewExpression(0.6f, 0.7f, 0.7f, false); // Talk
#else
            if (Input.GetKeyDown(KeyCode.F))
            {
                if (previewFaceActive) PreviewFaceLost();
                else PreviewFaceFound();
            }

            // Expression presets
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetPreviewExpression(0.0f, 1.0f, 1.0f, false); // Neutral
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetPreviewExpression(0.1f, 0.8f, 0.8f, true);  // Smile
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetPreviewExpression(0.8f, 0.9f, 0.9f, false); // Surprise
            if (Input.GetKeyDown(KeyCode.Alpha4)) SetPreviewExpression(0.0f, 0.0f, 1.0f, true);  // Wink
            if (Input.GetKeyDown(KeyCode.Alpha5)) SetPreviewExpression(0.6f, 0.7f, 0.7f, false); // Talk
#endif
        }

        private float previewMouth = 0f;
        private float previewLeftEye = 1f;
        private float previewRightEye = 1f;
        private bool previewSmile = false;

        private void SetPreviewExpression(float mouth, float leftEye, float rightEye, bool smile)
        {
            previewMouth = mouth;
            previewLeftEye = leftEye;
            previewRightEye = rightEye;
            previewSmile = smile;
            Debug.Log("[XR8FaceTracker] Preview expression: mouth=" + mouth.ToString("F1") + 
                " eyes=" + leftEye.ToString("F1") + "/" + rightEye.ToString("F1") + 
                " smile=" + smile);
        }

        private void PreviewUpdateFace()
        {
            previewTime += Time.deltaTime;

            // Face position: in front of camera with subtle sway
            var cam = Camera.main;
            if (cam == null) return;

            float swayX = Mathf.Sin(previewTime * 0.5f) * 0.05f;
            float swayY = Mathf.Cos(previewTime * 0.7f) * 0.03f;
            
            // Mouse drag adds offset
            Vector2 mouseOffset = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                var delta = mouse.delta.ReadValue();
                mouseOffset.x = delta.x * 0.005f;
                mouseOffset.y = delta.y * 0.005f;
            }
#else
            if (Input.GetMouseButton(1)) // Right click to drag face
            {
                mouseOffset.x = Input.GetAxis("Mouse X") * 0.1f;
                mouseOffset.y = Input.GetAxis("Mouse Y") * 0.1f;
            }
#endif

            var facePos = cam.transform.position + cam.transform.forward * 0.5f
                + cam.transform.right * (swayX + mouseOffset.x)
                + cam.transform.up * (swayY + mouseOffset.y);

            var faceRot = Quaternion.LookRotation(-cam.transform.forward, Vector3.up);

            // Animate subtle mouth movement for "talking" preset
            float animatedMouth = previewMouth;
            if (previewMouth > 0.3f)
            {
                animatedMouth = previewMouth + Mathf.Sin(previewTime * 8f) * 0.2f;
                animatedMouth = Mathf.Clamp01(animatedMouth);
            }

            // Build pose CSV
            string poseCsv = "0," +
                facePos.x.ToString("F6") + "," + facePos.y.ToString("F6") + "," + facePos.z.ToString("F6") + "," +
                faceRot.x.ToString("F6") + "," + faceRot.y.ToString("F6") + "," + faceRot.z.ToString("F6") + "," + faceRot.w.ToString("F6") + "," +
                animatedMouth.ToString("F4") + "," + previewLeftEye.ToString("F4") + "," + previewRightEye.ToString("F4") + "," +
                (previewSmile ? "1" : "0");

            OnFacePose(poseCsv);

            // Build landmark positions relative to face
            var landmarkOffsets = GetLandmarkOffsets();
            string landmarkCsv = "";
            bool first = true;
            foreach (var kvp in landmarkOffsets)
            {
                Vector3 worldPos = facePos + faceRot * kvp.Value;
                string part = kvp.Key + "," + worldPos.x.ToString("F6") + "," + worldPos.y.ToString("F6") + "," + worldPos.z.ToString("F6");
                if (first)
                {
                    part = "0," + part;
                    first = false;
                }
                landmarkCsv += (landmarkCsv.Length > 0 ? "|" : "") + part;
            }

            if (landmarkCsv.Length > 0)
            {
                OnFaceLandmarks(landmarkCsv);
            }
        }

        /// <summary>Default face landmark offsets from face center (approximate).</summary>
        private Dictionary<string, Vector3> GetLandmarkOffsets()
        {
            return new Dictionary<string, Vector3>
            {
                { "Forehead",      new Vector3( 0.00f,  0.08f,  0.02f) },
                { "NoseTip",       new Vector3( 0.00f, -0.01f,  0.05f) },
                { "NoseBridge",    new Vector3( 0.00f,  0.03f,  0.04f) },
                { "LeftEye",       new Vector3(-0.03f,  0.03f,  0.02f) },
                { "RightEye",      new Vector3( 0.03f,  0.03f,  0.02f) },
                { "LeftEar",       new Vector3(-0.08f,  0.01f, -0.02f) },
                { "RightEar",      new Vector3( 0.08f,  0.01f, -0.02f) },
                { "LeftCheek",     new Vector3(-0.05f, -0.01f,  0.02f) },
                { "RightCheek",    new Vector3( 0.05f, -0.01f,  0.02f) },
                { "Chin",          new Vector3( 0.00f, -0.07f,  0.01f) },
                { "MouthCenter",   new Vector3( 0.00f, -0.04f,  0.03f) },
                { "UpperLip",      new Vector3( 0.00f, -0.03f,  0.04f) },
                { "LowerLip",      new Vector3( 0.00f, -0.05f,  0.03f) },
                { "LeftEyebrow",   new Vector3(-0.03f,  0.05f,  0.03f) },
                { "RightEyebrow",  new Vector3( 0.03f,  0.05f,  0.03f) }
            };
        }
#endif

        private void OnDestroy()
        {
            if (isTracking) StopTracking();
        }
    }
}
