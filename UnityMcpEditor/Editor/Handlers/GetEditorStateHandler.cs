using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    public class GetEditorStateHandler : IRequestHandler
    {
        public string ToolName => "unity_get_editor_state";

        public object Handle(JObject @params)
        {
            return new
            {
                isCompiling = EditorApplication.isCompiling,
                isUpdating = EditorApplication.isUpdating,
                isPlaying = EditorApplication.isPlaying,
                unityVersion = Application.unityVersion,
                projectName = Application.productName,
            };
        }
    }
}
