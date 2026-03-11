using Newtonsoft.Json.Linq;
using UnityEditor;

namespace BreadPack.Mcp.Unity
{
    public class PlayModeHandler : IRequestHandler
    {
        public string ToolName => "unity_play_mode";

        public object Handle(JObject @params)
        {
            var action = @params?["action"]?.Value<string>() ?? "toggle";

            bool targetState = action switch
            {
                "enter" => true,
                "exit" => false,
                "toggle" => !EditorApplication.isPlaying,
                _ => throw new System.ArgumentException($"Unknown action: {action}. Use 'enter', 'exit', or 'toggle'")
            };

            if (EditorApplication.isPlaying == targetState)
            {
                return new
                {
                    changed = false,
                    isPlayMode = EditorApplication.isPlaying,
                    message = targetState ? "이미 Play Mode입니다" : "이미 Edit Mode입니다"
                };
            }

            if (EditorApplication.isCompiling)
                throw new System.Exception("컴파일 중에는 Play Mode를 전환할 수 없습니다");

            EditorApplication.isPlaying = targetState;

            return new
            {
                changed = true,
                isPlayMode = targetState,
                message = targetState
                    ? "Play Mode 전환을 요청했습니다. 도메인 리로드 후 MCP 재연결이 필요합니다."
                    : "Edit Mode 전환을 요청했습니다."
            };
        }
    }
}
