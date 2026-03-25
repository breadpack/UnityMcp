using Newtonsoft.Json.Linq;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace BreadPack.Mcp.Unity
{
    public class LoadSceneHandler : IRequestHandler
    {
        public string ToolName => "unity_load_scene";

        public object Handle(JObject @params)
        {
            var scenePath = @params?["scenePath"]?.Value<string>();

            if (string.IsNullOrEmpty(scenePath))
                throw new System.ArgumentException("scenePath is required.");

            if (!System.IO.File.Exists(scenePath))
                throw new System.IO.FileNotFoundException($"Scene file not found: {scenePath}");

            var additive = @params?["additive"]?.Value<bool>() ?? false;
            var mode = additive ? OpenSceneMode.Additive : OpenSceneMode.Single;

            var scene = EditorSceneManager.OpenScene(scenePath, mode);

            return new
            {
                sceneName = scene.name,
                scenePath = scene.path
            };
        }
    }
}
