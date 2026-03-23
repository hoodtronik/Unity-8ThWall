using UnityEngine;
using UnityEditor;

namespace XR8WebAR.Editor
{
    /// <summary>
    /// Menu items for quick XR8 scene setup.
    /// Accessible via GameObject menu or right-click in Hierarchy.
    /// </summary>
    public static class XR8MenuItems
    {
        // =================================================================
        // GameObject > XR8 WebAR menu
        // =================================================================

        [MenuItem("GameObject/XR8 WebAR/AR Camera Rig", false, 10)]
        static void CreateARCameraRig()
        {
            // Root
            var rig = new GameObject("AR Camera Rig");
            Undo.RegisterCreatedObjectUndo(rig, "Create AR Camera Rig");

            // Camera child
            var camObj = new GameObject("Main Camera");
            camObj.transform.SetParent(rig.transform);
            camObj.transform.localPosition = new Vector3(0, 1.6f, 0);
            camObj.tag = "MainCamera";

            var cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;

            // XR8Camera component
            camObj.AddComponent<XR8Camera>();

            Selection.activeGameObject = rig;
            Debug.Log("[XR8] Created AR Camera Rig");
        }

        [MenuItem("GameObject/XR8 WebAR/XR8 Manager", false, 11)]
        static void CreateXR8Manager()
        {
            // Check if one already exists
            if (Object.FindFirstObjectByType<XR8Manager>() != null)
            {
                EditorUtility.DisplayDialog("XR8 Manager",
                    "An XR8 Manager already exists in the scene.", "OK");
                return;
            }

            var obj = new GameObject("XR8Manager");
            Undo.RegisterCreatedObjectUndo(obj, "Create XR8 Manager");
            obj.AddComponent<XR8Manager>();
            Selection.activeGameObject = obj;
            Debug.Log("[XR8] Created XR8 Manager");
        }

        [MenuItem("GameObject/XR8 WebAR/Image Tracker", false, 12)]
        static void CreateImageTracker()
        {
            var obj = new GameObject("XR8ImageTracker");
            Undo.RegisterCreatedObjectUndo(obj, "Create Image Tracker");
            obj.AddComponent<XR8ImageTracker>();
            Selection.activeGameObject = obj;
            Debug.Log("[XR8] Created Image Tracker");
        }

        [MenuItem("GameObject/XR8 WebAR/Face Tracker", false, 13)]
        static void CreateFaceTracker()
        {
            var obj = new GameObject("XR8FaceTracker");
            Undo.RegisterCreatedObjectUndo(obj, "Create Face Tracker");
            obj.AddComponent<XR8FaceTracker>();
            Selection.activeGameObject = obj;
            Debug.Log("[XR8] Created Face Tracker");
        }

        // =================================================================
        // Advanced WebAR Components
        // =================================================================

        [MenuItem("GameObject/XR8 WebAR/Semantic Layer (Sky + Person)", false, 20)]
        static void CreateSemanticLayer()
        {
            var obj = new GameObject("XR8SemanticLayer");
            Undo.RegisterCreatedObjectUndo(obj, "Create Semantic Layer");
            obj.AddComponent<XR8SemanticLayer>();
            Selection.activeGameObject = obj;
            Debug.Log("[XR8] Created Semantic Layer — configure sky replacement in Inspector");
        }

        [MenuItem("GameObject/XR8 WebAR/Depth Occlusion", false, 21)]
        static void CreateDepthOcclusion()
        {
            var obj = new GameObject("XR8DepthOcclusion");
            Undo.RegisterCreatedObjectUndo(obj, "Create Depth Occlusion");
            obj.AddComponent<XR8DepthOcclusion>();
            Selection.activeGameObject = obj;
            Debug.Log("[XR8] Created Depth Occlusion — hides AR objects behind real-world geometry");
        }

        [MenuItem("GameObject/XR8 WebAR/Hand Tracker", false, 22)]
        static void CreateHandTracker()
        {
            var obj = new GameObject("XR8HandTracker");
            Undo.RegisterCreatedObjectUndo(obj, "Create Hand Tracker");
            obj.AddComponent<XR8HandTracker>();
            Selection.activeGameObject = obj;
            Debug.Log("[XR8] Created Hand Tracker — 21 landmarks + gesture detection");
        }

        [MenuItem("GameObject/XR8 WebAR/VPS Tracker", false, 23)]
        static void CreateVPSTracker()
        {
            var obj = new GameObject("XR8VPSTracker");
            Undo.RegisterCreatedObjectUndo(obj, "Create VPS Tracker");
            obj.AddComponent<XR8VPSTracker>();
            Selection.activeGameObject = obj;
            Debug.Log("[XR8] Created VPS Tracker — configure wayspot IDs in Inspector");
        }

