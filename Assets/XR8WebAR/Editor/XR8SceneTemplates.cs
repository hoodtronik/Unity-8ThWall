using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace XR8WebAR.Editor
{
    /// <summary>
    /// Scene Template Creator — one-click scene setups for common AR use cases.
    /// 
    /// Templates:
    ///   1. Image + Video     — Track image, play video overlay
    ///   2. Image + Video + Floor — Gallery mode with floor content
    ///   3. AR Portal         — Walk-through portal with stencil shaders
    ///   4. Face Filter       — Face tracking with attachment points
    ///   5. World Placement   — Tap-to-place objects on surfaces
    /// 
    /// Access via: XR8 WebAR > Scene Templates
    /// </summary>
    public class XR8SceneTemplates : EditorWindow
    {
        private Vector2 scrollPos;
        private string targetId = "my-target";
        private bool startNewScene = true;

        [MenuItem("XR8 WebAR/Scene Templates", false, 1)]
        public static void ShowWindow()
        {
            var win = GetWindow<XR8SceneTemplates>("AR Scene Templates");
            win.minSize = new Vector2(420, 480);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            EditorGUILayout.LabelField("🎬 AR Scene Templates", titleStyle);
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Choose a template to create a pre-configured AR scene.\n" +
                "Each template creates all necessary GameObjects, components, and materials.",
                MessageType.Info);
            EditorGUILayout.Space(4);

            targetId = EditorGUILayout.TextField("Image Target ID", targetId);
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Scene Mode", GUILayout.Width(EditorGUIUtility.labelWidth));
            GUI.backgroundColor = startNewScene ? new Color(0.3f, 0.85f, 0.5f) : new Color(0.9f, 0.9f, 0.9f);
            if (GUILayout.Button("🆕 New Scene", startNewScene ? EditorStyles.miniButtonLeft : EditorStyles.miniButtonLeft))
                startNewScene = true;
            GUI.backgroundColor = !startNewScene ? new Color(0.3f, 0.7f, 1f) : new Color(0.9f, 0.9f, 0.9f);
            if (GUILayout.Button("➕ Add to Scene", !startNewScene ? EditorStyles.miniButtonRight : EditorStyles.miniButtonRight))
                startNewScene = false;
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (startNewScene)
                EditorGUILayout.HelpBox("Creates a fresh empty scene, then adds template objects.", MessageType.None);
            else
                EditorGUILayout.HelpBox("Adds template objects into the current scene. Existing XR8 components will be replaced.", MessageType.None);

            EditorGUILayout.Space(8);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            // Template 1: Image + Video
            DrawTemplateCard(
                "🖼 Image + Video",
                "Track an image and play a video on it.\n" +
                "Great for: business cards, posters, album covers.",
                "Creates: Camera, Manager, ImageTracker, VideoController, sample quad",
                () => CreateTemplate_ImageVideo());

            // Template 2: Image + Video + Floor (Gallery)
            DrawTemplateCard(
                "🏛 Gallery Mode (Image + Video + Floor)",
                "Track a painting AND detect the floor beneath it.\n" +
                "Great for: museums, galleries, art exhibitions.",
                "Creates: Camera, Manager, ImageTracker, WorldTracker, CombinedTracker, floor content",
                () => CreateTemplate_Gallery());

            // Template 3: AR Portal
            DrawTemplateCard(
                "🚪 AR Portal",
                "Walk through a portal into another world.\n" +
                "Uses stencil buffer masking for the window effect.",
                "Creates: Camera, Manager, WorldTracker, portal frame + mask + interior room",
                () => CreateTemplate_Portal());

            // Template 4: Face Filter
            DrawTemplateCard(
                "😊 Face Filter",
                "Track faces and attach 3D objects.\n" +
                "Great for: face filters, glasses try-on, masks.",
                "Creates: Camera, Manager, FaceTracker with attachment points",
                () => CreateTemplate_FaceFilter());

            // Template 5: World Placement
            DrawTemplateCard(
                "📍 World Placement",
                "Tap to place 3D objects on surfaces.\n" +
                "Great for: furniture preview, product visualization.",
                "Creates: Camera, Manager, WorldTracker with tap-to-place",
                () => CreateTemplate_WorldPlacement());

            // Template 6: Product Viewer (Orbit)
            DrawTemplateCard(
                "🔄 Product Viewer (Orbit)",
                "Camera orbits around a 3D product.\nSwipe to rotate, pinch to zoom.\n" +
                "Great for: e-commerce, 3D configurators, showrooms.",
                "Creates: Camera, Manager, WorldTracker (orbit mode), gesture scripts",
                () => CreateTemplate_ProductViewer());

            // Template 7: Surface Placement with Indicator
            DrawTemplateCard(
                "🎯 Surface Placement with Indicator",
                "Visual reticle follows camera, tap to place content.\n" +
                "Great for: furniture placement, AR decorating, games.",
                "Creates: Camera, Manager, WorldTracker, PlacementIndicator, gesture scripts",
                () => CreateTemplate_PlacementWithIndicator());

            // Template 8: GPS Scavenger Hunt
            DrawTemplateCard(
                "📡 GPS Scavenger Hunt",
                "Place AR content at real GPS coordinates.\n" +
                "Great for: scavenger hunts, outdoor tours, location-based AR.",
                "Creates: Camera, Manager, WorldTracker, GPSTracker, sample pins",
                () => CreateTemplate_GPSScavengerHunt());

            EditorGUILayout.EndScrollView();
        }

        private void DrawTemplateCard(string title, string desc, string creates, System.Action onCreate)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(desc, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(2);
            var smallStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            smallStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            EditorGUILayout.LabelField(creates, smallStyle);
            EditorGUILayout.Space(4);

            GUI.backgroundColor = new Color(0.3f, 0.75f, 1f);
            string btnLabel = startNewScene ? "🆕 Create New Scene" : "➕ Add to Scene";
            if (GUILayout.Button(btnLabel, GUILayout.Height(26)))
            {
                if (PrepareScene())
                    onCreate();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        private bool PrepareScene()
        {
            if (startNewScene)
            {
                // Save current scene if dirty
                if (EditorSceneManager.GetActiveScene().isDirty)
                {
                    int choice = EditorUtility.DisplayDialogComplex("Unsaved Changes",
                        "Current scene has unsaved changes.", "Save & Continue", "Cancel", "Don't Save");
                    if (choice == 0) EditorSceneManager.SaveOpenScenes();
                    else if (choice == 1) return false;
                }
                // Create fresh empty scene
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            else
            {
                // Add to current scene — just confirm
                if (EditorSceneManager.GetActiveScene().isDirty)
                {
                    int choice = EditorUtility.DisplayDialogComplex("Unsaved Changes",
                        "Current scene has unsaved changes.", "Save & Continue", "Cancel", "Don't Save");
                    if (choice == 0) EditorSceneManager.SaveOpenScenes();
                    else if (choice == 1) return false;
                }
            }
            return true;
        }

        // =============================================
        // SHARED HELPERS
        // =============================================

        private GameObject CreateARCamera()
        {
            var existing = GameObject.FindGameObjectWithTag("MainCamera");
            if (existing != null) DestroyImmediate(existing);

            var camObj = new GameObject("Main Camera");
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

        private GameObject CreateManager(Camera cam, XR8Camera xr8Cam,
            bool image = false, bool world = false, bool face = false)
        {
            var existing = FindFirstObjectByType<XR8Manager>();
            if (existing != null) DestroyImmediate(existing.gameObject);

            var obj = new GameObject("XR8Manager");
            var mgr = obj.AddComponent<XR8Manager>();
            var so = new SerializedObject(mgr);
            so.FindProperty("arCamera").objectReferenceValue = cam;
            so.FindProperty("xr8CameraComponent").objectReferenceValue = xr8Cam;
            so.FindProperty("enableImageTracking").boolValue = image;
            so.FindProperty("enableWorldTracking").boolValue = world;
            so.FindProperty("enableFaceTracking").boolValue = face;
            so.FindProperty("previewMode").enumValueIndex = 1; // DesktopPreviewMode.Static
            so.ApplyModifiedProperties();
            return obj;
        }

        private GameObject CreateImageTracker(XR8Camera xr8Cam, string targetName, out Transform contentRoot)
        {
            var obj = new GameObject("XR8ImageTracker");
            var tracker = obj.AddComponent<XR8ImageTracker>();

            // Create target plane (anchor) — a visible quad representing the image target
            var targetPlane = new GameObject(targetName + "_TargetPlane");
            targetPlane.transform.SetParent(obj.transform);
            targetPlane.transform.localPosition = Vector3.zero;
            targetPlane.tag = "EditorOnly"; // Won't be included in builds

            // Add quad mesh visualization
            var mf = targetPlane.AddComponent<MeshFilter>();
            var mr = targetPlane.AddComponent<MeshRenderer>();

            var quad = CreateTargetQuad(targetName);
            mf.sharedMesh = quad;

            // Create material with target image (if found)
            var planeMat = CreateTargetPlaneMaterial(targetName);
            mr.sharedMaterial = planeMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            // Content root (child of target plane)
            var content = new GameObject(targetName + "_Content");
            content.transform.SetParent(targetPlane.transform);
            content.transform.localPosition = Vector3.zero;
            contentRoot = content.transform;

            var so = new SerializedObject(tracker);
            so.FindProperty("trackerCam").objectReferenceValue = xr8Cam;

            var targets = so.FindProperty("imageTargets");
            targets.ClearArray();
            targets.InsertArrayElementAtIndex(0);
            var elem = targets.GetArrayElementAtIndex(0);
            elem.FindPropertyRelative("id").stringValue = targetName;
            elem.FindPropertyRelative("anchor").objectReferenceValue = targetPlane.transform;
            elem.FindPropertyRelative("transform").objectReferenceValue = contentRoot;

            so.ApplyModifiedProperties();
            return obj;
        }

        /// <summary>Creates a flat XZ quad mesh for the target plane visualization.</summary>
        private Mesh CreateTargetQuad(string targetName)
        {
            float size = 0.15f;
            float aspect = 1f;

            // Try to get aspect ratio from the target image
            var tex = FindTargetImageForTemplate(targetName);
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

        /// <summary>Creates and saves a material with the target image texture.</summary>
        private Material CreateTargetPlaneMaterial(string targetName)
        {
            var tex = FindTargetImageForTemplate(targetName);
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

            // Save as asset
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

        /// <summary>Finds a target image texture from common project locations.</summary>
        private Texture2D FindTargetImageForTemplate(string targetId)
        {
            string[] searchPaths = {
                "Assets/image-targets",
                "Assets/ImageTargets",
                "Assets/StreamingAssets/image-targets",
                "Assets/XR8WebAR/Targets"
            };
            string[] suffixes = { "_original", "_luminance", "", "_thumb" };
            string[] exts = { ".jpg", ".png", ".jpeg" };

            foreach (var dir in searchPaths)
            {
                if (!System.IO.Directory.Exists(dir)) continue;
                foreach (var suffix in suffixes)
                {
                    foreach (var ext in exts)
                    {
                        string path = System.IO.Path.Combine(dir, targetId + suffix + ext).Replace("\\", "/");
                        var found = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                        if (found != null) return found;
                    }
                }
            }
            return null;
        }

        private GameObject CreateWorldTracker()
        {
            var obj = new GameObject("XR8WorldTracker");
            obj.AddComponent<XR8WorldTracker>();
            return obj;
        }

        // =============================================
        // TEMPLATE 1: IMAGE + VIDEO
        // =============================================

        private void CreateTemplate_ImageVideo()
        {
            var camObj = CreateARCamera();
            var cam = camObj.GetComponent<Camera>();
            var xr8Cam = camObj.GetComponent<XR8Camera>();

            var mgrObj = CreateManager(cam, xr8Cam, image: true);
            var trackerObj = CreateImageTracker(xr8Cam, targetId, out Transform contentRoot);

            // Wire manager
            var mgrSo = new SerializedObject(mgrObj.GetComponent<XR8Manager>());
            mgrSo.FindProperty("imageTracker").objectReferenceValue = trackerObj.GetComponent<XR8ImageTracker>();
            mgrSo.ApplyModifiedProperties();

            // Add video overlay quad
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "VideoOverlay";
            quad.transform.SetParent(contentRoot);
            quad.transform.localPosition = Vector3.zero;
            quad.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            quad.transform.localScale = new Vector3(0.3f, 0.17f, 1f);
            var col = quad.GetComponent<Collider>();
            if (col) DestroyImmediate(col);

            // Add video controller
            contentRoot.gameObject.AddComponent<XR8VideoController>();

            MarkDirtyAndSelect(mgrObj);
            ShowResult("Image + Video", "Assign your video clip to the XR8VideoController on " + targetId + "_Content");
        }

        // =============================================
        // TEMPLATE 2: GALLERY MODE
        // =============================================

        private void CreateTemplate_Gallery()
        {
            var camObj = CreateARCamera();
            var cam = camObj.GetComponent<Camera>();
            var xr8Cam = camObj.GetComponent<XR8Camera>();

            var mgrObj = CreateManager(cam, xr8Cam, image: true, world: true);
            var trackerObj = CreateImageTracker(xr8Cam, targetId, out Transform contentRoot);
            var worldObj = CreateWorldTracker();

            // Wire manager
            var mgrSo = new SerializedObject(mgrObj.GetComponent<XR8Manager>());
            mgrSo.FindProperty("imageTracker").objectReferenceValue = trackerObj.GetComponent<XR8ImageTracker>();
            mgrSo.FindProperty("worldTracker").objectReferenceValue = worldObj.GetComponent<XR8WorldTracker>();
            mgrSo.ApplyModifiedProperties();

            // Video overlay on image
            var overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
            overlay.name = "VideoOverlay";
            overlay.transform.SetParent(contentRoot);
            overlay.transform.localPosition = Vector3.zero;
            overlay.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            overlay.transform.localScale = new Vector3(0.3f, 0.17f, 1f);
            var col1 = overlay.GetComponent<Collider>();
            if (col1) DestroyImmediate(col1);

            // Floor content
            var floorContent = new GameObject("FloorContent");
            floorContent.transform.position = Vector3.zero;

            // Sample floor object: info panel
            var floorPanel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floorPanel.name = "InfoPanel";
            floorPanel.transform.SetParent(floorContent.transform);
            floorPanel.transform.localPosition = Vector3.zero;
            floorPanel.transform.localScale = new Vector3(0.4f, 0.02f, 0.3f);
            var col2 = floorPanel.GetComponent<Collider>();
            if (col2) DestroyImmediate(col2);

            // Combined tracker
            var combinedObj = new GameObject("XR8CombinedTracker");
            var combined = combinedObj.AddComponent<XR8CombinedTracker>();
            var combinedSo = new SerializedObject(combined);
            combinedSo.FindProperty("imageTracker").objectReferenceValue = trackerObj.GetComponent<XR8ImageTracker>();
            combinedSo.FindProperty("worldTracker").objectReferenceValue = worldObj.GetComponent<XR8WorldTracker>();
            combinedSo.FindProperty("arCamera").objectReferenceValue = cam;

            // Add binding
            var bindingsProp = combinedSo.FindProperty("bindings");
            bindingsProp.ClearArray();
            bindingsProp.InsertArrayElementAtIndex(0);
            var bindElem = bindingsProp.GetArrayElementAtIndex(0);
            bindElem.FindPropertyRelative("imageTargetId").stringValue = targetId;
            bindElem.FindPropertyRelative("floorContent").objectReferenceValue = floorContent.transform;
            combinedSo.ApplyModifiedProperties();

            // Wire to manager
            mgrSo = new SerializedObject(mgrObj.GetComponent<XR8Manager>());
            mgrSo.FindProperty("combinedTracker").objectReferenceValue = combined;
            mgrSo.ApplyModifiedProperties();

            MarkDirtyAndSelect(mgrObj);
            ShowResult("Gallery Mode",
                "• Image content: add your video/overlay to " + targetId + "_Content\n" +
                "• Floor content: add 3D objects as children of FloorContent\n" +
                "• The CombinedTracker auto-projects floor content below the painting");
        }

        // =============================================
        // TEMPLATE 3: AR PORTAL
        // =============================================

        private void CreateTemplate_Portal()
        {
            var camObj = CreateARCamera();
            var cam = camObj.GetComponent<Camera>();
            var xr8Cam = camObj.GetComponent<XR8Camera>();

            var mgrObj = CreateManager(cam, xr8Cam, world: true);
            var worldObj = CreateWorldTracker();

            var mgrSo = new SerializedObject(mgrObj.GetComponent<XR8Manager>());
            mgrSo.FindProperty("worldTracker").objectReferenceValue = worldObj.GetComponent<XR8WorldTracker>();
            mgrSo.ApplyModifiedProperties();

            // --- Portal Structure ---
            var portal = new GameObject("ARPortal");
            portal.transform.position = new Vector3(0, 0, 2f); // 2m in front of camera

            // 1. Portal Frame (visible doorway)
            var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "PortalFrame";
            frame.transform.SetParent(portal.transform);
            frame.transform.localPosition = Vector3.zero;
            frame.transform.localScale = new Vector3(1.2f, 2.2f, 0.05f);
            // Give it a visible material
            var frameRenderer = frame.GetComponent<Renderer>();
            frameRenderer.material = new Material(Shader.Find("Standard"));
            frameRenderer.material.color = new Color(0.2f, 0.2f, 0.25f);
            var frameCol = frame.GetComponent<Collider>();
            if (frameCol) DestroyImmediate(frameCol);

            // 2. Portal Mask (invisible stencil writer)
            var mask = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mask.name = "PortalMask";
            mask.transform.SetParent(portal.transform);
            mask.transform.localPosition = new Vector3(0, 0, 0.01f);
            mask.transform.localScale = new Vector3(1f, 2f, 1f);

            // Apply portal mask material
            var maskShader = Shader.Find("XR8WebAR/PortalMask");
            if (maskShader != null)
            {
                var maskMat = new Material(maskShader);
                maskMat.name = "PortalMaskMaterial";
                mask.GetComponent<Renderer>().material = maskMat;

                // Save material as asset
                string matPath = "Assets/XR8WebAR/Runtime/Materials";
                if (!AssetDatabase.IsValidFolder(matPath))
                {
                    AssetDatabase.CreateFolder("Assets/XR8WebAR/Runtime", "Materials");
                }
                AssetDatabase.CreateAsset(maskMat, matPath + "/PortalMaskMaterial.mat");
            }
            var maskCol = mask.GetComponent<Collider>();
            if (maskCol) DestroyImmediate(maskCol);

            // 3. Portal Interior — a room behind the portal
            var interior = new GameObject("PortalInterior");
            interior.transform.SetParent(portal.transform);
            interior.transform.localPosition = new Vector3(0, 0, -2f); // Behind mask

            // Create interior material
            Material interiorMat = null;
            var intShader = Shader.Find("XR8WebAR/PortalInterior");
            if (intShader != null)
            {
                interiorMat = new Material(intShader);
                interiorMat.name = "PortalInteriorMaterial";
                interiorMat.color = new Color(0.1f, 0.15f, 0.3f); // Dark blue room

                string matPath = "Assets/XR8WebAR/Runtime/Materials";
                AssetDatabase.CreateAsset(interiorMat, matPath + "/PortalInteriorMaterial.mat");
            }

            // Floor
            var floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
            floor.name = "InteriorFloor";
            floor.transform.SetParent(interior.transform);
            floor.transform.localPosition = new Vector3(0, -1f, 0);
            floor.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            floor.transform.localScale = new Vector3(3f, 4f, 1f);
            if (interiorMat) floor.GetComponent<Renderer>().material = interiorMat;
            var floorCol = floor.GetComponent<Collider>();
            if (floorCol) DestroyImmediate(floorCol);

            // Back wall
            var wall = GameObject.CreatePrimitive(PrimitiveType.Quad);
            wall.name = "InteriorBackWall";
            wall.transform.SetParent(interior.transform);
            wall.transform.localPosition = new Vector3(0, 0, -2f);
            wall.transform.localScale = new Vector3(3f, 2.5f, 1f);
            if (interiorMat)
            {
                var wallMat = new Material(interiorMat);
                wallMat.color = new Color(0.15f, 0.2f, 0.4f);
                wall.GetComponent<Renderer>().material = wallMat;
            }
            var wallCol = wall.GetComponent<Collider>();
            if (wallCol) DestroyImmediate(wallCol);

            // Left wall
            var lWall = GameObject.CreatePrimitive(PrimitiveType.Quad);
            lWall.name = "InteriorLeftWall";
            lWall.transform.SetParent(interior.transform);
            lWall.transform.localPosition = new Vector3(-1.5f, 0, -1f);
            lWall.transform.localRotation = Quaternion.Euler(0, 90, 0);
            lWall.transform.localScale = new Vector3(4f, 2.5f, 1f);
            if (interiorMat) lWall.GetComponent<Renderer>().material = interiorMat;
            var lwCol = lWall.GetComponent<Collider>();
            if (lwCol) DestroyImmediate(lwCol);

            // Right wall
            var rWall = GameObject.CreatePrimitive(PrimitiveType.Quad);
            rWall.name = "InteriorRightWall";
            rWall.transform.SetParent(interior.transform);
            rWall.transform.localPosition = new Vector3(1.5f, 0, -1f);
            rWall.transform.localRotation = Quaternion.Euler(0, -90, 0);
            rWall.transform.localScale = new Vector3(4f, 2.5f, 1f);
            if (interiorMat) rWall.GetComponent<Renderer>().material = interiorMat;
            var rwCol = rWall.GetComponent<Collider>();
            if (rwCol) DestroyImmediate(rwCol);

            // Ceiling
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ceiling.name = "InteriorCeiling";
            ceiling.transform.SetParent(interior.transform);
            ceiling.transform.localPosition = new Vector3(0, 1.2f, -1f);
            ceiling.transform.localRotation = Quaternion.Euler(-90f, 0, 0);
            ceiling.transform.localScale = new Vector3(3f, 4f, 1f);
            if (interiorMat)
            {
                var ceilMat = new Material(interiorMat);
                ceilMat.color = new Color(0.08f, 0.1f, 0.2f);
                ceiling.GetComponent<Renderer>().material = ceilMat;
            }
            var ceilCol = ceiling.GetComponent<Collider>();
            if (ceilCol) DestroyImmediate(ceilCol);

            // Light inside portal
            var light = new GameObject("PortalLight");
            light.transform.SetParent(interior.transform);
            light.transform.localPosition = new Vector3(0, 1f, -1f);
            var pointLight = light.AddComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.range = 5f;
            pointLight.intensity = 1.5f;
            pointLight.color = new Color(0.5f, 0.6f, 1f); // Cool blue

            MarkDirtyAndSelect(portal);
            ShowResult("AR Portal",
                "Portal created at (0, 0, 2) — 2 meters in front of camera.\n\n" +
                "How it works:\n" +
                "• PortalMask (stencil writer) = invisible window\n" +
                "• InteriorFloor/Walls/Ceiling (stencil tested) = only visible through the window\n" +
                "• PortalFrame = visible doorway\n\n" +
                "To customize:\n" +
                "• Add objects to PortalInterior using XR8WebAR/PortalInterior shader\n" +
                "• Adjust portal position via ARPortal transform\n" +
                "• StencilRef must match between Mask (1) and Interior materials (1)");
        }

        // =============================================
        // TEMPLATE 4: FACE FILTER
        // =============================================

        private void CreateTemplate_FaceFilter()
        {
            var camObj = CreateARCamera();
            var cam = camObj.GetComponent<Camera>();
            var xr8Cam = camObj.GetComponent<XR8Camera>();

            var mgrObj = CreateManager(cam, xr8Cam, face: true);

            var faceObj = new GameObject("XR8FaceTracker");
            var faceTracker = faceObj.AddComponent<XR8FaceTracker>();

            var mgrSo = new SerializedObject(mgrObj.GetComponent<XR8Manager>());
            mgrSo.FindProperty("faceTracker").objectReferenceValue = faceTracker;
            mgrSo.ApplyModifiedProperties();

            // Create attachment point examples
            var glasses = new GameObject("Glasses_Attachment");
            glasses.transform.SetParent(faceObj.transform);
            glasses.transform.localPosition = new Vector3(0, 0.02f, 0.08f);

            var hat = new GameObject("Hat_Attachment");
            hat.transform.SetParent(faceObj.transform);
            hat.transform.localPosition = new Vector3(0, 0.12f, 0);

            MarkDirtyAndSelect(mgrObj);
            ShowResult("Face Filter",
                "• Add 3D models as children of Glasses_Attachment or Hat_Attachment\n" +
                "• Configure attachment points in XR8FaceTracker inspector\n" +
                "• Test with Desktop Preview (F key toggle, 1-5 for expressions)");
        }

        // =============================================
        // TEMPLATE 5: WORLD PLACEMENT
        // =============================================

        private void CreateTemplate_WorldPlacement()
        {
            var camObj = CreateARCamera();
            var cam = camObj.GetComponent<Camera>();
            var xr8Cam = camObj.GetComponent<XR8Camera>();

            var mgrObj = CreateManager(cam, xr8Cam, world: true);
            var worldObj = CreateWorldTracker();

            // Set placement prefab to a sample cube
            var placementPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            placementPrefab.name = "PlacementObject";
            placementPrefab.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);
            var pRenderer = placementPrefab.GetComponent<Renderer>();
            pRenderer.material = new Material(Shader.Find("Standard"));
            pRenderer.material.color = new Color(0.3f, 0.8f, 0.5f);

            var worldSo = new SerializedObject(worldObj.GetComponent<XR8WorldTracker>());
            worldSo.FindProperty("trackerCam").objectReferenceValue = cam;
            worldSo.FindProperty("placementPrefab").objectReferenceValue = placementPrefab;
            worldSo.ApplyModifiedProperties();

            var mgrSo = new SerializedObject(mgrObj.GetComponent<XR8Manager>());
            mgrSo.FindProperty("worldTracker").objectReferenceValue = worldObj.GetComponent<XR8WorldTracker>();
            mgrSo.ApplyModifiedProperties();

            MarkDirtyAndSelect(mgrObj);
            ShowResult("World Placement",
                "• Replace PlacementObject with your 3D model (or make it a prefab)\n" +
                "• Use XR8WorldTracker.TapToPlace() from a UI button\n" +
                "• Objects will appear on detected surfaces");
        }

        // =============================================
        // TEMPLATE 6: PRODUCT VIEWER (ORBIT)
        // =============================================

        private void CreateTemplate_ProductViewer()
        {
            var camObj = CreateARCamera();
            var cam = camObj.GetComponent<Camera>();
            var xr8Cam = camObj.GetComponent<XR8Camera>();

            var mgrObj = CreateManager(cam, xr8Cam, world: true);
            var worldObj = CreateWorldTracker();

            // Product to orbit around
            var product = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            product.name = "ProductModel";
            product.transform.position = new Vector3(0, 0, 1f);
            product.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            var pRenderer = product.GetComponent<Renderer>();
            pRenderer.material = new Material(Shader.Find("Standard"));
            pRenderer.material.color = new Color(0.8f, 0.3f, 0.2f);
            pRenderer.material.SetFloat("_Metallic", 0.6f);
            pRenderer.material.SetFloat("_Glossiness", 0.8f);
            var pCol = product.GetComponent<Collider>();
            if (pCol) DestroyImmediate(pCol);

            // Configure world tracker for orbit mode
            var worldTracker = worldObj.GetComponent<XR8WorldTracker>();
            var worldSo = new SerializedObject(worldTracker);
            worldSo.FindProperty("trackerCam").objectReferenceValue = cam;
            worldSo.FindProperty("mode").enumValueIndex = (int)XR8WorldTracker.TrackingMode.Orbit;
            worldSo.FindProperty("orbitCenter").objectReferenceValue = product.transform;
            worldSo.FindProperty("orbitDistance").floatValue = 1f;
            worldSo.FindProperty("useSmoothing").boolValue = true;
            worldSo.FindProperty("smoothFactor").floatValue = 10f;
            worldSo.FindProperty("mainContent").objectReferenceValue = product;
            worldSo.ApplyModifiedProperties();

            var mgrSo = new SerializedObject(mgrObj.GetComponent<XR8Manager>());
            mgrSo.FindProperty("worldTracker").objectReferenceValue = worldTracker;
            mgrSo.ApplyModifiedProperties();

            // Add directional light for product showcase
            var lightObj = new GameObject("ProductLight");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.95f, 0.9f);
            lightObj.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            MarkDirtyAndSelect(product);
            ShowResult("Product Viewer (Orbit)",
                "• Replace ProductModel sphere with your 3D product\n" +
                "• Swipe to rotate around the product\n" +
                "• Pinch to zoom in/out\n" +
                "• Adjust orbit distance in WorldTracker inspector");
        }

        // =============================================
        // TEMPLATE 7: SURFACE PLACEMENT WITH INDICATOR
        // =============================================

        private void CreateTemplate_PlacementWithIndicator()
        {
            var camObj = CreateARCamera();
            var cam = camObj.GetComponent<Camera>();
            var xr8Cam = camObj.GetComponent<XR8Camera>();

            var mgrObj = CreateManager(cam, xr8Cam, world: true);
            var worldObj = CreateWorldTracker();

            // Content root (hidden until placed)
            var contentRoot = new GameObject("PlacedContent");
            contentRoot.transform.position = Vector3.zero;

            // Sample content
            var sampleObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sampleObj.name = "FurnitureItem";
            sampleObj.transform.SetParent(contentRoot.transform);
            sampleObj.transform.localPosition = new Vector3(0, 0.075f, 0);
            sampleObj.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);
            var sRenderer = sampleObj.GetComponent<Renderer>();
            sRenderer.material = new Material(Shader.Find("Standard"));
            sRenderer.material.color = new Color(0.4f, 0.6f, 0.9f);
            var sCol = sampleObj.GetComponent<Collider>();
            if (sCol) DestroyImmediate(sCol);

            // Placement indicator visual (reticle)
            var indicatorObj = new GameObject("PlacementReticle");
            var indicatorQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            indicatorQuad.name = "ReticleVisual";
            indicatorQuad.transform.SetParent(indicatorObj.transform);
            indicatorQuad.transform.localPosition = Vector3.zero;
            indicatorQuad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            indicatorQuad.transform.localScale = new Vector3(0.2f, 0.2f, 1f);
            var iRenderer = indicatorQuad.GetComponent<Renderer>();
            var iShader = Shader.Find("Unlit/Transparent");
            if (iShader == null) iShader = Shader.Find("Sprites/Default");
            iRenderer.material = new Material(iShader);
            iRenderer.material.color = new Color(0f, 1f, 0.5f, 0.5f);
            var iCol = indicatorQuad.GetComponent<Collider>();
            if (iCol) DestroyImmediate(iCol);

            // Add PlacementIndicator component
            var indicator = worldObj.AddComponent<XR8PlacementIndicator>();
            var indSo = new SerializedObject(indicator);
            indSo.FindProperty("trackerCam").objectReferenceValue = cam;
            indSo.FindProperty("indicatorVisual").objectReferenceValue = indicatorObj;
            indSo.FindProperty("contentRoot").objectReferenceValue = contentRoot;
            indSo.FindProperty("hideContentUntilPlaced").boolValue = true;
            indSo.ApplyModifiedProperties();

            // Add gesture scripts to content
            contentRoot.AddComponent<XR8SwipeToRotate>();
            contentRoot.AddComponent<XR8PinchToScale>();

            // Configure world tracker
            var worldTracker = worldObj.GetComponent<XR8WorldTracker>();
            var worldSo = new SerializedObject(worldTracker);
            worldSo.FindProperty("trackerCam").objectReferenceValue = cam;
            worldSo.FindProperty("usePlacementIndicator").boolValue = true;
            worldSo.FindProperty("placementIndicator").objectReferenceValue = indicator;
            worldSo.FindProperty("mainContent").objectReferenceValue = contentRoot;
            worldSo.ApplyModifiedProperties();

            var mgrSo = new SerializedObject(mgrObj.GetComponent<XR8Manager>());
            mgrSo.FindProperty("worldTracker").objectReferenceValue = worldTracker;
            mgrSo.ApplyModifiedProperties();

            MarkDirtyAndSelect(worldObj);
            ShowResult("Surface Placement with Indicator",
                "• Green reticle follows camera aim at the floor\n" +
                "• Call WorldTracker.PlaceOrigin() from a UI button to place content\n" +
                "• After placement: swipe to rotate, pinch to scale\n" +
                "• Replace FurnitureItem with your 3D model\n" +
                "• Call WorldTracker.ResetOrigin() to re-place");
        }

        // =============================================
        // TEMPLATE 8: GPS SCAVENGER HUNT
        // =============================================

        private void CreateTemplate_GPSScavengerHunt()
        {
            var camObj = CreateARCamera();
            var cam = camObj.GetComponent<Camera>();
            var xr8Cam = camObj.GetComponent<XR8Camera>();

            var mgrObj = CreateManager(cam, xr8Cam, world: true);
            var worldObj = CreateWorldTracker();

            var worldSo = new SerializedObject(worldObj.GetComponent<XR8WorldTracker>());
            worldSo.FindProperty("trackerCam").objectReferenceValue = cam;
            worldSo.ApplyModifiedProperties();

            var mgrSo = new SerializedObject(mgrObj.GetComponent<XR8Manager>());
            mgrSo.FindProperty("worldTracker").objectReferenceValue = worldObj.GetComponent<XR8WorldTracker>();
            mgrSo.ApplyModifiedProperties();

            // GPS Tracker
            var gpsTrackerObj = new GameObject("XR8GPSTracker");
            gpsTrackerObj.AddComponent<XR8GPSTracker>();

            // Sample GPS Pins
            var pinColors = new Color[]
            {
                new Color(1f, 0.3f, 0.2f),  // Red
                new Color(0.2f, 0.8f, 0.3f), // Green
                new Color(0.3f, 0.5f, 1f),   // Blue
            };
            string[] pinNames = { "Pin_Treasure1", "Pin_Treasure2", "Pin_Treasure3" };
            float[] latOffsets = { 0.0003f, -0.0002f, 0.0001f };
            float[] lonOffsets = { 0.0001f, 0.0003f, -0.0003f };

            for (int i = 0; i < 3; i++)
            {
                var pinObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pinObj.name = pinNames[i];
                pinObj.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
                pinObj.transform.position = new Vector3(i * 2f, 0.15f, 3f);

                var pinRenderer = pinObj.GetComponent<Renderer>();
                pinRenderer.material = new Material(Shader.Find("Standard"));
                pinRenderer.material.color = pinColors[i];
                pinRenderer.material.SetFloat("_Metallic", 0.3f);

                var pin = pinObj.AddComponent<XR8GPSPin>();
                pin.pinId = "treasure-" + (i + 1);
                pin.latitude = 39.1031 + latOffsets[i];
                pin.longitude = -84.5120 + lonOffsets[i];
            }

            MarkDirtyAndSelect(gpsTrackerObj);
            ShowResult("GPS Scavenger Hunt",
                "• 3 sample GPS pins created at Cincinnati coordinates\n" +
                "• Change lat/lon on each GPSPin to your desired locations\n" +
                "• Pins auto-show/hide based on activationRadius (default 50m)\n" +
                "• OnEnteredPin / OnExitedPin events fire at pinRadius (5m)\n" +
                "• Test in editor with WASD keys to simulate GPS movement\n" +
                "• Replace spheres with your treasure/landmark 3D models");
        }

        // =============================================
        // HELPERS
        // =============================================

        private void MarkDirtyAndSelect(GameObject obj)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = obj;
        }

        private void ShowResult(string template, string nextSteps)
        {
            EditorUtility.DisplayDialog(template + " — Created! 🎉",
                "Scene template created successfully.\n\n" +
                "Next steps:\n" + nextSteps,
                "OK");
            Debug.Log("[XR8 Templates] ✅ Created: " + template);
        }
    }
}
