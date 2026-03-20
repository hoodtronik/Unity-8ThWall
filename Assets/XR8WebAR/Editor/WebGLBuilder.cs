
using UnityEditor;
using UnityEngine;

public static class WebGLBuilder
{
    [MenuItem("XR8 WebAR/Build WebGL")]
    public static void BuildWebGL()
    {
        string buildPath = "Build";
        string[] scenes = new string[] { "Assets/Scenes/AR Scene.unity/AR Scene.unity" };
        
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = scenes;
        buildPlayerOptions.locationPathName = buildPath;
        buildPlayerOptions.target = BuildTarget.WebGL;
        buildPlayerOptions.options = BuildOptions.None;
        
        Debug.Log("[WebGLBuilder] Starting WebGL build to: " + buildPath);
        var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        
        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log("[WebGLBuilder] Build SUCCEEDED! Output: " + buildPath);
        }
        else
        {
            Debug.LogError("[WebGLBuilder] Build FAILED: " + report.summary.result);
        }
    }
}
