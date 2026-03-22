using System.IO;
using UnityEngine;
using UnityEditor;

namespace XR8WebAR.Editor
{
    /// <summary>
    /// Image Target Factory — creates Image Target prefabs from selected images.
    /// 
    /// Workflow (matches Imaginary Labs' approach):
    ///   1. Import your image into Unity.
    ///   2. Select the image in the Project window.
    ///   3. Go to Assets > XR8 WebAR > Create > Image Target
    ///   4. A target prefab is created (visible quad with the image).
    ///   5. Prefab is auto-added to the scene's Image Tracker.
    ///   6. Drag your 3D content as children of the target.
    ///
    /// The created Image Target prefab contains:
    ///   - A visible quad mesh showing the target image (XZ plane)
    ///   - A MeshRenderer with an Unlit/Transparent material
    ///   - A child "_Content" empty for your AR content
    ///   - Tagged "EditorOnly" so it's stripped from builds
    ///
    /// Access via: Assets > XR8 WebAR > Create > Image Target
    /// Context menu: Right-click image > XR8 WebAR > Create Image Target
    /// </summary>
    public static class XR8ImageTargetFactory
    {
        private const string PrefabFolder = "Assets/XR8WebAR/ImageTargets";
        private const string MaterialFolder = "Assets/XR8WebAR/Editor/TargetPlaneMaterials";

        // =====================================================================
        // MENU ITEMS
        // =====================================================================

        [MenuItem("Assets/XR8 WebAR/Create/Image Target", false, 100)]
        public static void CreateImageTarget()
        {
            var selected = Selection.activeObject;

            if (selected == null || !(selected is Texture2D))
            {
                EditorUtility.DisplayDialog("No Image Selected",
                    "Select an image (PNG/JPG) in the Project window,\n" +
                    "then go to Assets > XR8 WebAR > Create > Image Target.\n\n" +
                    "The image will become the target that triggers your AR content.",
                    "OK");
                return;
            }

            var texture = (Texture2D)selected;
            string imagePath = AssetDatabase.GetAssetPath(texture);
            string targetId = DeriveTargetId(Path.GetFileNameWithoutExtension(imagePath));

            CreateImageTargetFromTexture(texture, targetId, imagePath);
        }

        [MenuItem("Assets/XR8 WebAR/Create/Image Target", true)]
        public static bool ValidateCreateImageTarget()
        {
            return Selection.activeObject is Texture2D;
        }

        /// <summary>
        /// Alternative entry point: create from code with just a texture.
        /// </summary>
        public static GameObject CreateImageTargetFromTexture(Texture2D texture, string targetId, string imagePath = null)
        {
            if (string.IsNullOrEmpty(imagePath))
                imagePath = AssetDatabase.GetAssetPath(texture);

            // Ensure prefab folder exists
            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);

            // --- 1. CREATE THE TARGET PLANE GAMEOBJECT ---
            var targetGO = new GameObject(targetId + "_ImageTarget");
            targetGO.tag = "EditorOnly";

            // Mesh (flat quad on XZ plane)
            var mf = targetGO.AddComponent<MeshFilter>();
            var mr = targetGO.AddComponent<MeshRenderer>();

            float aspect = (float)texture.width / texture.height;
            float size = 0.15f;
            var mesh = CreateTargetMesh(targetId, size, aspect);
            mf.sharedMesh = mesh;

            // Save mesh as asset
            string meshPath = PrefabFolder + "/" + targetId + "_Mesh.asset";
            AssetDatabase.CreateAsset(mesh, meshPath);

            // Material with the image
            var mat = CreateTargetMaterial(texture, targetId);
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            // --- 2. CREATE CONTENT CHILD ---
            var contentGO = new GameObject(targetId + "_Content");
            contentGO.transform.SetParent(targetGO.transform);
            contentGO.transform.localPosition = Vector3.zero;

            // --- 3. SAVE AS PREFAB ---
            string prefabPath = PrefabFolder + "/" + targetId + "_ImageTarget.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
                targetGO, prefabPath, InteractionMode.UserAction);

            Debug.Log("[XR8] ✅ Created Image Target prefab: " + prefabPath);

            // --- 4. AUTO-WIRE INTO SCENE TRACKER ---
            bool wired = TryWireIntoSceneTracker(targetGO, targetId, contentGO.transform);

            // --- 5. SELECT AND FRAME ---
            Selection.activeGameObject = targetGO;
            SceneView.FrameLastActiveSceneView();

            // --- 6. COPY SOURCE IMAGE TO image-targets FOLDER ---
            CopyToImageTargetsFolder(imagePath, targetId);

            // --- 7. SHOW RESULT ---
            string msg = $"Image Target '{targetId}' created successfully!\n\n";
            if (wired)
            {
                msg += "✅ Auto-wired into your scene's XR8ImageTracker.\n\n";
            }
            else
            {
                msg += "⚠ No XR8ImageTracker found in scene.\n" +
                       "Create one (GameObject > XR8 WebAR > Image Tracker)\n" +
                       "and drag this prefab into the scene.\n\n";
            }
            msg += "Next steps:\n" +
                   "• Drag 3D content as children of " + targetId + "_Content\n" +
                   "• Adjust the Image Target's rotation/scale to match alignment\n" +
                   "• The target plane is for editor visualization only (stripped from builds)";

