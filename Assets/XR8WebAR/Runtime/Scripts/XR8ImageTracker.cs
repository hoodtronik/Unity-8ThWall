using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System.Runtime.InteropServices;

namespace XR8WebAR
{
    /// <summary>
    /// Serializable image target definition.
    /// Set the ID and assign a Transform that will be positioned at the tracked image.
    /// 
    /// The 'anchor' transform is what the tracker drives (position/rotation/scale).
    /// Child objects under the anchor keep their own local transforms, so you can
    /// freely rotate, offset, or scale your content.
    /// 
    /// If 'anchor' is null, the tracker will auto-create a wrapper anchor at runtime
    /// and re-parent the content under it.
    /// </summary>
    [System.Serializable]
    public class XR8ImageTarget
    {
        public string id;

        [Tooltip("The anchor transform that the tracker will drive. " +
            "Your content should be a CHILD of this anchor so it keeps its local offsets.")]
        public Transform anchor;

        [Tooltip("The content root (used for activate/deactivate on found/lost). " +
            "If anchor is null, this is also what gets driven directly (legacy mode).")]
        public Transform transform;

        [HideInInspector] public Vector3 targetPos;
        [HideInInspector] public Quaternion targetRot;

        /// <summary>The transform the tracker actually moves each frame.</summary>
        public Transform TrackedTransform => anchor != null ? anchor : transform;
    }

    /// <summary>
    /// XR8ImageTracker — 8th Wall image target tracker for Unity WebGL.
    /// Attach to a root-level GameObject. Receives tracking data from
    /// xr8-bridge.js via SendMessage and updates target transforms.
    ///
    /// Replaces Imagine.WebAR.ImageTracker with clean 8th Wall integration.
    /// </summary>
    public class XR8ImageTracker : MonoBehaviour
    {
        // --- JS Interop (defined in XR8TrackerLib.jslib) ---
        [DllImport("__Internal")] private static extern void StartXR8ImageTracker(string ids, string name);
        [DllImport("__Internal")] private static extern void StopXR8ImageTracker();
        [DllImport("__Internal")] private static extern bool IsXR8TrackerReady();
        [DllImport("__Internal")] private static extern void SetXR8TrackerSettings(string settings);

        // --- Configuration ---
        [SerializeField] private XR8Camera trackerCam;
        [SerializeField] private List<XR8ImageTarget> imageTargets;
        private Dictionary<string, XR8ImageTarget> targets = new Dictionary<string, XR8ImageTarget>();

        /// <summary>Returns the configured image target IDs.</summary>
        public List<string> GetTargetIds()
        {
            var ids = new List<string>();
            if (imageTargets != null)
                foreach (var t in imageTargets)
                    if (!string.IsNullOrEmpty(t.id)) ids.Add(t.id);
            return ids;
        }

        /// <summary>Returns the XR8ImageTarget for the given ID, or null if not found.</summary>
        public XR8ImageTarget GetTarget(string id)
        {
            if (targets.ContainsKey(id)) return targets[id];
            // Fallback to serialized list (before Start() populates dictionary)
            if (imageTargets != null)
                foreach (var t in imageTargets)
                    if (t.id == id) return t;
            return null;
        }

        private enum TrackerOrigin { CAMERA_ORIGIN, FIRST_TARGET_ORIGIN }
        [SerializeField] private TrackerOrigin trackerOrigin;
        [SerializeField] private List<string> trackedIds = new List<string>();
        private string serializedIds = "";

        [SerializeField] private XR8TrackerSettings trackerSettings;
        [SerializeField] private bool dontDeactivateOnLost = false;

        [SerializeField] private UnityEvent<string> OnImageFound, OnImageLost;

        [SerializeField][Range(1f, 5f)] private float debugCamMoveSensitivity = 2f;
        [SerializeField][Range(10f, 50f)] private float debugCamTiltSensitivity = 30f;

        // --- Private state ---
        private Vector3 firstTargetFinalPos, firstTargetCurrentPos;
        private Quaternion firstTargetFinalRot, firstTargetCurrentRot;
        private Transform dummyCamTransform;
        private int debugImageTargetIndex = 0;
        private bool isTrackerStopped = false;
        private bool isReady = false;

        /// <summary>True after Start() has initialized the targets dictionary.</summary>
        public bool IsReady => isReady;

        [Space][SerializeField] private bool startStopOnEnableDisable = false;
        [SerializeField] private bool stopOnDestroy = true;

        private Vector3 forward, up, right, pos;
        private Vector3 flippedScale = new Vector3(-1, 1, 1);
        private Quaternion rot;

