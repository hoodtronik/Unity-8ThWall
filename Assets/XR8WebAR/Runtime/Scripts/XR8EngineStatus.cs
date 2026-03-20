using UnityEngine;
using UnityEngine.Events;

namespace XR8WebAR
{
    /// <summary>
    /// Manages XR8 engine lifecycle events.
    /// Provides callbacks for engine ready, camera permissions, and errors.
    /// Attach to any persistent GameObject in the scene.
    /// </summary>
    public class XR8EngineStatus : MonoBehaviour
    {
        [Header("Events")]
        [SerializeField] public UnityEvent OnEngineReady;
        [SerializeField] public UnityEvent OnCameraPermissionGranted;
        [SerializeField] public UnityEvent OnCameraPermissionDenied;
        [SerializeField] public UnityEvent<string> OnEngineError;

        [Header("UI References (optional)")]
        [Tooltip("GameObject to show while loading (disabled when ready)")]
        [SerializeField] private GameObject loadingUI;
        [Tooltip("GameObject to show on error")]
        [SerializeField] private GameObject errorUI;

        private bool isReady = false;
        public bool IsReady => isReady;

        // --- Called from JS via SendMessage ---

        void OnXR8Ready()
        {
            Debug.Log("[XR8EngineStatus] Engine is ready");
            isReady = true;

            if (loadingUI != null)
                loadingUI.SetActive(false);

            OnEngineReady?.Invoke();
        }

        void OnXR8CameraPermissionGranted()
        {
            Debug.Log("[XR8EngineStatus] Camera permission granted");
            OnCameraPermissionGranted?.Invoke();
        }

        void OnXR8CameraPermissionDenied()
        {
            Debug.LogWarning("[XR8EngineStatus] Camera permission DENIED");
            OnCameraPermissionDenied?.Invoke();
        }

        void OnXR8Error(string errorMessage)
        {
            Debug.LogError("[XR8EngineStatus] Engine error: " + errorMessage);
            isReady = false;

            if (errorUI != null)
                errorUI.SetActive(true);

            OnEngineError?.Invoke(errorMessage);
        }
    }
}
