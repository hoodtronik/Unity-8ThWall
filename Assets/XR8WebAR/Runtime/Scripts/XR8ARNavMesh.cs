using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using System.Collections.Generic;

#if XR8_HAS_AI_NAVIGATION
using Unity.AI.Navigation;
#endif

namespace XR8WebAR
{
    /// <summary>
    /// XR8ARNavMesh — Runtime navigation mesh built from detected AR surfaces.
    ///
    /// Inspired by Lightship ARDK's LightshipNavMeshManager, adapted for WebAR.
    /// Uses surface detection data from XR8WorldTracker to create walkable areas,
    /// enabling AI characters (like XR8ConvaiCharacter) to walk on real floors
    /// and navigate around obstacles.
    ///
    /// Requires: com.unity.ai.navigation package (auto-detected via asmdef versionDefines).
    ///
    /// Setup:
    ///   1. Add XR8ARNavMesh to any GameObject in the scene
    ///   2. Assign the XR8WorldTracker reference
    ///   3. Make sure your character has a NavMeshAgent component
    ///   4. The NavMesh builds automatically as surfaces are detected
    ///
    /// Unity NavMesh runtime baking is WebGL-compatible.
    /// </summary>
    public class XR8ARNavMesh : MonoBehaviour
    {
        [Header("World Tracker")]
        [Tooltip("Reference to the XR8WorldTracker that provides surface data")]
        [SerializeField] private XR8WorldTracker worldTracker;

        [Header("NavMesh Settings")]
        [Tooltip("Size of each surface plane tile (meters)")]
        [SerializeField] private float tileSizeMeters = 2f;

        [Tooltip("Seconds between NavMesh rebuilds (lower = more responsive, higher = better performance)")]
        [SerializeField] private float rebuildInterval = 1.5f;

        [Tooltip("Minimum number of surfaces before first build")]
        [SerializeField] private int minSurfacesForBuild = 1;

        [Tooltip("Agent type index for NavMesh baking (match your NavMeshAgent)")]
        [SerializeField] private int agentTypeId = 0;

        [Header("Debug")]
        [Tooltip("Show the NavMesh surface planes (for debugging)")]
        [SerializeField] private bool showDebugPlanes = false;

        [Tooltip("Debug plane color")]
        [SerializeField] private Color debugPlaneColor = new Color(0f, 1f, 0.5f, 0.3f);

        [Header("Events")]
        [SerializeField] public UnityEvent OnNavMeshBuilt;
        [SerializeField] public UnityEvent OnNavMeshUpdated;
        [SerializeField] public UnityEvent<int> OnSurfacePlaneCreated; // count

        // ━━━ Internal State ━━━
#if XR8_HAS_AI_NAVIGATION
        private NavMeshSurface navMeshSurface;
#endif
        private Dictionary<string, GameObject> surfacePlanes = new Dictionary<string, GameObject>();
        private float rebuildTimer;
        private bool needsRebuild;
        private bool hasBuiltOnce;
        private int lastSurfaceCount;

        // Shared materials
        private Material debugMaterial;
        private Material invisibleMaterial;

        // ━━━ Public API ━━━

        /// <summary>Whether the NavMesh has been built at least once.</summary>
        public bool IsNavMeshReady => hasBuiltOnce;

        /// <summary>Number of active surface planes.</summary>
        public int SurfacePlaneCount => surfacePlanes.Count;

        /// <summary>Force an immediate NavMesh rebuild.</summary>
        public void ForceRebuild()
        {
            BuildNavMesh();
        }

        /// <summary>Clear all surface planes and NavMesh data.</summary>
        public void ClearNavMesh()
        {
            foreach (var kvp in surfacePlanes)
            {
                if (kvp.Value != null) Destroy(kvp.Value);
            }
            surfacePlanes.Clear();
            hasBuiltOnce = false;
            needsRebuild = false;

#if XR8_HAS_AI_NAVIGATION
            if (navMeshSurface != null)
                navMeshSurface.RemoveData();
#endif

            Debug.Log("[XR8ARNavMesh] NavMesh cleared");
        }

        // ━━━ Lifecycle ━━━

        private void Awake()
        {
#if XR8_HAS_AI_NAVIGATION
            navMeshSurface = GetComponent<NavMeshSurface>();
            if (navMeshSurface == null)
                navMeshSurface = gameObject.AddComponent<NavMeshSurface>();

            // Configure NavMeshSurface for runtime baking
            navMeshSurface.collectObjects = CollectObjects.Children;
            navMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            navMeshSurface.agentTypeID = agentTypeId;
#else
            Debug.LogWarning("[XR8ARNavMesh] AI Navigation package not detected. " +
                "Install 'com.unity.ai.navigation' from Package Manager for full NavMesh support.");
#endif

            // Create debug material
            debugMaterial = new Material(Shader.Find("Unlit/Color"));
            debugMaterial.color = debugPlaneColor;

            // Invisible material (for non-debug mode)
            invisibleMaterial = new Material(Shader.Find("Unlit/Color"));
            invisibleMaterial.color = new Color(0, 0, 0, 0);
            invisibleMaterial.SetFloat("_Mode", 3); // Transparent

            // Auto-find WorldTracker
            if (worldTracker == null)
                worldTracker = FindFirstObjectByType<XR8WorldTracker>();
        }

