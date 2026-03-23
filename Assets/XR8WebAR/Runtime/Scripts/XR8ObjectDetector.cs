using UnityEngine;
using System;
using System.Collections.Generic;

#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace XR8WebAR
{
    /// <summary>
    /// XR8 Object Detector — Browser-side ML object detection via TensorFlow.js.
    ///
    /// Detects real-world objects in the camera feed using COCO-SSD model
    /// running entirely in the browser (no server roundtrip).
    ///
    /// Features:
    ///   - Real-time object detection (labels + bounding boxes + confidence)
    ///   - Configurable detection interval for performance
    ///   - COCO-SSD detects 80 object categories (person, car, bottle, etc.)
    ///   - Events for detected/lost objects
    ///   - Optional debug label overlay
    ///
    /// Usage:
    ///   1. Add to any GameObject in scene
    ///   2. Subscribe to OnObjectDetected event
    ///   3. TF.js + COCO-SSD load from CDN automatically
    ///
    /// Works with: XR8ObjectDetectorLib.jslib
    /// </summary>
    public class XR8ObjectDetector : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void WebGLStartObjectDetection(string goName, float confidenceThreshold, int intervalMs);
        [DllImport("__Internal")] private static extern void WebGLStopObjectDetection();
        [DllImport("__Internal")] private static extern void WebGLSetDetectionThreshold(float threshold);
#endif

        [Serializable]
        public struct DetectionResult
        {
            public string label;
            public float confidence;
            public float x;      // Normalized 0-1 from left
            public float y;      // Normalized 0-1 from top
            public float width;  // Normalized 0-1
            public float height; // Normalized 0-1

            public Rect BoundingBox => new Rect(x, y, width, height);
        }

        [Header("Detection Settings")]
        [Tooltip("Minimum confidence threshold (0-1)")]
        [Range(0.1f, 0.95f)]
        public float confidenceThreshold = 0.5f;

        [Tooltip("Detection interval in milliseconds (higher = less CPU)")]
        [Range(100, 5000)]
        public int detectionIntervalMs = 500;

        [Tooltip("Maximum simultaneous detections")]
        [Range(1, 20)]
        public int maxDetections = 5;

        [Header("Filtering")]
        [Tooltip("Only detect these labels (empty = detect all)")]
        public List<string> labelFilter = new List<string>();

        [Header("Debug")]
        [Tooltip("Show detection labels in console")]
        public bool debugLog = false;

        // Current detections
        private List<DetectionResult> _currentDetections = new List<DetectionResult>();
        private Dictionary<string, float> _trackedLabels = new Dictionary<string, float>();

        // Events
        public event Action<DetectionResult> OnObjectDetected;
        public event Action<string> OnObjectLost;
        public event Action<List<DetectionResult>> OnDetectionsUpdated;

        /// <summary>Current active detections.</summary>
        public IReadOnlyList<DetectionResult> CurrentDetections => _currentDetections;

        /// <summary>Check if a specific label is currently detected.</summary>
        public bool IsDetected(string label)
        {
            return _trackedLabels.ContainsKey(label);
        }

        /// <summary>Get the best (highest confidence) detection for a label.</summary>
        public DetectionResult? GetDetection(string label)
        {
            for (int i = 0; i < _currentDetections.Count; i++)
            {
                if (_currentDetections[i].label == label)
                    return _currentDetections[i];
            }
            return null;
        }

        private void Start()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLStartObjectDetection(gameObject.name, confidenceThreshold, detectionIntervalMs);
#endif
        }

        private void OnDestroy()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLStopObjectDetection();
#endif
        }

        /// <summary>
        /// Update confidence threshold at runtime.
        /// </summary>
        public void SetThreshold(float threshold)
        {
            confidenceThreshold = Mathf.Clamp(threshold, 0.1f, 0.95f);
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLSetDetectionThreshold(confidenceThreshold);
#endif
        }

        // =============================================
        // SendMessage callback from JS bridge
        // =============================================

        /// <summary>
        /// Receives detection batch from JS: "label,conf,x,y,w,h|label,conf,x,y,w,h|..."
        /// Empty string means no detections.
        /// </summary>
        public void OnDetectionResults(string csv)
        {
            var previousLabels = new HashSet<string>(_trackedLabels.Keys);
            _currentDetections.Clear();
            _trackedLabels.Clear();

            if (string.IsNullOrEmpty(csv) || csv == "none")
            {
                // Fire lost events for everything that was tracked
                foreach (var label in previousLabels)
                    OnObjectLost?.Invoke(label);
                OnDetectionsUpdated?.Invoke(_currentDetections);
                return;
            }

            var detections = csv.Split('|');
            int count = 0;

            for (int i = 0; i < detections.Length && count < maxDetections; i++)
            {
                var parts = detections[i].Split(',');
                if (parts.Length < 6) continue;

                var result = new DetectionResult();
                result.label = parts[0];

                if (!float.TryParse(parts[1], out result.confidence)) continue;
                if (!float.TryParse(parts[2], out result.x)) continue;
                if (!float.TryParse(parts[3], out result.y)) continue;
                if (!float.TryParse(parts[4], out result.width)) continue;
                if (!float.TryParse(parts[5], out result.height)) continue;

                // Apply label filter
                if (labelFilter.Count > 0 && !labelFilter.Contains(result.label))
                    continue;

                _currentDetections.Add(result);
                _trackedLabels[result.label] = result.confidence;

                // Fire detected event for new labels
                if (!previousLabels.Contains(result.label))
                    OnObjectDetected?.Invoke(result);

                previousLabels.Remove(result.label);
                count++;

                if (debugLog)
                    Debug.Log($"[XR8ObjectDetector] {result.label}: {result.confidence:P0} at ({result.x:F2},{result.y:F2})");
            }

            // Fire lost events for labels no longer detected
            foreach (var label in previousLabels)
                OnObjectLost?.Invoke(label);

            OnDetectionsUpdated?.Invoke(_currentDetections);
        }
    }
}
