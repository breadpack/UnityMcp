using System;
using UnityEditor;
using UnityEngine.InputSystem;

namespace BreadPack.Mcp.Unity.Input
{
    [InitializeOnLoad]
    public static class VirtualInputDevices
    {
        private const string McpMouseName = "McpVirtualMouse";
        private const string McpKeyboardName = "McpVirtualKeyboard";
        private const string McpTouchscreenName = "McpVirtualTouchscreen";

        private static Mouse _mouse;
        private static Keyboard _keyboard;
        private static Touchscreen _touchscreen;

        static VirtualInputDevices()
        {
            AssemblyReloadEvents.afterAssemblyReload += Reset;
            EditorApplication.playModeStateChanged += _ => Reset();
        }

        public static Mouse Mouse
        {
            get { EnsureRegistered(); return _mouse; }
        }

        public static Keyboard Keyboard
        {
            get { EnsureRegistered(); return _keyboard; }
        }

        public static Touchscreen Touchscreen
        {
            get { EnsureRegistered(); return _touchscreen; }
        }

        public static void EnsureRegistered()
        {
            _mouse = EnsureDevice(_mouse, "Mouse", McpMouseName, device => (Mouse)device);
            _keyboard = EnsureDevice(_keyboard, "Keyboard", McpKeyboardName, device => (Keyboard)device);
            _touchscreen = EnsureDevice(_touchscreen, "Touchscreen", McpTouchscreenName, device => (Touchscreen)device);
        }

        private static T EnsureDevice<T>(T cached, string layoutName, string deviceName, Func<InputDevice, T> cast)
            where T : InputDevice
        {
            if (cached != null && cached.added) return cached;

            foreach (var device in InputSystem.devices)
            {
                if (device is T typed && device.name == deviceName)
                    return typed;
            }

            return cast(InputSystem.AddDevice(layoutName, deviceName));
        }

        private static void Reset()
        {
            _mouse = null;
            _keyboard = null;
            _touchscreen = null;
        }
    }
}
