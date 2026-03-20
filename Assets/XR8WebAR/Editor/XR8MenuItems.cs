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

        [MenuItem("GameObject/XR8 WebAR/Image Target Content", false, 30)]
        static void CreateImageTargetContent()
        {
            var obj = new GameObject("ImageTarget_Content");
            Undo.RegisterCreatedObjectUndo(obj, "Create Image Target Content");

            // If something is selected, parent to it
            if (Selection.activeGameObject != null)
            {
                obj.transform.SetParent(Selection.activeGameObject.transform);
                obj.transform.localPosition = Vector3.zero;
                obj.transform.localRotation = Quaternion.identity;
            }

            // Add a placeholder quad so you can see something
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Overlay";
            quad.transform.SetParent(obj.transform);
            quad.transform.localPosition = Vector3.zero;
            quad.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            quad.transform.localScale = new Vector3(0.2f, 0.2f, 1f);

            // Remove collider from the quad
            var collider = quad.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);

            Selection.activeGameObject = obj;
            Debug.Log("[XR8] Created Image Target Content — assign this to your image target's Content Root");
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

            // 4. Content placeholder
            var contentObj = new GameObject("ImageTarget_Content");
            Undo.RegisterCreatedObjectUndo(contentObj, "Create Content");
            contentObj.transform.SetParent(trackerObj.transform);
            contentObj.transform.localPosition = Vector3.zero;

            Selection.activeGameObject = managerObj;

            EditorUtility.DisplayDialog("XR8 Scene Setup Complete",
                "Created:\n" +
                "• AR Camera Rig (with XR8Camera)\n" +
                "• XR8 Manager\n" +
                "• XR8 Image Tracker\n" +
                "• Image Target Content placeholder\n\n" +
                "Next steps:\n" +
                "1. In XR8 Manager, configure tracking modes\n" +
                "2. In XR8 Image Tracker, add your image targets\n" +
                "3. Add 3D content as children of ImageTarget_Content",
                "Got it!");

            Debug.Log("[XR8] Complete AR scene setup created");
        }
    }
}
