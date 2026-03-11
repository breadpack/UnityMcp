using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace BreadPack.Mcp.Unity
{
    public class McpRequestDispatcher
    {
        private readonly Dictionary<string, IRequestHandler> _handlers = new();

        public void Register(IRequestHandler handler)
        {
            _handlers[handler.ToolName] = handler;
        }

        public async Task<object> HandleAsync(string tool, JObject @params)
        {
            if (_handlers.TryGetValue(tool, out var handler))
            {
                if (handler is IAsyncRequestHandler asyncHandler)
                    return await asyncHandler.HandleAsync(@params);
                return handler.Handle(@params);
            }

            if (tool == "ping")
                return new
                {
                    message = "pong",
                    isPlayMode = EditorApplication.isPlaying,
                    isCompiling = EditorApplication.isCompiling,
                    editorSettings = new
                    {
                        autoRefreshMode = RefreshAssetDatabaseHandler.GetAutoRefreshModeName(),
                        activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                    }
                };

            throw new System.NotSupportedException($"Unknown tool: {tool}");
        }
    }
}
