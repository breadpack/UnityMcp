using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    public class SelectGameObjectHandler : IRequestHandler
    {
        public string ToolName => "unity_select_gameobject";

        public object Handle(JObject @params)
        {
            var go = GameObjectResolver.Resolve(@params);

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);

            return new
            {
                name = go.name,
                path = GameObjectResolver.GetPath(go),
                instanceId = go.GetInstanceID()
            };
        }
    }
}
