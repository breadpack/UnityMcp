using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityMcp.Editor
{
    public class SetTransformHandler : IRequestHandler
    {
        public string ToolName => "unity_set_transform";

        public object Handle(JObject @params)
        {
            var go = GameObjectResolver.Resolve(@params);
            var space = @params?["space"]?.Value<string>() ?? "local";
            var isLocal = space.ToLower() != "world";

            UndoHelper.RecordObject(go.transform, $"Set Transform {go.name}");

            var posToken = @params?["position"];
            var rotToken = @params?["rotation"];
            var scaleToken = @params?["scale"];

            if (posToken != null)
            {
                var pos = (Vector3)PropertySetter.ConvertValue(posToken, typeof(Vector3));
                if (isLocal) go.transform.localPosition = pos;
                else go.transform.position = pos;
            }

            if (rotToken != null)
            {
                var rot = (Vector3)PropertySetter.ConvertValue(rotToken, typeof(Vector3));
                if (isLocal) go.transform.localEulerAngles = rot;
                else go.transform.eulerAngles = rot;
            }

            if (scaleToken != null)
            {
                var scale = (Vector3)PropertySetter.ConvertValue(scaleToken, typeof(Vector3));
                go.transform.localScale = scale;
            }

            UndoHelper.MarkDirty(go.transform);

            return new
            {
                name = go.name,
                path = GameObjectResolver.GetPath(go),
                localPosition = FormatVector3(go.transform.localPosition),
                localRotation = FormatVector3(go.transform.localEulerAngles),
                localScale = FormatVector3(go.transform.localScale)
            };
        }

        private static object FormatVector3(Vector3 v)
            => new { x = v.x, y = v.y, z = v.z };
    }
}
