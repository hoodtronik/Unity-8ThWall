using UnityEngine;
using UnityEditor;
using System.IO;

namespace XR8WebAR.Editor
{
    /// <summary>
    /// Gaussian Splat Importer — drag-and-drop .ply or .splat files.
    /// Automatically:
    ///   1. Copies to Assets/GaussianSplats/ as .bytes
    ///   2. Creates a material with the GaussianSplat shader
    ///   3. Creates a prefab with GaussianSplatRenderer pre-configured
    ///   4. Optionally drops the prefab into the scene
    /// 
    /// Access via: XR8 WebAR > Import Gaussian Splat
    /// </summary>
    public class GaussianSplatImporter : EditorWindow
    {
        private string lastImportPath = "";
        private bool addToScene = true;
        private float splatScale = 1f;
        private int maxSplats = 50000;
        private int sortInterval = 2;
        private float maxDistance = 50f;

        private const string SPLAT_FOLDER = "Assets/GaussianSplats";
        private const string MAT_FOLDER = "Assets/GaussianSplats/Materials";
        private const string PREFAB_FOLDER = "Assets/GaussianSplats/Prefabs";

        [MenuItem("XR8 WebAR/Import Gaussian Splat", false, 100)]
        public static void ShowWindow()
        {
            var win = GetWindow<GaussianSplatImporter>("Splat Importer");
            win.minSize = new Vector2(380, 340);
        }

        private void OnGUI()
        {
            // Header
            EditorGUILayout.Space(8);
            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16 };
            EditorGUILayout.LabelField("🔮 Gaussian Splat Importer", headerStyle);
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Import .ply or .splat files as ready-to-use prefabs.\n" +
                "Supports standard 3DGS and Mobile-GS compressed output.",
                MessageType.Info);
            EditorGUILayout.Space(8);

            // Settings
            EditorGUILayout.LabelField("Import Settings", EditorStyles.boldLabel);
            splatScale = EditorGUILayout.Slider("Splat Scale", splatScale, 0.01f, 5f);
            maxSplats = EditorGUILayout.IntSlider("Max Visible Splats", maxSplats, 1000, 500000);
            sortInterval = EditorGUILayout.IntSlider("Sort Interval (frames)", sortInterval, 1, 10);
            maxDistance = EditorGUILayout.Slider("Max Render Distance", maxDistance, 5f, 200f);
            addToScene = EditorGUILayout.Toggle("Add to Scene on Import", addToScene);

            EditorGUILayout.Space(12);

            // Import button
            if (GUILayout.Button("📂  Browse & Import .ply / .splat", GUILayout.Height(36)))
            {
                string path = EditorUtility.OpenFilePanel(
                    "Select Gaussian Splat File",
                    lastImportPath.Length > 0 ? Path.GetDirectoryName(lastImportPath) : "",
                    "ply,splat,bytes");

                if (!string.IsNullOrEmpty(path))
                {
                    lastImportPath = path;
                    ImportSplatFile(path);
                }
            }

            EditorGUILayout.Space(4);

