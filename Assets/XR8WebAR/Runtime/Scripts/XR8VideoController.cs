using UnityEngine;
using UnityEngine.Video;
using UnityEngine.Events;

namespace XR8WebAR
{
    /// <summary>
    /// Controls video playback based on image tracking events.
    /// Attach to the same GameObject that has a VideoPlayer component.
    /// Wire the XR8ImageTracker's OnImageFound/OnImageLost events to this component.
    /// </summary>
    [RequireComponent(typeof(VideoPlayer))]
    public class XR8VideoController : MonoBehaviour
    {
        [Header("Playback Settings")]
        [Tooltip("Auto-play when image is found")]
        [SerializeField] private bool autoPlayOnFound = true;

        [Tooltip("Pause (not stop) when image is lost — resumes from same position")]
        [SerializeField] private bool pauseOnLost = true;

        [Tooltip("Restart from beginning each time the image is found")]
        [SerializeField] private bool restartOnFound = false;

        [Tooltip("Mute audio until user taps (required by mobile browsers)")]
        [SerializeField] private bool startMuted = true;

        [Tooltip("Loop the video")]
        [SerializeField] private bool loop = true;

        [Header("Fade Settings")]
        [Tooltip("Fade in/out the video plane when tracking starts/stops")]
        [SerializeField] private bool useFade = true;
        [SerializeField] private float fadeSpeed = 5f;

        [Header("Events")]
        [SerializeField] public UnityEvent OnVideoStarted;
        [SerializeField] public UnityEvent OnVideoPaused;
        [SerializeField] public UnityEvent OnVideoFinished;

        // --- Private state ---
        private VideoPlayer videoPlayer;
        private Renderer meshRenderer;
        private Material material;
        private RenderTexture renderTexture;
        private float targetAlpha = 0f;
        private bool isTracking = false;
        private bool hasPlayedOnce = false;

        private void Awake()
        {
            videoPlayer = GetComponent<VideoPlayer>();
            meshRenderer = GetComponent<Renderer>();

            if (meshRenderer != null)
            {
                material = meshRenderer.material;
            }

            // Configure video player
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = loop;
            videoPlayer.renderMode = VideoRenderMode.MaterialOverride;

            if (meshRenderer != null)
            {
                videoPlayer.targetMaterialRenderer = meshRenderer;
                videoPlayer.targetMaterialProperty = "_BaseMap"; // URP main texture
            }

            // Create render texture if needed
            if (videoPlayer.renderMode == VideoRenderMode.RenderTexture && videoPlayer.targetTexture == null)
            {
                renderTexture = new RenderTexture(1920, 1080, 0);
                videoPlayer.targetTexture = renderTexture;
            }

            // Events
            videoPlayer.loopPointReached += OnLoopPointReached;
            videoPlayer.prepareCompleted += OnPrepareCompleted;

            // Start transparent
            if (useFade && material != null)
            {
                SetAlpha(0f);
            }

            // Hide initially
            if (meshRenderer != null)
                meshRenderer.enabled = false;
        }

        private void Update()
        {
            // Smooth fade
            if (useFade && material != null)
            {
                float currentAlpha = material.color.a;
                if (Mathf.Abs(currentAlpha - targetAlpha) > 0.01f)
                {
                    float newAlpha = Mathf.Lerp(currentAlpha, targetAlpha, Time.deltaTime * fadeSpeed);
                    SetAlpha(newAlpha);
                }
            }
        }

        /// <summary>
        /// Call this from XR8ImageTracker.OnImageFound event
        /// </summary>
        public void OnImageFound(string targetId)
        {
            Debug.Log("[XR8VideoController] Image found: " + targetId);
            isTracking = true;

            if (meshRenderer != null)
                meshRenderer.enabled = true;

            if (useFade)
                targetAlpha = 1f;
            else if (material != null)
                SetAlpha(1f);

            if (autoPlayOnFound)
            {
                if (restartOnFound || !hasPlayedOnce)
                {
                    videoPlayer.time = 0;
                    videoPlayer.Prepare();
                }
                else
                {
                    videoPlayer.Play();
                }

                hasPlayedOnce = true;
            }
        }

        /// <summary>
        /// Call this from XR8ImageTracker.OnImageLost event
        /// </summary>
        public void OnImageLost(string targetId)
        {
            Debug.Log("[XR8VideoController] Image lost: " + targetId);
            isTracking = false;

            if (pauseOnLost)
            {
                videoPlayer.Pause();
                OnVideoPaused?.Invoke();
            }

            if (useFade)
                targetAlpha = 0f;
            else
            {
                if (material != null) SetAlpha(0f);
                if (meshRenderer != null) meshRenderer.enabled = false;
            }
        }

        /// <summary>
        /// Unmute the video (call from a UI button tap — required by mobile browsers)
        /// </summary>
        public void Unmute()
        {
            videoPlayer.SetDirectAudioMute(0, false);
        }

        // --- Private helpers ---

        private void OnPrepareCompleted(VideoPlayer source)
        {
            if (startMuted)
                source.SetDirectAudioMute(0, true);

            source.Play();
            OnVideoStarted?.Invoke();
        }

        private void OnLoopPointReached(VideoPlayer source)
        {
            if (!loop)
            {
                OnVideoFinished?.Invoke();
            }
        }

        private void SetAlpha(float alpha)
        {
            if (material == null) return;
            var color = material.color;
            color.a = alpha;
            material.color = color;
        }

        private void OnDestroy()
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }

            if (videoPlayer != null)
            {
                videoPlayer.loopPointReached -= OnLoopPointReached;
                videoPlayer.prepareCompleted -= OnPrepareCompleted;
            }
        }
    }
}
