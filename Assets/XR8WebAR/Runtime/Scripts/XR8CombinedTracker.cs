using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace XR8WebAR
{
    /// <summary>
    /// XR8CombinedTracker — Connects image tracking with world tracking.
    /// 
    /// Gallery Use Case:
    ///   Track a painting on the wall → detect the floor beneath it →
    ///   place AR content anchored to both the image AND the floor.
    ///
    /// How It Works:
    ///   1. Listens to XR8ImageTracker for image found/lost events
    ///   2. When an image is found, projects a ray downward from the image position
    ///   3. Uses XR8WorldTracker's surface data or physics raycasting to find the floor
    ///   4. Activates "floor content" at the projected floor position, aligned below the image
    ///   5. Optionally spawns connecting elements (beams, lines, particles) between image and floor
    ///
    /// Setup:
    ///   1. In XR8Manager, enable BOTH Image Tracking AND World Tracking
    ///   2. Add this component to any GameObject
    ///   3. Assign image tracker, world tracker, and floor content references
    ///   4. Floor content will appear below tracked images when the floor is detected
    /// </summary>
    public class XR8CombinedTracker : MonoBehaviour
    {
        // === References ===
        [Header("Tracking Sources")]
        [Tooltip("The image tracker component")]
        [SerializeField] private XR8ImageTracker imageTracker;

        [Tooltip("The world tracker component (for surface/floor detection)")]
        [SerializeField] private XR8WorldTracker worldTracker;

        [Tooltip("The AR camera")]
        [SerializeField] private Camera arCamera;

        // === Image-to-Floor Bindings ===
        [Header("Image-to-Floor Bindings")]
        [Tooltip("Map image targets to floor content. When an image is found, its paired floor content appears below.")]
        [SerializeField] private List<ImageFloorBinding> bindings = new List<ImageFloorBinding>();

        // === Settings ===
        [Header("Floor Detection")]
        [Tooltip("How to find the floor position")]
        [SerializeField] private FloorDetectionMode floorMode = FloorDetectionMode.RaycastDown;

        [Tooltip("Maximum distance to search for floor below the image (meters)")]
        [SerializeField] private float maxFloorDistance = 5f;

        [Tooltip("Floor height offset (positive = higher, negative = lower)")]
        [SerializeField] private float floorOffset = 0f;

        [Tooltip("Fallback floor height if no surface detected (default: Y=0)")]
        [SerializeField] private float fallbackFloorY = 0f;

        [Tooltip("Use world tracker surfaces for floor detection")]
        [SerializeField] private bool useWorldSurfaces = true;

        [Header("Positioning")]
        [Tooltip("Keep floor content directly below the image (X/Z locked to image)")]
        [SerializeField] private bool lockXZToImage = true;

        [Tooltip("Smoothly interpolate floor content position")]
        [SerializeField] private bool smoothMovement = true;

        [Tooltip("Smoothing speed for position interpolation")]
        [SerializeField][Range(1f, 20f)] private float smoothSpeed = 8f;

        [Header("Connector (Optional)")]
        [Tooltip("Optional LineRenderer to draw a line from image to floor")]
        [SerializeField] private bool showConnector = false;
        [SerializeField] private Material connectorMaterial;
        [SerializeField] private float connectorWidth = 0.005f;
        [SerializeField] private Color connectorColor = new Color(0.3f, 0.8f, 1f, 0.6f);

        // === Events ===
        [Header("Events")]
        [SerializeField] public UnityEvent<string, Vector3> OnFloorPositionFound;
        [SerializeField] public UnityEvent<string> OnFloorPositionLost;

        // === Runtime State ===
        private Dictionary<string, FloorTrackingState> activeStates = new Dictionary<string, FloorTrackingState>();
        private HashSet<string> trackedImageIds = new HashSet<string>();

        public enum FloorDetectionMode
        {
            [Tooltip("Cast a ray downward from the image to find floor surfaces")]
            RaycastDown,
            [Tooltip("Use the nearest surface from XR8WorldTracker")]
            NearestSurface,
            [Tooltip("Use a fixed Y position as the floor")]
            FixedHeight
        }

        private class FloorTrackingState
        {
            public ImageFloorBinding binding;
            public Vector3 targetFloorPos;
            public Vector3 currentFloorPos;
            public bool floorFound;
            public LineRenderer connector;
        }

        private void Awake()
        {
            // Auto-find if not assigned
            if (imageTracker == null)
                imageTracker = FindFirstObjectByType<XR8ImageTracker>();
            if (worldTracker == null)
                worldTracker = FindFirstObjectByType<XR8WorldTracker>();
            if (arCamera == null)
                arCamera = Camera.main;
        }

        private void Start()
        {
            // Deactivate all floor content initially
            foreach (var binding in bindings)
            {
                if (binding.floorContent != null)
                    binding.floorContent.gameObject.SetActive(false);
            }

            // Subscribe to image tracker events
            if (imageTracker != null)
            {
                // We'll poll tracked state in Update instead of events,
                // since the events may fire before we can add listeners
                Debug.Log("[XR8Combined] Initialized with " + bindings.Count + " image-floor bindings");
            }
            else
            {
                Debug.LogWarning("[XR8Combined] No XR8ImageTracker found! Assign one in the inspector.");
            }
        }

        private void Update()
        {
            if (imageTracker == null) return;

            // Check which image IDs are currently being tracked
            var currentlyTracked = new HashSet<string>();
            foreach (var binding in bindings)
            {
                if (string.IsNullOrEmpty(binding.imageTargetId)) continue;

                var imageTransform = GetImageTargetTransform(binding.imageTargetId);
                bool isActive = imageTransform != null && imageTransform.gameObject.activeInHierarchy;

                if (isActive)
                {
                    currentlyTracked.Add(binding.imageTargetId);

                    // Image is visible — compute floor position
                    if (!activeStates.ContainsKey(binding.imageTargetId))
                    {
                        // Just found
                        OnImageTargetFound(binding);
                    }

                    UpdateFloorPosition(binding, imageTransform);
                }
            }

            // Check for lost images
            var lostIds = new List<string>();
            foreach (var id in trackedImageIds)
            {
                if (!currentlyTracked.Contains(id))
                    lostIds.Add(id);
            }
            foreach (var id in lostIds)
            {
                OnImageTargetLost(id);
            }

            trackedImageIds = currentlyTracked;

            // Smooth movement
            if (smoothMovement)
            {
                foreach (var kvp in activeStates)
                {
                    var state = kvp.Value;
                    if (state.floorFound && state.binding.floorContent != null)
                    {
                        state.currentFloorPos = Vector3.Lerp(
                            state.currentFloorPos, state.targetFloorPos,
                            Time.deltaTime * smoothSpeed);
                        state.binding.floorContent.position = state.currentFloorPos;
                    }
                }
            }

            // Update connectors
            if (showConnector)
            {
                foreach (var kvp in activeStates)
                {
                    var state = kvp.Value;
                    if (state.connector != null && state.floorFound)
                    {
                        var imageTransform = GetImageTargetTransform(kvp.Key);
                        if (imageTransform != null)
                        {
                            state.connector.SetPosition(0, imageTransform.position);
                            state.connector.SetPosition(1, state.currentFloorPos);
                        }
                    }
                }
            }
        }

        private void OnImageTargetFound(ImageFloorBinding binding)
        {
            Debug.Log("[XR8Combined] Image found: " + binding.imageTargetId + " — searching for floor");

            var state = new FloorTrackingState
            {
                binding = binding,
                floorFound = false,
                targetFloorPos = Vector3.zero,
                currentFloorPos = Vector3.zero
            };

            // Create connector if needed
            if (showConnector)
            {
                state.connector = CreateConnector(binding.imageTargetId);
            }

            activeStates[binding.imageTargetId] = state;

            // Activate floor content (it'll be positioned in UpdateFloorPosition)
            if (binding.floorContent != null)
                binding.floorContent.gameObject.SetActive(true);
        }

        private void OnImageTargetLost(string imageId)
        {
            Debug.Log("[XR8Combined] Image lost: " + imageId + " — hiding floor content");

            if (activeStates.TryGetValue(imageId, out var state))
            {
                if (state.binding.floorContent != null)
                {
                    if (!state.binding.keepFloorOnLost)
                        state.binding.floorContent.gameObject.SetActive(false);
                }

                // Remove connector
                if (state.connector != null)
                    Destroy(state.connector.gameObject);

                activeStates.Remove(imageId);
            }

            OnFloorPositionLost?.Invoke(imageId);
        }

        private void UpdateFloorPosition(ImageFloorBinding binding, Transform imageTransform)
        {
            if (!activeStates.ContainsKey(binding.imageTargetId)) return;
            var state = activeStates[binding.imageTargetId];

            Vector3 imagePos = imageTransform.position;
            Vector3 floorPos = imagePos;

            switch (floorMode)
            {
                case FloorDetectionMode.RaycastDown:
                    floorPos = FindFloorByRaycast(imagePos);
                    break;

                case FloorDetectionMode.NearestSurface:
                    floorPos = FindFloorBySurface(imagePos);
                    break;

                case FloorDetectionMode.FixedHeight:
                    floorPos = new Vector3(
                        lockXZToImage ? imagePos.x : floorPos.x,
                        fallbackFloorY + floorOffset,
                        lockXZToImage ? imagePos.z : floorPos.z
                    );
                    break;
            }

            state.targetFloorPos = floorPos;

            if (!state.floorFound)
            {
                // First frame — snap instead of lerp
                state.currentFloorPos = floorPos;
                state.floorFound = true;

                if (binding.floorContent != null)
                    binding.floorContent.position = floorPos;

                OnFloorPositionFound?.Invoke(binding.imageTargetId, floorPos);
            }

            if (!smoothMovement && binding.floorContent != null)
            {
                binding.floorContent.position = floorPos;
            }

            // Orient floor content to face camera or match image rotation
            if (binding.floorContent != null)
            {
                switch (binding.floorRotation)
                {
                    case ImageFloorBinding.FloorRotationMode.FaceCamera:
                        if (arCamera != null)
                        {
                            var lookDir = arCamera.transform.position - binding.floorContent.position;
                            lookDir.y = 0; // Keep upright
                            if (lookDir.sqrMagnitude > 0.001f)
                                binding.floorContent.rotation = Quaternion.LookRotation(lookDir);
                        }
                        break;

                    case ImageFloorBinding.FloorRotationMode.MatchImage:
                        var euler = imageTransform.eulerAngles;
                        binding.floorContent.rotation = Quaternion.Euler(0, euler.y, 0);
                        break;

                    case ImageFloorBinding.FloorRotationMode.Fixed:
                        // Don't change rotation
                        break;
                }
            }
        }

        private Vector3 FindFloorByRaycast(Vector3 origin)
        {
            // First try: Unity physics raycast downward
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxFloorDistance))
            {
                return new Vector3(
                    lockXZToImage ? origin.x : hit.point.x,
                    hit.point.y + floorOffset,
                    lockXZToImage ? origin.z : hit.point.z
                );
            }

            // Second try: world tracker surfaces
            if (useWorldSurfaces && worldTracker != null)
            {
                return FindFloorBySurface(origin);
            }

            // Fallback: fixed floor
            return new Vector3(origin.x, fallbackFloorY + floorOffset, origin.z);
        }

        private Vector3 FindFloorBySurface(Vector3 origin)
        {
            if (worldTracker == null || worldTracker.ActiveSurfaces.Count == 0)
            {
                return new Vector3(origin.x, fallbackFloorY + floorOffset, origin.z);
            }

            // Find the surface closest below the image position
            float bestY = fallbackFloorY;
            float bestDist = float.MaxValue;

            foreach (var kvp in worldTracker.ActiveSurfaces)
            {
                Vector3 surfacePos = kvp.Value;

                // Must be below the image
                if (surfacePos.y < origin.y)
                {
                    float dist = origin.y - surfacePos.y;
                    if (dist < bestDist && dist <= maxFloorDistance)
                    {
                        bestDist = dist;
                        bestY = surfacePos.y;
                    }
                }
            }

            return new Vector3(
                origin.x,
                bestY + floorOffset,
                origin.z
            );
        }

        private Transform GetImageTargetTransform(string targetId)
        {
            if (imageTracker == null) return null;

            // Use the public API to check target IDs and find the matching transform
            var targetIds = imageTracker.GetTargetIds();
            if (!targetIds.Contains(targetId)) return null;

            // Find the target's transform in the scene hierarchy
            // The image tracker parents content under its own transform
            foreach (Transform child in imageTracker.transform)
            {
                if (child.name.Contains(targetId))
                    return child;
            }

            // Search all image targets
            var tracker = imageTracker;
            foreach (Transform child in tracker.transform)
            {
                if (child.gameObject.activeInHierarchy && child.name.Contains(targetId))
                    return child;
            }

            return null;
        }

        private LineRenderer CreateConnector(string id)
        {
            var connObj = new GameObject("Connector_" + id);
            connObj.transform.SetParent(transform);

            var lr = connObj.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.startWidth = connectorWidth;
            lr.endWidth = connectorWidth;
            lr.useWorldSpace = true;

            if (connectorMaterial != null)
            {
                lr.material = connectorMaterial;
            }
            else
            {
                // Create a simple unlit material
                lr.material = new Material(Shader.Find("Sprites/Default"));
            }

            lr.startColor = connectorColor;
            lr.endColor = connectorColor;

            return lr;
        }

        /// <summary>
        /// Add a binding at runtime.
        /// </summary>
        public void AddBinding(string imageTargetId, Transform floorContent)
        {
            var binding = new ImageFloorBinding
            {
                imageTargetId = imageTargetId,
                floorContent = floorContent
            };
            bindings.Add(binding);
            floorContent.gameObject.SetActive(false);
        }

        /// <summary>
        /// Get the current floor position for a tracked image.
        /// Returns Vector3.zero if not currently tracked.
        /// </summary>
        public Vector3 GetFloorPosition(string imageTargetId)
        {
            if (activeStates.TryGetValue(imageTargetId, out var state))
                return state.currentFloorPos;
            return Vector3.zero;
        }

        /// <summary>
        /// Is the given image's floor position currently being tracked?
        /// </summary>
        public bool IsFloorTracked(string imageTargetId)
        {
            return activeStates.ContainsKey(imageTargetId) && activeStates[imageTargetId].floorFound;
        }
    }

    /// <summary>
    /// Maps an image target to floor content.
    /// When the image is detected, the floor content is placed below it.
    /// </summary>
    [System.Serializable]
    public class ImageFloorBinding
    {
        [Tooltip("The image target ID (must match XR8ImageTracker target)")]
        public string imageTargetId;

        [Tooltip("The Transform to place on the floor below this image")]
        public Transform floorContent;

        [Tooltip("How to rotate the floor content")]
        public FloorRotationMode floorRotation = FloorRotationMode.FaceCamera;

        [Tooltip("Keep floor content visible even when image tracking is lost")]
        public bool keepFloorOnLost = false;

        public enum FloorRotationMode
        {
            [Tooltip("Floor content faces the camera")]
            FaceCamera,
            [Tooltip("Floor content matches the image's Y rotation")]
            MatchImage,
            [Tooltip("Floor content rotation doesn't change")]
            Fixed
        }
    }
}
