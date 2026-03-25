using System.Text.Json;

namespace UnityMcpBridge.Tools;

public static class ResponseFormatter
{
    public static string Format(JsonDocument result)
    {
        var root = result.RootElement;
        if (root.TryGetProperty("success", out var s) && !s.GetBoolean())
            return $"Error: {root.GetProperty("error").GetString()}";
        return root.GetProperty("data").GetRawText();
    }
}