        IEnumerator Start()
        {
            if (transform.parent != null)
            {
                Debug.LogError("[XR8ImageTracker] Must be a root transform to receive JS messages");
            }

            if (trackerCam == null)
            {
                trackerCam = FindFirstObjectByType<XR8Camera>();
            }

            foreach (var target in imageTargets)
            {
                if (string.IsNullOrEmpty(target.id) || target.transform == null)
                {
                    Debug.LogWarning("[XR8ImageTracker] Skipping target with empty ID or null transform");
                    continue;
                }

                // Auto-create anchor wrapper if not assigned
                if (target.anchor == null)
                {
                    var anchorGO = new GameObject(target.id + "_Anchor");
                    anchorGO.transform.SetParent(this.transform, false);
                    target.anchor = anchorGO.transform;

                    // Reparent the content under the anchor, preserving local transform
                    target.transform.SetParent(target.anchor, true);

                    Debug.Log("[XR8ImageTracker] Auto-created anchor for '" + target.id + 
                        "'. Content '" + target.transform.name + "' is now a child — " +
                        "its local rotation/position/scale are preserved.");
                }

                targets.Add(target.id, target);
                var renderer = target.transform.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.enabled = false;
                // Deactivate the anchor (which hides all children)
                target.anchor.gameObject.SetActive(false);

                serializedIds += target.id;
                if (target != imageTargets[imageTargets.Count - 1])
                {
                    serializedIds += ",";
                }
            }

            Debug.Log("[XR8ImageTracker] Target IDs: " + serializedIds);

            Application.targetFrameRate = (int)trackerSettings.targetFrameRate;
            trackerSettings.ApplyQualityPreset();

#if !UNITY_EDITOR && UNITY_WEBGL
            StartXR8ImageTracker(serializedIds, name);
            Debug.Log("[XR8ImageTracker] Settings: " + trackerSettings.Serialize());
            SetXR8TrackerSettings(trackerSettings.Serialize());
#endif

            if (trackerOrigin == TrackerOrigin.FIRST_TARGET_ORIGIN && trackerSettings.maxSimultaneousTargets > 1)
            {
                dummyCamTransform = (new GameObject("XR8 Dummy Cam Transform")).transform;
            }

            isReady = true;
            Debug.Log("[XR8ImageTracker] Tracker started (" + targets.Count + " targets ready)");
            yield break;
        }

        private void OnEnable()
        {
            if (startStopOnEnableDisable) StartTracker();
        }

        private void OnDisable()
        {
            if (startStopOnEnableDisable) StopTracker();
        }

        private void OnDestroy()
        {
            if (stopOnDestroy) StopTracker();
        }

        public void StartTracker()
        {
            if (!isTrackerStopped) return;
            Debug.Log("[XR8ImageTracker] Starting...");
#if !UNITY_EDITOR && UNITY_WEBGL
            if (IsXR8TrackerReady())
            {
                StartXR8ImageTracker(serializedIds, name);
            }
#endif
            isTrackerStopped = false;
        }

        public void StopTracker()
        {
            if (isTrackerStopped) return;
            Debug.Log("[XR8ImageTracker] Stopping...");
#if !UNITY_EDITOR && UNITY_WEBGL
            if (IsXR8TrackerReady())
            {
                StopXR8ImageTracker();
            }
#endif
            isTrackerStopped = true;
        }

        // --- Called from JS via SendMessage ---

        void OnTrackingFound(string id)
        {
            if (!targets.ContainsKey(id)) return;

            // Activate the anchor (which shows all children including content)
            targets[id].anchor.gameObject.SetActive(true);
            targets[id].transform.gameObject.SetActive(true);

            if (!trackedIds.Contains(id))
                trackedIds.Add(id);
            else
                Debug.LogError("[XR8ImageTracker] Found already-tracked: " + id);

            OnImageFound?.Invoke(id);
        }

        void OnTrackingLost(string id)
        {
            if (!targets.ContainsKey(id)) return;

            // Deactivate the anchor (hides all content children)
            if (!dontDeactivateOnLost)
                targets[id].anchor.gameObject.SetActive(false);
            targets[id].transform.gameObject.SetActive(dontDeactivateOnLost);

            var index = trackedIds.FindIndex(t => t == id);
            if (index > -1)
                trackedIds.RemoveAt(index);
            else
                Debug.LogError("[XR8ImageTracker] Lost untracked: " + id);

            OnImageLost?.Invoke(id);
        }

        void OnTrack(string data)
        {
            ParseData(data);
        }

