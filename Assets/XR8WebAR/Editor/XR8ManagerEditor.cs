using UnityEngine;
using UnityEditor;

namespace XR8WebAR.Editor
{
    /// <summary>
    /// Custom inspector for XR8Manager.
    /// Provides a clean, organized layout with visual tracking mode toggles.
    /// </summary>
    [CustomEditor(typeof(XR8Manager))]
    public class XR8ManagerEditor : UnityEditor.Editor
    {
        private bool showEngineSettings = false;
        private bool showEvents = false;

        // Color palette
        private static readonly Color imageColor = new Color(0.2f, 0.7f, 1f);
        private static readonly Color worldColor = new Color(0.3f, 0.9f, 0.4f);
        private static readonly Color faceColor = new Color(1f, 0.6f, 0.2f);
        private static readonly Color previewColor = new Color(0.8f, 0.5f, 1f);

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // === HEADER ===
            EditorGUILayout.Space(5);
            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16 };
            EditorGUILayout.LabelField("XR8 Manager", headerStyle);
            EditorGUILayout.LabelField("Unified AR tracking controller", EditorStyles.miniLabel);
            EditorGUILayout.Space(10);

            // === TRACKING MODES ===
            DrawTrackingModes();

            EditorGUILayout.Space(10);

            // === CAMERA ===
            DrawCameraSection();

            EditorGUILayout.Space(10);

            // === TRACKING CONFIG SECTIONS ===
            DrawTrackingConfig();

            EditorGUILayout.Space(10);

            // === DESKTOP PREVIEW ===
            DrawDesktopPreview();

            EditorGUILayout.Space(10);

