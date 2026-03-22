using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace XR8WebAR
{
    /// <summary>
    /// Tap to reposition the AR content's anchor point.
    /// On tap, projects the viewport position onto the floor plane.
    /// Supports both Legacy and New Input Systems.
    /// </summary>
    public class XR8TapToReposition : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The placement indicator to reposition. If null, works standalone.")]
        [SerializeField] private XR8PlacementIndicator placementIndicator;
        [Tooltip("Camera for viewport calculations")]
        [SerializeField] private Camera trackerCam;

        [Header("Settings")]
        [Tooltip("Maximum finger movement in pixels that still counts as a 'tap' (not a drag)")]
        [SerializeField] private float tapThreshold = 10f;
        [Tooltip("Ignore taps over UI elements")]
        [SerializeField] private bool ignoreOverUI = true;

        [Header("Events")]
        public UnityEvent<Vector3> OnTapReposition;

        private Vector2 tapStartPos;
        private bool isTapping = false;

        private void Awake()
        {
            if (trackerCam == null) trackerCam = Camera.main;
        }

#if ENABLE_INPUT_SYSTEM
        private void Update()
        {
            if (placementIndicator != null && !placementIndicator.IsPlaced) return;
            if (ignoreOverUI && IsPointerOverUI()) return;

            bool pressed = (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                           (Touchscreen.current != null && Touchscreen.current.primaryTouch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began);
            bool released = (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame) ||
                            (Touchscreen.current != null && Touchscreen.current.primaryTouch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Ended);

            if (pressed)
            {
                tapStartPos = GetPointerPosition();
                isTapping = true;
            }
            else if (released && isTapping)
            {
                Vector2 endPos = GetPointerPosition();
                float distance = Vector2.Distance(tapStartPos, endPos);

                if (distance < tapThreshold)
                {
                    DoReposition(endPos);
                }

                isTapping = false;
            }
        }
#else
        private void Update()
        {
            if (placementIndicator != null && !placementIndicator.IsPlaced) return;
            if (ignoreOverUI && IsPointerOverUI()) return;

            if (Input.GetMouseButtonDown(0))
            {
                tapStartPos = GetPointerPosition();
                isTapping = true;
            }
            else if (Input.GetMouseButtonUp(0) && isTapping)
            {
                Vector2 endPos = GetPointerPosition();
                float distance = Vector2.Distance(tapStartPos, endPos);

                if (distance < tapThreshold)
                {
                    DoReposition(endPos);
                }

                isTapping = false;
            }
        }
#endif

        private void DoReposition(Vector2 screenPos)
        {
            if (trackerCam == null) return;

            Ray ray = trackerCam.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));
            Plane floorPlane = new Plane(Vector3.up, Vector3.zero);

            if (floorPlane.Raycast(ray, out float enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);

                if (placementIndicator != null)
                {
                    placementIndicator.ResetPlacement();
                }

                OnTapReposition?.Invoke(hitPoint);
            }
        }

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
    }
}
