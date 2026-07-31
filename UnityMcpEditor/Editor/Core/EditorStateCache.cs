using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BreadPack.Mcp.Unity
{
    /// <summary>
    /// ping/get_editor_state 처럼 "Editor 가 살아있는지 / 어느 프로젝트인지"를 묻는 경량 질의를
    /// 메인 스레드를 거치지 않고 TCP 스레드에서 즉시 응답하기 위한 캐시.
    ///
    /// 핸들러를 MainThreadDispatcher 로 감싸면, 컴파일·도메인 리로드로 EditorApplication.update 가
    /// 멈추는 동안 큐가 처리되지 않아 응답이 나가지 못한다 — 헬스체크/포트 디스커버리가 가장 필요한
    /// 순간(컴파일 중)에 무응답이 되는 역설이 생긴다. 그래서 불변 메타데이터는 1회 캐시하고,
    /// 가변 상태는 메인 스레드 이벤트/콜백에서 volatile 필드로 갱신해 lock-free 로 읽는다.
    /// </summary>
    public static class EditorStateCache
    {
        public static bool IsInitialized { get; private set; }

        // 불변 메타데이터 — Initialize 에서 1회 캐시 (인스턴스 수명 동안 변하지 않음)
        public static string ProjectPath { get; private set; } = "";
        public static string ProjectName { get; private set; } = "";
        public static string UnityVersion { get; private set; } = "";
        public static string PackageVersion { get; private set; } = "";

        // 가변 상태 — 메인 스레드에서만 기록, TCP 스레드에서 읽음
        private static volatile bool _isCompiling;
        private static volatile bool _isUpdating;
        private static volatile bool _isPlaying;
        private static volatile string _activeScene = "";
        private static volatile string _autoRefreshMode = "";
        private static volatile bool _inPrefabStage;
        private static volatile string _prefabStagePath = "";

        /// <summary>
        /// 메인 스레드에서 호출해야 한다(McpServerBootstrap.StartServer). 멱등.
        /// </summary>
        public static void Initialize()
        {
            ProjectPath = Path.GetDirectoryName(Application.dataPath) ?? "";
            ProjectName = Application.productName;
            UnityVersion = Application.unityVersion;
            PackageVersion = GetPackageVersion();
            _autoRefreshMode = RefreshAssetDatabaseHandler.GetAutoRefreshModeName();
            _activeScene = SceneManager.GetActiveScene().name ?? "";

            // 컴파일 상태는 이벤트로 정확히 추적 — update 가 멈춰도 시작/종료 시점은 메인 스레드에서 통지된다.
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;

            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneOpened += OnSceneOpened;

            // Prefab 편집 모드 진입/종료를 이벤트로 추적 — 도메인 리로드로 스테이지가 닫히면
            // prefabStageClosing 이 통지돼 상태가 따라간다(에이전트가 모드 드롭을 관측 가능).
            PrefabStage.prefabStageOpened -= OnPrefabStageOpened;
            PrefabStage.prefabStageOpened += OnPrefabStageOpened;
            PrefabStage.prefabStageClosing -= OnPrefabStageClosing;
            PrefabStage.prefabStageClosing += OnPrefabStageClosing;

            // 서버 기동 시점에 이미 스테이지가 열려 있을 수 있으므로 현재 값으로 초기화.
            var openStage = PrefabStageUtility.GetCurrentPrefabStage();
            _inPrefabStage = openStage != null;
            _prefabStagePath = openStage != null ? openStage.assetPath : "";

            // 자주 바뀌지 않는 값(isUpdating/isPlaying/activeScene)은 가벼운 폴링으로 보강.
            EditorApplication.update -= RefreshVolatile;
            EditorApplication.update += RefreshVolatile;

            RefreshVolatile();
            IsInitialized = true;
        }

        private static string GetPackageVersion()
        {
            try
            {
                return UnityEditor.PackageManager.PackageInfo
                    .FindForPackageName("com.breadpack.unity-mcp")?.version ?? "";
            }
            catch
            {
                // 버전 안내는 best-effort다. Package Manager 메타데이터 조회 실패가
                // MCP 서버 자체의 시작을 막아서는 안 된다.
                return "";
            }
        }

        private static void OnCompilationStarted(object _) => _isCompiling = true;
        private static void OnCompilationFinished(object _) => _isCompiling = false;
        private static void OnBeforeAssemblyReload() => _isUpdating = true;
        private static void OnPlayModeChanged(PlayModeStateChange _) => _isPlaying = EditorApplication.isPlayingOrWillChangePlaymode;
        private static void OnActiveSceneChanged(Scene _, Scene current) => _activeScene = current.name ?? "";
        private static void OnSceneOpened(Scene scene, OpenSceneMode _) => _activeScene = scene.name ?? "";
        private static void OnPrefabStageOpened(PrefabStage stage)
        {
            _inPrefabStage = true;
            _prefabStagePath = stage != null ? stage.assetPath : "";
        }
        private static void OnPrefabStageClosing(PrefabStage stage)
        {
            _inPrefabStage = false;
            _prefabStagePath = "";
        }

        // 메인 스레드(EditorApplication.update) 에서만 호출. bool 읽기는 할당이 없어 매 프레임 갱신해도 가볍다.
        private static void RefreshVolatile()
        {
            _isCompiling = EditorApplication.isCompiling;
            _isUpdating = EditorApplication.isUpdating;
            _isPlaying = EditorApplication.isPlaying;
        }

        /// <summary>get_editor_state 응답을 TCP 스레드에서 즉시 구성한다.</summary>
        public static object BuildEditorState() => new
        {
            isCompiling = _isCompiling,
            isUpdating = _isUpdating,
            isPlaying = _isPlaying,
            inPrefabStage = _inPrefabStage,
            prefabStagePath = _prefabStagePath,
            unityVersion = UnityVersion,
            packageVersion = PackageVersion,
            projectName = ProjectName,
            projectPath = ProjectPath,
        };

        /// <summary>ping 응답을 TCP 스레드에서 즉시 구성한다.</summary>
        public static object BuildPing() => new
        {
            message = "pong",
            isPlayMode = _isPlaying,
            isCompiling = _isCompiling,
            editorSettings = new
            {
                autoRefreshMode = _autoRefreshMode,
                activeScene = _activeScene,
                inPrefabStage = _inPrefabStage,
                prefabStagePath = _prefabStagePath,
            }
        };
    }
}
