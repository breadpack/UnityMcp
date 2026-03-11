using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace BreadPack.Mcp.Unity
{
    public class GetAvailableActionsHandler : IRequestHandler
    {
        public string ToolName => "unity_get_available_actions";

        public object Handle(JObject @params)
        {
            if (!EditorApplication.isPlaying)
                throw new System.Exception("Play Mode에서만 사용 가능합니다");

            var doc = Object.FindFirstObjectByType<UIDocument>();
            if (doc == null || doc.rootVisualElement == null)
                return new { actions = new object[0] };

            return new { actions = VisualElementSerializer.GetClickableElements(doc.rootVisualElement) };
        }
    }
}
