using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace XR8WebAR
{
    /// <summary>
    /// Two-finger pinch to scale an object.
    /// Drop this on any GameObject to enable pinch scaling.
    /// Supports both Legacy and New Input Systems.
    /// </summary>
    public class XR8PinchToScale : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The transform to scale. If null, uses this transform.")]
        [SerializeField] private Transform scaleTarget;

        [Header("Limits")]
        [SerializeField] private float minScale = 0.1f;
        [SerializeField] private float maxScale = 5f;

        private Vector3 originalScale;
        private Vector3 startScale;
        private float startPinchDistance;
        private bool isPinching = false;
        private Vector2 touch0Start, touch1Start;

        private void Awake()
        {
            if (scaleTarget == null) scaleTarget = transform;
            originalScale = scaleTarget.localScale;
#if ENABLE_LEGACY_INPUT_MANAGER
            Input.multiTouchEnabled = true;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private void Update()
        {
            if (Touchscreen.current == null) { isPinching = false; return; }

            int activeTouches = 0;
            for (int i = 0; i < Touchscreen.current.touches.Count; i++)
            {
                var tp = Touchscreen.current.touches[i].phase.ReadValue();
                if (tp == UnityEngine.InputSystem.TouchPhase.Began ||
                    tp == UnityEngine.InputSystem.TouchPhase.Moved ||
                    tp == UnityEngine.InputSystem.TouchPhase.Stationary)
                    activeTouches++;
            }
            if (activeTouches < 2) { isPinching = false; return; }

            var t0 = Touchscreen.current.touches[0];
            var t1 = Touchscreen.current.touches[1];

            if (t0.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began ||
                t1.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
            {
                touch0Start = t0.position.ReadValue();
                touch1Start = t1.position.ReadValue();
                startPinchDistance = Vector2.Distance(touch0Start, touch1Start);
                startScale = scaleTarget.localScale;
                isPinching = true;
            }
            else if (t0.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Ended ||
                     t1.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Ended ||
                     t0.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled ||
                     t1.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                isPinching = false;
            }

            if (isPinching && startPinchDistance > 0.01f)
            {
                float currentDist = Vector2.Distance(t0.position.ReadValue(), t1.position.ReadValue());
                ApplyScale(currentDist / startPinchDistance);
            }
        }
#else
        private void Update()
        {
            if (Input.touchCount < 2)
            {
                isPinching = false;
                return;
            }

            var touch0 = Input.GetTouch(0);
            var touch1 = Input.GetTouch(1);

            if (touch0.phase == TouchPhase.Began || touch1.phase == TouchPhase.Began)
            {
                startPinchDistance = Vector2.Distance(touch0.position, touch1.position);
                startScale = scaleTarget.localScale;
                isPinching = true;
            }
            else if (touch0.phase == TouchPhase.Ended || touch1.phase == TouchPhase.Ended ||
                     touch0.phase == TouchPhase.Canceled || touch1.phase == TouchPhase.Canceled)
            {
                isPinching = false;
            }

            if (isPinching && startPinchDistance > 0.01f)
            {
                float currentDist = Vector2.Distance(touch0.position, touch1.position);
                ApplyScale(currentDist / startPinchDistance);
            }
        }
#endif

        private void ApplyScale(float scaleFactor)
        {
            Vector3 newScale = startScale * scaleFactor;
            newScale.x = Mathf.Clamp(newScale.x, originalScale.x * minScale, originalScale.x * maxScale);
            newScale.y = Mathf.Clamp(newScale.y, originalScale.y * minScale, originalScale.y * maxScale);
            newScale.z = Mathf.Clamp(newScale.z, originalScale.z * minScale, originalScale.z * maxScale);
            scaleTarget.localScale = newScale;
        }

        /// <summary>Reset scale to original.</summary>
        public void ResetScale()
        {
            scaleTarget.localScale = originalScale;
        }
    }
}
