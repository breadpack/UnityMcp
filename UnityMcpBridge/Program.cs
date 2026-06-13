using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace UnityMcpBridge;

public class Program
{
    public static async Task Main(string[] args)
    {
        // UNITY_TCP_PORT 가 명시되면 그 포트로 고정(수동 오버라이드).
        // 아니면 workspace 디스커버리 모드 — 재연결 시점마다 포트를 다시 찾으므로,
        // 컴파일/리로드·다중 인스턴스 경합으로 포트가 바뀌어도 따라간다.
        UnityConnection connection;
        var explicitPort = Environment.GetEnvironmentVariable("UNITY_TCP_PORT");
        if (!string.IsNullOrEmpty(explicitPort) && int.TryParse(explicitPort, out var ep))
        {
            connection = new UnityConnection("127.0.0.1", ep);
        }
        else
        {
            var workspace = Environment.GetEnvironmentVariable("CLAUDE_PROJECT_DIR")
                            ?? Directory.GetCurrentDirectory();
            connection = UnityConnection.ForWorkspace(workspace);
        }

        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Services.AddSingleton(connection);
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        await builder.Build().RunAsync();
    }
}

[McpServerToolType]
public static class PingTool
{
    [McpServerTool(Name = "unity_ping"), Description("Unity Editor 연결 상태를 확인합니다")]
    public static async Task<string> Ping(UnityConnection connection, CancellationToken ct)
    {
        try
        {
            var result = await connection.SendRequestAsync("ping", ct: ct);
            var root = result.RootElement;
            if (root.TryGetProperty("success", out var s) && !s.GetBoolean())
                return $"Error: {root.GetProperty("error").GetString()}";
            return root.GetProperty("data").GetRawText();
        }
        catch (Exception ex)
        {
            return $"Unity Editor에 연결할 수 없습니다: {ex.Message}";
        }
    }
}
