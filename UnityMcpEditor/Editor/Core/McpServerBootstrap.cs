using System;
using System.Collections.Generic;
using System.Reflection;
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

        // SessionState 는 프로젝트별 + 도메인 리로드 간 유지(Editor 재시작 시 소멸)라, 다중 Unity 인스턴스가
        // 포트 힌트를 공유하지 않는다. EditorPrefs(머신/유저 전역)를 쓰면 인스턴스끼리 lastPort 를 서로
        // 덮어써 포트가 출렁이므로 SessionState 로 격리한다.
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
                EditorStateCache.Initialize();

                RegisterHandlers();

                Exception lastEx = null;
                int lastPort = SessionState.GetInt(PortPrefsKey, -1);
                var portsToTry = new List<int>();
                if (lastPort >= BasePort && lastPort < BasePort + MaxPortRetries) portsToTry.Add(lastPort);
                for (int i = 0; i < MaxPortRetries; i++)
                {
                    int p = BasePort + i;
                    if (p != lastPort) portsToTry.Add(p);
                }

                foreach (var port in portsToTry)
                {
                    try
                    {
                        _server = new McpTcpServer(port, HandleRequestAsync);
                        _server.Start();
                        _actualPort = port;
                        _isRunning = true;
                        SessionState.SetInt(PortPrefsKey, port);
                        break;
                    }
                    catch (Exception ex)
                    {
                        _server?.Dispose();
                        _server = null;
                        lastEx = ex;
                    }
                }
                if (!_isRunning) throw lastEx ?? new Exception("No available port");

                Debug.Log($"[MCP] Server started on port {_actualPort}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP] Failed to start server: {ex.Message}");
            }
        }

        public static void StopServer()
        {
            GameViewCaptureService.Shutdown();
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

        /// <summary>
        /// TCP 서버를 거치지 않고 등록된 핸들러를 직접 호출한다. Unity Pipeline [CliCommand] 어댑터
        /// (BreadPack.Mcp.Unity.Pipeline)가 사용한다. 메인 스레드에서 호출해야 한다 — Pipeline 은
        /// MainThreadRequired 명령을 메인 스레드에서 실행하므로 추가 디스패치가 필요 없다.
        /// TCP 서버 기동 여부와 무관하게(포트 바인딩 실패 등) 핸들러 레지스트리만 있으면 동작한다.
        /// </summary>
        public static Task<object> DispatchAsync(string tool, JObject @params)
        {
            if (_dispatcher == null)
            {
                MainThreadDispatcher.EnsureInitialized();
                if (!EditorStateCache.IsInitialized) EditorStateCache.Initialize();
                RegisterHandlers();
            }
            return _dispatcher.HandleAsync(tool, @params ?? new JObject());
        }

        private static void RegisterHandlers()
        {
            _logBuffer = new ConsoleLogBuffer();
            _logBuffer.Start();
            _dispatcher = new McpRequestDispatcher();

            // 특수 핸들러 (생성자 파라미터 필요)
            _dispatcher.Register(new GetConsoleLogsHandler(_logBuffer));

            // 자동 등록: 파라미터 없는 생성자를 가진 IRequestHandler/IAsyncRequestHandler
            foreach (var type in GetUnityMcpHandlerTypes())
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

            // Custom tool registration (user-defined [McpTool] methods)
            CustomToolRegistry.ScanAndRegister(_dispatcher);
        }

        private static IEnumerable<Type> GetUnityMcpHandlerTypes()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var assemblyName = assembly.GetName().Name;
                if (string.IsNullOrEmpty(assemblyName) || !assemblyName.StartsWith("BreadPack.Mcp.Unity", StringComparison.Ordinal))
                    continue;

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }

                foreach (var type in types)
                {
                    if (type != null) yield return type;
                }
            }
        }

        private static async Task<McpResponse> HandleRequestAsync(McpRequest request)
        {
            // 컴파일/도메인 리로드로 메인 스레드(EditorApplication.update)가 멈춰도 응답해야 하는
            // 경량 질의(ping/get_editor_state)는 캐시로 TCP 스레드에서 즉답한다. 메인 스레드 큐에
            // 실으면 컴파일이 끝날 때까지 응답이 막혀, 헬스체크/포트 디스커버리가 무응답이 된다.
            if (EditorStateCache.IsInitialized &&
                TryHandleOnBackground(request.Tool, out var cached))
            {
                return new McpResponse { Id = request.Id, Success = true, Data = cached };
            }

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

        // 메인 스레드 없이 캐시로 즉답 가능한 경량 도구. 그 외는 false → 기존 메인 스레드 경로로.
        private static bool TryHandleOnBackground(string tool, out object data)
        {
            switch (tool)
            {
                case "ping":
                    data = EditorStateCache.BuildPing();
                    return true;
                case "unity_get_editor_state":
                    data = EditorStateCache.BuildEditorState();
                    return true;
                default:
                    data = null;
                    return false;
            }
        }
    }
}
