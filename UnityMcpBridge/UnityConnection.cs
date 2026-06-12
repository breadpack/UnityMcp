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

    // 요청별 타임아웃 — 무한 대기 방지. 도구 특성에 따라 차등.
    // execute_code/build/run_tests 등 장기 작업은 일반 도구보다 길게 둔다.
    private static readonly Dictionary<string, TimeSpan> ToolTimeouts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["unity_execute_code"] = TimeSpan.FromSeconds(60),
        ["unity_build"] = TimeSpan.FromSeconds(300),
        ["unity_run_tests"] = TimeSpan.FromSeconds(300),
        ["unity_refresh_assets"] = TimeSpan.FromSeconds(60),
        ["unity_manage_package"] = TimeSpan.FromSeconds(120),
    };

    private static readonly TimeSpan DefaultTimeout = ReadTimeoutFromEnv("UNITY_REQUEST_TIMEOUT_SEC", 15);

    // 연결 확립(소켓 connect) 단계의 단일 시도 타임아웃.
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

    // 컴파일/도메인 리로드로 Editor TCP 서버가 일시 부재할 때 재연결을 견디는 총 윈도우.
    // check-unity.js 의 UNITY_MAX_WAIT_SEC 와 동일 환경변수를 공유한다.
    private static readonly TimeSpan ReconnectWindow = ReadTimeoutFromEnv("UNITY_MAX_WAIT_SEC", 60);

    public UnityConnection(string host = "127.0.0.1", int port = 9876)
    {
        _host = host;
        _port = port;
    }

    private static TimeSpan ReadTimeoutFromEnv(string name, int defaultSeconds)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (!string.IsNullOrEmpty(raw) && int.TryParse(raw, out var sec) && sec > 0)
            return TimeSpan.FromSeconds(sec);
        return TimeSpan.FromSeconds(defaultSeconds);
    }

    private static TimeSpan GetTimeout(string tool)
        => ToolTimeouts.TryGetValue(tool, out var t) ? t : DefaultTimeout;

    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_client?.Connected == true && _stream != null) return;

        DisposeConnection();
        var client = new TcpClient();
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        connectCts.CancelAfter(ConnectTimeout);
        try
        {
            await client.ConnectAsync(_host, _port, connectCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            client.Dispose();
            throw new SocketException((int)SocketError.TimedOut);
        }
        catch
        {
            client.Dispose();
            throw;
        }
        _client = client;
        _stream = client.GetStream();
    }

    public async Task<JsonDocument> SendRequestAsync(string tool, JsonElement? @params = null, CancellationToken ct = default)
    {
        var requestTimeout = GetTimeout(tool);
        await _lock.WaitAsync(ct);
        try
        {
            Exception? lastError = null;
            var attempt = 0;
            var reconnectDeadline = DateTime.UtcNow + ReconnectWindow;

            while (true)
            {
                attempt++;
                ct.ThrowIfCancellationRequested();

                // 1) 연결 확립 — 컴파일/리로드로 서버 부재 시 데드라인까지 backoff 재시도.
                try
                {
                    await EnsureConnectedAsync(ct);
                }
                catch (Exception ex) when (ex is SocketException or IOException)
                {
                    lastError = ex;
                    if (DateTime.UtcNow >= reconnectDeadline)
                        throw new IOException(
                            $"Unity Editor 연결 실패 (재시도 {attempt}회, {ReconnectWindow.TotalSeconds:F0}s 초과). " +
                            $"Editor가 컴파일/도메인 리로드 중이거나 종료되었을 수 있습니다: {ex.Message}", ex);
                    await Task.Delay(BackoffFor(attempt), ct);
                    continue;
                }

                // 2) 요청 송수신 — 도구별 타임아웃 적용.
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(requestTimeout);
                try
                {
                    return await SendAndReceiveAsync(tool, @params, timeoutCts.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    // 요청 타임아웃 — 연결이 오염되었으므로 폐기. 재시도하지 않는다
                    // (무한 루프성 코드는 재시도해도 또 막히므로 즉시 에러로 복구).
                    DisposeConnection();
                    throw new TimeoutException(
                        $"'{tool}' 요청이 {requestTimeout.TotalSeconds:F0}s 내 응답하지 않아 연결을 재설정했습니다. " +
                        $"execute_code 의 경우 무한 루프나 메인 스레드 블로킹 코드일 수 있습니다.");
                }
                catch (Exception ex) when (ex is IOException or SocketException)
                {
                    // 전송 중 끊김(컴파일 시작 등) — 폐기 후 데드라인까지 재연결 재시도.
                    DisposeConnection();
                    lastError = ex;
                    if (DateTime.UtcNow >= reconnectDeadline)
                        throw new IOException(
                            $"Unity Editor 연결이 끊어졌습니다 (재시도 {attempt}회): {ex.Message}", ex);
                    await Task.Delay(BackoffFor(attempt), ct);
                    continue;
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<JsonDocument> SendAndReceiveAsync(string tool, JsonElement? @params, CancellationToken ct)
    {
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

    private static TimeSpan BackoffFor(int attempt)
    {
        // 300ms → 600ms → ... 최대 2s.
        var ms = Math.Min(300 * attempt, 2000);
        return TimeSpan.FromMilliseconds(ms);
    }

    private void DisposeConnection()
    {
        try { _stream?.Dispose(); } catch { /* best-effort */ }
        try { _client?.Dispose(); } catch { /* best-effort */ }
        _stream = null;
        _client = null;
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
        DisposeConnection();
    }
}
