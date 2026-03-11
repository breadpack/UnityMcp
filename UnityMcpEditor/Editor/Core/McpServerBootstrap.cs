using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    [InitializeOnLoad]
    public static class McpServerBootstrap
    {
        private const int BasePort = 9876;
        private const int MaxPortRetries = 10;

        private static McpTcpServer _server;
        private static McpRequestDispatcher _dispatcher;
        private static ConsoleLogBuffer _logBuffer;
        private static bool _isRunning;
        private static int _actualPort;

        public static bool IsClientConnected => _server?.IsClientConnected == true;
        public static bool IsRunning => _isRunning;
        public static int Port => _actualPort;

        static McpServerBootstrap()
        {
            EditorApplication.quitting += StopServer;
            EditorApplication.delayCall += StartServer;
        }

        public static void StartServer()
        {
            if (_isRunning) return;

            try
            {
                _logBuffer = new ConsoleLogBuffer();
                _logBuffer.Start();

                _dispatcher = new McpRequestDispatcher();
                _dispatcher.Register(new GetScreenHandler());
                _dispatcher.Register(new GetUiTreeHandler());
                _dispatcher.Register(new GetAvailableActionsHandler());
                _dispatcher.Register(new TakeScreenshotHandler());
                _dispatcher.Register(new GetHierarchyHandler());
                _dispatcher.Register(new GetConsoleLogsHandler(_logBuffer));
                _dispatcher.Register(new RefreshAssetDatabaseHandler());
                _dispatcher.Register(new RenderUxmlHandler());
                _dispatcher.Register(new PlayModeHandler());

                // Phase 1: Scene Manipulation
                _dispatcher.Register(new CreateGameObjectHandler());
                _dispatcher.Register(new DeleteGameObjectHandler());
                _dispatcher.Register(new SetTransformHandler());
                _dispatcher.Register(new ReparentGameObjectHandler());
                _dispatcher.Register(new AddComponentHandler());
                _dispatcher.Register(new RemoveComponentHandler());
                _dispatcher.Register(new SetPropertyHandler());

                // Phase 2: Asset
                _dispatcher.Register(new InstantiatePrefabHandler());
                _dispatcher.Register(new SetAssetReferenceHandler());

                // Inspection
                _dispatcher.Register(new GetAssetHierarchyHandler());
                _dispatcher.Register(new GetComponentDetailsHandler());

#if UNITY_MCP_ADDRESSABLES
                // Phase 3: Addressable
                _dispatcher.Register(new AddressableAddHandler());
                _dispatcher.Register(new AddressableSetAddressHandler());
#endif

                _actualPort = FindAvailablePort(BasePort, MaxPortRetries);
                _server = new McpTcpServer(_actualPort, HandleRequestAsync);
                _server.Start();
                _isRunning = true;

                Debug.Log($"[MCP] Server started on port {_actualPort}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP] Failed to start server: {ex.Message}");
            }
        }

        public static void StopServer()
        {
            if (!_isRunning) return;

            _logBuffer?.Stop();
            _server?.Dispose();
            _server = null;
            _isRunning = false;

            Debug.Log("[MCP] Server stopped");
        }

        public static void Restart()
        {
            StopServer();
            StartServer();
        }

        private static int FindAvailablePort(int basePort, int maxRetries)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                int port = basePort + i;
                try
                {
                    var listener = new System.Net.Sockets.TcpListener(
                        System.Net.IPAddress.Loopback, port);
                    listener.Start();
                    listener.Stop();
                    return port;
                }
                catch (System.Net.Sockets.SocketException) { /* 포트 사용 중, 다음 시도 */ }
            }
            throw new Exception($"No available port found in range {basePort}-{basePort + maxRetries - 1}");
        }

        private static async Task<McpResponse> HandleRequestAsync(McpRequest request)
        {
            try
            {
                // UniTask로 메인 스레드 전환 (ConcurrentQueue + Update() 폴링 대체)
                await UniTask.SwitchToMainThread();

                var data = await _dispatcher.HandleAsync(request.Tool, request.Params ?? new JObject());
                return new McpResponse
                {
                    Id = request.Id,
                    Success = true,
                    Data = data
                };
            }
            catch (Exception ex)
            {
                return new McpResponse
                {
                    Id = request.Id,
                    Success = false,
                    Error = ex.Message
                };
            }
        }
    }
}
