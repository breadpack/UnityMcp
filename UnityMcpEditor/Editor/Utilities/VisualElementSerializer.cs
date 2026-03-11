using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace UnityMcp.Editor
{
    public static class VisualElementSerializer
    {
        public static Dictionary<string, object> Serialize(VisualElement element, int maxDepth = 10, int currentDepth = 0)
        {
            if (element == null || currentDepth > maxDepth) return null;

            var result = new Dictionary<string, object>
            {
                ["type"] = element.GetType().Name,
                ["name"] = element.name,
                ["classes"] = element.GetClasses().ToList(),
                ["enabled"] = element.enabledSelf,
                ["visible"] = element.resolvedStyle.display != DisplayStyle.None
            };

            if (element is TextElement textEl)
                result["text"] = textEl.text;

            if (element is BaseField<string> stringField)
                result["value"] = stringField.value;

            if (element is BaseField<float> floatField)
                result["value"] = floatField.value;

            var children = new List<Dictionary<string, object>>();
            foreach (var child in element.Children())
            {
                var serialized = Serialize(child, maxDepth, currentDepth + 1);
                if (serialized != null) children.Add(serialized);
            }
            if (children.Count > 0)
                result["children"] = children;

            return result;
        }

        public static List<Dictionary<string, object>> GetClickableElements(VisualElement root)
        {
            var result = new List<Dictionary<string, object>>();
            CollectClickable(root, result);
            return result;
        }

        private static void CollectClickable(VisualElement element, List<Dictionary<string, object>> result)
        {
            if (element == null) return;

            bool isEnabled = element.enabledSelf && element.resolvedStyle.display != DisplayStyle.None;
            bool isButton = element is Button;
            bool hasName = !string.IsNullOrEmpty(element.name);

            if (isEnabled && (isButton || hasName))
            {
                var info = new Dictionary<string, object>
                {
                    ["elementName"] = element.name ?? "",
                    ["elementType"] = element.GetType().Name,
                    ["actionType"] = "click"
                };
                if (element is TextElement textEl)
                    info["text"] = textEl.text;
                result.Add(info);
            }

            foreach (var child in element.Children())
                CollectClickable(child, result);
        }
    }
}
