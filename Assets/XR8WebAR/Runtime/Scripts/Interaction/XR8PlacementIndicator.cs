using UnityEngine;
using UnityEngine.Events;

namespace XR8WebAR
{
    /// <summary>
    /// XR8PlacementIndicator — visual reticle that follows camera raycast.
    /// User sees the indicator, taps to place content at that point.
    /// 
    /// Like Imaginary Labs' placement system but works with XR8 world tracking.
    /// Supports horizontal and vertical plane placement.
    /// </summary>
    public class XR8PlacementIndicator : MonoBehaviour
    {
        public enum PlaneMode { Horizontal, Vertical }

        [Header("Configuration")]
        [Tooltip("Which plane to project the indicator onto")]
        [SerializeField] private PlaneMode planeMode = PlaneMode.Horizontal;
        [Tooltip("Camera used for raycasting")]
        [SerializeField] private Camera trackerCam;

        [Header("Indicator")]
        [Tooltip("The visual reticle/indicator prefab or child object")]
        [SerializeField] private GameObject indicatorVisual;
        [Tooltip("Min distance from camera")]
        [SerializeField] private float minDistance = 0.5f;
        [Tooltip("Max distance from camera")]
        [SerializeField] private float maxDistance = 5f;

        [Header("Content")]
        [Tooltip("The GameObject to show/hide when placed")]
        [SerializeField] private GameObject contentRoot;
        [Tooltip("If true, content starts hidden and appears on placement")]
        [SerializeField] private bool hideContentUntilPlaced = true;

        [Header("Events")]
        public UnityEvent OnIndicatorShown;
        public UnityEvent OnIndicatorHidden;
        public UnityEvent<Vector3> OnContentPlaced;
        public UnityEvent OnContentReset;

        /// <summary>Whether content has been placed.</summary>
        public bool IsPlaced { get; private set; }

        /// <summary>Current indicator world position.</summary>
        public Vector3 IndicatorPosition => indicatorVisual != null
            ? indicatorVisual.transform.position
            : transform.position;

        private void Awake()
        {
            if (trackerCam == null) trackerCam = Camera.main;
        }

        private void Start()
        {
            if (hideContentUntilPlaced && contentRoot != null)
                contentRoot.SetActive(false);
        }

        private void Update()
        {
            if (IsPlaced) return;
            UpdateIndicator();
        }

        /// <summary>
        /// Raycast from camera forward onto the floor/wall plane to position the indicator.
        /// </summary>
        private void UpdateIndicator()
        {
            if (trackerCam == null || indicatorVisual == null) return;

            Plane targetPlane;
            Vector3 planeOrigin = trackerCam.transform.position;

            if (planeMode == PlaneMode.Horizontal)
            {
                planeOrigin.y = 0f;
                targetPlane = new Plane(Vector3.up, planeOrigin);
                indicatorVisual.transform.eulerAngles = Vector3.zero;
            }
            else // Vertical
            {
                planeOrigin.z += minDistance;
                targetPlane = new Plane(Vector3.back, planeOrigin);
                indicatorVisual.transform.eulerAngles = new Vector3(-90f, 0f, 0f);
            }

            var ray = new Ray(trackerCam.transform.position, trackerCam.transform.forward);

            if (targetPlane.Raycast(ray, out float enter))
            {
                // Show indicator
                if (!indicatorVisual.activeSelf)
                {
                    indicatorVisual.SetActive(true);
                    OnIndicatorShown?.Invoke();
                }

                Vector3 hitPoint = ray.GetPoint(enter);
                float distance = Vector3.Distance(hitPoint, planeOrigin);

                // Clamp to min/max distance
                if (distance < minDistance)
                    hitPoint = planeOrigin + (hitPoint - planeOrigin).normalized * minDistance;
                else if (distance > maxDistance)
                    hitPoint = planeOrigin + (hitPoint - planeOrigin).normalized * maxDistance;

                indicatorVisual.transform.position = hitPoint;
            }
            else
            {
                // No intersection — hide indicator
                if (indicatorVisual.activeSelf)
                {
                    indicatorVisual.SetActive(false);
                    OnIndicatorHidden?.Invoke();
                }
            }
        }

        /// <summary>
        /// Place content at the indicator's current position.
        /// Call from a UI button or tap handler.
        /// </summary>
        public void PlaceContent()
        {
            if (IsPlaced)
            {
                Debug.LogWarning("[XR8PlacementIndicator] Already placed. Call ResetPlacement() first.");
                return;
            }

            IsPlaced = true;
            Vector3 placedPos = indicatorVisual.transform.position;

            // Move content to indicator position
            if (contentRoot != null)
            {
                contentRoot.transform.position = placedPos;
                contentRoot.SetActive(true);

                // Face the camera
                Vector3 lookAtPos = trackerCam.transform.position;
                lookAtPos.y = 0f;
                contentRoot.transform.LookAt(lookAtPos, Vector3.up);
            }

            // Hide indicator
            if (indicatorVisual != null)
                indicatorVisual.SetActive(false);

            OnContentPlaced?.Invoke(placedPos);
        }

        /// <summary>
        /// Reset placement — show indicator again and hide content.
        /// </summary>
        public void ResetPlacement()
        {
            IsPlaced = false;

            if (hideContentUntilPlaced && contentRoot != null)
                contentRoot.SetActive(false);

            // Indicator will re-appear on next Update
            OnContentReset?.Invoke();
        }
    }
}
