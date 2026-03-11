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
        var port = int.Parse(Environment.GetEnvironmentVariable("UNITY_TCP_PORT") ?? "9876");

        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Services.AddSingleton(new UnityConnection("127.0.0.1", port));
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
