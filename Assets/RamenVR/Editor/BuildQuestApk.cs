using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RamenVR.EditorTools
{
    /// Builds the Meta Quest APK and writes the result to Builds/build_result.txt
    public static class BuildQuestApk
    {
        public const string OutDir = "Builds";
        public const string ApkPath = "Builds/VRVillage.apk";

        [MenuItem("Ramen VR/Build Quest APK")]
        public static void Build()
        {
            Directory.CreateDirectory(OutDir);
            string log = Path.Combine(OutDir, "build_result.txt");
            File.WriteAllText(log, "BUILDING " + System.DateTime.Now + "\n");
            try
            {
                var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                var opts = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = ApkPath,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None
                };
                var report = BuildPipeline.BuildPlayer(opts);
                var s = report.summary;
                string text = $"RESULT {s.result}\nsize {s.totalSize / (1024 * 1024)} MB\ntime {s.totalTime}\nerrors {s.totalErrors} warnings {s.totalWarnings}\nscenes {string.Join(", ", scenes)}\n";
                foreach (var step in report.steps)
                    foreach (var m in step.messages)
                        if (m.type == LogType.Error || m.type == LogType.Exception) text += "ERR " + m.content + "\n";
                File.AppendAllText(log, text);
            }
            catch (System.Exception e)
            {
                File.AppendAllText(log, "EXCEPTION " + e + "\n");
            }
        }
    }
}
