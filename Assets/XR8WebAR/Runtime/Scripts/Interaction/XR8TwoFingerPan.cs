using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace XR8WebAR
{
    /// <summary>
    /// Two-finger drag to pan/move an object along the camera's XY plane.
    /// Good for repositioning placed AR objects.
    /// Supports both Legacy and New Input Systems.
    /// </summary>
    public class XR8TwoFingerPan : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The transform to pan. If null, uses this transform.")]
        [SerializeField] private Transform panTarget;

        [Header("Settings")]
        [SerializeField] private float sensitivity = 0.005f;
        [Tooltip("Camera used for calculating pan direction")]
        [SerializeField] private Camera panCamera;

        private Vector2 startPanCenter;
        private Vector3 startPosition;
        private bool isPanning = false;
        private Vector3 originalPosition;

        private void Awake()
        {
            if (panTarget == null) panTarget = transform;
            if (panCamera == null) panCamera = Camera.main;
            originalPosition = panTarget.position;
#if ENABLE_LEGACY_INPUT_MANAGER
            Input.multiTouchEnabled = true;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private void Update()
        {
            if (Touchscreen.current == null) { isPanning = false; return; }

            int activeTouches = 0;
            for (int i = 0; i < Touchscreen.current.touches.Count; i++)
            {
                var tp = Touchscreen.current.touches[i].phase.ReadValue();
                if (tp == UnityEngine.InputSystem.TouchPhase.Began ||
                    tp == UnityEngine.InputSystem.TouchPhase.Moved ||
                    tp == UnityEngine.InputSystem.TouchPhase.Stationary)
                    activeTouches++;
            }
            if (activeTouches < 2) { isPanning = false; return; }

            var t0 = Touchscreen.current.touches[0];
            var t1 = Touchscreen.current.touches[1];

            if (t0.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began ||
                t1.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
            {
                startPanCenter = (t0.position.ReadValue() + t1.position.ReadValue()) / 2f;
                startPosition = panTarget.position;
                isPanning = true;
            }
            else if (t0.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Ended ||
                     t1.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Ended ||
                     t0.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled ||
                     t1.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                isPanning = false;
            }

            if (isPanning && panCamera != null)
            {
                Vector2 currentCenter = (t0.position.ReadValue() + t1.position.ReadValue()) / 2f;
                Vector2 delta = currentCenter - startPanCenter;

                Vector3 worldDelta = panCamera.transform.right * delta.x * sensitivity
                                   + panCamera.transform.up * delta.y * sensitivity;
                panTarget.position = startPosition + worldDelta;
            }
        }
#else
        private void Update()
        {
            if (Input.touchCount < 2)
            {
                isPanning = false;
                return;
            }

            var touch0 = Input.GetTouch(0);
            var touch1 = Input.GetTouch(1);

            if (touch0.phase == TouchPhase.Began || touch1.phase == TouchPhase.Began)
            {
                startPanCenter = (touch0.position + touch1.position) / 2f;
                startPosition = panTarget.position;
                isPanning = true;
            }
            else if (touch0.phase == TouchPhase.Ended || touch1.phase == TouchPhase.Ended ||
                     touch0.phase == TouchPhase.Canceled || touch1.phase == TouchPhase.Canceled)
            {
                isPanning = false;
            }

            if (isPanning && panCamera != null)
            {
                Vector2 currentCenter = (touch0.position + touch1.position) / 2f;
                Vector2 delta = currentCenter - startPanCenter;

                Vector3 worldDelta = panCamera.transform.right * delta.x * sensitivity
                                   + panCamera.transform.up * delta.y * sensitivity;
                panTarget.position = startPosition + worldDelta;
            }
        }
#endif

        /// <summary>Reset position to original.</summary>
        public void ResetPosition()
        {
            panTarget.position = originalPosition;
        }
    }
}
