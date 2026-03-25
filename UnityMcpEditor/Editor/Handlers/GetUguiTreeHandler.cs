using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BreadPack.Mcp.Unity
{
    public class GetUguiTreeHandler : IRequestHandler
    {
        public string ToolName => "unity_get_ugui_tree";

        public object Handle(JObject @params)
        {
            int maxDepth = @params?["maxDepth"]?.Value<int>() ?? 5;
            bool includeDetails = @params?["includeDetails"]?.Value<bool>() ?? false;

            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            if (canvases == null || canvases.Length == 0)
                throw new System.Exception("씬에서 Canvas를 찾을 수 없습니다");

            var canvasArray = new JArray();
            foreach (var canvas in canvases)
            {
                var canvasObj = new JObject
                {
                    ["name"] = canvas.name,
                    ["renderMode"] = canvas.renderMode.ToString(),
                    ["children"] = BuildChildren(canvas.GetComponent<RectTransform>(), includeDetails, 1, maxDepth)
                };
                canvasArray.Add(canvasObj);
            }

            return new { canvases = canvasArray };
        }

        private JArray BuildChildren(RectTransform parent, bool includeDetails, int currentDepth, int maxDepth)
        {
            var children = new JArray();
            if (parent == null || currentDepth > maxDepth)
                return children;

            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i) as RectTransform;
                if (child == null)
                    continue;

                var node = new JObject
                {
                    ["name"] = child.name,
                    ["active"] = child.gameObject.activeSelf,
                    ["components"] = GetUiComponentNames(child)
                };

                if (includeDetails)
                {
                    node["anchoredPosition"] = new JObject
                    {
                        ["x"] = child.anchoredPosition.x,
                        ["y"] = child.anchoredPosition.y
                    };
                    node["sizeDelta"] = new JObject
                    {
                        ["x"] = child.sizeDelta.x,
                        ["y"] = child.sizeDelta.y
                    };
                    node["pivot"] = new JObject
                    {
                        ["x"] = child.pivot.x,
                        ["y"] = child.pivot.y
                    };
                    node["anchorMin"] = new JObject
                    {
                        ["x"] = child.anchorMin.x,
                        ["y"] = child.anchorMin.y
                    };
                    node["anchorMax"] = new JObject
                    {
                        ["x"] = child.anchorMax.x,
                        ["y"] = child.anchorMax.y
                    };
                }

                if (currentDepth < maxDepth)
                {
                    node["children"] = BuildChildren(child, includeDetails, currentDepth + 1, maxDepth);
                }

                children.Add(node);
            }

            return children;
        }

        private JArray GetUiComponentNames(RectTransform rectTransform)
        {
            var names = new JArray();
            var components = rectTransform.GetComponents<Component>();

            foreach (var component in components)
            {
                if (component == null)
                    continue;

                var type = component.GetType();

                // Skip Transform/RectTransform as they are always present
                if (type == typeof(Transform) || type == typeof(RectTransform))
                    continue;

                names.Add(type.Name);
            }

            return names;
        }
    }
}
