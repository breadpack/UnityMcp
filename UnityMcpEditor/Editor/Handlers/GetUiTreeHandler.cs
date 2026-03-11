using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMcp.Editor
{
    public class GetUiTreeHandler : IRequestHandler
    {
        public string ToolName => "unity_get_ui_tree";

        public object Handle(JObject @params)
        {
            if (!EditorApplication.isPlaying)
                throw new System.Exception("Play Mode에서만 사용 가능합니다");

            int maxDepth = @params?["maxDepth"]?.Value<int>() ?? 10;

            var doc = Object.FindFirstObjectByType<UIDocument>();
            if (doc == null || doc.rootVisualElement == null)
                throw new System.Exception("UIDocument를 찾을 수 없습니다");

            return new { root = VisualElementSerializer.Serialize(doc.rootVisualElement, maxDepth) };
        }
    }
}
