using Newtonsoft.Json.Linq;
using UnityEditor;

namespace BreadPack.Mcp.Unity
{
    public class UndoRedoHandler : IRequestHandler
    {
        public string ToolName => "unity_undo";

        public object Handle(JObject @params)
        {
            var action = @params?["action"]?.Value<string>() ?? "undo";
            var count = @params?["count"]?.Value<int>() ?? 1;

            switch (action)
            {
                case "undo":
                    for (int i = 0; i < count; i++)
                        Undo.PerformUndo();
                    return new
                    {
                        action = "undo",
                        count,
                        message = $"{count}회 Undo 실행"
                    };

                case "redo":
                    for (int i = 0; i < count; i++)
                        Undo.PerformRedo();
                    return new
                    {
                        action = "redo",
                        count,
                        message = $"{count}회 Redo 실행"
                    };

                case "history":
                    return new
                    {
                        action = "history",
                        currentGroupName = Undo.GetCurrentGroupName(),
                        message = "Unity Undo 히스토리의 전체 목록은 public API로 접근할 수 없습니다. 현재 그룹 이름만 반환합니다."
                    };

                default:
                    throw new System.ArgumentException(
                        $"Unknown action: {action}. Use 'undo', 'redo', or 'history'");
            }
        }
    }
}