        [MenuItem("GameObject/XR8 WebAR/AR NavMesh", false, 24)]
        static void CreateARNavMesh()
        {
            var obj = new GameObject("XR8ARNavMesh");
            Undo.RegisterCreatedObjectUndo(obj, "Create AR NavMesh");
            obj.AddComponent<XR8ARNavMesh>();
            Selection.activeGameObject = obj;
            Debug.Log("[XR8] Created AR NavMesh — requires XR8WorldTracker in scene");
        }

        [MenuItem("GameObject/XR8 WebAR/Light Estimation", false, 25)]
        static void CreateLightEstimation()
        {
            var obj = new GameObject("XR8LightEstimation");
            Undo.RegisterCreatedObjectUndo(obj, "Create Light Estimation");
            obj.AddComponent<XR8LightEstimation>();
            Selection.activeGameObject = obj;
            Debug.Log("[XR8] Created Light Estimation — auto-assigns directional light");
        }

        [MenuItem("GameObject/XR8 WebAR/Object Detector", false, 26)]
        static void CreateObjectDetector()
        {
            var obj = new GameObject("XR8ObjectDetector");
            Undo.RegisterCreatedObjectUndo(obj, "Create Object Detector");
            obj.AddComponent<XR8ObjectDetector>();
            Selection.activeGameObject = obj;
            Debug.Log("[XR8] Created Object Detector — TF.js COCO-SSD runs in browser");
        }

        [MenuItem("GameObject/XR8 WebAR/Shared Session", false, 27)]
        static void CreateSharedSession()
        {
            var obj = new GameObject("XR8SharedSession");
            Undo.RegisterCreatedObjectUndo(obj, "Create Shared Session");
            obj.AddComponent<XR8SharedSession>();
            Selection.activeGameObject = obj;
            Debug.Log("[XR8] Created Shared Session — set relay server URL in Inspector");
        }

        [MenuItem("GameObject/XR8 WebAR/Session Recorder", false, 28)]
        static void CreateSessionRecorder()
        {
            var obj = new GameObject("XR8SessionRecorder");
            Undo.RegisterCreatedObjectUndo(obj, "Create Session Recorder");
            obj.AddComponent<XR8SessionRecorder>();
            Selection.activeGameObject = obj;
            Debug.Log("[XR8] Created Session Recorder — records poses for editor playback");
        }

        [MenuItem("GameObject/XR8 WebAR/Create Image Target (from image)", false, 30)]
        static void CreateImageTargetFromMenu()
        {
            // Delegate to the factory
            XR8ImageTargetFactory.CreateImageTarget();
        }

        [MenuItem("GameObject/XR8 WebAR/Create Image Target (from image)", true)]
        static bool ValidateCreateImageTargetFromMenu()
        {
            return Selection.activeObject is Texture2D;
        }

        // =================================================================
        // Full scene setup
        // =================================================================

        [MenuItem("GameObject/XR8 WebAR/--- Complete AR Scene Setup ---", false, 50)]
        static void CreateFullARScene()
        {
            if (Object.FindFirstObjectByType<XR8Manager>() != null)
            {
                if (!EditorUtility.DisplayDialog("XR8 Scene Setup",
                    "XR8 components already exist in the scene. Create anyway?",
                    "Yes", "Cancel"))
                    return;
            }

            // 1. Camera Rig
            CreateARCameraRig();
            var cameraRig = Selection.activeGameObject;

            // 2. Manager
            var managerObj = new GameObject("XR8Manager");
            Undo.RegisterCreatedObjectUndo(managerObj, "Create XR8 Manager");
            managerObj.AddComponent<XR8Manager>();

            // 3. Image Tracker
            var trackerObj = new GameObject("XR8ImageTracker");
            Undo.RegisterCreatedObjectUndo(trackerObj, "Create Image Tracker");
            trackerObj.AddComponent<XR8ImageTracker>();

            Selection.activeGameObject = managerObj;

            EditorUtility.DisplayDialog("XR8 Scene Setup Complete",
                "Created:\n" +
                "• AR Camera Rig (with XR8Camera)\n" +
                "• XR8 Manager\n" +
                "• XR8 Image Tracker\n\n" +
                "Next steps:\n" +
                "1. Select an image in Project > Assets > XR8 WebAR > Create > Image Target\n" +
                "2. The Image Target prefab auto-wires into the tracker\n" +
                "3. Drag 3D content as children of the target",
                "Got it!");

            Debug.Log("[XR8] Complete AR scene setup created");
        }
    }
}
