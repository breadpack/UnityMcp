using UnityEditor;
using UnityEngine.InputSystem;

namespace BreadPack.Mcp.Unity.Input
{
    [InitializeOnLoad]
    public static class VirtualInputDevices
    {
        private const string MouseLayoutName = "Mouse";
        private const string McpMouseName = "McpVirtualMouse";

        private static Mouse _mouse;

        static VirtualInputDevices()
        {
            // 도메인 리로드 후 / Play Mode 전환 시 디바이스 참조가 사라지므로 플래그 리셋
            AssemblyReloadEvents.afterAssemblyReload += Reset;
            EditorApplication.playModeStateChanged += _ => Reset();
        }

        public static Mouse Mouse
        {
            get
            {
                EnsureRegistered();
                return _mouse;
            }
        }

        public static void EnsureRegistered()
        {
            if (_mouse != null && _mouse.added) return;

            // 이미 같은 이름의 디바이스가 등록되어 있으면 재사용
            foreach (var device in InputSystem.devices)
            {
                if (device is Mouse m && device.name == McpMouseName)
                {
                    _mouse = m;
                    return;
                }
            }

            _mouse = (Mouse)InputSystem.AddDevice(MouseLayoutName, McpMouseName);
        }

        private static void Reset()
        {
            _mouse = null;
        }
    }
}
