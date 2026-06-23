using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BreadPack.Mcp.Unity
{
    /// <summary>
    /// "지금 에이전트가 편집하는 대상 씬/루트는 무엇인가"에 대한 단일 진실 공급원.
    ///
    /// Prefab 편집 모드에서 prefab 콘텐츠는 별도의 PrefabStage preview scene 에 존재하며
    /// <see cref="SceneManager.GetActiveScene"/> 은 여전히 메인 씬을 반환한다. 따라서 generic
    /// 씬 조작 도구가 활성 씬만 바라보면 prefab 내부를 못 찾거나(메인 씬의 동명 오브젝트를
    /// 조용히 편집해) 신뢰를 잃는다. 모든 해석/탐색이 이 컨텍스트를 거치게 하면 한 곳만
    /// 고쳐도 전 도구가 prefab 스테이지를 올바르게 대상으로 삼는다.
    ///
    /// Prefab 모드가 아닐 때 모든 멤버는 활성 씬으로 패스스루(no-op)된다.
    /// </summary>
    public static class PrefabStageContext
    {
        /// <summary>현재 열린 PrefabStage. 없으면 null.</summary>
        public static PrefabStage CurrentStage => PrefabStageUtility.GetCurrentPrefabStage();

        /// <summary>Prefab 편집 모드 여부.</summary>
        public static bool IsInPrefabMode => CurrentStage != null;

        /// <summary>
        /// generic 도구가 path 해석·루트 열거의 기준으로 삼아야 하는 씬.
        /// Prefab 모드면 스테이지 씬, 아니면 활성 씬.
        /// </summary>
        public static Scene EditTargetScene
        {
            get
            {
                var stage = CurrentStage;
                return stage != null ? stage.scene : SceneManager.GetActiveScene();
            }
        }

        /// <summary>Prefab 모드일 때 prefab 의 단일 루트 GameObject. 아니면 null.</summary>
        public static GameObject PrefabRoot => CurrentStage?.prefabContentsRoot;

        /// <summary>편집 중인 prefab 에셋 경로. Prefab 모드가 아니면 null.</summary>
        public static string PrefabAssetPath => CurrentStage?.assetPath;

        /// <summary>
        /// 편집 대상 씬을 dirty 로 표시한다. Prefab 모드에서는 스테이지 씬을 MarkSceneDirty 해야
        /// 미저장 변경 상태·auto-save 가 올바르게 동작한다. Prefab 모드가 아니면 no-op.
        /// </summary>
        public static void MarkTargetSceneDirty()
        {
            var stage = CurrentStage;
            if (stage != null)
                EditorSceneManager.MarkSceneDirty(stage.scene);
        }
    }
}
