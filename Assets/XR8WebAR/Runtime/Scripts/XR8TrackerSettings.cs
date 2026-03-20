using UnityEngine;

namespace XR8WebAR
{
    /// <summary>
    /// Tracker settings for XR8 image tracking.
    /// 8th Wall handles most tuning internally, so this is simpler
    /// than Imagine WebAR's TrackerSettings.
    /// </summary>
    [System.Serializable]
    public class XR8TrackerSettings
    {
        [Tooltip("Maximum number of images to track simultaneously")]
        [SerializeField] public int maxSimultaneousTargets = 1;

        public enum FrameRate { FR_30_FPS = 30, FR_60FPS = -1 }

        [SerializeField] public FrameRate targetFrameRate = FrameRate.FR_30_FPS;

        [Tooltip("Disable world tracking for image-target-only mode (better performance)")]
        [SerializeField] public bool disableWorldTracking = true;

        [Space]
        [SerializeField] public bool useExtraSmoothing = false;
        [SerializeField][Range(1f, 20)] public float smoothenFactor = 10;

        [Tooltip("Enable in-editor debug controls (WASD + arrows)")]
        [Space][SerializeField] public bool debugMode = false;

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
