using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;
using System.Linq;

public static class WebGLBuilder
{
    private const string DefaultBuildPath = "WebGLBuild";

    [MenuItem("XR8 WebAR/Build WebGL")]
    public static void BuildWebGL()
    {
        // Check WebGL support
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
        {
            EditorUtility.DisplayDialog("WebGL Not Installed",
                "WebGL Build Support is not installed.\n\n" +
                "Open Unity Hub → Installs → (your version) → Add Modules → WebGL Build Support",
                "OK");
            Debug.LogError("[WebGLBuilder] WebGL Build Support not installed. Add it via Unity Hub.");
            return;
        }

        // Determine scenes to build (prefer Build Settings, fall back to current scene)
        string[] scenes;
        var buildSettingsScenes = EditorBuildSettings.scenes
            .Where(s => s.enabled && !string.IsNullOrEmpty(s.path) && File.Exists(s.path))
            .Select(s => s.path)
            .ToArray();

        if (buildSettingsScenes.Length > 0)
        {
            scenes = buildSettingsScenes;
            Debug.Log("[WebGLBuilder] Using " + scenes.Length + " scene(s) from Build Settings");
        }
        else
        {
            // Use the currently open scene
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(activeScene.path))
            {
                EditorUtility.DisplayDialog("No Scene",
                    "No scenes in Build Settings and current scene is unsaved.\n\n" +
                    "Save your scene first, or add scenes to File → Build Settings.",
                    "OK");
                Debug.LogError("[WebGLBuilder] No valid scenes to build.");
                return;
            }
            scenes = new string[] { activeScene.path };
            Debug.Log("[WebGLBuilder] No Build Settings scenes found, using active scene: " + activeScene.path);
        }

        // Ask for build folder
        string buildPath = EditorUtility.SaveFolderPanel("Choose WebGL Build Folder",
            Path.GetDirectoryName(Application.dataPath), DefaultBuildPath);

        if (string.IsNullOrEmpty(buildPath))
        {
            Debug.Log("[WebGLBuilder] Build cancelled by user.");
            return;
        }

        // Prevent using project root as build path
        string projectRoot = Path.GetDirectoryName(Application.dataPath).Replace("\\", "/");
        if (buildPath.Replace("\\", "/").TrimEnd('/') == projectRoot.TrimEnd('/'))
        {
            buildPath = Path.Combine(buildPath, DefaultBuildPath);
            Debug.Log("[WebGLBuilder] Project root selected — using subdirectory: " + buildPath);
        }

        // Ensure build directory exists
        if (!Directory.Exists(buildPath))
            Directory.CreateDirectory(buildPath);

        // Build
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = buildPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        Debug.Log("[WebGLBuilder] Starting WebGL build...\n" +
                  "  Output: " + buildPath + "\n" +
                  "  Scenes: " + string.Join(", ", scenes));

        BuildReport report = BuildPipeline.BuildPlayer(options);

        if (report == null)
        {
            Debug.LogError("[WebGLBuilder] Build returned null report — check build path and settings.");
            return;
        }

        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log("[WebGLBuilder] Build SUCCEEDED! (" +
                      report.summary.totalTime.TotalSeconds.ToString("F1") + "s)\n" +
                      "  Output: " + buildPath);

            if (EditorUtility.DisplayDialog("Build Complete!",
                "WebGL build succeeded!\n\nOutput: " + buildPath +
                "\n\nOpen the build folder?", "Open Folder", "Close"))
            {
                EditorUtility.RevealInFinder(buildPath);
            }
        }
        else
        {
            Debug.LogError("[WebGLBuilder] Build FAILED: " + report.summary.result +
                           " (" + report.summary.totalErrors + " errors)");
        }
    }
}
