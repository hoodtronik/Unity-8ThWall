using UnityEngine;
using UnityEngine.Video;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

namespace XR8WebAR.Editor
{
    /// <summary>
    /// Image → Video Quick Setup Wizard.
    /// 
    /// One-click setup for the most common XR8 use case:
    ///   Point camera at an image target → play a video overlay on top.
    /// 
    /// Creates and wires: Camera, Manager, ImageTracker, VideoPlayer,
    /// XR8VideoController — all in a single button click.
    /// 
    /// Access via: XR8 WebAR > Image → Video Quick Setup
    /// </summary>
    public class XR8VideoTargetWizard : EditorWindow
    {
        // --- User inputs ---
        private Texture2D targetImage;
        private VideoClip videoClip;
        private string targetId = "my-target";
        private Vector2 quadSize = new Vector2(0.3f, 0.17f);
        private bool autoSizeFromImage = true;

        // --- Video options ---
        private bool loop = true;
        private bool startMuted = true;
        private bool autoPlayOnFound = true;
        private bool useFade = true;
        private float fadeSpeed = 5f;

        // --- Build options ---
        private bool configureWebGL = true;
        private DesktopPreviewMode previewMode = DesktopPreviewMode.Static;

        // --- State ---
        private Vector2 scrollPos;
        private bool defaultsLoaded = false;

        [MenuItem("XR8 WebAR/Image → Video Quick Setup", false, 1)]
        public static void ShowWindow()
        {
            var win = GetWindow<XR8VideoTargetWizard>("Image → Video Setup");
            win.minSize = new Vector2(400, 520);
        }

        private void OnEnable()
        {
            defaultsLoaded = false;
        }

        private void LoadDefaults()
        {
            if (defaultsLoaded) return;
            defaultsLoaded = true;

            // Auto-find default target image
            if (targetImage == null)
            {
                string[] searchPaths = {
                    "Assets/image-targets", "Assets/ImageTargets",
                    "Assets/StreamingAssets/image-targets", "Assets/XR8WebAR/Targets"
                };
                string[] exts = { "_original.jpg", "_original.png", ".jpg", ".png" };

                foreach (var dir in searchPaths)
                {
                    if (!Directory.Exists(dir)) continue;
                    foreach (var file in Directory.GetFiles(dir))
                    {
                        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(
                            file.Replace("\\", "/"));
                        if (tex != null)
                        {
                            targetImage = tex;
                            targetId = Path.GetFileNameWithoutExtension(file)
                                .Replace("_original", "").Replace("_luminance", "");
                            break;
                        }
                    }
                    if (targetImage != null) break;
                }
            }

            // Auto-find default video
            if (videoClip == null)
            {
                string[] videoPaths = { "Assets/video.mp4", "Assets/Videos/video.mp4" };
                foreach (var vp in videoPaths)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(vp);
                    if (clip != null) { videoClip = clip; break; }
                }

                // Fallback: search for any .mp4 in Assets root
                if (videoClip == null)
                {
                    foreach (var file in Directory.GetFiles("Assets", "*.mp4"))
                    {
                        var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(
                            file.Replace("\\", "/"));
                        if (clip != null) { videoClip = clip; break; }
                    }
                }
            }

            UpdateQuadSizeFromImage();
        }

        private void UpdateQuadSizeFromImage()
        {
            if (!autoSizeFromImage || targetImage == null) return;
            float aspect = (float)targetImage.width / targetImage.height;
            // Default physical size: 20cm on the longest side
            // This represents the real-world size of the printed target image
            float maxDim = 0.20f;
            if (aspect >= 1f) // Landscape
                quadSize = new Vector2(maxDim, maxDim / aspect);
            else // Portrait
                quadSize = new Vector2(maxDim * aspect, maxDim);
        }

        // =================================================================
        // GUI
        // =================================================================

        private void OnGUI()
        {
            LoadDefaults();

            EditorGUILayout.Space(6);
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
                { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            EditorGUILayout.LabelField("🎬 Image → Video Quick Setup", titleStyle);
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField(
                "Point camera at image → play video overlay",
                new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter });
            EditorGUILayout.Space(6);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            // --- Target Image ---
            EditorGUILayout.LabelField("📷 Target Image", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "The image that triggers the AR experience.\n" +
                "Upload this same image to your 8th Wall project console.",
                MessageType.None);

            EditorGUI.BeginChangeCheck();
            targetImage = (Texture2D)EditorGUILayout.ObjectField(
                "Target Image", targetImage, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck())
            {
                if (targetImage != null)
                {
                    targetId = targetImage.name
                        .Replace("_original", "").Replace("_luminance", "");
                    UpdateQuadSizeFromImage();
                }
            }

            // Show thumbnail
            if (targetImage != null)
            {
                var rect = GUILayoutUtility.GetRect(120, 80, GUILayout.ExpandWidth(false));
                rect.x = (EditorGUIUtility.currentViewWidth - 120) * 0.5f;
                rect.width = 120;
                GUI.DrawTexture(rect, targetImage, ScaleMode.ScaleToFit);
                EditorGUILayout.Space(4);
            }

            EditorGUILayout.Space(8);

            // --- Video ---
            EditorGUILayout.LabelField("🎞 Video Clip", EditorStyles.boldLabel);
            videoClip = (VideoClip)EditorGUILayout.ObjectField(
                "Video", videoClip, typeof(VideoClip), false);

            if (videoClip != null)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Duration",
                    $"{videoClip.length:F1}s  •  {videoClip.width}×{videoClip.height}");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(8);

