using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BreadPack.Mcp.Unity.Input
{
    public sealed class TargetSpec
    {
        public string Path;          // "Canvas/Panel/Button" or VisualElement name
        public int? Index;           // 동명 분별
        public int? InstanceId;
        public string VisualElement; // UI Toolkit
        public Vector2? Position;    // 직접 스크린 좌표
        public Vector3? WorldPoint;  // 3D 월드 좌표

        public static TargetSpec Parse(JObject root, string targetKey = "target")
        {
            var spec = new TargetSpec();

            // worldPoint
            if (root[$"worldPoint"] is JObject wp)
            {
                spec.WorldPoint = new Vector3(
                    wp["x"]?.Value<float>() ?? 0,
                    wp["y"]?.Value<float>() ?? 0,
                    wp["z"]?.Value<float>() ?? 0);
                return spec;
            }

            // position
            if (root["position"] is JObject pos)
            {
                spec.Position = new Vector2(
                    pos["x"]?.Value<float>() ?? 0,
                    pos["y"]?.Value<float>() ?? 0);
                return spec;
            }

            // target (string or object)
            var target = root[targetKey];
            if (target == null)
                throw new System.ArgumentException($"target/position/worldPoint 중 하나는 필수입니다");

            if (target.Type == JTokenType.String)
            {
                spec.Path = target.Value<string>();
                return spec;
            }

            if (target is JObject obj)
            {
                spec.Path = obj["path"]?.Value<string>();
                spec.Index = obj["index"]?.Value<int?>();
                spec.InstanceId = obj["instanceId"]?.Value<int?>();
                spec.VisualElement = obj["ve"]?.Value<string>();
                return spec;
            }

            throw new System.ArgumentException($"{targetKey}의 형식이 올바르지 않습니다");
        }
    }

    public sealed class CommonOptions
    {
        public int WaitFrames = 1;
        public JObject WaitFor;        // null이면 미사용
        public bool CaptureResult;

        public static CommonOptions Parse(JObject root)
        {
            return new CommonOptions
            {
                WaitFrames = root["waitFrames"]?.Value<int?>() ?? 1,
                WaitFor = root["waitFor"] as JObject,
                CaptureResult = root["captureResult"]?.Value<bool?>() ?? false
            };
        }
    }
}
