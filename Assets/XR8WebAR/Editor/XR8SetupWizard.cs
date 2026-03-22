using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

namespace XR8WebAR.Editor
{
    /// <summary>
    /// XR8 Quick Setup Wizard — one-window setup for 8th Wall AR in Unity.
    /// 
    /// Replaces the multi-step manual process with a guided flow:
    ///   1. Choose tracking mode (Image, World, Face, or combo)
    ///   2. Auto-creates and wires all required GameObjects
    ///   3. Configures build settings (WebGL template, player settings)
    ///   4. Validates everything before build
    /// 
    /// Access via: XR8 WebAR > Quick Setup Wizard
    /// </summary>
    public class XR8SetupWizard : EditorWindow
    {
        // Setup options
        private bool setupImage = true;
        private bool setupWorld = false;
        private bool setupFace = false;
        private DesktopPreviewMode setupPreviewMode = DesktopPreviewMode.Static;
        private string firstTargetId = "my-target";
        private int maxSimTargets = 1;
        private bool createSampleContent = true;

        // Build options
        private bool autoConfigureBuild = true;

        // State
        private int currentTab = 0;
        private Vector2 scrollPos;

        private static readonly string[] tabs = { "🎯 Setup", "🔧 Validate", "📦 Build" };

        [MenuItem("XR8 WebAR/Quick Setup Wizard", false, 0)]
        public static void ShowWindow()
        {
            var win = GetWindow<XR8SetupWizard>("XR8 Setup Wizard");
            win.minSize = new Vector2(420, 500);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            EditorGUILayout.LabelField("XR8 WebAR Setup Wizard", titleStyle);
            EditorGUILayout.Space(4);

            currentTab = GUILayout.Toolbar(currentTab, tabs, GUILayout.Height(28));
            EditorGUILayout.Space(8);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            switch (currentTab)
            {
                case 0: DrawSetupTab(); break;
                case 1: DrawValidateTab(); break;
                case 2: DrawBuildTab(); break;
            }

            EditorGUILayout.EndScrollView();
        }

        // =================================================================
        // SETUP TAB
        // =================================================================
        private void DrawSetupTab()
        {
            EditorGUILayout.LabelField("Tracking Modes", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Choose which tracking modes to enable.\n" +
                "The wizard will create and wire all required components.",
                MessageType.Info);
            EditorGUILayout.Space(4);

            setupImage = EditorGUILayout.ToggleLeft("🖼  Image Tracking (markers, posters)", setupImage);
            if (setupImage)
            {
                EditorGUI.indentLevel++;
                firstTargetId = EditorGUILayout.TextField("First Target ID", firstTargetId);
                maxSimTargets = EditorGUILayout.IntSlider("Max Simultaneous Targets", maxSimTargets, 1, 5);
                createSampleContent = EditorGUILayout.Toggle("Create Sample Content", createSampleContent);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);
            setupWorld = EditorGUILayout.ToggleLeft("🌍  World Tracking (SLAM, surfaces)", setupWorld);
            EditorGUILayout.Space(4);
            setupFace = EditorGUILayout.ToggleLeft("😊  Face Tracking (filters, effects)", setupFace);

            // Show combined tracking hint
            if (setupImage && setupWorld)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "✨ Combined Tracking enabled!\n" +
                    "Image + World = floor detection beneath tracked images.\n" +
                    "Perfect for gallery/museum AR experiences.",
                    MessageType.Info);
            }

            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
            setupPreviewMode = (DesktopPreviewMode)EditorGUILayout.EnumPopup("Desktop Preview Mode", setupPreviewMode);
            autoConfigureBuild = EditorGUILayout.ToggleLeft("Auto-configure WebGL build settings", autoConfigureBuild);

            EditorGUILayout.Space(16);

