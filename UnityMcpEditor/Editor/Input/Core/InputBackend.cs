using System;
using UnityEngine;

namespace BreadPack.Mcp.Unity.Input
{
    [Flags]
    public enum InputCapabilities
    {
        None = 0,
        Pointer = 1,
        Keyboard = 2,
        Text = 4,
        Touch = 8
    }

    public enum McpMouseButton { Left, Right, Middle }
    public enum McpTouchPhase { Began, Moved, Stationary, Ended, Canceled }

    public interface IInputBackend
    {
        string Name { get; }
        string Delivery { get; }
        int Priority { get; }

        bool Supports(InputCapabilities capability, ResolvedTarget target = null);
        void EnsureReady(InputCapabilities capability, ResolvedTarget target = null);

        void PointerMove(Vector2 screenPosition);
        void PointerDown(McpMouseButton button);
        void PointerUp(McpMouseButton button);
        void Scroll(Vector2 delta);
        void KeyDown(string key);
        void KeyUp(string key);
        void SendText(char character);
        void TouchSet(int touchIndex, Vector2 position, McpTouchPhase phase, int touchId);
    }
}
