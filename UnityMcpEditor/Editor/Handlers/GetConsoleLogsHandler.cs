using Newtonsoft.Json.Linq;

namespace BreadPack.Mcp.Unity
{
    public class GetConsoleLogsHandler : IRequestHandler
    {
        private readonly ConsoleLogBuffer _buffer;

        public GetConsoleLogsHandler(ConsoleLogBuffer buffer)
        {
            _buffer = buffer;
        }

        public string ToolName => "unity_get_console_logs";

        public object Handle(JObject @params)
        {
            int count = @params?["count"]?.Value<int>() ?? 50;
            string logType = @params?["logType"]?.ToString();

            return new
            {
                logs = _buffer.GetLogs(count, logType),
                totalBuffered = _buffer.TotalBuffered
            };
        }
    }
}
