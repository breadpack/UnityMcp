using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace UnityMcpBridge;

public class UnityConnection : IDisposable
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private readonly string _host;
    private readonly int _port;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public UnityConnection(string host = "127.0.0.1", int port = 9876)
    {
        _host = host;
        _port = port;
    }

    public async Task EnsureConnectedAsync(CancellationToken ct = default)
    {
        if (_client?.Connected == true) return;

        _client?.Dispose();
        _client = new TcpClient();
        await _client.ConnectAsync(_host, _port, ct);
        _stream = _client.GetStream();
    }

    public async Task<JsonDocument> SendRequestAsync(string tool, JsonElement? @params = null, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await EnsureConnectedAsync(ct);

            var request = new
            {
                id = Guid.NewGuid().ToString(),
                tool,
                @params = @params?.ValueKind == JsonValueKind.Object ? @params : JsonDocument.Parse("{}").RootElement
            };

            var json = JsonSerializer.Serialize(request);
            var payload = Encoding.UTF8.GetBytes(json);
            var lengthBytes = new byte[4];
            lengthBytes[0] = (byte)(payload.Length >> 24);
            lengthBytes[1] = (byte)(payload.Length >> 16);
            lengthBytes[2] = (byte)(payload.Length >> 8);
            lengthBytes[3] = (byte)(payload.Length);

            await _stream!.WriteAsync(lengthBytes, ct);
            await _stream.WriteAsync(payload, ct);
            await _stream.FlushAsync(ct);

            // Read response
            var respLenBuf = new byte[4];
            await ReadExactAsync(_stream, respLenBuf, 4, ct);
            int respLen = (respLenBuf[0] << 24) | (respLenBuf[1] << 16)
                        | (respLenBuf[2] << 8) | respLenBuf[3];

            var respPayload = new byte[respLen];
            await ReadExactAsync(_stream, respPayload, respLen, ct);

            return JsonDocument.Parse(Encoding.UTF8.GetString(respPayload));
        }
        finally
        {
            _lock.Release();
        }
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, int count, CancellationToken ct)
    {
        int offset = 0;
        while (offset < count)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), ct);
            if (read == 0) throw new IOException("Connection closed");
            offset += read;
        }
    }

    public void Dispose()
    {
        _stream?.Dispose();
        _client?.Dispose();
    }
}
