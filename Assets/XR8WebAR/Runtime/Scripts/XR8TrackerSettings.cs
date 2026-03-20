using UnityEngine;

namespace XR8WebAR
{
    /// <summary>
    /// Tracker settings for XR8 image tracking.
    /// 
    /// 8th Wall handles low-level CV tuning internally (frame resolution,
    /// feature point count, detect intervals, etc.) — unlike Imagine WebAR
    /// which exposed those raw knobs.
    /// 
    /// Instead, we provide TrackingQuality presets that control client-side
    /// post-processing (smoothing, interpolation) of the raw pose data
    /// that 8th Wall sends us.
    /// </summary>
    [System.Serializable]
    public class XR8TrackerSettings
    {
        // === Quality Preset ===
        public enum TrackingQuality
        {
            [Tooltip("Raw poses, no smoothing. Best for fast-moving targets or lowest latency.")]
            Performance,
            [Tooltip("Moderate smoothing. Good balance of stability and responsiveness.")]
            Balanced,
            [Tooltip("Heavy smoothing. Best for stationary/slow targets, buttery smooth.")]
            Quality
        }

        [Tooltip("Tracking quality preset — controls client-side pose smoothing")]
        [SerializeField] public TrackingQuality trackingQuality = TrackingQuality.Balanced;

        // === Core Settings ===
        [Tooltip("Maximum number of images to track simultaneously")]
        [SerializeField] public int maxSimultaneousTargets = 1;

        public enum FrameRate { FR_30_FPS = 30, FR_60FPS = -1 }
        [SerializeField] public FrameRate targetFrameRate = FrameRate.FR_30_FPS;

        [Tooltip("Disable world tracking for image-target-only mode (better performance)")]
        [SerializeField] public bool disableWorldTracking = true;

        // === Smoothing (auto-set by quality preset, or manual override) ===
        [Space]
        [Tooltip("Override quality preset with manual smoothing values")]
        [SerializeField] public bool manualSmoothing = false;

        [Tooltip("Enable position/rotation interpolation")]
        [SerializeField] public bool useExtraSmoothing = true;

        [Tooltip("Position interpolation speed (higher = snappier, lower = smoother)")]
        [SerializeField][Range(1f, 30f)] public float smoothenFactor = 10f;

        [Tooltip("Rotation interpolation speed (higher = snappier)")]
        [SerializeField][Range(1f, 30f)] public float rotationSmoothing = 12f;

        // === Debug ===
        [Tooltip("Enable in-editor debug controls (WASD + arrows)")]
        [Space][SerializeField] public bool debugMode = false;

        /// <summary>
        /// Apply the quality preset values (called automatically if manualSmoothing is false).
        /// </summary>
        public void ApplyQualityPreset()
        {
            if (manualSmoothing) return;

            switch (trackingQuality)
            {
                case TrackingQuality.Performance:
                    useExtraSmoothing = false;
                    smoothenFactor = 25f;
                    rotationSmoothing = 25f;
                    break;

                case TrackingQuality.Balanced:
                    useExtraSmoothing = true;
                    smoothenFactor = 10f;
                    rotationSmoothing = 12f;
                    break;

                case TrackingQuality.Quality:
                    useExtraSmoothing = true;
                    smoothenFactor = 5f;
                    rotationSmoothing = 6f;
                    break;
            }
        }

        public string Serialize()
        {
            var json = "{";
            json += "\"MAX_SIMULTANEOUS_TRACK\":" + maxSimultaneousTargets + ",";
            json += "\"FRAMERATE\":" + (int)targetFrameRate + ",";
            json += "\"DISABLE_WORLD_TRACKING\":" + (disableWorldTracking ? "true" : "false");
            json += "}";
            return json;
        }
    }
}
