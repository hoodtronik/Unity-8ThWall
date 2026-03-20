using System;
using System.Collections.Generic;
using UnityEngine;

namespace XR8WebAR.GaussianSplat
{
    /// <summary>
    /// WebGL-compatible Gaussian Splat renderer.
    /// 
    /// Uses billboard quads with GPU instancing — no compute shaders required.
    /// CPU-side depth sorting for correct alpha blending.
    /// 
    /// Workflow:
    ///   1. Prepare .ply with Mobile-GS (https://github.com/xiaobiaodu/Mobile-GS)
    ///      or any standard 3DGS pipeline
    ///   2. Place .ply/.splat as a TextAsset (rename to .bytes) or load at runtime
    ///   3. Attach this component, assign the file, hit Play
    /// 
    /// Performance targets:
    ///   - 50k splats @ 30fps on mid-range mobile (Snapdragon 7xx)
    ///   - 200k splats @ 60fps on desktop
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class GaussianSplatRenderer : MonoBehaviour
    {
        [Header("Splat Data")]
        [Tooltip("Gaussian splat file (.ply or .splat renamed to .bytes)")]
        [SerializeField] private TextAsset splatAsset;

        [Header("Rendering")]
        [Tooltip("Material using XR8WebAR/GaussianSplat shader")]
        [SerializeField] private Material splatMaterial;

        [SerializeField][Range(0.01f, 5f)] private float splatScale = 1f;
        [SerializeField][Range(0f, 1f)] private float globalOpacity = 1f;

        [Tooltip("Max splats to render per frame (for mobile performance)")]
        [SerializeField] private int maxVisibleSplats = 50000;

        [Tooltip("Re-sort depth every N frames (1 = every frame, 3 = every 3rd)")]
        [SerializeField][Range(1, 10)] private int sortInterval = 2;

        [Header("Culling")]
        [SerializeField] private float maxRenderDistance = 50f;
        [SerializeField][Range(0f, 0.01f)] private float minOpacity = 0.005f;

        // --- Runtime ---
        private GaussianSplatLoader.SplatData[] allSplats;
        private int[] sortedIndices;
        private int visibleCount;
        private int frameCounter;

        // GPU instancing buffers
        private Matrix4x4[] instanceMatrices;
        private Vector4[] instancePositions;
        private Vector4[] instanceColors;
        private Vector4[] instanceCov2D;
        private MaterialPropertyBlock propertyBlock;

        // Quad mesh for instancing
        private Mesh quadMesh;

        // Batch limits (GPU instancing max per draw call)
        private const int BATCH_SIZE = 1023; // Unity limit for DrawMeshInstanced

        private bool isLoaded = false;

        private void Start()
        {
            propertyBlock = new MaterialPropertyBlock();

            CreateQuadMesh();

            if (splatMaterial == null)
            {
                splatMaterial = new Material(Shader.Find("XR8WebAR/GaussianSplat"));
                if (splatMaterial.shader == null)
                {
                    Debug.LogError("[GaussianSplatRenderer] Could not find GaussianSplat shader!");
                    return;
                }
            }

            if (splatAsset != null)
            {
                LoadFromAsset(splatAsset);
            }
        }

        /// <summary>Load splat data from a TextAsset (.bytes file).</summary>
        public void LoadFromAsset(TextAsset asset)
        {
            if (asset == null) return;

            byte[] data = asset.bytes;
            
            // Detect format: PLY starts with "ply\n", otherwise treat as .splat
            bool isPly = data.Length > 4 && data[0] == 'p' && data[1] == 'l' && data[2] == 'y';

            if (isPly)
                allSplats = GaussianSplatLoader.LoadPlyFile(data);
            else
                allSplats = GaussianSplatLoader.LoadSplatFile(data);

            InitializeBuffers();
        }

        /// <summary>Load splat data from raw bytes at runtime.</summary>
        public void LoadFromBytes(byte[] data, bool isPly = true)
        {
            if (isPly)
                allSplats = GaussianSplatLoader.LoadPlyFile(data);
            else
                allSplats = GaussianSplatLoader.LoadSplatFile(data);

            InitializeBuffers();
        }

        /// <summary>Load pre-parsed splat data directly.</summary>
        public void LoadFromData(GaussianSplatLoader.SplatData[] data)
        {
            allSplats = data;
            InitializeBuffers();
        }

        private void InitializeBuffers()
        {
            if (allSplats == null || allSplats.Length == 0)
            {
                Debug.LogWarning("[GaussianSplatRenderer] No splat data to render!");
                return;
            }

            int count = Mathf.Min(allSplats.Length, maxVisibleSplats);

            sortedIndices = new int[allSplats.Length];
            for (int i = 0; i < allSplats.Length; i++)
                sortedIndices[i] = i;

            // Pre-allocate instancing arrays (batch size)
            int batchCount = Mathf.Min(count, BATCH_SIZE);
            instanceMatrices = new Matrix4x4[batchCount];
            instancePositions = new Vector4[batchCount];
            instanceColors = new Vector4[batchCount];
            instanceCov2D = new Vector4[batchCount];

            isLoaded = true;
            frameCounter = 0;

            Debug.Log("[GaussianSplatRenderer] Ready: " + allSplats.Length + " splats loaded, " +
                "max visible = " + maxVisibleSplats);
        }

        private void CreateQuadMesh()
        {
            // Simple quad: 4 vertices at corners, UVs from -1 to 1
            quadMesh = new Mesh();
            quadMesh.name = "SplatQuad";

            quadMesh.vertices = new Vector3[]
            {
                new Vector3(-1, -1, 0),
                new Vector3( 1, -1, 0),
                new Vector3( 1,  1, 0),
                new Vector3(-1,  1, 0)
            };

            quadMesh.uv = new Vector2[]
            {
                new Vector2(-1, -1),
                new Vector2( 1, -1),
                new Vector2( 1,  1),
                new Vector2(-1,  1)
            };

            quadMesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
            quadMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
        }

        private void Update()
        {
            if (!isLoaded || allSplats == null) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            frameCounter++;

            // Depth sort (CPU) — back to front for proper alpha blending
            if (frameCounter % sortInterval == 0)
            {
                DepthSort(cam);
            }

            // Render batches
            RenderSplats(cam);
        }

        private void DepthSort(Camera cam)
        {
            Vector3 camPos = cam.transform.position;
            Vector3 camFwd = cam.transform.forward;
            var worldToLocal = transform.worldToLocalMatrix;

            // Sort by distance along camera forward (dot product) — back to front
            // Use a simple approach: compute depth for each, then sort indices
            float maxDistSq = maxRenderDistance * maxRenderDistance;
            var depths = new float[allSplats.Length];
            visibleCount = 0;

            for (int i = 0; i < allSplats.Length; i++)
            {
                Vector3 worldPos = transform.TransformPoint(allSplats[i].position);
                Vector3 toCam = worldPos - camPos;
                float distSq = toCam.sqrMagnitude;

                if (distSq > maxDistSq || allSplats[i].color.a < minOpacity)
                {
                    depths[i] = float.MaxValue; // will be culled
                    continue;
                }

                depths[i] = Vector3.Dot(toCam, camFwd); // depth along view direction
                visibleCount++;
            }

            // Sort indices by depth (back to front = descending)
            Array.Sort(sortedIndices, (a, b) => depths[b].CompareTo(depths[a]));

            // Trim to max visible
            visibleCount = Mathf.Min(visibleCount, maxVisibleSplats);
        }

        private void RenderSplats(Camera cam)
        {
            if (visibleCount <= 0) return;

            splatMaterial.SetFloat("_SplatScale", splatScale);
            splatMaterial.SetFloat("_Opacity", globalOpacity);

            // Process in batches of BATCH_SIZE
            int rendered = 0;
            int batchStart = 0;

            while (rendered < visibleCount && batchStart < sortedIndices.Length)
            {
                int batchCount = Mathf.Min(BATCH_SIZE, visibleCount - rendered);
                
                for (int b = 0; b < batchCount; b++)
                {
                    int idx = sortedIndices[batchStart + b];
                    if (idx < 0 || idx >= allSplats.Length) continue;

                    var splat = allSplats[idx];

                    // Identity matrix (position handled in shader via property)
                    instanceMatrices[b] = Matrix4x4.identity;

                    // World position + scale magnitude
                    Vector3 worldPos = transform.TransformPoint(splat.position);
                    float avgScale = (splat.scale.x + splat.scale.y + splat.scale.z) / 3f;
                    instancePositions[b] = new Vector4(worldPos.x, worldPos.y, worldPos.z, avgScale);

                    // Color
                    instanceColors[b] = new Vector4(splat.color.r, splat.color.g, splat.color.b, splat.color.a);

                    // Compute 2D covariance from 3D scale + rotation
                    ComputeCov2D(cam, worldPos, splat.scale, splat.rotation, ref instanceCov2D[b]);
                }

                // Set per-instance properties
                propertyBlock.SetVectorArray("_SplatPosition", instancePositions);
                propertyBlock.SetVectorArray("_SplatColor", instanceColors);
                propertyBlock.SetVectorArray("_SplatCov2D", instanceCov2D);

                // Draw instanced batch
                Graphics.DrawMeshInstanced(
                    quadMesh, 0, splatMaterial,
                    instanceMatrices, batchCount,
                    propertyBlock,
                    UnityEngine.Rendering.ShadowCastingMode.Off,
                    false, // no shadows
                    gameObject.layer,
                    cam
                );

                rendered += batchCount;
                batchStart += batchCount;
            }
        }

        /// <summary>
        /// Project 3D covariance to 2D screen-space covariance.
        /// Returns the upper triangle of the 2x2 covariance matrix: (cov_xx, cov_xy, cov_yy).
        /// </summary>
        private void ComputeCov2D(Camera cam, Vector3 worldPos, Vector3 scale, Quaternion rot, ref Vector4 cov2d)
        {
            // Build 3D covariance: Cov = R * S * S^T * R^T
            Matrix4x4 R = Matrix4x4.Rotate(rot);
            
            // Scale matrix (diagonal)
            float s0 = scale.x, s1 = scale.y, s2 = scale.z;

            // RS = R * S (scale columns of R)
            float rs00 = R.m00 * s0, rs01 = R.m01 * s1, rs02 = R.m02 * s2;
            float rs10 = R.m10 * s0, rs11 = R.m11 * s1, rs12 = R.m12 * s2;
            float rs20 = R.m20 * s0, rs21 = R.m21 * s1, rs22 = R.m22 * s2;

            // Cov3D = RS * RS^T (symmetric, only need 6 values)
            float c00 = rs00 * rs00 + rs01 * rs01 + rs02 * rs02;
            float c01 = rs00 * rs10 + rs01 * rs11 + rs02 * rs12;
            float c02 = rs00 * rs20 + rs01 * rs21 + rs02 * rs22;
            float c11 = rs10 * rs10 + rs11 * rs11 + rs12 * rs12;
            float c12 = rs10 * rs20 + rs11 * rs21 + rs12 * rs22;
            float c22 = rs20 * rs20 + rs21 * rs21 + rs22 * rs22;

            // View-space Jacobian (simplified: focal length projection)
            Matrix4x4 view = cam.worldToCameraMatrix;
            Vector3 viewPos = view.MultiplyPoint3x4(worldPos);

            float focalX = cam.projectionMatrix.m00 * cam.pixelWidth * 0.5f;
            float focalY = cam.projectionMatrix.m11 * cam.pixelHeight * 0.5f;
            
            float invZ = 1f / Mathf.Max(viewPos.z, 0.001f);
            float invZ2 = invZ * invZ;

            // Jacobian of projection
            float j00 = focalX * invZ;
            float j02 = -focalX * viewPos.x * invZ2;
            float j11 = focalY * invZ;
            float j12 = -focalY * viewPos.y * invZ2;

            // JT = J * ViewRotation (3x3 upper-left of view matrix)
            // Simplified: transform cov3D to view space then project
            // T = J * W where W is the 3x3 part of the view matrix
            float w00 = view.m00, w01 = view.m01, w02 = view.m02;
            float w10 = view.m10, w11 = view.m11, w12 = view.m12;
            float w20 = view.m20, w21 = view.m21, w22 = view.m22;

            // T = J * W (2x3 matrix)
            float t00 = j00 * w00 + j02 * w20;
            float t01 = j00 * w01 + j02 * w21;
            float t02 = j00 * w02 + j02 * w22;
            float t10 = j11 * w10 + j12 * w20;
            float t11 = j11 * w11 + j12 * w21;
            float t12 = j11 * w12 + j12 * w22;

            // Cov2D = T * Cov3D * T^T (2x2 symmetric)
            // First: M = T * Cov3D (2x3)
            float m00 = t00 * c00 + t01 * c01 + t02 * c02;
            float m01_v = t00 * c01 + t01 * c11 + t02 * c12;
            float m02_v = t00 * c02 + t01 * c12 + t02 * c22;
            float m10 = t10 * c00 + t11 * c01 + t12 * c02;
            float m11_v = t10 * c01 + t11 * c11 + t12 * c12;
            float m12_v = t10 * c02 + t11 * c12 + t12 * c22;

            // Then: Cov2D = M * T^T (2x2)
            float cov_xx = m00 * t00 + m01_v * t01 + m02_v * t02;
            float cov_xy = m00 * t10 + m01_v * t11 + m02_v * t12;
            float cov_yy = m10 * t10 + m11_v * t11 + m12_v * t12;

            // Add small value to diagonal for numerical stability
            cov_xx += 0.3f;
            cov_yy += 0.3f;

            cov2d = new Vector4(cov_xx, cov_xy, cov_yy, 0);
        }

        private void OnDestroy()
        {
            if (quadMesh != null)
                Destroy(quadMesh);
        }

        // --- Public API ---
        public int SplatCount => allSplats?.Length ?? 0;
        public int VisibleCount => visibleCount;
        public bool IsLoaded => isLoaded;
    }
}
