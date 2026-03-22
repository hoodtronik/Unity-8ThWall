using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace XR8WebAR
{
    /// <summary>
    /// Single-finger swipe to rotate an object around the Y axis.
    /// Drop this on any GameObject to enable swipe rotation.
    /// Supports both Legacy and New Input Systems.
    /// </summary>
    public class XR8SwipeToRotate : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The transform to rotate. If null, uses this transform.")]
        [SerializeField] private Transform rotateTarget;

        [Header("Settings")]
        [SerializeField] private float sensitivity = 0.25f;
        [Tooltip("Invert rotation direction")]
        [SerializeField] private bool invertDirection = false;
        [Tooltip("Ignore swipe when touching UI elements")]
        [SerializeField] private bool ignoreOverUI = true;

        private Vector2 startDragPos;
        private Quaternion startRot;
        private Quaternion originalRotation;
        private bool isDragging = false;

        private void Awake()
        {
            if (rotateTarget == null) rotateTarget = transform;
            originalRotation = rotateTarget.rotation;
        }

#if ENABLE_INPUT_SYSTEM
        private void Update()
        {
            // Multi-touch guard
            if (Touchscreen.current != null)
            {
                int activeTouches = 0;
                for (int i = 0; i < Touchscreen.current.touches.Count; i++)
                {
                    var tp = Touchscreen.current.touches[i].phase.ReadValue();
                    if (tp == UnityEngine.InputSystem.TouchPhase.Began ||
                        tp == UnityEngine.InputSystem.TouchPhase.Moved ||
                        tp == UnityEngine.InputSystem.TouchPhase.Stationary)
                        activeTouches++;
                }
                if (activeTouches > 1) { isDragging = false; return; }
            }

            if (ignoreOverUI && IsPointerOverUI()) return;

            bool pressed = (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                           (Touchscreen.current != null && Touchscreen.current.primaryTouch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began);
            bool released = (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame) ||
                            (Touchscreen.current != null && Touchscreen.current.primaryTouch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Ended);

            if (pressed)
            {
                startDragPos = GetPointerPosition();
                startRot = rotateTarget.rotation;
                isDragging = true;
            }
            else if (released)
            {
                isDragging = false;
            }
            else if (isDragging)
            {
                Vector2 currentPos = GetPointerPosition();
                float deltaX = currentPos.x - startDragPos.x;
                float angle = deltaX * sensitivity * (invertDirection ? 1f : -1f);
                rotateTarget.rotation = startRot * Quaternion.AngleAxis(angle, Vector3.up);
            }
        }
#else
        private void Update()
        {
            if (Input.touchCount > 1)
            {
                isDragging = false;
                return;
            }

            if (ignoreOverUI && IsPointerOverUI()) return;

            if (Input.GetMouseButtonDown(0))
            {
                startDragPos = GetPointerPosition();
                startRot = rotateTarget.rotation;
                isDragging = true;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
            }
            else if (isDragging)
            {
                Vector2 currentPos = GetPointerPosition();
                float deltaX = currentPos.x - startDragPos.x;
                float angle = deltaX * sensitivity * (invertDirection ? 1f : -1f);
                rotateTarget.rotation = startRot * Quaternion.AngleAxis(angle, Vector3.up);
            }
        }
#endif

        private Vector2 GetPointerPosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null &&
                Touchscreen.current.primaryTouch.phase.ReadValue() != UnityEngine.InputSystem.TouchPhase.None)
                return Touchscreen.current.primaryTouch.position.ReadValue();
            if (Mouse.current != null)
                return Mouse.current.position.ReadValue();
            return Vector2.zero;
#else
            if (Input.touchCount > 0) return Input.GetTouch(0).position;
            return (Vector2)Input.mousePosition;
#endif
        }

        private bool IsPointerOverUI()
        {
            var eventSystem = EventSystem.current;
            return eventSystem != null && eventSystem.IsPointerOverGameObject();
        }

        /// <summary>Reset rotation to the original rotation from Awake.</summary>
        public void ResetRotation()
        {
            rotateTarget.rotation = originalRotation;
        }
    }
}
