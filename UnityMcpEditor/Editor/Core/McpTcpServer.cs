using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace BreadPack.Mcp.Unity
{
    public class McpTcpServer : IDisposable
    {
        private static readonly JsonSerializerSettings CamelCaseSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        private const int MaxPayloadSize = 10 * 1024 * 1024; // 10 MB

        private TcpListener _listener;
        private TcpClient _client;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;
        private CancellationTokenSource _readCts;
        private readonly object _clientLock = new();
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private readonly int _port;
        private readonly Func<McpRequest, Task<McpResponse>> _handler;

        public bool IsClientConnected => _client?.Connected == true;
        public int Port => _port;

        public McpTcpServer(int port, Func<McpRequest, Task<McpResponse>> handler)
        {
            _port = port;
            _handler = handler;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Loopback, _port);
            // SO_REUSEADDR 를 켜면 Windows 에서 여러 Unity 인스턴스가 동일 포트(9876)에
            // 중복 바인딩 "성공" 처리되어 McpServerBootstrap 의 포트 폴백(9877+)이 작동하지 않는다.
            // 인스턴스별 포트 분리를 위해 배타 점유를 강제한다 — 이미 점유된 포트면 예외 → 다음 포트로 폴백.
            _listener.Server.ExclusiveAddressUse = true;
            _listener.Start();
            _ = AcceptLoopAsync(_cts.Token).ContinueWith(t =>
            {
                if (t.Exception != null)
                    UnityEngine.Debug.LogError($"[MCP] AcceptLoop error: {t.Exception.InnerException?.Message}");
            });
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    var remote = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
                    CancellationTokenSource readCts;
                    NetworkStream stream;
                    lock (_clientLock)
                    {
                        _readCts?.Cancel();
                        _readCts?.Dispose();
                        _client?.Close();
                        _client = client;
                        _stream = client.GetStream();
                        _readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        readCts = _readCts;
                        stream = _stream;
                    }
                    _ = ReadLoopAsync(stream, remote, readCts.Token);
                }
                catch (ObjectDisposedException) { break; }
                catch (Exception) { /* 재시도 */ }
            }
        }

        private async Task ReadLoopAsync(NetworkStream stream, string remote, CancellationToken ct)
        {
            var lengthBuffer = new byte[4];
            while (!ct.IsCancellationRequested && stream.CanRead)
            {
                try
                {
                    if (!await ReadExactAsync(stream, lengthBuffer, 4, ct)) break;
                    int length = (lengthBuffer[0] << 24) | (lengthBuffer[1] << 16)
                               | (lengthBuffer[2] << 8) | lengthBuffer[3];

                    if (length <= 0 || length > MaxPayloadSize)
                    {
                        // length prefix 자리에 ASCII HTTP 메서드가 들어오면(예: "GET ") 비정상적으로 큰 값으로
                        // 해석된다. 브라우저/HTTP 클라이언트/로컬 모니터링이 MCP 포트에 잘못 접속한 경우이므로,
                        // 프로토콜 위반은 Error 가 아니라 Warning 으로 안내하고 원격 endpoint 를 함께 남긴다.
                        if (TryGetHttpMethod(lengthBuffer, out var httpMethod))
                        {
                            UnityEngine.Debug.LogWarning(
                                $"[MCP] 비-MCP(HTTP {httpMethod}) 요청이 포트 {_port} 에 접속했습니다 (from {remote}). " +
                                $"브라우저/HTTP 클라이언트가 잘못 연결한 것으로 MCP 프로토콜이 아닙니다. 연결을 끊습니다.");
                        }
                        else
                        {
                            UnityEngine.Debug.LogError(
                                $"[MCP] Invalid payload size: {length} bytes (max {MaxPayloadSize}) from {remote}. Disconnecting.");
                        }
                        break;
                    }

                    var payload = ArrayPool<byte>.Shared.Rent(length);
                    try
                    {
                        if (!await ReadExactAsync(stream, payload, length, ct)) break;

                        var json = Encoding.UTF8.GetString(payload, 0, length);
                        var request = JsonConvert.DeserializeObject<McpRequest>(json);

                        // 핸들러 호출 (MainThreadDispatcher로 메인 스레드 전환)
                        McpResponse response;
                        try
                        {
                            response = await _handler(request);
                        }
                        catch (Exception hex)
                        {
                            response = new McpResponse { Id = request?.Id, Success = false, Error = hex.Message };
                        }
                        await SendAsync(JsonConvert.SerializeObject(response, CamelCaseSettings));
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(payload);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[MCP] ReadLoop error: {ex.Message}");
                    break;
                }
            }
        }

        private async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buffer, int count, CancellationToken ct)
        {
            int offset = 0;
            while (offset < count)
            {
                int read = await stream.ReadAsync(buffer, offset, count - offset, ct);
                if (read == 0) return false;
                offset += read;
            }
            return true;
        }

        // length prefix 로 읽은 4바이트가 HTTP 요청 라인의 시작인지 판별한다.
        // 예: "GET " → 0x47455420(=1195725856) 처럼 MaxPayloadSize 를 넘는 값으로 오독되는 케이스.
        private static readonly string[] HttpMethodPrefixes =
            { "GET ", "POST", "PUT ", "HEAD", "DELE", "OPTI", "PATC", "TRAC", "CONN" };

        private static bool TryGetHttpMethod(byte[] first4, out string method)
        {
            var s = Encoding.ASCII.GetString(first4, 0, 4);
            foreach (var prefix in HttpMethodPrefixes)
            {
                if (s.StartsWith(prefix, StringComparison.Ordinal))
                {
                    method = prefix.Trim();
                    return true;
                }
            }
            method = null;
            return false;
        }

        private async Task SendAsync(string json)
        {
            if (_stream == null || !_stream.CanWrite) return;
            await _sendLock.WaitAsync();
            try
            {
                if (_stream == null || !_stream.CanWrite) return;
                var payload = Encoding.UTF8.GetBytes(json);
                var length = new byte[4];
                length[0] = (byte)(payload.Length >> 24);
                length[1] = (byte)(payload.Length >> 16);
                length[2] = (byte)(payload.Length >> 8);
                length[3] = (byte)(payload.Length);
                await _stream.WriteAsync(length, 0, 4);
                await _stream.WriteAsync(payload, 0, payload.Length);
                await _stream.FlushAsync();
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _readCts?.Cancel();
            _readCts?.Dispose();
            _client?.Close();
            _listener?.Stop();
            _cts?.Dispose();
            _sendLock.Dispose();
        }
    }
}
