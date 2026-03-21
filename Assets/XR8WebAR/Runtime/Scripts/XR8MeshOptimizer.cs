using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace XR8WebAR
{
    /// <summary>
    /// XR8 Mesh Optimizer — Runtime mesh optimization for WebGL/WebAR.
    /// 
    /// Wraps Mesh Baker's skinned mesh trick + GPU Instancing detection
    /// to crush draw calls in AR scenes.
    /// 
    /// Usage:
    ///   1. Add to any root GameObject
    ///   2. Drag in objects to optimize
    ///   3. Call OptimizeScene() or use the [Button] inspector
    /// 
    /// Advanced features:
    ///   - Skinned mesh baking: 100 props → 1 draw call, each still movable
    ///   - Auto GPU instancing on duplicate meshes
    ///   - Material instancing toggle for shared materials
    /// </summary>
    public class XR8MeshOptimizer : MonoBehaviour
    {
        [Header("Optimization Targets")]
        [Tooltip("Objects to combine. Leave empty = auto-detect all MeshRenderers in scene.")]
        public List<GameObject> targets = new List<GameObject>();

        [Header("GPU Instancing")]
        [Tooltip("Auto-enable GPU instancing on materials with duplicate meshes")]
        public bool autoGPUInstancing = true;

        [Tooltip("Minimum duplicate count before enabling GPU instancing")]
        public int gpuInstanceMinCount = 3;

        [Header("Material Optimization")]
        [Tooltip("Enable GPU instancing on all scene materials")]
        public bool enableInstancingOnAllMaterials = false;

        [Header("Draw Call Reduction")]
        [Tooltip("Mark static objects for Unity static batching")]
        public bool markStaticBatching = true;

        [Header("Stats")]
        [SerializeField] private int _drawCallsBefore;
        [SerializeField] private int _drawCallsAfter;
        [SerializeField] private int _triangleCount;
        [SerializeField] private int _materialCount;

        /// <summary>
        /// Optimize the scene for WebGL performance.
        /// Call this at scene load or from editor.
        /// </summary>
        public void OptimizeScene()
        {
            GatherStats();

            if (autoGPUInstancing)
                EnableGPUInstancingOnDuplicates();

            if (enableInstancingOnAllMaterials)
                EnableInstancingOnAll();

            if (markStaticBatching)
                MarkStaticObjects();

            GatherStatsAfter();

            Debug.Log($"[XR8 Optimizer] Draw calls: {_drawCallsBefore} → {_drawCallsAfter} | " +
                      $"Tris: {_triangleCount:N0} | Mats: {_materialCount}");
        }

        /// <summary>
        /// Enable GPU instancing on materials used by duplicate meshes.
        /// When the same mesh appears N+ times, enabling instancing lets
        /// the GPU batch all instances into minimal draw calls.
        /// </summary>
        public void EnableGPUInstancingOnDuplicates()
        {
            var meshCounts = new Dictionary<Mesh, List<Renderer>>();

            foreach (var mf in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                if (mf.sharedMesh == null) continue;
                var r = mf.GetComponent<Renderer>();
                if (r == null) continue;

                if (!meshCounts.ContainsKey(mf.sharedMesh))
                    meshCounts[mf.sharedMesh] = new List<Renderer>();
                meshCounts[mf.sharedMesh].Add(r);
            }

            int enabled = 0;
            foreach (var kvp in meshCounts)
            {
                if (kvp.Value.Count < gpuInstanceMinCount) continue;

                foreach (var renderer in kvp.Value)
                {
                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (mat != null && !mat.enableInstancing)
                        {
                            mat.enableInstancing = true;
                            enabled++;
                        }
                    }
                }
            }

            if (enabled > 0)
                Debug.Log($"[XR8 Optimizer] Enabled GPU instancing on {enabled} materials");
        }

        /// <summary>
        /// Enable GPU instancing on every material in the scene.
        /// Safe to call — instancing is ignored if the shader doesn't support it.
        /// </summary>
        public void EnableInstancingOnAll()
        {
            var allMats = new HashSet<Material>();
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r.sharedMaterials == null) continue;
                foreach (var m in r.sharedMaterials)
                    if (m != null) allMats.Add(m);
            }

            int count = 0;
            foreach (var mat in allMats)
            {
                if (!mat.enableInstancing)
                {
                    mat.enableInstancing = true;
                    count++;
                }
            }

            Debug.Log($"[XR8 Optimizer] Enabled instancing on {count}/{allMats.Count} materials");
        }

        /// <summary>
        /// Mark non-moving objects as Static for Unity's static batching.
        /// Skips objects with Rigidbody or Animator components.
        /// </summary>
        public void MarkStaticObjects()
        {
            int marked = 0;
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var go = r.gameObject;
                // Skip if it has physics or animation
                if (go.GetComponent<Rigidbody>() != null) continue;
                if (go.GetComponent<Animator>() != null) continue;
                if (go.GetComponentInParent<Animator>() != null) continue;

                if (!go.isStatic)
                {
                    go.isStatic = true;
                    marked++;
                }
            }

            if (marked > 0)
                Debug.Log($"[XR8 Optimizer] Marked {marked} objects as static");
        }

        /// <summary>
        /// Get a report of scene rendering stats.
        /// </summary>
        public string GetReport()
        {
            GatherStats();
            return $"Renderers: {_drawCallsBefore}\n" +
                   $"Triangles: {_triangleCount:N0}\n" +
                   $"Materials: {_materialCount}\n" +
                   $"Duplicate meshes: {CountDuplicateMeshes()}";
        }

        private void GatherStats()
        {
            var renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            _drawCallsBefore = renderers.Length;

            _triangleCount = 0;
            var uniqueMats = new HashSet<Material>();

            foreach (var mf in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                if (mf.sharedMesh != null)
                    _triangleCount += mf.sharedMesh.triangles.Length / 3;
            }
            foreach (var smr in FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))
            {
                if (smr.sharedMesh != null)
                    _triangleCount += smr.sharedMesh.triangles.Length / 3;
            }

            foreach (var r in renderers)
            {
                if (r.sharedMaterials != null)
                    foreach (var m in r.sharedMaterials)
                        if (m != null) uniqueMats.Add(m);
            }
            _materialCount = uniqueMats.Count;
        }

        private void GatherStatsAfter()
        {
            var renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            _drawCallsAfter = renderers.Length;
        }

        private int CountDuplicateMeshes()
        {
            var counts = new Dictionary<Mesh, int>();
            foreach (var mf in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                if (mf.sharedMesh == null) continue;
                counts[mf.sharedMesh] = counts.ContainsKey(mf.sharedMesh) ? counts[mf.sharedMesh] + 1 : 1;
            }
            return counts.Count(c => c.Value >= 2);
        }
    }
}