            // Drag-drop zone
            var dropArea = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "— or drag & drop .ply / .splat files here —", EditorStyles.helpBox);
            HandleDragDrop(dropArea);

            EditorGUILayout.Space(8);

            // Quick info
            if (lastImportPath.Length > 0)
            {
                EditorGUILayout.LabelField("Last Import", EditorStyles.miniLabel);
                EditorGUILayout.SelectableLabel(Path.GetFileName(lastImportPath),
                    EditorStyles.miniTextField, GUILayout.Height(18));
            }
        }

        private void HandleDragDrop(Rect dropArea)
        {
            Event evt = Event.current;
            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform) return;
            if (!dropArea.Contains(evt.mousePosition)) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (string path in DragAndDrop.paths)
                {
                    string ext = Path.GetExtension(path).ToLower();
                    if (ext == ".ply" || ext == ".splat" || ext == ".bytes")
                    {
                        ImportSplatFile(path);
                    }
                }
            }

            evt.Use();
        }

        private void ImportSplatFile(string sourcePath)
        {
            string fileName = Path.GetFileNameWithoutExtension(sourcePath);
            string ext = Path.GetExtension(sourcePath).ToLower();

            // Sanitize name
            fileName = fileName.Replace(" ", "_").Replace("-", "_");
            if (string.IsNullOrEmpty(fileName)) fileName = "imported_splat";

            EditorUtility.DisplayProgressBar("Importing Gaussian Splat", "Creating folders...", 0.1f);

            try
            {
                // 1. Ensure folders exist
                EnsureFolder(SPLAT_FOLDER);
                EnsureFolder(MAT_FOLDER);
                EnsureFolder(PREFAB_FOLDER);

                // 2. Copy file as .bytes (TextAsset)
                string bytesPath = SPLAT_FOLDER + "/" + fileName + ".bytes";
                EditorUtility.DisplayProgressBar("Importing Gaussian Splat", "Copying splat data...", 0.3f);

                File.Copy(sourcePath, Path.GetFullPath(bytesPath), true);
                AssetDatabase.ImportAsset(bytesPath, ImportAssetOptions.ForceUpdate);

                TextAsset splatAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(bytesPath);
                if (splatAsset == null)
                {
                    Debug.LogError("[SplatImporter] Failed to import " + bytesPath + " as TextAsset!");
                    return;
                }

                // 3. Create material
                EditorUtility.DisplayProgressBar("Importing Gaussian Splat", "Creating material...", 0.5f);

                string matPath = MAT_FOLDER + "/" + fileName + "_Mat.mat";
                Shader splatShader = Shader.Find("XR8WebAR/GaussianSplat");
                if (splatShader == null)
                {
                    Debug.LogError("[SplatImporter] GaussianSplat shader not found! Make sure it's compiled.");
                    return;
                }

                Material mat = new Material(splatShader);
                mat.SetFloat("_SplatScale", splatScale);
                mat.SetFloat("_Opacity", 1f);
                mat.SetFloat("_CutoffAlpha", 0.02f);
                mat.enableInstancing = true;
                AssetDatabase.CreateAsset(mat, matPath);

                // 4. Create prefab
                EditorUtility.DisplayProgressBar("Importing Gaussian Splat", "Creating prefab...", 0.7f);

                string prefabPath = PREFAB_FOLDER + "/" + fileName + ".prefab";

                // Build temporary GameObject
                var go = new GameObject(fileName);
                go.AddComponent<MeshFilter>();
                go.AddComponent<MeshRenderer>();

                var renderer = go.AddComponent<GaussianSplat.GaussianSplatRenderer>();

                // Set serialized fields via SerializedObject
                var tempPrefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                DestroyImmediate(go);

                // Now set the serialized references on the prefab asset
                var so = new SerializedObject(tempPrefab.GetComponent<GaussianSplat.GaussianSplatRenderer>());
                
                var splatAssetProp = so.FindProperty("splatAsset");
                if (splatAssetProp != null) splatAssetProp.objectReferenceValue = splatAsset;
                
                var matProp = so.FindProperty("splatMaterial");
                if (matProp != null) matProp.objectReferenceValue = mat;

                var scaleProp = so.FindProperty("splatScale");
                if (scaleProp != null) scaleProp.floatValue = splatScale;

                var maxProp = so.FindProperty("maxVisibleSplats");
                if (maxProp != null) maxProp.intValue = maxSplats;

                var sortProp = so.FindProperty("sortInterval");
                if (sortProp != null) sortProp.intValue = sortInterval;

                var distProp = so.FindProperty("maxRenderDistance");
                if (distProp != null) distProp.floatValue = maxDistance;

                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();

                EditorUtility.DisplayProgressBar("Importing Gaussian Splat", "Finalizing...", 0.9f);

                // 5. Optionally add to scene
                if (addToScene)
                {
                    var instance = PrefabUtility.InstantiatePrefab(tempPrefab) as GameObject;
                    if (instance != null)
                    {
                        instance.transform.position = Vector3.zero;
                        Undo.RegisterCreatedObjectUndo(instance, "Import Gaussian Splat");
                        Selection.activeGameObject = instance;
                    }
                }

                AssetDatabase.Refresh();

                // Log file size
                var fileInfo = new FileInfo(sourcePath);
                string sizeStr = fileInfo.Length > 1048576
                    ? (fileInfo.Length / 1048576f).ToString("F1") + " MB"
                    : (fileInfo.Length / 1024f).ToString("F1") + " KB";

                Debug.Log("[SplatImporter] ✅ Imported '" + fileName + "' (" + sizeStr + ")\n" +
                    "  Data: " + bytesPath + "\n" +
                    "  Material: " + matPath + "\n" +
                    "  Prefab: " + prefabPath);

                EditorUtility.DisplayDialog("Gaussian Splat Imported! 🎉",
                    "Successfully imported '" + fileName + "' (" + sizeStr + ")\n\n" +
                    "Created:\n" +
                    "• Splat data: " + bytesPath + "\n" +
                    "• Material: " + matPath + "\n" +
                    "• Prefab: " + prefabPath + "\n\n" +
                    (addToScene ? "The prefab has been added to your scene." : "Drag the prefab into your scene to use it."),
                    "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            string folder = Path.GetFileName(path);

            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