        private void OnEnable()
        {
            if (worldTracker != null)
            {
                worldTracker.OnSurfaceDetected.AddListener(OnSurfaceDetected);
            }
        }

        private void OnDisable()
        {
            if (worldTracker != null)
            {
                worldTracker.OnSurfaceDetected.RemoveListener(OnSurfaceDetected);
            }
        }

        private void Update()
        {
            // Sync with WorldTracker's surface dictionary
            SyncSurfaces();

            // Periodic rebuild
            if (needsRebuild)
            {
                rebuildTimer -= Time.deltaTime;
                if (rebuildTimer <= 0f)
                {
                    BuildNavMesh();
                    rebuildTimer = rebuildInterval;
                }
            }
        }

        private void OnDestroy()
        {
            if (debugMaterial != null) Destroy(debugMaterial);
            if (invisibleMaterial != null) Destroy(invisibleMaterial);
        }

        // ━━━ Surface Management ━━━

        private void OnSurfaceDetected(Vector3 position, Vector3 normal)
        {
            needsRebuild = true;
        }

        private void SyncSurfaces()
        {
            if (worldTracker == null) return;

            var activeSurfaces = worldTracker.ActiveSurfaces;
            if (activeSurfaces.Count == lastSurfaceCount) return;
            lastSurfaceCount = activeSurfaces.Count;

            // Create planes for new surfaces
            foreach (var kvp in activeSurfaces)
            {
                if (!surfacePlanes.ContainsKey(kvp.Key))
                {
                    CreateSurfacePlane(kvp.Key, kvp.Value);
                }
                else
                {
                    // Update existing plane position
                    surfacePlanes[kvp.Key].transform.position = kvp.Value;
                }
            }

            // Remove planes for lost surfaces
            var toRemove = new List<string>();
            foreach (var kvp in surfacePlanes)
            {
                if (!activeSurfaces.ContainsKey(kvp.Key))
                    toRemove.Add(kvp.Key);
            }
            foreach (var id in toRemove)
            {
                if (surfacePlanes[id] != null) Destroy(surfacePlanes[id]);
                surfacePlanes.Remove(id);
                needsRebuild = true;
            }
        }

        private void CreateSurfacePlane(string id, Vector3 position)
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "NavSurface_" + id;
            plane.transform.SetParent(transform);
            plane.transform.position = position;
            plane.transform.localScale = Vector3.one * (tileSizeMeters / 10f);

            var renderer = plane.GetComponent<MeshRenderer>();
            if (showDebugPlanes)
            {
                renderer.material = debugMaterial;
            }
            else
            {
                renderer.enabled = false;
            }

            surfacePlanes[id] = plane;
            needsRebuild = true;

            OnSurfacePlaneCreated?.Invoke(surfacePlanes.Count);
            Debug.Log("[XR8ARNavMesh] Surface plane created: " + id + " at " + position +
                      " (total: " + surfacePlanes.Count + ")");
        }

        // ━━━ NavMesh Building ━━━

        private void BuildNavMesh()
        {
#if !XR8_HAS_AI_NAVIGATION
            Debug.LogWarning("[XR8ARNavMesh] Cannot build — AI Navigation package not installed");
            needsRebuild = false;
            return;
#else
            if (surfacePlanes.Count < minSurfacesForBuild)
            {
                Debug.Log("[XR8ARNavMesh] Waiting for more surfaces... (" +
                          surfacePlanes.Count + "/" + minSurfacesForBuild + ")");
                return;
            }

            navMeshSurface.BuildNavMesh();
            needsRebuild = false;

            if (!hasBuiltOnce)
            {
                hasBuiltOnce = true;
                OnNavMeshBuilt?.Invoke();
                Debug.Log("[XR8ARNavMesh] NavMesh built for the first time! (" +
                          surfacePlanes.Count + " surfaces)");
            }
            else
            {
                OnNavMeshUpdated?.Invoke();
                Debug.Log("[XR8ARNavMesh] NavMesh updated (" + surfacePlanes.Count + " surfaces)");
            }
#endif
        }

        // ━━━ Debug Visualization ━━━

        /// <summary>Toggle debug plane visibility at runtime.</summary>
        public void SetDebugVisualization(bool enabled)
        {
            showDebugPlanes = enabled;
            foreach (var kvp in surfacePlanes)
            {
                if (kvp.Value != null)
                {
                    var renderer = kvp.Value.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        renderer.enabled = enabled;
                        if (enabled)
                            renderer.material = debugMaterial;
                    }
                }
            }
        }
    }
}
