using System;
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
            _listener.Start();
            _ = AcceptLoopAsync(_cts.Token);
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();
                    _client?.Close();
                    _client = client;
                    _stream = client.GetStream();
                    _ = ReadLoopAsync(_stream, ct);
                }
                catch (ObjectDisposedException) { break; }
                catch (Exception) { /* 재시도 */ }
            }
        }

        private async Task ReadLoopAsync(NetworkStream stream, CancellationToken ct)
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
                        UnityEngine.Debug.LogError($"[MCP] Invalid payload size: {length} bytes (max {MaxPayloadSize}). Disconnecting.");
                        break;
                    }

                    var payload = new byte[length];
                    if (!await ReadExactAsync(stream, payload, length, ct)) break;

                    var json = Encoding.UTF8.GetString(payload);
                    var request = JsonConvert.DeserializeObject<McpRequest>(json);

                    // 핸들러 호출 (MainThreadDispatcher로 메인 스레드 전환)
                    var response = await _handler(request);
                    await SendAsync(JsonConvert.SerializeObject(response, CamelCaseSettings));
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

        private async Task SendAsync(string json)
        {
            if (_stream == null || !_stream.CanWrite) return;
            await _sendLock.WaitAsync();
            try
            {
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
            _stream?.Close();
            _client?.Close();
            _listener?.Stop();
            _cts?.Dispose();
            _sendLock.Dispose();
        }
    }
}
