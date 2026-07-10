using UnityEditor;
using UnityEngine.EventSystems;

namespace BreadPack.Mcp.Unity.Input
{
    public enum TargetKind { UGui, UiToolkit, World, Screen }

    public static class InputSystemGuard
    {
        // 타겟 해석 전에 호출. Edit Mode/컴파일 중에 씬 traversal하지 않도록 차단.
        public static void EnsurePlayMode()
        {
            if (!EditorApplication.isPlaying)
                throw new System.Exception("입력 시뮬레이션은 Play Mode에서만 가능합니다. 먼저 unity_play_mode로 진입하세요.");

            if (EditorApplication.isCompiling)
                throw new System.Exception("컴파일이 끝난 후 다시 시도하세요.");
        }

        // 타겟 해석 후 호출. backend 독립적인 조건만 검증한다.
        public static void EnsureReady(TargetKind kind)
        {
            EnsurePlayMode();

            if (kind == TargetKind.UGui)
            {
                var es = EventSystem.current;
                if (es == null)
                    throw new System.Exception("씬에 EventSystem이 없습니다. uGUI 입력을 받으려면 EventSystem이 필요합니다.");

            }
            // UiToolkit: UIDocument 사용 시 PanelEventHandler가 자동 추가되므로 별도 검증 생략
            // World/Screen: 추가 검증 없음
        }
    }
}