            // Big setup button
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
            if (GUILayout.Button("⚡  Run Setup", GUILayout.Height(40)))
            {
                RunSetup();
            }
            GUI.backgroundColor = Color.white;
        }

        private void RunSetup()
        {
            // Check for existing setup
            var existing = FindFirstObjectByType<XR8Manager>();
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("XR8 Setup",
                    "XR8 components already exist in the scene.\nReplace them?",
                    "Replace", "Cancel"))
                    return;

                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            EditorUtility.DisplayProgressBar("XR8 Setup", "Creating components...", 0.1f);

            try
            {
                // 1. Camera
                var camObj = SetupCamera();

                // 2. XR8Manager
                var managerObj = new GameObject("XR8Manager");
                Undo.RegisterCreatedObjectUndo(managerObj, "XR8 Setup");
                var manager = managerObj.AddComponent<XR8Manager>();

                // Wire manager fields via SerializedObject
                var so = new SerializedObject(manager);
                so.FindProperty("arCamera").objectReferenceValue = camObj.GetComponent<Camera>();
                so.FindProperty("xr8CameraComponent").objectReferenceValue = camObj.GetComponent<XR8Camera>();
                so.FindProperty("enableImageTracking").boolValue = setupImage;
                so.FindProperty("enableWorldTracking").boolValue = setupWorld;
                so.FindProperty("enableFaceTracking").boolValue = setupFace;
                so.FindProperty("previewMode").enumValueIndex = (int)setupPreviewMode;

                EditorUtility.DisplayProgressBar("XR8 Setup", "Setting up trackers...", 0.4f);

                // 3. Image Tracker
                if (setupImage)
                {
                    var trackerObj = SetupImageTracker(camObj);
                    so.FindProperty("imageTracker").objectReferenceValue = trackerObj.GetComponent<XR8ImageTracker>();
                }

                // 4. World Tracker
                if (setupWorld)
                {
                    var worldObj = new GameObject("XR8WorldTracker");
                    Undo.RegisterCreatedObjectUndo(worldObj, "XR8 Setup World");
                    worldObj.AddComponent<XR8WorldTracker>();
                    so.FindProperty("worldTracker").objectReferenceValue = worldObj.GetComponent<XR8WorldTracker>();
                }

                // 5. Face Tracker
                if (setupFace)
                {
                    var faceObj = SetupFaceTracker();
                    so.FindProperty("faceTracker").objectReferenceValue = faceObj.GetComponent<XR8FaceTracker>();
                }

                // 6. Combined Tracker (when Image + World both on)
                if (setupImage && setupWorld)
                {
                    var combinedObj = new GameObject("XR8CombinedTracker");
                    Undo.RegisterCreatedObjectUndo(combinedObj, "XR8 Setup Combined");
                    combinedObj.AddComponent<XR8CombinedTracker>();
                    so.FindProperty("combinedTracker").objectReferenceValue = combinedObj.GetComponent<XR8CombinedTracker>();
                }

                so.ApplyModifiedProperties();

                EditorUtility.DisplayProgressBar("XR8 Setup", "Configuring build...", 0.7f);

                // 5. Build settings
                if (autoConfigureBuild)
                {
                    ConfigureBuildSettings();
                }

                EditorUtility.DisplayProgressBar("XR8 Setup", "Saving...", 0.9f);

                // Save scene
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Selection.activeGameObject = managerObj;

                EditorUtility.DisplayDialog("XR8 Setup Complete! 🎉",
                    "Created:\n" +
                    "• Main Camera (with XR8Camera)\n" +
                    "• XR8Manager (all references wired)\n" +
                    (setupImage ? "• XR8ImageTracker (target: '" + firstTargetId + "')\n" : "") +
                    (setupWorld ? "• XR8WorldTracker (surface detection)\n" : "") +
                    (setupImage && setupWorld ? "• XR8CombinedTracker (image-to-floor)\n" : "") +
                    (setupFace ? "• XR8FaceTracker\n" : "") +
                    (setupPreviewMode != DesktopPreviewMode.None ? "• Desktop Preview: " + setupPreviewMode + "\n" : "") +
                    (autoConfigureBuild ? "• WebGL build settings configured\n" : "") +
                    "\nAll components are auto-wired. Just hit Play to preview!",
                    "OK");

                Debug.Log("[XR8 Setup Wizard] ✅ Setup complete — all components created and wired");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private GameObject SetupCamera()
        {
            // Remove existing MainCamera if present  
            var existingCam = GameObject.FindGameObjectWithTag("MainCamera");
            if (existingCam != null)
            {
                Undo.DestroyObjectImmediate(existingCam);
            }

            var camObj = new GameObject("Main Camera");
            Undo.RegisterCreatedObjectUndo(camObj, "XR8 Setup Camera");
            camObj.tag = "MainCamera";

            var cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;

            camObj.AddComponent<AudioListener>();
            camObj.AddComponent<XR8Camera>();

            return camObj;
        }

        private GameObject SetupImageTracker(GameObject camObj)
        {
            var trackerObj = new GameObject("XR8ImageTracker");
            Undo.RegisterCreatedObjectUndo(trackerObj, "XR8 Setup Tracker");
            var tracker = trackerObj.AddComponent<XR8ImageTracker>();

            // Wire camera reference
            var trackerSo = new SerializedObject(tracker);
            trackerSo.FindProperty("trackerCam").objectReferenceValue = camObj.GetComponent<XR8Camera>();

            // Set maxSimultaneousTargets
            var settingsProp = trackerSo.FindProperty("trackerSettings");
            if (settingsProp != null)
            {
                var maxTargetsProp = settingsProp.FindPropertyRelative("maxSimultaneousTargets");
                if (maxTargetsProp != null) maxTargetsProp.intValue = maxSimTargets;
            }

            // Create first image target with content
            if (!string.IsNullOrEmpty(firstTargetId))
            {
                // Create target plane (anchor) — visible image plane
                var targetPlane = new GameObject(firstTargetId + "_TargetPlane");
                targetPlane.transform.SetParent(trackerObj.transform);
                targetPlane.transform.localPosition = Vector3.zero;
                targetPlane.tag = "EditorOnly";

                // Add quad mesh visualization
                var mf = targetPlane.AddComponent<MeshFilter>();
                var mr = targetPlane.AddComponent<MeshRenderer>();

                var quadMesh = CreateSetupTargetQuad(firstTargetId);
                mf.sharedMesh = quadMesh;

                var planeMat = CreateSetupPlaneMaterial(firstTargetId);
                mr.sharedMaterial = planeMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                Undo.RegisterCreatedObjectUndo(targetPlane, "XR8 Setup Target Plane");

                // Create content GameObject (child of target plane)
                GameObject contentObj;
                if (createSampleContent)
                {
                    contentObj = new GameObject(firstTargetId + "_Content");
                    contentObj.transform.SetParent(targetPlane.transform);
                    contentObj.transform.localPosition = Vector3.zero;

                    // Add a visible quad
                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    quad.name = "Overlay";
                    quad.transform.SetParent(contentObj.transform);
                    quad.transform.localPosition = Vector3.zero;
                    quad.transform.localRotation = Quaternion.Euler(90f, 0, 0);
                    quad.transform.localScale = new Vector3(0.3f, 0.17f, 1f);
                    var col = quad.GetComponent<Collider>();
                    if (col != null) DestroyImmediate(col);

                    Undo.RegisterCreatedObjectUndo(contentObj, "XR8 Setup Content");
                }
                else
                {
                    contentObj = new GameObject(firstTargetId + "_Content");
                    contentObj.transform.SetParent(targetPlane.transform);
                    contentObj.transform.localPosition = Vector3.zero;
                    Undo.RegisterCreatedObjectUndo(contentObj, "XR8 Setup Content");
                }

                // Add to imageTargets array
                var targetsProp = trackerSo.FindProperty("imageTargets");
                if (targetsProp != null)
                {
                    // Clear existing
                    targetsProp.ClearArray();
                    targetsProp.InsertArrayElementAtIndex(0);
                    var elem = targetsProp.GetArrayElementAtIndex(0);
                    elem.FindPropertyRelative("id").stringValue = firstTargetId;
                    elem.FindPropertyRelative("anchor").objectReferenceValue = targetPlane.transform;
                    elem.FindPropertyRelative("transform").objectReferenceValue = contentObj.transform;
                }
            }

            trackerSo.ApplyModifiedProperties();
            return trackerObj;
        }

        /// <summary>Creates a flat XZ quad mesh for setup wizard target plane.</summary>
        private Mesh CreateSetupTargetQuad(string targetName)
        {
            float size = 0.15f;
            float aspect = 1f;
            var tex = FindSetupTargetImage(targetName);
            if (tex != null) aspect = (float)tex.width / tex.height;

            var mesh = new Mesh { name = targetName + "_PlaneQuad" };
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
            return mesh;
        }

        /// <summary>Creates and saves a material with the target image.</summary>
        private Material CreateSetupPlaneMaterial(string targetName)
        {
            var tex = FindSetupTargetImage(targetName);
            var shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            if (tex != null)
            {
                mat.mainTexture = tex;
                mat.color = new Color(1, 1, 1, 0.8f);
            }
            else
            {
                mat.color = new Color(0, 0.9f, 0.4f, 0.3f);
            }
            mat.name = targetName + "_PlaneMat";

            string matFolder = "Assets/XR8WebAR/Editor/TargetPlaneMaterials";
            if (!AssetDatabase.IsValidFolder(matFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/XR8WebAR/Editor"))
                    AssetDatabase.CreateFolder("Assets/XR8WebAR", "Editor");
                AssetDatabase.CreateFolder("Assets/XR8WebAR/Editor", "TargetPlaneMaterials");
            }
            AssetDatabase.CreateAsset(mat, matFolder + "/" + mat.name + ".mat");
            return mat;
        }

        /// <summary>Finds target image texture from project.</summary>
        private Texture2D FindSetupTargetImage(string targetId)
        {
            string[] dirs = { "Assets/image-targets", "Assets/ImageTargets",
                "Assets/StreamingAssets/image-targets", "Assets/XR8WebAR/Targets" };
            string[] suffixes = { "_original", "_luminance", "", "_thumb" };
            string[] exts = { ".jpg", ".png", ".jpeg" };
            foreach (var dir in dirs)
            {
                if (!System.IO.Directory.Exists(dir)) continue;
                foreach (var suffix in suffixes)
                    foreach (var ext in exts)
                    {
                        string path = System.IO.Path.Combine(dir, targetId + suffix + ext).Replace("\\", "/");
                        var found = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                        if (found != null) return found;
                    }
            }
            return null;
        }

        private GameObject SetupFaceTracker()
        {
            var faceObj = new GameObject("XR8FaceTracker");
            Undo.RegisterCreatedObjectUndo(faceObj, "XR8 Setup Face");
            var faceTracker = faceObj.AddComponent<XR8FaceTracker>();

            // Enable desktop preview for face tracker too
            if (setupPreviewMode != DesktopPreviewMode.None)
            {
                var faceSo = new SerializedObject(faceTracker);
                var previewProp = faceSo.FindProperty("enableDesktopPreview");
                if (previewProp != null) previewProp.boolValue = true;
                faceSo.ApplyModifiedProperties();
            }

            return faceObj;
        }

        private void ConfigureBuildSettings()
        {
            // Set WebGL as the build target (doesn't switch, just verifies)
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                Debug.Log("[XR8 Setup] Note: Active build target is not WebGL. " +
                    "Switch via File > Build Settings > WebGL > Switch Platform");
            }

            // Set the WebGL template
            PlayerSettings.WebGL.template = "PROJECT:8thWallTracker";

            // Recommended WebGL settings
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled; // For dev
            PlayerSettings.WebGL.dataCaching = false;
            PlayerSettings.WebGL.decompressionFallback = true;

            // Color space
            PlayerSettings.colorSpace = ColorSpace.Gamma; // WebGL works best with Gamma

            Debug.Log("[XR8 Setup] WebGL build settings configured (template: 8thWallTracker)");
        }

        // =================================================================
        // VALIDATE TAB
        // =================================================================
        private void DrawValidateTab()
        {
            EditorGUILayout.LabelField("Scene Validation", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Check that everything is wired correctly before building.",
                MessageType.Info);
            EditorGUILayout.Space(8);

            var manager = FindFirstObjectByType<XR8Manager>();
            DrawCheck("XR8Manager exists", manager != null);

            if (manager != null)
            {
                var so = new SerializedObject(manager);

                // Camera
                var cam = so.FindProperty("arCamera").objectReferenceValue;
                DrawCheck("AR Camera assigned", cam != null);

                var xr8Cam = so.FindProperty("xr8CameraComponent").objectReferenceValue;
                DrawCheck("XR8Camera component assigned", xr8Cam != null);

                // Image tracking
                bool imageEnabled = so.FindProperty("enableImageTracking").boolValue;
                if (imageEnabled)
                {
                    var tracker = so.FindProperty("imageTracker").objectReferenceValue;
                    DrawCheck("Image Tracker assigned", tracker != null);

                    if (tracker != null)
                    {
                        var trackerSo = new SerializedObject(tracker as XR8ImageTracker);
                        var targets = trackerSo.FindProperty("imageTargets");
                        int validTargets = 0;
                        for (int i = 0; i < targets.arraySize; i++)
                        {
                            var elem = targets.GetArrayElementAtIndex(i);
                            var id = elem.FindPropertyRelative("id").stringValue;
                            var tf = elem.FindPropertyRelative("transform").objectReferenceValue;
                            if (!string.IsNullOrEmpty(id) && tf != null) validTargets++;
                        }
                        DrawCheck("Image targets configured (" + validTargets + ")", validTargets > 0);
                    }
                }

                // Face tracking
                bool faceEnabled = so.FindProperty("enableFaceTracking").boolValue;
                if (faceEnabled)
                {
                    var faceTracker = so.FindProperty("faceTracker").objectReferenceValue;
                    DrawCheck("Face Tracker assigned", faceTracker != null);
                }

                // World tracking
                bool worldEnabled = so.FindProperty("enableWorldTracking").boolValue;
                if (worldEnabled)
                {
                    var worldTracker = so.FindProperty("worldTracker").objectReferenceValue;
                    DrawCheck("World Tracker assigned", worldTracker != null);
                }

                // Combined tracking
                if (imageEnabled && worldEnabled)
                {
                    var combined = so.FindProperty("combinedTracker").objectReferenceValue;
                    DrawCheck("Combined Tracker assigned", combined != null);
                }
            }

            // WebGL template
            string template = PlayerSettings.WebGL.template;
            DrawCheck("WebGL template set to 8thWallTracker", template.Contains("8thWallTracker"));

            // WebGL template files exist
            bool templateExists = Directory.Exists(Path.Combine(Application.dataPath, "WebGLTemplates/8thWallTracker"));
            DrawCheck("8thWallTracker template files exist", templateExists);

            // Check bridge JS
            bool bridgeExists = File.Exists(Path.Combine(Application.dataPath, "WebGLTemplates/8thWallTracker/xr8-bridge.js"));
            DrawCheck("xr8-bridge.js exists", bridgeExists);

            // Build target
            DrawCheck("Build target is WebGL", EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL);

            EditorGUILayout.Space(12);
            if (GUILayout.Button("🔄 Re-check", GUILayout.Height(30)))
            {
                Repaint();
            }
        }

        private void DrawCheck(string label, bool passed)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(passed ? "✅" : "❌", GUILayout.Width(24));
            var style = new GUIStyle(EditorStyles.label);
            if (!passed) style.normal.textColor = new Color(1f, 0.4f, 0.3f);
            EditorGUILayout.LabelField(label, style);
            EditorGUILayout.EndHorizontal();
        }

        // =================================================================
        // BUILD TAB
        // =================================================================
        private void DrawBuildTab()
        {
            EditorGUILayout.LabelField("Build & Deploy", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Build your WebGL project and test on a phone.\n" +
                "8th Wall requires HTTPS — use 'npx serve' for local testing.",
                MessageType.Info);
            EditorGUILayout.Space(8);

            if (GUILayout.Button("📦  Build WebGL", GUILayout.Height(36)))
            {
                WebGLBuilder.BuildWebGL();
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "After building, serve locally with:\n" +
                "  cd <project>/Build\n" +
                "  npx serve .\n\n" +
                "Then open the URL on your phone (same WiFi network).\n" +
                "For production, deploy to any HTTPS host.",
                MessageType.None);

            EditorGUILayout.Space(12);

            if (GUILayout.Button("🗂  Open Build Folder", GUILayout.Height(28)))
            {
                string buildPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Build");
                if (Directory.Exists(buildPath))
                    EditorUtility.RevealInFinder(buildPath);
                else
                    EditorUtility.DisplayDialog("Build Folder", "No Build folder found. Run a build first!", "OK");
            }
        }
    }
}
