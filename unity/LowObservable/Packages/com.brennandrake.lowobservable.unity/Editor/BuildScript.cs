using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LowObservable.EditorTools
{
    /// <summary>
    /// Batch-mode build entry points used by scripts/pc/build_unity.sh and the Gitea runner:
    ///   Unity -batchmode -quit -projectPath unity/LowObservable -executeMethod LowObservable.EditorTools.BuildScript.WebGL
    ///         -buildVersion 0.1 -buildSha abc1234
    /// Output goes to build/&lt;target&gt;/ at the repository root. The version and git sha become
    /// Application.version ("0.1+abc1234"), which the HUD shows, so playtest feedback names an exact build.
    /// </summary>
    public static class BuildScript
    {
        public static void WebGL() => Build(BuildTarget.WebGL, "webgl", "index.html");
        public static void Linux() => Build(BuildTarget.StandaloneLinux64, "linux", "LowObservable.x86_64");
        public static void Windows() => Build(BuildTarget.StandaloneWindows64, "windows", "LowObservable.exe");

        /// <summary>GameCI entry point: same as WebGL, version from the environment if not passed as arguments.</summary>
        public static void WebGLFromCi() => WebGL();

        private static void Build(BuildTarget target, string folder, string executable)
        {
            string version = Arg("-buildVersion") ?? ReadVersionFile() ?? "0.0";
            string sha = Arg("-buildSha") ?? Environment.GetEnvironmentVariable("GITHUB_SHA")?.Substring(0, 7) ?? "local";
            PlayerSettings.bundleVersion = $"{version}+{sha}";
            PlayerSettings.productName = "Low Observable";
            PlayerSettings.companyName = "Brennan Drake";

            if (target == BuildTarget.WebGL)
            {
                // Brotli is smallest; docker/web/nginx.conf serves .br with the right headers.
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
                PlayerSettings.WebGL.decompressionFallback = false;
            }

            // The escape campaign is the game; creating its scene also makes it the first build scene.
            if (!File.Exists(LowObservableMenu.EscapeScenePath)) LowObservableMenu.CreateSceneAt(LowObservableMenu.EscapeScenePath, escape: true);
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) scenes = new[] { LowObservableMenu.EscapeScenePath };

            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            string outDir = Path.Combine(repoRoot, "build", folder);
            Directory.CreateDirectory(outDir);
            string location = target == BuildTarget.WebGL ? outDir : Path.Combine(outDir, executable);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = location,
                target = target,
                options = BuildOptions.None,
            });

            var summary = report.summary;
            Debug.Log($"[LowObservable] {target} build {summary.result}: {summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:0}s -> {location}");
            if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        private static string ReadVersionFile()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../VERSION"));
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
    }
}
