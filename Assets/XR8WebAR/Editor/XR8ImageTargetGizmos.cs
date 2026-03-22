using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace XR8WebAR.Editor
{
    /// <summary>
    /// Always-visible scene gizmo that draws image target thumbnails
    /// at their content root positions, even when not selected.
    /// </summary>
    [InitializeOnLoad]
    public static class XR8ImageTargetGizmos
    {
        private static Mesh _quadMesh;
        private static Material _textureMat;
        private static Dictionary<string, Texture2D> _thumbnailCache = new Dictionary<string, Texture2D>();

        static XR8ImageTargetGizmos()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            // Find all XR8ImageTrackers in the scene
            var trackers = Object.FindObjectsByType<XR8ImageTracker>(FindObjectsSortMode.None);
            foreach (var tracker in trackers)
            {
                DrawTrackerTargets(tracker);
            }
        }

        private static void DrawTrackerTargets(XR8ImageTracker tracker)
        {
            // Use SerializedObject to read the private imageTargets list
            var so = new SerializedObject(tracker);
            var imageTargetsProp = so.FindProperty("imageTargets");
            if (imageTargetsProp == null) return;

            for (int i = 0; i < imageTargetsProp.arraySize; i++)
            {
                var element = imageTargetsProp.GetArrayElementAtIndex(i);
                var idProp = element.FindPropertyRelative("id");
                var anchorProp = element.FindPropertyRelative("anchor");
                var transformProp = element.FindPropertyRelative("transform");

                // Determine which transform to use for positioning
                Transform drawAt = null;
                if (anchorProp != null && anchorProp.objectReferenceValue != null)
                    drawAt = (Transform)anchorProp.objectReferenceValue;
                else if (transformProp.objectReferenceValue != null)
                    drawAt = (Transform)transformProp.objectReferenceValue;

                if (drawAt == null) continue;

                string targetId = idProp.stringValue;
                if (string.IsNullOrEmpty(targetId)) continue;

                // Skip drawing wireframe gizmo if target plane has a Renderer (it's already visible)
                var renderer = drawAt.GetComponent<Renderer>();
                if (renderer != null && renderer.enabled)
                {
                    // Just draw the label
                    var labelStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        normal = { textColor = new Color(0f, 0.9f, 0.4f) },
                        fontSize = 11
                    };
                    Handles.Label(drawAt.position + Vector3.up * 0.2f, "📷 " + targetId, labelStyle);
                    continue;
                }

                var thumb = FindThumbnail(targetId);
                DrawTargetInScene(drawAt, targetId, thumb);
            }
        }

        private static Texture2D FindThumbnail(string targetId)
        {
            if (_thumbnailCache.ContainsKey(targetId))
                return _thumbnailCache[targetId];

            // Search common locations
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
                if (!Directory.Exists(dir)) continue;
                foreach (var suffix in suffixes)
                {
                    foreach (var ext in exts)
                    {
                        string path = Path.Combine(dir, targetId + suffix + ext).Replace("\\", "/");
                        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                        if (tex != null)
                        {
                            _thumbnailCache[targetId] = tex;
                            return tex;
                        }
                    }
                }
            }

            _thumbnailCache[targetId] = null;
            return null;
        }

        private static Mesh GetQuadMesh()
        {
            if (_quadMesh != null) return _quadMesh;
            _quadMesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            _quadMesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, 0, -0.5f),
                new Vector3(0.5f, 0, -0.5f),
                new Vector3(0.5f, 0, 0.5f),
                new Vector3(-0.5f, 0, 0.5f)
            };
            _quadMesh.uv = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };
            _quadMesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
            _quadMesh.RecalculateNormals();
            return _quadMesh;
        }

        private static Material GetTextureMaterial()
        {
            if (_textureMat != null) return _textureMat;
            // Use a shader that renders both sides and is unlit
            var shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("UI/Default");
            _textureMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return _textureMat;
        }

        private static void DrawTargetInScene(Transform t, string id, Texture2D thumb)
        {
            var pos = t.position;
            var rot = t.rotation;

            if (thumb != null)
            {
                float aspect = (float)thumb.width / thumb.height;
                float quadSize = 0.3f;
                var scale = new Vector3(quadSize * aspect, 1f, quadSize);
                var matrix = Matrix4x4.TRS(pos, rot, scale);

                var mat = GetTextureMaterial();
                mat.mainTexture = thumb;
                mat.SetPass(0);
                Graphics.DrawMeshNow(GetQuadMesh(), matrix);

                // Draw border
                Handles.color = new Color(0f, 0.9f, 0.4f, 1f);
                Handles.matrix = Matrix4x4.TRS(pos, rot, Vector3.one);
                float halfW = quadSize * aspect / 2f;
                float halfH = quadSize / 2f;
                Vector3[] outline = {
                    new Vector3(-halfW, 0.002f, -halfH),
                    new Vector3(halfW, 0.002f, -halfH),
                    new Vector3(halfW, 0.002f, halfH),
                    new Vector3(-halfW, 0.002f, halfH)
                };
                Handles.DrawLine(outline[0], outline[1]);
                Handles.DrawLine(outline[1], outline[2]);
                Handles.DrawLine(outline[2], outline[3]);
                Handles.DrawLine(outline[3], outline[0]);
                Handles.matrix = Matrix4x4.identity;
            }
            else
            {
                // Wireframe placeholder
                float size = 0.3f;
                Handles.color = new Color(0f, 0.9f, 0.4f, 0.8f);
                Handles.matrix = Matrix4x4.TRS(pos, rot, Vector3.one);
                Vector3[] c = {
                    new Vector3(-size/2, 0, -size/2),
                    new Vector3(size/2, 0, -size/2),
                    new Vector3(size/2, 0, size/2),
                    new Vector3(-size/2, 0, size/2)
                };
                Handles.DrawLine(c[0], c[1]);
                Handles.DrawLine(c[1], c[2]);
                Handles.DrawLine(c[2], c[3]);
                Handles.DrawLine(c[3], c[0]);
                Handles.DrawDottedLine(c[0], c[2], 3f);
                Handles.matrix = Matrix4x4.identity;
            }

            // Label
            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = new Color(0f, 0.9f, 0.4f) },
                fontSize = 11
            };
            Handles.Label(pos + Vector3.up * 0.2f, "📷 " + id, style);
        }
    }
}
