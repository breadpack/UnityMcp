using System;
using System.Linq;
using System.Threading.Tasks;
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

        private const string PortPrefsKey = "UnityMcp_LastPort";

        static McpServerBootstrap()
        {
            EditorApplication.quitting += StopServer;
            EditorApplication.delayCall += StartServer;
            AssemblyReloadEvents.beforeAssemblyReload += StopServer;
            AssemblyReloadEvents.afterAssemblyReload += () => EditorApplication.delayCall += StartServer;
        }

        public static void StartServer()
        {
            if (_isRunning) return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += StartServer;
                return;
            }

            try
            {
                MainThreadDispatcher.EnsureInitialized();

                RegisterHandlers();

                _actualPort = FindAvailablePort(BasePort, MaxPortRetries);
                _server = new McpTcpServer(_actualPort, HandleRequestAsync);
                _server.Start();
                _isRunning = true;
                EditorPrefs.SetInt(PortPrefsKey, _actualPort);

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

        private static void RegisterHandlers()
        {
            _logBuffer = new ConsoleLogBuffer();
            _logBuffer.Start();
            _dispatcher = new McpRequestDispatcher();

            // 특수 핸들러 (생성자 파라미터 필요)
            _dispatcher.Register(new GetConsoleLogsHandler(_logBuffer));

            // 자동 등록: 파라미터 없는 생성자를 가진 IRequestHandler/IAsyncRequestHandler
            foreach (var type in typeof(McpServerBootstrap).Assembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface) continue;
                if (type == typeof(GetConsoleLogsHandler)) continue; // 이미 등록됨

                if (typeof(IRequestHandler).IsAssignableFrom(type) || typeof(IAsyncRequestHandler).IsAssignableFrom(type))
                {
                    var ctor = type.GetConstructor(Type.EmptyTypes);
                    if (ctor != null)
                    {
                        var handler = ctor.Invoke(null);
                        if (handler is IRequestHandler rh) _dispatcher.Register(rh);
                        else if (handler is IAsyncRequestHandler arh) _dispatcher.Register(arh);
                    }
                }
            }
        }

        private static bool TryListenPort(int port)
        {
            try
            {
                using (var listener = new System.Net.Sockets.TcpListener(
                    System.Net.IPAddress.Loopback, port))
                {
                    listener.Start();
                    listener.Stop();
                }
                return true;
            }
            catch (System.Net.Sockets.SocketException)
            {
                return false;
            }
        }

        private static int FindAvailablePort(int basePort, int maxRetries)
        {
            int lastPort = EditorPrefs.GetInt(PortPrefsKey, -1);
            if (lastPort >= basePort && lastPort < basePort + maxRetries && TryListenPort(lastPort))
                return lastPort;

            for (int i = 0; i < maxRetries; i++)
            {
                int port = basePort + i;
                if (port == lastPort) continue;
                if (TryListenPort(port)) return port;
            }
            throw new Exception($"No available port found in range {basePort}-{basePort + maxRetries - 1}");
        }

        private static async Task<McpResponse> HandleRequestAsync(McpRequest request)
        {
            try
            {
                var data = await MainThreadDispatcher.RunOnMainThread(
                    () => _dispatcher.HandleAsync(request.Tool, request.Params ?? new JObject()));
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
