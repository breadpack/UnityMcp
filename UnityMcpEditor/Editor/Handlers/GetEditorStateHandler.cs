using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    public class GetEditorStateHandler : IRequestHandler
    {
        public string ToolName => "unity_get_editor_state";

        public object Handle(JObject @params)
        {
            return new
            {
                isCompiling = EditorApplication.isCompiling,
                isUpdating = EditorApplication.isUpdating,
                isPlaying = EditorApplication.isPlaying,
                unityVersion = Application.unityVersion,
                projectName = Application.productName,
                // 프로젝트 루트 경로 (dataPath = <root>/Assets) — 다중 Unity 환경에서 포트 디스커버리 매칭용
                projectPath = System.IO.Path.GetDirectoryName(Application.dataPath),
            };
        }
    }
}
