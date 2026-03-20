using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace XR8WebAR.Editor
{
    /// <summary>
    /// Custom inspector for XR8ImageTracker.
    /// Auto-discovers image target JSON files and provides one-click setup.
    /// </summary>
    [CustomEditor(typeof(XR8ImageTracker))]
    public class XR8ImageTrackerEditor : UnityEditor.Editor
    {
        private XR8ImageTracker _target;
        private bool showDebug = false;
        private bool showAdvanced = false;

        // Cached discovered targets
        private List<DiscoveredTarget> discoveredTargets = new List<DiscoveredTarget>();
        private double lastScanTime = 0;

        private struct DiscoveredTarget
        {
            public string name;
            public string jsonPath;
            public string imagePath;
            public Texture2D thumbnail;
        }



        public override void OnInspectorGUI()
        {
            // === HEADER ===
            EditorGUILayout.Space(5);
            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            EditorGUILayout.LabelField("XR8 Image Tracker", headerStyle);
            EditorGUILayout.Space(5);

            // === CAMERA ===
            EditorGUILayout.PropertyField(serializedObject.FindProperty("trackerCam"),
                new GUIContent("AR Camera", "The camera with XR8Camera component"));

            EditorGUILayout.Space(10);

            // === IMAGE TARGETS — THE MAIN UI ===
            DrawImageTargetsSection();

            EditorGUILayout.Space(10);

            // === EVENTS ===
            EditorGUILayout.PropertyField(serializedObject.FindProperty("OnImageFound"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("OnImageLost"));

            EditorGUILayout.Space(10);

            // === ADVANCED ===
            showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced Settings", true);
            if (showAdvanced)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("trackerOrigin"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("trackerSettings"), true);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("dontDeactivateOnLost"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("startStopOnEnableDisable"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("stopOnDestroy"));
                EditorGUI.indentLevel--;
            }

            // === EDITOR DEBUG ===
            EditorGUILayout.Space(10);
            DrawEditorDebug();

            serializedObject.ApplyModifiedProperties();
        }

        // =========================================================================
        // IMAGE TARGETS SECTION
        // =========================================================================
        private void DrawImageTargetsSection()
        {
            EditorGUILayout.LabelField("Image Targets", EditorStyles.boldLabel);

            var imageTargetsProp = serializedObject.FindProperty("imageTargets");

            // Show current targets
            if (imageTargetsProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "No image targets configured.\n\n" +
                    "Add targets from the discovered list below, or manually add one.",
                    MessageType.Info);
            }
            else
            {
                for (int i = 0; i < imageTargetsProp.arraySize; i++)
                {
                    DrawImageTargetEntry(imageTargetsProp, i);
                }
            }

            EditorGUILayout.Space(5);

            // --- DISCOVERED TARGETS ---
            DrawDiscoveredTargets(imageTargetsProp);

            // --- MANUAL ADD ---
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Add Manually", GUILayout.Height(24)))
            {
                imageTargetsProp.InsertArrayElementAtIndex(imageTargetsProp.arraySize);
                var newElement = imageTargetsProp.GetArrayElementAtIndex(imageTargetsProp.arraySize - 1);
                newElement.FindPropertyRelative("id").stringValue = "new-target";
                newElement.FindPropertyRelative("transform").objectReferenceValue = _target.transform;
            }
            if (GUILayout.Button("Rescan Targets", GUILayout.Width(120), GUILayout.Height(24)))
            {
                ScanForImageTargets();
            }
            EditorGUILayout.EndHorizontal();

            // --- DRAG AND DROP ZONE ---
            DrawDragDropZone(imageTargetsProp);
        }

        // =========================================================================
        // DRAG AND DROP ZONE
        // =========================================================================
        private void DrawDragDropZone(SerializedProperty imageTargetsProp)
        {
            EditorGUILayout.Space(5);

            // Draw drop zone
            var dropArea = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
            var style = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Italic
            };

            // Check if dragging
            bool isDragging = Event.current.type == EventType.DragUpdated ||
                              Event.current.type == EventType.DragPerform;
            bool isOverDropArea = dropArea.Contains(Event.current.mousePosition);

            if (isDragging && isOverDropArea)
            {
                GUI.color = new Color(0.3f, 0.8f, 1f, 0.3f);
                GUI.Box(dropArea, "Drop to add as image target", style);
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = new Color(1f, 1f, 1f, 0.5f);
                GUI.Box(dropArea, "📷  Drag & drop image or JSON here to add target", style);
                GUI.color = Color.white;
            }

            // Handle drag events
            if (isOverDropArea)
            {
                if (Event.current.type == EventType.DragUpdated)
                {
                    // Check if any dragged object is an image or JSON
                    bool hasValidAsset = false;
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is Texture2D || obj is TextAsset)
                        {
                            hasValidAsset = true;
                            break;
                        }
                    }
                    // Also check paths for JSON files
                    if (!hasValidAsset)
                    {
                        foreach (var path in DragAndDrop.paths)
                        {
                            if (path.EndsWith(".json") || path.EndsWith(".jpg") ||
                                path.EndsWith(".png") || path.EndsWith(".jpeg"))
                            {
                                hasValidAsset = true;
                                break;
                            }
                        }
                    }

                    DragAndDrop.visualMode = hasValidAsset
                        ? DragAndDropVisualMode.Copy
                        : DragAndDropVisualMode.Rejected;

                    Event.current.Use();
                }
                else if (Event.current.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();

                    foreach (var path in DragAndDrop.paths)
                    {
                        string targetName = null;

                        if (path.EndsWith(".json"))
                        {
                            // Parse name from JSON
                            try
                            {
                                string content = File.ReadAllText(path);
                                targetName = ExtractJsonField(content, "name");
                            }
                            catch { }

                            if (string.IsNullOrEmpty(targetName))
                                targetName = Path.GetFileNameWithoutExtension(path);
                        }
                        else if (path.EndsWith(".jpg") || path.EndsWith(".png") || path.EndsWith(".jpeg"))
                        {
                            // Use image filename as target ID
                            targetName = Path.GetFileNameWithoutExtension(path);

                            // Strip common suffixes
                            string[] suffixes = { "_original", "_luminance", "_thumb", "_target" };
                            foreach (var suffix in suffixes)
                            {
                                if (targetName.EndsWith(suffix))
                                {
                                    targetName = targetName.Substring(0, targetName.Length - suffix.Length);
                                    break;
                                }
                            }
                        }
                        else continue;

                        if (string.IsNullOrEmpty(targetName)) continue;

                        // Check if already exists
                        bool exists = false;
                        for (int i = 0; i < imageTargetsProp.arraySize; i++)
                        {
                            if (imageTargetsProp.GetArrayElementAtIndex(i)
                                .FindPropertyRelative("id").stringValue == targetName)
                            {
                                exists = true;
                                break;
                            }
                        }

                        if (!exists)
                        {
                            int newIndex = imageTargetsProp.arraySize;
                            imageTargetsProp.InsertArrayElementAtIndex(newIndex);
                            var newElement = imageTargetsProp.GetArrayElementAtIndex(newIndex);
                            newElement.FindPropertyRelative("id").stringValue = targetName;
                            newElement.FindPropertyRelative("transform").objectReferenceValue = _target.transform;
                            Debug.Log("[XR8Editor] Added image target: " + targetName);
                        }
                    }

                    ScanForImageTargets();
                    Event.current.Use();
                }
            }
        }

        private void DrawImageTargetEntry(SerializedProperty arrayProp, int index)
        {
            var element = arrayProp.GetArrayElementAtIndex(index);
            var idProp = element.FindPropertyRelative("id");
            var transformProp = element.FindPropertyRelative("transform");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();

            // Show thumbnail if we have one
            var discovered = discoveredTargets.Find(d => d.name == idProp.stringValue);
            if (discovered.thumbnail != null)
            {
                GUILayout.Label(discovered.thumbnail, GUILayout.Width(48), GUILayout.Height(48));
            }

            EditorGUILayout.BeginVertical();
            EditorGUILayout.PropertyField(idProp, new GUIContent("Target ID"));
            EditorGUILayout.PropertyField(transformProp, new GUIContent("Content Root"));
            EditorGUILayout.EndVertical();

            // Remove button
            GUI.color = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("✕", GUILayout.Width(24), GUILayout.Height(24)))
            {
                arrayProp.DeleteArrayElementAtIndex(index);
            }
            GUI.color = Color.white;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        // =========================================================================
        // DISCOVERED TARGETS — auto-scan Assets/image-targets/
        // =========================================================================
        private void DrawDiscoveredTargets(SerializedProperty imageTargetsProp)
        {
            if (discoveredTargets.Count == 0) return;

            // Get list of already-configured IDs
            var configuredIds = new HashSet<string>();
            for (int i = 0; i < imageTargetsProp.arraySize; i++)
            {
                configuredIds.Add(imageTargetsProp.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("id").stringValue);
            }

            // Check if there are any un-configured targets
            bool hasNew = false;
            foreach (var dt in discoveredTargets)
            {
                if (!configuredIds.Contains(dt.name)) { hasNew = true; break; }
            }

            if (!hasNew) return;

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Available Targets (auto-discovered)", EditorStyles.miniLabel);

            foreach (var dt in discoveredTargets)
            {
                if (configuredIds.Contains(dt.name)) continue;

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                // Thumbnail
                if (dt.thumbnail != null)
                {
                    GUILayout.Label(dt.thumbnail, GUILayout.Width(40), GUILayout.Height(40));
                }

                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(dt.name, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(dt.jsonPath, EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();

                GUI.color = new Color(0.4f, 0.9f, 0.4f);
                if (GUILayout.Button("+ Add", GUILayout.Width(60), GUILayout.Height(36)))
                {
                    int newIndex = imageTargetsProp.arraySize;
                    imageTargetsProp.InsertArrayElementAtIndex(newIndex);
                    var newElement = imageTargetsProp.GetArrayElementAtIndex(newIndex);
                    newElement.FindPropertyRelative("id").stringValue = dt.name;
                    newElement.FindPropertyRelative("transform").objectReferenceValue = _target.transform;
                }
                GUI.color = Color.white;

                EditorGUILayout.EndHorizontal();
            }
        }

        private void ScanForImageTargets()
        {
            discoveredTargets.Clear();

            // Scan for JSON files in common locations
            string[] searchPaths = {
                "Assets/image-targets",
                "Assets/ImageTargets",
                "Assets/StreamingAssets/image-targets",
                "Assets/XR8WebAR/Targets"
            };

            foreach (string searchPath in searchPaths)
            {
                if (!Directory.Exists(searchPath)) continue;

                var jsonFiles = Directory.GetFiles(searchPath, "*.json");
                foreach (var jsonFile in jsonFiles)
                {
                    try
                    {
                        string content = File.ReadAllText(jsonFile);

                        // Parse target name from JSON (look for "name" field)
                        string targetName = ExtractJsonField(content, "name");
                        if (string.IsNullOrEmpty(targetName))
                        {
                            // Use filename as fallback
                            targetName = Path.GetFileNameWithoutExtension(jsonFile);
                        }

                        // Look for thumbnail image
                        string dir = Path.GetDirectoryName(jsonFile);
                        string baseName = Path.GetFileNameWithoutExtension(jsonFile);
                        Texture2D thumb = null;

                        // Try common naming patterns
                        string[] imageSuffixes = { "_original", "_luminance", "", "_thumb" };
                        string[] imageExts = { ".jpg", ".png", ".jpeg" };

                        foreach (var suffix in imageSuffixes)
                        {
                            foreach (var ext in imageExts)
                            {
                                string imgPath = Path.Combine(dir, baseName + suffix + ext).Replace("\\", "/");
                                thumb = AssetDatabase.LoadAssetAtPath<Texture2D>(imgPath);
                                if (thumb != null) break;
                            }
                            if (thumb != null) break;
                        }

                        discoveredTargets.Add(new DiscoveredTarget
                        {
                            name = targetName,
                            jsonPath = jsonFile.Replace("\\", "/"),
                            imagePath = "",
                            thumbnail = thumb
                        });
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning("[XR8Editor] Failed to parse: " + jsonFile + " — " + e.Message);
                    }
                }
            }

            lastScanTime = EditorApplication.timeSinceStartup;
        }

        private string ExtractJsonField(string json, string field)
        {
            // Simple JSON field extraction (avoid dependency on JsonUtility for editor)
            string search = "\"" + field + "\"";
            int idx = json.IndexOf(search);
            if (idx < 0) return null;

            // Find the value after the colon
            idx = json.IndexOf(':', idx + search.Length);
            if (idx < 0) return null;

            // Find the opening quote
            idx = json.IndexOf('"', idx + 1);
            if (idx < 0) return null;

            int endIdx = json.IndexOf('"', idx + 1);
            if (endIdx < 0) return null;

            return json.Substring(idx + 1, endIdx - idx - 1);
        }

        // =========================================================================
        // EDITOR DEBUG — Found/Lost buttons
        // =========================================================================
        private void DrawEditorDebug()
        {
            showDebug = EditorGUILayout.Foldout(showDebug, "Editor Debug", true);
            if (!showDebug) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (Application.IsPlaying(_target))
            {
                var imageTargetsProp = serializedObject.FindProperty("imageTargets");
                var trackedIdsProp = serializedObject.FindProperty("trackedIds");
                var trackedIds = new List<string>();
                if (trackedIdsProp != null)
                {
                    for (int i = 0; i < trackedIdsProp.arraySize; i++)
                        trackedIds.Add(trackedIdsProp.GetArrayElementAtIndex(i).stringValue);
                }

                for (int i = 0; i < imageTargetsProp.arraySize; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    var elem = imageTargetsProp.GetArrayElementAtIndex(i);
                    var id = elem.FindPropertyRelative("id").stringValue;
                    EditorGUILayout.LabelField(id);

                    bool found = trackedIds.Contains(id);

                    GUI.enabled = !found;
                    if (GUILayout.Button("Simulate Found", GUILayout.Width(110)))
                    {
                        _target.SendMessage("OnTrackingFound", id);
                    }
                    GUI.enabled = found;
                    if (GUILayout.Button("Simulate Lost", GUILayout.Width(110)))
                    {
                        _target.SendMessage("OnTrackingLost", id);
                    }
                    GUI.enabled = true;

                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                GUI.color = Color.yellow;
                EditorGUILayout.LabelField("Enter Play Mode to debug tracking");
                GUI.color = Color.white;
            }

            EditorGUILayout.EndVertical();
        }

        private void OnEnable()
        {
            _target = (XR8ImageTracker)target;
            ScanForImageTargets();
        }
    }
}
