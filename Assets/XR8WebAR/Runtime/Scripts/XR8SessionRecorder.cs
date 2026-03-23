using UnityEngine;
using System;
using System.Text;

#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace XR8WebAR
{
    /// <summary>
    /// XR8 Session Recorder — Records live AR camera poses for editor playback.
    ///
    /// Records camera position + rotation each frame to a CSV buffer,
    /// then triggers a browser download. The CSV is compatible with
    /// XR8Manager's RecordedPlayback desktop preview mode.
    ///
    /// CSV format per row: timestamp,posX,posY,posZ,rotX,rotY,rotZ,rotW
    ///
    /// Usage:
    ///   1. Add to any GameObject in the AR scene
    ///   2. Call StartRecording() / StopRecording() from UI or code
    ///   3. On stop, triggers browser download of the .csv file
    ///   4. Drop the .csv into Assets/ and use RecordedPlayback mode in XR8Manager
    ///
    /// Works with: XR8SessionRecorderLib.jslib
    /// </summary>
    public class XR8SessionRecorder : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void WebGLDownloadCSV(string filename, string csvContent);
#endif

        [Header("Recording Settings")]
        [Tooltip("Camera to record (uses main camera if not set)")]
        public Camera targetCamera;

        [Tooltip("Record every N-th frame (1 = every frame, 2 = half rate, etc.)")]
        [Range(1, 10)]
        public int frameSkip = 1;

        [Tooltip("Maximum recording duration in seconds (0 = unlimited)")]
        public float maxDuration = 300f;

        [Tooltip("Filename for the downloaded CSV")]
        public string filename = "ar_session_recording";

        [Header("Status")]
        [SerializeField] private bool isRecording = false;
        [SerializeField] private float recordingTime = 0f;
        [SerializeField] private int frameCount = 0;

        // Internal
        private StringBuilder _csvBuffer;
        private int _frameCounter;

        // Events
        public event Action OnRecordingStarted;
        public event Action<string> OnRecordingStopped; // passes filename
        public event Action<float> OnRecordingProgress; // passes elapsed time

        /// <summary>Is recording currently active?</summary>
        public bool IsRecording => isRecording;

        /// <summary>Current recording time in seconds.</summary>
        public float RecordingTime => recordingTime;

        /// <summary>Number of frames recorded so far.</summary>
        public int FrameCount => frameCount;

        private void Start()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (!isRecording || targetCamera == null) return;

            recordingTime += Time.deltaTime;

            // Check max duration
            if (maxDuration > 0 && recordingTime >= maxDuration)
            {
                StopRecording();
                return;
            }

            // Frame skip
            _frameCounter++;
            if (_frameCounter % frameSkip != 0) return;

            // Record pose
            var pos = targetCamera.transform.position;
            var rot = targetCamera.transform.rotation;

            _csvBuffer.AppendFormat("{0:F4},{1:F6},{2:F6},{3:F6},{4:F6},{5:F6},{6:F6},{7:F6}\n",
                recordingTime, pos.x, pos.y, pos.z, rot.x, rot.y, rot.z, rot.w);

            frameCount++;
            OnRecordingProgress?.Invoke(recordingTime);
        }

        /// <summary>
        /// Start recording camera poses.
        /// </summary>
        public void StartRecording()
        {
            if (isRecording) return;

            _csvBuffer = new StringBuilder(1024 * 64); // 64KB initial
            _csvBuffer.AppendLine("timestamp,posX,posY,posZ,rotX,rotY,rotZ,rotW");

            isRecording = true;
            recordingTime = 0f;
            frameCount = 0;
            _frameCounter = 0;

            OnRecordingStarted?.Invoke();
            Debug.Log("[XR8SessionRecorder] Recording started");
        }

        /// <summary>
        /// Stop recording and trigger CSV download.
        /// </summary>
        public void StopRecording()
        {
            if (!isRecording) return;
            isRecording = false;

            string csv = _csvBuffer.ToString();
            string fullFilename = $"{filename}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

            Debug.Log($"[XR8SessionRecorder] Recording stopped — {frameCount} frames, {recordingTime:F1}s");

#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLDownloadCSV(fullFilename, csv);
#else
            // In editor, save to Assets folder
            string path = System.IO.Path.Combine(Application.dataPath, fullFilename);
            System.IO.File.WriteAllText(path, csv);
            Debug.Log($"[XR8SessionRecorder] Saved to: {path}");
#endif

            OnRecordingStopped?.Invoke(fullFilename);
            _csvBuffer = null;
        }

        /// <summary>
        /// Toggle recording on/off.
        /// </summary>
        public void ToggleRecording()
        {
            if (isRecording) StopRecording();
            else StartRecording();
        }
    }
}
