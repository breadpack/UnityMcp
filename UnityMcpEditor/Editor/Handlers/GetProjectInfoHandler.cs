using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BreadPack.Mcp.Unity
{
    public class GetProjectInfoHandler : IRequestHandler
    {
        public string ToolName => "unity_get_project_info";

        public object Handle(JObject @params)
        {
            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);

            return new
            {
                projectName = Application.productName,
                companyName = Application.companyName,
                unityVersion = Application.unityVersion,
                projectPath = Application.dataPath.Replace("/Assets", ""),
                buildTarget = buildTarget.ToString(),
                buildTargetGroup = buildTargetGroup.ToString(),
                scriptingBackend = PlayerSettings.GetScriptingBackend(buildTargetGroup).ToString(),
                apiCompatibilityLevel = PlayerSettings.GetApiCompatibilityLevel(buildTargetGroup).ToString(),
                renderPipeline = GetRenderPipelineName(),
                colorSpace = PlayerSettings.colorSpace.ToString(),
                isPlaying = EditorApplication.isPlaying,
                isCompiling = EditorApplication.isCompiling,
                platform = Application.platform.ToString(),
                packages = GetInstalledPackages()
            };
        }

        private static string GetRenderPipelineName()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null) return "Built-in";
            var typeName = pipeline.GetType().Name;
            if (typeName.Contains("Universal")) return "URP";
            if (typeName.Contains("HDRenderPipeline") || typeName.Contains("HDRP")) return "HDRP";
            return typeName;
        }

        private static object GetInstalledPackages()
        {
            try
            {
                var manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
                var manifest = JObject.Parse(File.ReadAllText(manifestPath));
                var deps = manifest["dependencies"] as JObject;
                if (deps == null) return new object[0];
                return deps.Properties()
                    .Select(p => new { name = p.Name, version = p.Value.ToString() })
                    .ToList();
            }
            catch
            {
                return new object[0];
            }
        }
    }
}
