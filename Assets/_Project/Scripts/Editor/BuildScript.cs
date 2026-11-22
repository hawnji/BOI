using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hollowcrawl.EditorTools
{
    /// <summary>
    /// Windows build entry points. Menu: Hollowcrawl > Build > Windows (x64). Command line / CI:
    ///   Unity -batchmode -quit -projectPath . -executeMethod Hollowcrawl.EditorTools.BuildScript.BuildWindowsCI -buildOutput Builds/Windows
    /// The game boots from GameBootstrap (RuntimeInitializeOnLoadMethod), so the only scene it needs is an empty one;
    /// it is created on first build if missing.
    /// </summary>
    public static class BuildScript
    {
        public const string MainScene = "Assets/_Project/Scenes/Main.unity";
        public const string ExeName = "Hollowcrawl.exe";

        [MenuItem("Hollowcrawl/Build/Windows (x64)", priority = 100)]
        public static void BuildWindowsMenu()
        {
            var report = BuildWindows(Path.Combine("Builds", "Windows"), development: false);
            Debug.Log("[Build] " + report.summary.result + " -> " + report.summary.outputPath);
        }

        [MenuItem("Hollowcrawl/Build/Windows (x64, development)", priority = 101)]
        public static void BuildWindowsDevMenu() => BuildWindows(Path.Combine("Builds", "WindowsDev"), development: true);

        /// <summary>Batch-mode entry point; exits with 0 on success, 1 on failure.</summary>
        public static void BuildWindowsCI()
        {
            string output = Arg("-buildOutput") ?? Path.Combine("Builds", "Windows");
            bool dev = Environment.GetCommandLineArgs().Contains("-development");
            var report = BuildWindows(output, dev);
            Debug.Log($"[Build] {report.summary.result}: {report.summary.totalErrors} errors, {report.summary.totalWarnings} warnings, {report.summary.totalSize} bytes");
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        public static BuildReport BuildWindows(string outputDir, bool development)
        {
            EnsureMainScene();
            PlayerSettings.companyName = "Hawnji";
            PlayerSettings.productName = "Hollowcrawl";
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            Directory.CreateDirectory(outputDir);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { MainScene },
                locationPathName = Path.Combine(outputDir, ExeName),
                target = BuildTarget.StandaloneWindows64,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };
            return BuildPipeline.BuildPlayer(options);
        }

        private static void EnsureMainScene()
        {
            if (!File.Exists(MainScene))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(MainScene));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, MainScene);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScene, true) };
        }

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