            // === ENGINE SETTINGS ===
            showEngineSettings = EditorGUILayout.Foldout(showEngineSettings, "Engine Settings", true);
            if (showEngineSettings)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("targetFrameRate"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("enableLighting"));
                EditorGUI.indentLevel--;
            }

            // === EVENTS ===
            showEvents = EditorGUILayout.Foldout(showEvents, "Events", true);
            if (showEvents)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("OnEngineReady"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("OnEngineError"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("OnCameraPermissionGranted"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("OnCameraPermissionDenied"));
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawTrackingModes()
        {
            EditorGUILayout.LabelField("Tracking Modes", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Image Tracking toggle
            DrawTrackingToggle("enableImageTracking", "📷  Image Tracking",
                "Track images/markers in the camera feed", imageColor);

            // World Tracking toggle
            DrawTrackingToggle("enableWorldTracking", "🌍  World Tracking",
                "Track surfaces for placing 3D objects (SLAM)", worldColor);

            // Face Tracking toggle
            DrawTrackingToggle("enableFaceTracking", "😀  Face Tracking",
                "Track faces for filters and effects", faceColor);

            EditorGUILayout.EndVertical();
        }

        private void DrawTrackingToggle(string propName, string label, string tooltip, Color color)
        {
            var prop = serializedObject.FindProperty(propName);

            EditorGUILayout.BeginHorizontal();
            GUI.color = prop.boolValue ? color : new Color(0.6f, 0.6f, 0.6f);
            prop.boolValue = EditorGUILayout.ToggleLeft(
                new GUIContent("  " + label, tooltip), prop.boolValue,
                new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold });
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCameraSection()
        {
            EditorGUILayout.LabelField("Camera", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("arCamera"),
                new GUIContent("AR Camera"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("xr8CameraComponent"),
                new GUIContent("XR8 Camera Component"));
            EditorGUILayout.EndVertical();
        }

        private void DrawTrackingConfig()
        {
            var imageEnabled = serializedObject.FindProperty("enableImageTracking").boolValue;
            var worldEnabled = serializedObject.FindProperty("enableWorldTracking").boolValue;
            var faceEnabled = serializedObject.FindProperty("enableFaceTracking").boolValue;

            // Image tracking config
            if (imageEnabled)
            {
                GUI.color = imageColor;
                EditorGUILayout.LabelField("📷 Image Tracking Config", EditorStyles.boldLabel);
                GUI.color = Color.white;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("imageTracker"),
                    new GUIContent("Image Tracker", "XR8ImageTracker component in the scene"));

                var trackerProp = serializedObject.FindProperty("imageTracker");
                if (trackerProp.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox(
                        "No image tracker assigned. Add an XR8ImageTracker component to a root GameObject.",
                        MessageType.Warning);
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            // World tracking config
            if (worldEnabled)
            {
                GUI.color = worldColor;
                EditorGUILayout.LabelField("🌍 World Tracking Config", EditorStyles.boldLabel);
                GUI.color = Color.white;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("enableSurfaceDetection"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("showSurfaceMeshes"));
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            // Face tracking config
            if (faceEnabled)
            {
                GUI.color = faceColor;
                EditorGUILayout.LabelField("😀 Face Tracking Config", EditorStyles.boldLabel);
                GUI.color = Color.white;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("maxFaces"),
                    new GUIContent("Max Faces"));
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }
        }

        private void DrawDesktopPreview()
        {
            GUI.color = previewColor;
            EditorGUILayout.LabelField("🖥️ Desktop Preview", EditorStyles.boldLabel);
            GUI.color = Color.white;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Primary mode dropdown
            EditorGUILayout.PropertyField(serializedObject.FindProperty("previewMode"),
                new GUIContent("Preview Mode", "Select a simulation mode for in-editor testing"));

            var mode = (DesktopPreviewMode)serializedObject.FindProperty("previewMode").enumValueIndex;

            if (mode != DesktopPreviewMode.None)
            {
                EditorGUILayout.Space(3);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("previewReferenceImage"),
                    new GUIContent("Reference Image", "Image to simulate as camera input (optional)"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("previewUseWebcam"),
                    new GUIContent("Use Webcam Background"));

                EditorGUILayout.Space(5);

                switch (mode)
                {
                    case DesktopPreviewMode.Static:
                        EditorGUILayout.HelpBox(
                            "Target locked in front of camera.\n" +
                            "[T] Toggle tracking  [Tab] Cycle targets\n" +
                            "[Mouse Drag] Move  [Scroll] Distance  [R] Reset",
                            MessageType.Info);
                        break;

                    case DesktopPreviewMode.FlyThrough:
                        EditorGUILayout.LabelField("FlyThrough Settings", EditorStyles.miniBoldLabel);
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("flySpeed"),
                            new GUIContent("Speed (m/s)"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("flyLookSensitivity"),
                            new GUIContent("Look Sensitivity"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("autoTrackNearest"),
                            new GUIContent("Auto-track Nearest", "Fire tracking events for nearest image target"));
                        EditorGUI.indentLevel--;
                        EditorGUILayout.HelpBox(
                            "Free-fly camera like a scene editor.\n" +
                            "[WASD] Move  [Q/E] Down/Up  [Shift] Sprint\n" +
                            "[Right-click + Mouse] Look  [Scroll] Speed\n" +
                            "[T] Toggle tracking  [Tab] Cycle targets",
                            MessageType.Info);
                        break;

                    case DesktopPreviewMode.RecordedPlayback:
                        EditorGUILayout.LabelField("Playback Settings", EditorStyles.miniBoldLabel);
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("playbackDataFile"),
                            new GUIContent("Pose Data (CSV)", "CSV: id,px,py,pz,fx,fy,fz,ux,uy,uz,rx,ry,rz"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("playbackLoop"),
                            new GUIContent("Loop"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("playbackSpeed"),
                            new GUIContent("Playback Speed"));
                        EditorGUI.indentLevel--;
                        EditorGUILayout.HelpBox(
                            "Replay recorded pose data from CSV.\n" +
                            "[Space] Pause/Resume  [←/→] Step frame\n" +
                            "[R] Restart from beginning",
                            MessageType.Info);
                        break;

                    case DesktopPreviewMode.SimulatedNoise:
                        EditorGUILayout.LabelField("Noise Settings", EditorStyles.miniBoldLabel);
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("positionJitter"),
                            new GUIContent("Position Jitter (m)", "Perlin noise position offset magnitude"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("rotationJitter"),
                            new GUIContent("Rotation Jitter (°)", "Perlin noise rotation offset magnitude"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("trackingLossChance"),
                            new GUIContent("Loss Chance", "Probability per frame of simulated tracking loss"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty("trackingLossDuration"),
                            new GUIContent("Loss Duration (s)", "Min/Max seconds tracking stays lost"));
                        EditorGUI.indentLevel--;
                        EditorGUILayout.HelpBox(
                            "Static mode + realistic tracking noise.\n" +
                            "Simulates jitter, drift, and random tracking loss.\n" +
                            "Same controls as Static mode.",
                            MessageType.Info);
                        break;
                }

                EditorGUILayout.Space(3);
                GUI.color = Color.yellow;
                EditorGUILayout.LabelField("⚠ Preview active — XR8 engine will NOT start in editor",
                    EditorStyles.miniLabel);
                GUI.color = Color.white;
            }

            EditorGUILayout.EndVertical();
        }
    }
}
