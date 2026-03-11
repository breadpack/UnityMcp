using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    public class GetComponentDetailsHandler : IRequestHandler
    {
        public string ToolName => "unity_get_component_details";

        public object Handle(JObject @params)
        {
            var go = GameObjectResolver.Resolve(@params);
            string componentType = @params?["componentType"]?.Value<string>();
            int index = @params?["index"]?.Value<int>() ?? 0;

            var components = GetTargetComponents(go, componentType, index);
            var result = new List<object>();

            foreach (var comp in components)
            {
                if (comp == null) continue;

                using var so = new SerializedObject(comp);
                var properties = SerializedPropertyReader.ReadAllProperties(so);

                result.Add(new
                {
                    type = comp.GetType().Name,
                    fullType = comp.GetType().FullName,
                    properties
                });
            }

            return new
            {
                gameObject = go.name,
                path = GameObjectResolver.GetPath(go),
                instanceId = go.GetInstanceID(),
                components = result
            };
        }

        private List<Component> GetTargetComponents(GameObject go, string componentType, int index)
        {
            // componentType 미지정 → 모든 컴포넌트 반환
            if (string.IsNullOrEmpty(componentType))
            {
                return go.GetComponents<Component>()
                    .Where(c => c != null)
                    .ToList();
            }

            // 특정 타입 지정 → ComponentResolver로 해석하여 index번째 반환
            var comp = ComponentResolver.GetComponent(go, componentType, index);
            return new List<Component> { comp };
        }
    }
}
