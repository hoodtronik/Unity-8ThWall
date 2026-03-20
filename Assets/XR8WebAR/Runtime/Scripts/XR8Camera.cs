using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using System.Runtime.InteropServices;

#if IMAGINE_URP || XR8_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace XR8WebAR
{
    /// <summary>
    /// XR8Camera — 8th Wall AR camera bridge for Unity WebGL.
    /// Attach to the Camera entity. Manages camera lifecycle, video
    /// background, FOV updates, and orientation changes.
    ///
    /// Replaces Imagine.WebAR.ARCamera with clean 8th Wall integration.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class XR8Camera : MonoBehaviour
    {
        // --- JS Interop (defined in XR8CameraLib.jslib) ---
        [DllImport("__Internal")] private static extern void WebGLStartXR8();
        [DllImport("__Internal")] private static extern void WebGLStopXR8();
        [DllImport("__Internal")] private static extern bool WebGLIsXR8Started();
        [DllImport("__Internal")] private static extern void WebGLPauseXR8Camera();
        [DllImport("__Internal")] private static extern void WebGLUnpauseXR8Camera();
        [DllImport("__Internal")] private static extern float WebGLGetXR8CameraFov();
        [DllImport("__Internal")] private static extern string WebGLGetXR8VideoDims();
        [DllImport("__Internal")] private static extern void WebGLSubscribeXR8VideoTexturePtr(int textureId);

        // --- Public settings ---
        public enum VideoPlaneMode { NONE, TEXTURE_PTR }

        [SerializeField] public VideoPlaneMode videoPlaneMode = VideoPlaneMode.TEXTURE_PTR;
        [SerializeField] private Material videoPlaneMat;
        [SerializeField] private float videoDistance = 100;

        [SerializeField] public UnityEvent<Vector2> OnResized;

        [Space]
        [SerializeField] private bool unpausePauseOnEnableDisable = false;
        [SerializeField] private bool pauseOnDestroy = false;

        [SerializeField] private bool pauseOnApplicationLostFocus = false;
        [SerializeField][Range(0, 1000)] private int resizeDelay = 50;

        [SerializeField] public UnityEvent<bool> OnCameraImageFlipped;
        [HideInInspector] public bool isFlipped = false;

        public enum ARCameraOrientation { PORTRAIT, LANDSCAPE }
        [SerializeField] public UnityEvent<ARCameraOrientation> OnCameraOrientationChanged;
        [HideInInspector] public ARCameraOrientation orientation;

        // --- Private state ---
        [HideInInspector] public Camera cam;
        private GameObject videoBackground;
        private Texture2D videoTexture;
        private int videoTextureId;
        private bool paused = false;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        private IEnumerator Start()
        {
            OnCameraImageFlipped?.Invoke(isFlipped);
            OnCameraOrientationChanged?.Invoke(
                Screen.height > Screen.width ? ARCameraOrientation.PORTRAIT : ARCameraOrientation.LANDSCAPE
            );

#if IMAGINE_URP || XR8_URP
            // URP support — configure camera for AR transparency
            if (GraphicsSettings.currentRenderPipeline != null &&
                GraphicsSettings.defaultRenderPipeline.GetType().ToString().EndsWith("UniversalRenderPipelineAsset") &&
                videoPlaneMode == VideoPlaneMode.NONE)
            {
                Debug.Log("[XR8Camera] URP detected");
                cam.clearFlags = CameraClearFlags.Depth;
                cam.allowHDR = false;
                var camData = GetComponent<UniversalAdditionalCameraData>();
                camData.renderPostProcessing = false;
            }
#endif

            // Start XR8 engine
            StartCamera();
            yield break;
        }

        private void OnEnable()
        {
            if (unpausePauseOnEnableDisable) UnpauseCamera();
        }

        private void OnDisable()
        {
            if (unpausePauseOnEnableDisable) PauseCamera();
        }

        private void OnDestroy()
        {
            if (pauseOnDestroy) PauseCamera();
        }

        void StartCamera()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Debug.Log("[XR8Camera] Starting XR8 engine...");
            WebGLStartXR8();
#endif
        }

        // --- Called from JS via SendMessage ---

        void OnStartWebcamSuccess()
        {
            Debug.Log("[XR8Camera] XR8 camera started successfully");
            SetVideoDims();
        }

        void OnStartWebcamFail()
        {
            Debug.LogError("[XR8Camera] XR8 camera failed to start!");
        }

        void SetCameraFov(float fov)
        {
            cam.fieldOfView = fov;
            Debug.Log("[XR8Camera] FOV set to " + cam.fieldOfView);
        }

        public void PauseCamera()
        {
            if (paused) return;
            Debug.Log("[XR8Camera] Pausing...");
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLPauseXR8Camera();
#endif
            paused = true;
        }

        public void UnpauseCamera()
        {
            if (!paused) return;
            Debug.Log("[XR8Camera] Unpausing...");
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLUnpauseXR8Camera();
#endif
            paused = false;
        }

        public void Resize(string dims)
        {
            var vals = dims.Split(new string[] { "," }, System.StringSplitOptions.RemoveEmptyEntries);
            var width = int.Parse(vals[0]);
            var height = int.Parse(vals[1]);

            Debug.Log("[XR8Camera] Video dimensions: " + width + " x " + height);
            OnResized?.Invoke(new Vector2(width, height));

            if (videoPlaneMode == VideoPlaneMode.NONE) return;

            if (videoBackground != null)
                Destroy(videoBackground);

            CreateVideoPlane(width, height);

            if (videoTexture != null)
                Destroy(videoTexture);

            videoTexture = new Texture2D(width, height);
            videoPlaneMat.mainTexture = videoTexture;
            videoTextureId = (int)videoTexture.GetNativeTexturePtr();

            Debug.Log("[XR8Camera] WebGL texture pointer: " + videoTextureId);
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLSubscribeXR8VideoTexturePtr(videoTextureId);
#endif
        }

        void CreateVideoPlane(int width, int height)
        {
            Debug.Log("[XR8Camera] Creating video background plane");

            videoBackground = GameObject.CreatePrimitive(PrimitiveType.Quad);
            videoBackground.name = "VideoBackground";
            videoBackground.transform.parent = transform;

            videoPlaneMat.mainTexture = null;
            videoBackground.GetComponent<Renderer>().material = videoPlaneMat;

            var ar = (float)Screen.width / (float)Screen.height;
            var v_ar = (float)width / (float)height;
            float heightScale;

            if (v_ar > ar)
            {
                heightScale = 2 * videoDistance * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad / 2);
            }
            else
            {
                var heightRatio = ar / v_ar;
                heightScale = 2 * videoDistance * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad / 2) * heightRatio;
            }

            var widthScale = heightScale * v_ar * (isFlipped ? -1 : 1);

            videoBackground.transform.localScale = new Vector3(widthScale, heightScale, 1);
            videoBackground.transform.localPosition = new Vector3(0, 0, videoDistance);
            videoBackground.transform.localEulerAngles = Vector3.zero;
        }

        void SetVideoDims()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Resize(WebGLGetXR8VideoDims());
#endif
        }

        void SetFlippedMessage(string message)
        {
            Debug.Log("[XR8Camera] Flipped: " + message);
            isFlipped = message == "true";
            OnCameraImageFlipped?.Invoke(isFlipped);

            if (videoBackground != null)
            {
                var newScale = videoBackground.transform.localScale;
                newScale.x = Mathf.Abs(newScale.x) * (isFlipped ? -1 : 1);
                videoBackground.transform.localScale = newScale;
            }
        }

        void SetOrientationMessage(string message)
        {
            Debug.Log("[XR8Camera] Orientation: " + message);
            orientation = message == "PORTRAIT" ? ARCameraOrientation.PORTRAIT : ARCameraOrientation.LANDSCAPE;
            OnCameraOrientationChanged?.Invoke(orientation);
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (pauseOnApplicationLostFocus)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                if (WebGLIsXR8Started())
                {
                    if (hasFocus) UnpauseCamera();
                    else PauseCamera();
                }
#endif
            }
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseOnApplicationLostFocus)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                if (WebGLIsXR8Started())
                {
                    if (!pauseStatus) UnpauseCamera();
                    else PauseCamera();
                }
#endif
            }
        }
    }
}