            // --- Settings ---
            EditorGUILayout.LabelField("⚙ Settings", EditorStyles.boldLabel);
            targetId = EditorGUILayout.TextField("Target ID", targetId);

            autoSizeFromImage = EditorGUILayout.Toggle("Auto-size from image", autoSizeFromImage);
            EditorGUI.BeginDisabledGroup(autoSizeFromImage);
            quadSize = EditorGUILayout.Vector2Field("Quad Size (meters)", quadSize);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.HelpBox(
                "Quad size = real-world dimensions of the printed target image.\n" +
                "The video overlay will fill this exact area.\n" +
                "Example: 20cm × 11cm for a standard postcard.",
                MessageType.None);

            EditorGUILayout.Space(4);
            loop = EditorGUILayout.Toggle("Loop Video", loop);
            startMuted = EditorGUILayout.Toggle("Start Muted (mobile req.)", startMuted);
            autoPlayOnFound = EditorGUILayout.Toggle("Auto-play on Found", autoPlayOnFound);
            useFade = EditorGUILayout.Toggle("Fade In/Out", useFade);
            if (useFade)
            {
                EditorGUI.indentLevel++;
                fadeSpeed = EditorGUILayout.Slider("Fade Speed", fadeSpeed, 1f, 20f);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);
            previewMode = (DesktopPreviewMode)EditorGUILayout.EnumPopup("Preview Mode", previewMode);
            configureWebGL = EditorGUILayout.Toggle("Auto-configure WebGL", configureWebGL);

            EditorGUILayout.Space(16);

            // --- Validation warnings ---
            if (targetImage == null)
                EditorGUILayout.HelpBox("No target image assigned. A placeholder will be used.",
                    MessageType.Warning);
            if (videoClip == null)
                EditorGUILayout.HelpBox("No video clip assigned. Add one after setup via the VideoPlayer.",
                    MessageType.Warning);
            if (string.IsNullOrEmpty(targetId))
                EditorGUILayout.HelpBox("Target ID is required.", MessageType.Error);

            EditorGUILayout.Space(4);

