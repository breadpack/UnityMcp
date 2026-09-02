#if UNITY_PIPELINE_PRESENT
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace BreadPack.Mcp.Unity.Pipeline
{
    /// <summary>
    /// Unity Pipeline [CliCommand] 어댑터가 기존 UnityMcp 핸들러(IRequestHandler)로 위임할 때 쓰는 헬퍼.
    ///
    /// 파라미터 규약은 Bridge(MCP) 와 동일하다: 문자열 인자가 JSON 객체/배열처럼 보이면 파싱해서 넘기고,
    /// 아니면 문자열 그대로 넘긴다. 그래서 `unity command unity_input_click -- --target Canvas/Button` 과
    /// `--target '{"path":"Button","index":1}'` 이 MCP 호출과 같은 결과를 낸다.
    ///
    /// 실행 스레드: Pipeline 은 MainThreadRequired(기본값) 명령을 Editor 메인 스레드에서 호출하므로
    /// 핸들러를 별도 디스패치 없이 바로 부른다. 비동기 핸들러가 돌려주는 Task 는 Pipeline 이 await 한다.
    /// </summary>
    public static class PipelineBridge
    {
        public static Task<object> Invoke(string tool, JObject @params)
        {
            return McpServerBootstrap.DispatchAsync(tool, @params ?? new JObject());
        }

        /// <summary>JSON 처럼 보이는 문자열은 JToken 으로, 아니면 문자열로 넣는다. null 은 생략.</summary>
        public static void Put(JObject p, string key, string value)
        {
            if (value == null) return;
            p[key] = ParseIfJson(value);
        }

        public static void Put(JObject p, string key, int value) => p[key] = value;
        public static void Put(JObject p, string key, bool value) => p[key] = value;
        public static void Put(JObject p, string key, float value) => p[key] = value;

        public static void Put(JObject p, string key, int? value) { if (value.HasValue) p[key] = value.Value; }
        public static void Put(JObject p, string key, bool? value) { if (value.HasValue) p[key] = value.Value; }
        public static void Put(JObject p, string key, float? value) { if (value.HasValue) p[key] = value.Value; }

        /// <summary>
        /// 입력 도구의 from/center 처럼 "타겟 스펙" 을 받는 인자: JSON 이면 그대로, 순수 문자열이면
        /// {"target": s} 로 감싼다 (Bridge SwipeTool 과 동일한 편의 규칙).
        /// </summary>
        public static void PutTargetSpec(JObject p, string key, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            var token = ParseIfJson(value);
            p[key] = token.Type == JTokenType.String
                ? new JObject { ["target"] = value }
                : token;
        }

        /// <summary>JSON 객체 문자열을 JObject 로. 비어 있거나 객체가 아니면 빈 객체.</summary>
        public static JObject ParseObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new JObject();
            return JToken.Parse(json) as JObject ?? new JObject();
        }

        private static JToken ParseIfJson(string value)
        {
            var t = value.TrimStart();
            if (t.Length > 0 && (t[0] == '{' || t[0] == '['))
            {
                try { return JToken.Parse(value); }
                catch (Newtonsoft.Json.JsonException) { /* 문자열로 취급 */ }
            }
            return value;
        }
    }
}
#endif
