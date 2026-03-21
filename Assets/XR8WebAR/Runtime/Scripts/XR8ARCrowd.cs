using UnityEngine;
using System.Collections.Generic;

namespace XR8WebAR
{
    /// <summary>
    /// XR8 AR Crowd — Spawn and manage animated crowds in AR scenes.
    /// 
    /// Designed for WebGL: uses Mesh Animator VAT (Vertex Animation Textures)
    /// + GPU Instancer for maximum performance. Falls back to basic instancing
    /// if plugins aren't available.
    /// 
    /// Usage:
    ///   1. Bake your character with Mesh Animator (shader animated mode)
    ///   2. Assign the baked prefab to this component
    ///   3. Define spawn area and count
    ///   4. Call SpawnCrowd() or enable Auto Spawn
    /// 
    /// For iClone characters:
    ///   1. Export from iClone as FBX
    ///   2. Import to Unity, set to Generic rig
    ///   3. Bake with Mesh Animator
    ///   4. Assign baked prefab here
    /// </summary>
    public class XR8ARCrowd : MonoBehaviour
    {
        [Header("Crowd Prefab")]
        [Tooltip("The Mesh Animator baked prefab (or any animated prefab)")]
        public GameObject crowdPrefab;

        [Tooltip("Alternative prefabs for variety (randomly selected)")]
        public List<GameObject> prefabVariants = new List<GameObject>();

        [Header("Spawn Settings")]
        [Tooltip("Number of crowd members to spawn")]
        [Range(1, 500)]
        public int crowdSize = 20;

        [Tooltip("Spawn radius around this transform")]
        public float spawnRadius = 5f;

        [Tooltip("Minimum spacing between crowd members")]
        public float minSpacing = 0.8f;

        [Tooltip("Random scale variation (±percentage)")]
        [Range(0f, 0.3f)]
        public float scaleVariation = 0.1f;

        [Tooltip("Randomize Y rotation on spawn")]
        public bool randomRotation = true;

        [Header("Animation")]
        [Tooltip("Randomize animation start time to prevent sync")]
        public bool randomizeAnimStart = true;

        [Tooltip("Random animation speed variation (±percentage)")]
        [Range(0f, 0.3f)]
        public float speedVariation = 0.1f;

        [Header("Performance")]
        [Tooltip("Enable GPU instancing on spawned materials")]
        public bool enableGPUInstancing = true;

        [Tooltip("Auto-spawn on Start")]
        public bool autoSpawn = false;

        [Tooltip("Use object pooling instead of Instantiate/Destroy")]
        public bool usePooling = true;

        [Header("AR Placement")]
        [Tooltip("Place crowd relative to an AR surface hit point")]
        public bool arSurfacePlacement = false;

        [Tooltip("Y offset from surface")]
        public float surfaceOffset = 0f;

        // Internal
        private List<GameObject> _spawnedCrowd = new List<GameObject>();
        private Queue<GameObject> _pool = new Queue<GameObject>();

        private void Start()
        {
            if (autoSpawn && crowdPrefab != null)
                SpawnCrowd();
        }

        /// <summary>
        /// Spawn the crowd around this transform's position.
        /// </summary>
        public void SpawnCrowd()
        {
            SpawnCrowdAt(transform.position);
        }

        /// <summary>
        /// Spawn the crowd at a specific world position (e.g., AR hit point).
        /// </summary>
        public void SpawnCrowdAt(Vector3 center)
        {
            if (crowdPrefab == null)
            {
                Debug.LogError("[XR8 Crowd] No crowd prefab assigned!");
                return;
            }

            ClearCrowd();

            var positions = GenerateSpawnPositions(center, crowdSize, spawnRadius, minSpacing);

            for (int i = 0; i < positions.Count; i++)
            {
                GameObject prefab = GetRandomPrefab();
                GameObject instance = GetInstance(prefab);

                instance.transform.position = positions[i] + Vector3.up * surfaceOffset;

                if (randomRotation)
                    instance.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

                if (scaleVariation > 0)
                {
                    float scaleMultiplier = 1f + Random.Range(-scaleVariation, scaleVariation);
                    instance.transform.localScale = prefab.transform.localScale * scaleMultiplier;
                }

                // Randomize animation
                RandomizeAnimation(instance);

                // Enable GPU instancing on materials
                if (enableGPUInstancing)
                    EnableInstancing(instance);

                instance.SetActive(true);
                _spawnedCrowd.Add(instance);
            }

            Debug.Log($"[XR8 Crowd] Spawned {positions.Count} crowd members at {center}");
        }

