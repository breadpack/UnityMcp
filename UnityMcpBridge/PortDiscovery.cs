using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace UnityMcpBridge;

/// <summary>
/// 여러 Unity Editor가 동시에 떠 있을 때, 현재 workspace 에 해당하는 Unity 의 TCP 포트를 찾아낸다.
/// Unity 측(McpServerBootstrap)은 9876 부터 순차 폴백하므로, 그 범위를 스캔하며 각 인스턴스의
/// projectPath 를 질의해 workspace 경로와 매칭한다.
/// </summary>
public static class PortDiscovery
{
    private const int MaxPayloadSize = 10 * 1024 * 1024; // 10 MB (McpTcpServer 와 동일)
    private const int ConnectTimeoutMs = 500;

    /// <summary>
    /// workspaceDir 에 해당하는 Unity 포트를 반환. 매칭 실패 시 basePort 로 fallback.
    /// </summary>
    public static async Task<int> DiscoverAsync(
        string workspaceDir, int basePort = 9876, int range = 10, CancellationToken ct = default)
    {
        var ws = Normalize(workspaceDir);
        int bestPort = -1;
        int bestLen = -1;
        string? bestProject = null;

        for (int i = 0; i < range; i++)
        {
            int port = basePort + i;
            var projectPath = await TryGetProjectPathAsync(port, ct);
            if (projectPath == null) continue;

            var p = Normalize(projectPath);
            if (p == ws || p.StartsWith(ws + "/"))
            {
                // 후보 다수 시 가장 깊은(긴) 경로 우선
                if (p.Length > bestLen)
                {
                    bestLen = p.Length;
                    bestPort = port;
                    bestProject = projectPath;
                }
            }
        }

        if (bestPort >= 0)
        {
            Console.Error.WriteLine(
                $"[Unity MCP] Matched Unity on port {bestPort} ({bestProject}) for workspace '{workspaceDir}'");
            return bestPort;
        }

        Console.Error.WriteLine(
            $"[Unity MCP] No Unity matched workspace '{workspaceDir}' in ports {basePort}-{basePort + range - 1}. " +
            $"Falling back to {basePort}.");
        return basePort;
    }

    private static async Task<string?> TryGetProjectPathAsync(int port, CancellationToken ct)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(ConnectTimeoutMs);

            await client.ConnectAsync("127.0.0.1", port, cts.Token);
            using var stream = client.GetStream();

            // 1) 경량 엔드포인트(get_editor_state) 우선
            var state = await RequestAsync(stream, "unity_get_editor_state", cts.Token);
            var pp = ExtractProjectPath(state);
            if (pp != null) return pp;

            // 2) 구버전 Unity 패키지(projectPath 미포함) 폴백
            var info = await RequestAsync(stream, "unity_get_project_info", cts.Token);
            return ExtractProjectPath(info);
        }
        catch
        {
            return null; // 닫힌 포트 / 타임아웃 / 비-MCP 프로세스
        }
    }

    private static string? ExtractProjectPath(JsonDocument? doc)
    {
        if (doc == null) return null;
        var root = doc.RootElement;
        if (root.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.False) return null;
        if (root.TryGetProperty("data", out var data) &&
            data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty("projectPath", out var pp) &&
            pp.ValueKind == JsonValueKind.String)
        {
            return pp.GetString();
        }
        return null;
    }

    private static async Task<JsonDocument?> RequestAsync(NetworkStream stream, string tool, CancellationToken ct)
    {
        var request = new { id = Guid.NewGuid().ToString(), tool, @params = new { } };
        var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request));

        var len = new byte[4];
        len[0] = (byte)(payload.Length >> 24);
        len[1] = (byte)(payload.Length >> 16);
        len[2] = (byte)(payload.Length >> 8);
        len[3] = (byte)payload.Length;

        await stream.WriteAsync(len, ct);
        await stream.WriteAsync(payload, ct);
        await stream.FlushAsync(ct);

        var respLenBuf = new byte[4];
        if (!await ReadExactAsync(stream, respLenBuf, 4, ct)) return null;
        int respLen = (respLenBuf[0] << 24) | (respLenBuf[1] << 16) | (respLenBuf[2] << 8) | respLenBuf[3];
        if (respLen <= 0 || respLen > MaxPayloadSize) return null;

        var respPayload = new byte[respLen];
        if (!await ReadExactAsync(stream, respPayload, respLen, ct)) return null;
        return JsonDocument.Parse(Encoding.UTF8.GetString(respPayload));
    }

    private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, int count, CancellationToken ct)
    {
        int offset = 0;
        while (offset < count)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), ct);
            if (read == 0) return false;
            offset += read;
        }
        return true;
    }

    private static string Normalize(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        string full;
        try { full = Path.GetFullPath(path); }
        catch { full = path; }
        full = full.Replace('\\', '/').TrimEnd('/');
        if (OperatingSystem.IsWindows()) full = full.ToLowerInvariant();
        return full;
    }
}