            // --- Build Button ---
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(targetId));
            GUI.backgroundColor = new Color(0.2f, 0.7f, 1f);
            if (GUILayout.Button("⚡  Build Scene", GUILayout.Height(42)))
            {
                BuildVideoTargetScene();
            }
            GUI.backgroundColor = Color.white;
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndScrollView();
        }

        // =================================================================
        // SCENE BUILDER
        // =================================================================

        private void BuildVideoTargetScene()
        {
            // Confirm if scene has existing XR8 components
            var existing = FindFirstObjectByType<XR8Manager>();
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("Image → Video Setup",
                    "XR8 components already exist in the scene.\nReplace them?",
                    "Replace", "Cancel"))
                    return;
                Undo.DestroyObjectImmediate(existing.gameObject);

                // Also clean up existing trackers
                var oldTracker = FindFirstObjectByType<XR8ImageTracker>();
                if (oldTracker != null) Undo.DestroyObjectImmediate(oldTracker.gameObject);
            }

            EditorUtility.DisplayProgressBar("Image → Video Setup", "Creating camera...", 0.1f);

            try
            {
                // 1. Camera
                var camObj = CreateCamera();
                var cam = camObj.GetComponent<Camera>();
                var xr8Cam = camObj.GetComponent<XR8Camera>();

                // 2. Light
                CreateLight();

                EditorUtility.DisplayProgressBar("Image → Video Setup", "Creating tracker...", 0.3f);

                // 3. Image Tracker + Video Content
                var trackerObj = CreateImageTrackerWithVideo(xr8Cam, out var contentObj);
                var tracker = trackerObj.GetComponent<XR8ImageTracker>();

                EditorUtility.DisplayProgressBar("Image → Video Setup", "Creating manager...", 0.6f);

                // 4. XR8Manager
                var managerObj = new GameObject("XR8Manager");
                Undo.RegisterCreatedObjectUndo(managerObj, "Video Setup Manager");
                var manager = managerObj.AddComponent<XR8Manager>();

                var mgrSo = new SerializedObject(manager);
                mgrSo.FindProperty("arCamera").objectReferenceValue = cam;
                mgrSo.FindProperty("xr8CameraComponent").objectReferenceValue = xr8Cam;
                mgrSo.FindProperty("enableImageTracking").boolValue = true;
                mgrSo.FindProperty("enableWorldTracking").boolValue = false;
                mgrSo.FindProperty("enableFaceTracking").boolValue = false;
                mgrSo.FindProperty("previewMode").enumValueIndex = (int)previewMode;
                mgrSo.FindProperty("imageTracker").objectReferenceValue = tracker;
                mgrSo.ApplyModifiedProperties();

                EditorUtility.DisplayProgressBar("Image → Video Setup", "Configuring build...", 0.8f);

                // 5. Build settings
                if (configureWebGL)
                    ConfigureBuildSettings();

                // 6. Save
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Selection.activeGameObject = contentObj;

                EditorUtility.DisplayDialog("Image → Video Setup Complete! 🎬",
                    "Created:\n" +
                    "• Main Camera (XR8Camera)\n" +
                    "• XR8Manager (wired)\n" +
                    "• XR8ImageTracker (target: '" + targetId + "')\n" +
                    "• Video Quad + VideoPlayer + XR8VideoController\n" +
                    (targetImage != null ? "• Target image applied to preview plane\n" : "") +
                    (videoClip != null ? "• Video clip: " + videoClip.name + "\n" : "") +
                    (configureWebGL ? "• WebGL build settings configured\n" : "") +
                    "\nAll wired and ready. Hit Play to preview!",
                    "OK");

                Debug.Log("[XR8 Video Wizard] ✅ Scene built — target: '" + targetId +
                    "', video: " + (videoClip != null ? videoClip.name : "none"));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // =================================================================
        // COMPONENT CREATORS
        // =================================================================

        private GameObject CreateCamera()
        {
            var existingCam = GameObject.FindGameObjectWithTag("MainCamera");
            if (existingCam != null)
                Undo.DestroyObjectImmediate(existingCam);

            var obj = new GameObject("Main Camera");
            Undo.RegisterCreatedObjectUndo(obj, "Video Setup Camera");
            obj.tag = "MainCamera";

            // Position camera to simulate phone held over a printed target
            // ~40cm above and slightly back for a natural viewing angle
            obj.transform.position = new Vector3(0, 0.4f, -0.15f);
            obj.transform.rotation = Quaternion.Euler(70f, 0, 0); // Angled down at target

            var cam = obj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;

            obj.AddComponent<AudioListener>();
            obj.AddComponent<XR8Camera>();
            return obj;
        }

        private GameObject CreateLight()
        {
            var obj = new GameObject("Directional Light");
            Undo.RegisterCreatedObjectUndo(obj, "Video Setup Light");
            var light = obj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.84f);
            light.intensity = 1f;
            light.shadows = LightShadows.Soft;
            obj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            obj.transform.position = new Vector3(0, 3, 0);
            return obj;
        }

        private GameObject CreateImageTrackerWithVideo(XR8Camera xr8Cam, out GameObject contentObj)
        {
            // --- Tracker root ---
            var trackerObj = new GameObject("XR8ImageTracker");
            Undo.RegisterCreatedObjectUndo(trackerObj, "Video Setup Tracker");
            var tracker = trackerObj.AddComponent<XR8ImageTracker>();

            // --- Anchor (target plane — editor preview only) ---
            var anchor = new GameObject(targetId + "_Anchor");
            anchor.transform.SetParent(trackerObj.transform);
            anchor.transform.localPosition = Vector3.zero;

            // Add target image quad for editor visualization
            CreateTargetPreviewQuad(anchor);

            // --- Content: Video Quad ---
            contentObj = new GameObject(targetId + "_VideoContent");
            Undo.RegisterCreatedObjectUndo(contentObj, "Video Setup Content");
            contentObj.transform.SetParent(anchor.transform);
            contentObj.transform.localPosition = Vector3.zero;

            // Quad mesh — always matches target image dimensions
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "VideoQuad";
            quad.transform.SetParent(contentObj.transform);
            quad.transform.localPosition = new Vector3(0, 0.001f, 0); // Tiny offset above anchor
            quad.transform.localRotation = Quaternion.Euler(90f, 0, 0); // Lay flat on XZ plane
            // Use SAME dimensions as target image quad — video fills the image area
            quad.transform.localScale = new Vector3(quadSize.x, quadSize.y, 1f);

            // Remove collider (not needed for video display)
            var col = quad.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);

            // Create video material (unlit, receives video texture)
            var videoMat = CreateVideoMaterial();
            var renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = videoMat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // --- VideoPlayer ---
            var videoPlayer = quad.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = loop;
            videoPlayer.renderMode = VideoRenderMode.MaterialOverride;
            videoPlayer.targetMaterialRenderer = renderer;
            videoPlayer.targetMaterialProperty = "_BaseMap"; // URP
            if (videoClip != null)
                videoPlayer.clip = videoClip;

            // --- XR8VideoController ---
            var videoCtrl = quad.AddComponent<XR8VideoController>();
            var ctrlSo = new SerializedObject(videoCtrl);
            ctrlSo.FindProperty("autoPlayOnFound").boolValue = autoPlayOnFound;
            ctrlSo.FindProperty("pauseOnLost").boolValue = true;
            ctrlSo.FindProperty("restartOnFound").boolValue = false;
            ctrlSo.FindProperty("startMuted").boolValue = startMuted;
            ctrlSo.FindProperty("loop").boolValue = loop;
            ctrlSo.FindProperty("useFade").boolValue = useFade;
            ctrlSo.FindProperty("fadeSpeed").floatValue = fadeSpeed;
            ctrlSo.ApplyModifiedProperties();

            // --- Wire tracker ---
            var trackerSo = new SerializedObject(tracker);
            trackerSo.FindProperty("trackerCam").objectReferenceValue = xr8Cam;

            var targetsProp = trackerSo.FindProperty("imageTargets");
            targetsProp.ClearArray();
            targetsProp.InsertArrayElementAtIndex(0);
            var elem = targetsProp.GetArrayElementAtIndex(0);
            elem.FindPropertyRelative("id").stringValue = targetId;
            elem.FindPropertyRelative("anchor").objectReferenceValue = anchor.transform;
            elem.FindPropertyRelative("transform").objectReferenceValue = contentObj.transform;

            trackerSo.ApplyModifiedProperties();

            return trackerObj;
        }

        private void CreateTargetPreviewQuad(GameObject anchor)
        {
            var previewObj = new GameObject("TargetPreview");
            previewObj.transform.SetParent(anchor.transform);
            previewObj.transform.localPosition = Vector3.zero;
            previewObj.tag = "EditorOnly";

            var mf = previewObj.AddComponent<MeshFilter>();
            var mr = previewObj.AddComponent<MeshRenderer>();

            // Build flat XZ quad
            float aspect = targetImage != null
                ? (float)targetImage.width / targetImage.height
                : quadSize.x / quadSize.y;
            float halfW = quadSize.x * 0.5f;
            float halfH = quadSize.y * 0.5f;

            var mesh = new Mesh { name = targetId + "_PreviewQuad" };
            mesh.vertices = new Vector3[]
            {
                new Vector3(-halfW, 0, -halfH),
                new Vector3( halfW, 0, -halfH),
                new Vector3( halfW, 0,  halfH),
                new Vector3(-halfW, 0,  halfH)
            };
            mesh.uv = new Vector2[]
            {
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(1, 1), new Vector2(0, 1)
            };
            mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            mf.sharedMesh = mesh;

            // Material with target image
            var shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            if (targetImage != null)
            {
                mat.mainTexture = targetImage;
                mat.color = new Color(1, 1, 1, 0.6f);
            }
            else
            {
                mat.color = new Color(0, 0.9f, 0.4f, 0.3f);
            }
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        private Material CreateVideoMaterial()
        {
            // Use Unlit shader so video displays at full brightness regardless of lighting
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Texture");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            var mat = new Material(shader);
            mat.name = targetId + "_VideoMat";
            mat.color = Color.white;

            // Save as asset so it persists
            string matFolder = "Assets/XR8WebAR/Editor/VideoMaterials";
            EnsureFolder(matFolder);
            string matPath = matFolder + "/" + mat.name + ".mat";
            AssetDatabase.CreateAsset(mat, matPath);
            return AssetDatabase.LoadAssetAtPath<Material>(matPath);
        }

        private void ConfigureBuildSettings()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                Debug.Log("[XR8 Video Wizard] Note: Build target is not WebGL. " +
                    "Switch via File > Build Settings > WebGL > Switch Platform");
            }

            PlayerSettings.WebGL.template = "PROJECT:8thWallTracker";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.dataCaching = false;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.colorSpace = ColorSpace.Gamma;

            Debug.Log("[XR8 Video Wizard] WebGL build settings configured");
        }

        // =================================================================
        // UTILITIES
        // =================================================================

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
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
