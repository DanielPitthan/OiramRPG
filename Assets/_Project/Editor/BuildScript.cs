using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Oiram.EditorTools
{
    /// <summary>Builds de linha de comando: -executeMethod Oiram.EditorTools.BuildScript.BuildWindows</summary>
    public static class BuildScript
    {
        [MenuItem("OiramRPG/Build Windows (Builds/Windows)")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/OiramRPG.exe");

        [MenuItem("OiramRPG/Build WebGL (Builds/WebGL)")]
        public static void BuildWebGL() => Build(BuildTarget.WebGL, "Builds/WebGL");

        static void Build(BuildTarget target, string location)
        {
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = location,
                target = target,
                options = BuildOptions.Development,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[OiramRPG] Build {target}: {summary.result} — {summary.totalErrors} erro(s), {summary.totalSize / (1024 * 1024)} MB em {summary.totalTime}");
            if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
