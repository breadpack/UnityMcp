using UnityEditor;
using UnityEngine;

namespace BreadPack.Mcp.Unity
{
    public class McpEditorPlugin : EditorWindow
    {
        [MenuItem("Tools/MCP Server")]
        public static void ShowWindow()
        {
            GetWindow<McpEditorPlugin>("MCP Server");
        }

        void OnGUI()
        {
            GUILayout.Label("MCP Server", EditorStyles.boldLabel);
            GUILayout.Label($"Status: {(McpServerBootstrap.IsClientConnected ? "Connected" : "Waiting...")}");

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Port: {McpServerBootstrap.Port}");
            if (McpServerBootstrap.IsRunning && GUILayout.Button("Copy", GUILayout.Width(50)))
            {
                EditorGUIUtility.systemCopyBuffer = McpServerBootstrap.Port.ToString();
                Debug.Log($"[MCP] Port {McpServerBootstrap.Port} copied to clipboard");
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);

            if (McpServerBootstrap.IsRunning)
            {
                if (GUILayout.Button("Restart"))
                    McpServerBootstrap.Restart();
            }
            else
            {
                if (GUILayout.Button("Start"))
                    McpServerBootstrap.StartServer();
            }
        }
    }
}