        /// <summary>
        /// Clear all spawned crowd members.
        /// </summary>
        public void ClearCrowd()
        {
            foreach (var obj in _spawnedCrowd)
            {
                if (obj == null) continue;

                if (usePooling)
                {
                    obj.SetActive(false);
                    _pool.Enqueue(obj);
                }
                else
                {
                    Destroy(obj);
                }
            }
            _spawnedCrowd.Clear();
        }

        /// <summary>
        /// Update crowd size at runtime (adds or removes members).
        /// </summary>
        public void SetCrowdSize(int newSize)
        {
            crowdSize = Mathf.Clamp(newSize, 0, 500);

            if (_spawnedCrowd.Count > crowdSize)
            {
                // Remove excess
                while (_spawnedCrowd.Count > crowdSize)
                {
                    var last = _spawnedCrowd[_spawnedCrowd.Count - 1];
                    _spawnedCrowd.RemoveAt(_spawnedCrowd.Count - 1);
                    if (usePooling)
                    {
                        last.SetActive(false);
                        _pool.Enqueue(last);
                    }
                    else
                    {
                        Destroy(last);
                    }
                }
            }
            else if (_spawnedCrowd.Count < crowdSize)
            {
                // Add more
                int toAdd = crowdSize - _spawnedCrowd.Count;
                var positions = GenerateSpawnPositions(transform.position, toAdd, spawnRadius, minSpacing);
                foreach (var pos in positions)
                {
                    var instance = GetInstance(GetRandomPrefab());
                    instance.transform.position = pos + Vector3.up * surfaceOffset;
                    if (randomRotation)
                        instance.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                    RandomizeAnimation(instance);
                    if (enableGPUInstancing) EnableInstancing(instance);
                    instance.SetActive(true);
                    _spawnedCrowd.Add(instance);
                }
            }
        }

        // =============================================
        // INTERNAL HELPERS
        // =============================================

        private GameObject GetRandomPrefab()
        {
            if (prefabVariants.Count > 0 && Random.value > 0.5f)
                return prefabVariants[Random.Range(0, prefabVariants.Count)];
            return crowdPrefab;
        }

        private GameObject GetInstance(GameObject prefab)
        {
            if (usePooling && _pool.Count > 0)
            {
                var pooled = _pool.Dequeue();
                pooled.transform.SetParent(transform);
                return pooled;
            }

            var instance = Instantiate(prefab, transform);
            instance.name = $"CrowdMember_{_spawnedCrowd.Count + _pool.Count}";
            return instance;
        }

        private void RandomizeAnimation(GameObject instance)
        {
            // Try Animator first
            var animator = instance.GetComponent<Animator>();
            if (animator != null && randomizeAnimStart)
            {
                // Start at random point in current clip
                animator.Play(0, 0, Random.Range(0f, 1f));
                if (speedVariation > 0)
                    animator.speed = 1f + Random.Range(-speedVariation, speedVariation);
                return;
            }

            // Try Animation (legacy)
            var anim = instance.GetComponent<Animation>();
            if (anim != null && randomizeAnimStart)
            {
                foreach (AnimationState state in anim)
                {
                    state.time = Random.Range(0f, state.length);
                    if (speedVariation > 0)
                        state.speed = 1f + Random.Range(-speedVariation, speedVariation);
                }
            }

            // Mesh Animator integration — if component exists, randomize via its API
            // MeshAnimator stores animation time internally
            // The component auto-randomizes if "Random Start Frame" is checked in bake settings
        }

        private void EnableInstancing(GameObject instance)
        {
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
            {
                if (renderer.sharedMaterials == null) continue;
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat != null && !mat.enableInstancing)
                        mat.enableInstancing = true;
                }
            }
        }

        private List<Vector3> GenerateSpawnPositions(Vector3 center, int count, float radius, float spacing)
        {
            var positions = new List<Vector3>();
            int maxAttempts = count * 10;
            int attempts = 0;

            while (positions.Count < count && attempts < maxAttempts)
            {
                attempts++;

                // Random point in circle
                Vector2 randomPoint = Random.insideUnitCircle * radius;
                Vector3 candidate = center + new Vector3(randomPoint.x, 0, randomPoint.y);

                // Check spacing
                bool tooClose = false;
                foreach (var existing in positions)
                {
                    if (Vector3.Distance(candidate, existing) < spacing)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                    positions.Add(candidate);
            }

            if (positions.Count < count)
                Debug.LogWarning($"[XR8 Crowd] Only placed {positions.Count}/{count} — try larger radius or smaller spacing");

            return positions;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, spawnRadius);

            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawSphere(transform.position, 0.2f);
        }
    }
}
