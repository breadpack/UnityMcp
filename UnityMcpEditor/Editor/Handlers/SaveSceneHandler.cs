using Newtonsoft.Json.Linq;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace BreadPack.Mcp.Unity
{
    public class SaveSceneHandler : IRequestHandler
    {
        public string ToolName => "unity_save_scene";

        public object Handle(JObject @params)
        {
            var scenePath = @params?["scenePath"]?.Value<string>();
            var saveAs = @params?["saveAs"]?.Value<bool>() ?? false;

            var scene = SceneManager.GetActiveScene();

            if (!scene.IsValid())
                throw new System.InvalidOperationException("No valid active scene found.");

            bool saved;
            if (!string.IsNullOrEmpty(scenePath))
            {
                saved = EditorSceneManager.SaveScene(scene, scenePath, saveAs);
            }
            else
            {
                saved = EditorSceneManager.SaveScene(scene);
            }

            if (!saved)
                throw new System.InvalidOperationException($"Failed to save scene: {scene.name}");

            return new
            {
                saved = true,
                scenePath = scene.path
            };
        }
    }
}
