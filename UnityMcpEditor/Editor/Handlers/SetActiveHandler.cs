using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    public class SetActiveHandler : IRequestHandler
    {
        public string ToolName => "unity_set_active";

        public object Handle(JObject @params)
        {
            var go = GameObjectResolver.Resolve(@params);
            var active = @params["active"]?.Value<bool>() ?? true;

            Undo.RecordObject(go, "Set Active");
            go.SetActive(active);
            EditorUtility.SetDirty(go);

            return new
            {
                name = go.name,
                path = GameObjectResolver.GetPath(go),
                active = go.activeSelf,
                instanceId = go.GetInstanceID()
            };
        }
    }
}