        void ParseData(string data)
        {
            string[] values = data.Split(new char[] { ',' });

            string id = values[0];
            if (!targets.ContainsKey(id)) return;

            forward.x = float.Parse(values[4], System.Globalization.CultureInfo.InvariantCulture);
            forward.y = float.Parse(values[5], System.Globalization.CultureInfo.InvariantCulture);
            forward.z = float.Parse(values[6], System.Globalization.CultureInfo.InvariantCulture);

            up.x = float.Parse(values[7], System.Globalization.CultureInfo.InvariantCulture);
            up.y = float.Parse(values[8], System.Globalization.CultureInfo.InvariantCulture);
            up.z = float.Parse(values[9], System.Globalization.CultureInfo.InvariantCulture);

            right.x = float.Parse(values[10], System.Globalization.CultureInfo.InvariantCulture);
            right.y = float.Parse(values[11], System.Globalization.CultureInfo.InvariantCulture);
            right.z = float.Parse(values[12], System.Globalization.CultureInfo.InvariantCulture);

            rot = Quaternion.LookRotation(forward, up);

            pos.x = float.Parse(values[1], System.Globalization.CultureInfo.InvariantCulture);
            pos.y = float.Parse(values[2], System.Globalization.CultureInfo.InvariantCulture);
            pos.z = float.Parse(values[3], System.Globalization.CultureInfo.InvariantCulture);

            // Drive the anchor transform, NOT the content transform directly.
            // Content keeps its own local rotation/position/scale under the anchor.
            var trackedXform = targets[id].TrackedTransform;

            if (trackerCam.isFlipped)
            {
                rot.eulerAngles = new Vector3(rot.eulerAngles.x, rot.eulerAngles.y * -1, rot.eulerAngles.z * -1);
                pos.x *= -1;
                trackedXform.localScale = flippedScale;
            }
            else
            {
                trackedXform.localScale = Vector3.one;
            }

            if (trackerOrigin == TrackerOrigin.CAMERA_ORIGIN)
            {
                if (!trackerSettings.useExtraSmoothing)
                {
                    trackedXform.position = trackerCam.transform.TransformPoint(pos);
                    trackedXform.rotation = trackerCam.transform.rotation * rot;
                }
                else
                {
                    targets[id].targetPos = trackerCam.transform.TransformPoint(pos);
                    targets[id].targetRot = trackerCam.transform.rotation * rot;
                }
            }
            else if (trackerOrigin == TrackerOrigin.FIRST_TARGET_ORIGIN)
            {
                if (trackedIds[0] == id)
                {
                    trackedXform.position = Vector3.zero;
                    trackedXform.rotation = Quaternion.identity;

                    if (!trackerSettings.useExtraSmoothing)
                    {
                        trackerCam.transform.position = Quaternion.Inverse(rot) * -pos;
                        trackerCam.transform.rotation = Quaternion.Inverse(rot);
                    }
                    else
                    {
                        firstTargetFinalPos = pos;
                        firstTargetFinalRot = rot;
                        dummyCamTransform.position = Quaternion.Inverse(rot) * -pos;
                        dummyCamTransform.rotation = Quaternion.Inverse(rot);
                    }
                }
                else
                {
                    if (!trackerSettings.useExtraSmoothing)
                    {
                        trackedXform.position = trackerCam.transform.TransformPoint(pos);
                        trackedXform.rotation = trackerCam.transform.rotation * rot;
                    }
                    else
                    {
                        targets[id].targetPos = dummyCamTransform.TransformPoint(pos);
                        targets[id].targetRot = dummyCamTransform.rotation * rot;
                    }
                }
            }
        }

        private void Update()
        {
            if (trackerSettings.useExtraSmoothing)
            {
                foreach (var target in imageTargets)
                {
                    var trackedXform = target.TrackedTransform;
                    if (trackedXform != null && trackedXform.gameObject.activeSelf)
                    {
                        trackedXform.position = Vector3.Lerp(
                            trackedXform.position, target.targetPos,
                            Time.deltaTime * trackerSettings.smoothenFactor);
                        trackedXform.rotation = Quaternion.Slerp(
                            trackedXform.rotation, target.targetRot,
                            Time.deltaTime * trackerSettings.rotationSmoothing);
                    }
                }

                if (trackerOrigin == TrackerOrigin.FIRST_TARGET_ORIGIN)
                {
                    firstTargetCurrentPos = Vector3.Lerp(
                        firstTargetCurrentPos, firstTargetFinalPos,
                        Time.deltaTime * trackerSettings.smoothenFactor);
                    firstTargetCurrentRot = Quaternion.Slerp(
                        firstTargetCurrentRot, firstTargetFinalRot,
                        Time.deltaTime * trackerSettings.rotationSmoothing);
                    trackerCam.transform.position = Quaternion.Inverse(firstTargetCurrentRot) * -firstTargetCurrentPos;
                    trackerCam.transform.rotation = Quaternion.Inverse(firstTargetCurrentRot);
                }
            }

#if UNITY_EDITOR
            Update_Debug();
#endif
        }

        private void Update_Debug()
        {
            // Debug camera controls have been moved to XR8Manager's Desktop Preview mode.
            // Enable "Desktop Preview" on the XR8Manager component to simulate tracking
            // in the editor without needing a phone or camera.
            //
            // If you need direct WASD camera controls here, add "Unity.InputSystem" 
            // (GUID:75469ad4d38634e559750d17036d5f7c) to XR8WebAR.Runtime.asmdef references
            // and uncomment the Keyboard.current code below.
        }
    }
}
