using Newtonsoft.Json.Linq;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace BreadPack.Mcp.Unity
{
    public class BuildHandler : IRequestHandler
    {
        public string ToolName => "unity_build";

        public object Handle(JObject @params)
        {
            var outputPath = @params["outputPath"]?.Value<string>();
            if (string.IsNullOrEmpty(outputPath))
                throw new System.ArgumentException("outputPath is required");

            var targetStr = @params["target"]?.Value<string>();
            var buildTarget = ResolveBuildTarget(targetStr);

            var scenesToken = @params["scenes"];
            string[] scenes;
            if (scenesToken != null && scenesToken.Type != JTokenType.Null)
            {
                scenes = scenesToken.Type == JTokenType.String
                    ? JArray.Parse(scenesToken.Value<string>()).Select(s => s.Value<string>()).ToArray()
                    : scenesToken.ToObject<string[]>();
            }
            else
            {
                scenes = EditorBuildSettings.scenes
                    .Where(s => s.enabled)
                    .Select(s => s.path)
                    .ToArray();
            }

            var report = BuildPipeline.BuildPlayer(scenes, outputPath, buildTarget, BuildOptions.None);
            var summary = report.summary;

            return new
            {
                success = summary.result == BuildResult.Succeeded,
                totalErrors = summary.totalErrors,
                totalWarnings = summary.totalWarnings,
                outputPath,
                totalTime = summary.totalTime.TotalSeconds
            };
        }

        private static BuildTarget ResolveBuildTarget(string target)
        {
            if (string.IsNullOrEmpty(target))
                return EditorUserBuildSettings.activeBuildTarget;

            return target switch
            {
                "Windows" => BuildTarget.StandaloneWindows64,
                "macOS" => BuildTarget.StandaloneOSX,
                "Linux" => BuildTarget.StandaloneLinux64,
                "Android" => BuildTarget.Android,
                "iOS" => BuildTarget.iOS,
                "WebGL" => BuildTarget.WebGL,
                _ => throw new System.ArgumentException(
                    $"Unknown build target: {target}. Use 'Windows', 'macOS', 'Linux', 'Android', 'iOS', or 'WebGL'")
            };
        }
    }
}