            EditorUtility.DisplayDialog("Image Target Created! 🎯", msg, "Got it!");
            return targetGO;
        }

        // =====================================================================
        // CORE HELPERS
        // =====================================================================

        /// <summary>Creates a flat XZ quad mesh for the image target.</summary>
        private static Mesh CreateTargetMesh(string name, float size, float aspect)
        {
            var mesh = new Mesh { name = name + "_TargetMesh" };
            mesh.vertices = new Vector3[]
            {
                new Vector3(-size * aspect, 0, -size),
                new Vector3( size * aspect, 0, -size),
                new Vector3( size * aspect, 0,  size),
                new Vector3(-size * aspect, 0,  size)
            };
            mesh.uv = new Vector2[]
            {
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(1, 1), new Vector2(0, 1)
            };
            mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Creates and saves a material with the target image texture.</summary>
        private static Material CreateTargetMaterial(Texture2D texture, string targetId)
        {
            var shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            mat.mainTexture = texture;
            mat.color = new Color(1, 1, 1, 0.85f);
            mat.name = targetId + "_TargetMat";

            string matPath = MaterialFolder + "/" + mat.name + ".mat";

            // Check if material already exists and overwrite
            var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing != null)
                AssetDatabase.DeleteAsset(matPath);

            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
        }

        /// <summary>
        /// Tries to find an XR8ImageTracker in the scene and wire the new target into it.
        /// Returns true if successfully wired.
        /// </summary>
        private static bool TryWireIntoSceneTracker(GameObject targetGO, string targetId, Transform contentRoot)
        {
            var tracker = Object.FindFirstObjectByType<XR8ImageTracker>();
            if (tracker == null) return false;

            // Parent the target under the tracker
            targetGO.transform.SetParent(tracker.transform, false);
            targetGO.transform.localPosition = Vector3.zero;

            // Add to the imageTargets array via SerializedObject
            var so = new SerializedObject(tracker);
            var targetsProp = so.FindProperty("imageTargets");

            // Check if this target ID already exists
            for (int i = 0; i < targetsProp.arraySize; i++)
            {
                var elem = targetsProp.GetArrayElementAtIndex(i);
                if (elem.FindPropertyRelative("id").stringValue == targetId)
                {
                    // Update existing entry
                    elem.FindPropertyRelative("anchor").objectReferenceValue = targetGO.transform;
                    elem.FindPropertyRelative("transform").objectReferenceValue = contentRoot;
                    so.ApplyModifiedProperties();
                    Debug.Log("[XR8] Updated existing target entry: " + targetId);
                    return true;
                }
            }

            // Add new entry
            int newIndex = targetsProp.arraySize;
            targetsProp.InsertArrayElementAtIndex(newIndex);
            var newElem = targetsProp.GetArrayElementAtIndex(newIndex);
            newElem.FindPropertyRelative("id").stringValue = targetId;
            newElem.FindPropertyRelative("anchor").objectReferenceValue = targetGO.transform;
            newElem.FindPropertyRelative("transform").objectReferenceValue = contentRoot;
            so.ApplyModifiedProperties();

            Debug.Log("[XR8] Auto-wired '" + targetId + "' into XR8ImageTracker");
            return true;
        }

        /// <summary>
        /// Copies the source image to Assets/image-targets/ for the build pipeline.
        /// The build process exports images from this folder to the WebGL output.
        /// </summary>
        private static void CopyToImageTargetsFolder(string sourcePath, string targetId)
        {
            if (string.IsNullOrEmpty(sourcePath)) return;

            string destFolder = "Assets/image-targets";
            EnsureFolder(destFolder);

            string ext = Path.GetExtension(sourcePath);
            string destPath = destFolder + "/" + targetId + "_original" + ext;

            // Don't overwrite if it already exists at the destination
            if (File.Exists(destPath)) return;

            // Don't copy if source is already in image-targets
            if (sourcePath.Replace("\\", "/").Contains("image-targets/")) return;

            AssetDatabase.CopyAsset(sourcePath, destPath);
            Debug.Log("[XR8] Copied target image to: " + destPath);
        }

        /// <summary>Derives a clean target ID from the filename.</summary>
        private static string DeriveTargetId(string fileName)
        {
            // Strip common suffixes
            string[] suffixes = { "_original", "_luminance", "_thumb", "_target", "-original", "-luminance", "-thumb" };
            string name = fileName;
            foreach (var suffix in suffixes)
            {
                if (name.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(0, name.Length - suffix.Length);
                    break;
                }
            }

            // Replace spaces/underscores with hyphens for consistency
            name = name.Replace(' ', '-').ToLowerInvariant();
            return name;
        }

        /// <summary>Ensures a folder path exists, creating intermediate folders as needed.</summary>
        private static void EnsureFolder(string path)
        {
            string[] parts = path.Replace("\\", "/").Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
