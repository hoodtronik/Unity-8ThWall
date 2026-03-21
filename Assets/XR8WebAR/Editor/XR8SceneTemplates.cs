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
            if (GUILayout.Button("Create Scene", GUILayout.Height(26)))
            {
                if (ConfirmNewScene())
                    onCreate();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        private bool ConfirmNewScene()
        {
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                int choice = EditorUtility.DisplayDialogComplex("Unsaved Changes",
                    "Current scene has unsaved changes.", "Save & Continue", "Cancel", "Don't Save");
                if (choice == 0) EditorSceneManager.SaveOpenScenes();
                else if (choice == 1) return false;
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
            so.FindProperty("enableDesktopPreview").boolValue = true;
            so.ApplyModifiedProperties();
            return obj;
        }

        private GameObject CreateImageTracker(XR8Camera xr8Cam, string targetName, out Transform contentRoot)
        {
            var obj = new GameObject("XR8ImageTracker");
            var tracker = obj.AddComponent<XR8ImageTracker>();

            // Content root
            var content = new GameObject(targetName + "_Content");
            content.transform.SetParent(obj.transform);
            content.transform.localPosition = Vector3.zero;
            contentRoot = content.transform;

            var so = new SerializedObject(tracker);
            so.FindProperty("trackerCam").objectReferenceValue = xr8Cam;

            var targets = so.FindProperty("imageTargets");
            targets.ClearArray();
            targets.InsertArrayElementAtIndex(0);
            var elem = targets.GetArrayElementAtIndex(0);
            elem.FindPropertyRelative("id").stringValue = targetName;
            elem.FindPropertyRelative("transform").objectReferenceValue = contentRoot;

            so.ApplyModifiedProperties();
            return obj;
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
