#:package Microsoft.Extensions.Hosting.WindowsServices@10.0.14
#:property TargetFramework=net8.0
#:property LangVersion=latest
#:property PublishAot=true

using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;

Environment.CurrentDirectory = AppContext.BaseDirectory;

var builder = Host.CreateEmptyApplicationBuilder(new()
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});
builder.Configuration.AddJsonFile("Client.json", optional: true, reloadOnChange: false);
builder.Configuration.AddCommandLine(args);

if (WindowsServiceHelpers.IsWindowsService())
{
    builder.Services.AddSingleton<IHostLifetime, WindowsServiceLifetime>();
}
else
{
    builder.Logging.AddSimpleConsole(o => o.TimestampFormat = "HH:mm:ss.fff ");
}

builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Services.AddHostedService<BridgeWorker>();

var host = builder.Build();
await host.RunAsync();

// 头 10 字节：[0]type [1]flags [2..5]streamId(BE) [6..9]payloadLen(BE)
enum FrameType : byte { Open = 3, Data = 4, Head = 5, End = 6, Reset = 7 }
enum FrameFlags : byte { Text = 1, More = 2 }
record BridgeFrame(FrameType Type, FrameFlags Flags, uint Id, byte[] Payload);

static class FrameCodec
{
    public const int HeaderSize = 10;

    public static byte[] Encode(FrameType type, uint id, ReadOnlySpan<byte> payload, FrameFlags flags = 0)
    {
        var buf = new byte[HeaderSize + payload.Length];
        buf[0] = (byte)type;
        buf[1] = (byte)flags;
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(2, 4), id);
        BinaryPrimitives.WriteUInt32BigEndian(buf.AsSpan(6, 4), (uint)payload.Length);
        payload.CopyTo(buf.AsSpan(HeaderSize));
        return buf;
    }

    public static byte[] EncodeJson<T>(FrameType type, uint id, T value, JsonTypeInfo<T> info, FrameFlags flags = 0)
        => Encode(type, id, JsonSerializer.SerializeToUtf8Bytes(value, info), flags);

    public static byte[] EncodeEmpty(FrameType type, uint id)
        => Encode(type, id, []);

    public static BridgeFrame Decode(ReadOnlySpan<byte> raw)
    {
        if (raw.Length < HeaderSize) throw new InvalidDataException("帧长度不足");
        var length = BinaryPrimitives.ReadUInt32BigEndian(raw.Slice(6, 4));
        if (length > raw.Length - HeaderSize) throw new InvalidDataException("帧被截断");
        return new BridgeFrame((FrameType)raw[0], (FrameFlags)raw[1], BinaryPrimitives.ReadUInt32BigEndian(raw.Slice(2, 4)), raw.Slice(HeaderSize, (int)length).ToArray());
    }
}

sealed record class OpenMessage(string Method, string Scheme, string Host, string Path, string Query, HeaderMap Headers, bool IsWebSocket, long? ContentLength, string? ClientIp);

sealed record class HeadMessage(int Status, HeaderMap Headers);

sealed record class ResetMessage(string Reason);

sealed class HeaderMap : Dictionary<string, string[]>
{
    public HeaderMap() : base(StringComparer.OrdinalIgnoreCase) { }
}

// 源生成的序列化上下文：AOT 下不走反射，帧内的 JSON 编解码全部经由这里
[JsonSerializable(typeof(OpenMessage))]
[JsonSerializable(typeof(HeadMessage))]
[JsonSerializable(typeof(ResetMessage))]
partial class BridgeJsonContext : JsonSerializerContext
{
}

sealed class WsTransport(WebSocket ws, int bufferSize, CancellationToken tunnelToken = default)
{
    readonly WebSocket _ws = ws;
    readonly CancellationToken _tunnelToken = tunnelToken;
    static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(15);
    public WebSocketState State => _ws.State;
    readonly SemaphoreSlim _sendLock = new(1, 1);
    readonly byte[] _buffer = new byte[Math.Max(4096, bufferSize)];

    public async Task SendAsync(byte[] frame, CancellationToken ct)
    {
        await _sendLock.WaitAsync(ct);
        try
        {
            using var sendCts = CancellationTokenSource.CreateLinkedTokenSource(_tunnelToken);
            sendCts.CancelAfter(SendTimeout);
            await _ws.SendAsync(new ArraySegment<byte>(frame), WebSocketMessageType.Binary, true, sendCts.Token);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async Task<BridgeFrame?> ReadFrameAsync(CancellationToken ct)
    {
        using var ms = new MemoryStream();
        while (true)
        {
            var result = await _ws.ReceiveAsync(new ArraySegment<byte>(_buffer), ct);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }
            if (result.Count > 0) ms.Write(_buffer, 0, result.Count);
            if (result.EndOfMessage) break;
        }
        if (ms.Length < FrameCodec.HeaderSize)
        {
            return null;
        }
        // 坏帧只丢当前帧，不打断读循环
        try
        {
            return FrameCodec.Decode(ms.GetBuffer().AsSpan(0, (int)ms.Length));
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }
}

sealed class StreamContext(uint id, int capacity)
{
    public uint Id { get; } = id;
    public Channel<BridgeFrame> Queue { get; } = Channel.CreateBounded<BridgeFrame>(new BoundedChannelOptions(capacity)
    {
        SingleReader = true,
        SingleWriter = true,
        FullMode = BoundedChannelFullMode.Wait
    });
}

sealed class FrameBodyStream : Stream
{
    readonly ChannelReader<BridgeFrame> _reader;
    byte[] _current = [];
    int _offset;

    public FrameBodyStream(ChannelReader<BridgeFrame> reader) => _reader = reader;

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            if (_offset < _current.Length)
            {
                var n = Math.Min(buffer.Length, _current.Length - _offset);
                _current.AsSpan(_offset, n).CopyTo(buffer.Span);
                _offset += n;
                return n;
            }
            if (!await _reader.WaitToReadAsync(cancellationToken)) return 0;
            if (!_reader.TryRead(out var frame)) continue;
            switch (frame.Type)
            {
                case FrameType.Data:
                    _current = frame.Payload;
                    _offset = 0;
                    break;
                case FrameType.End:
                case FrameType.Reset:
                    return 0;
            }
        }
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override int Read(byte[] buffer, int offset, int count)
        => ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

sealed class BridgeWorker : BackgroundService
{
    readonly int BodyBufferSize = 65536;
    readonly int MaxConcurrentStreams = 256;
    public string Server { get; set; }
    public string ClientId { get; set; }
    public string Token { get; set; }
    public string Upstream { get; set; }
    public string Secret { get; set; }
    public bool AllowUntrustedCertificate { get; set; }
    public bool PreserveHostHeader { get; set; }
    static readonly HashSet<string> HopByHop = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection", "Keep-Alive", "Transfer-Encoding", "Upgrade",
        "Proxy-Connection", "Proxy-Authenticate", "Proxy-Authorization", "TE", "Trailer",
        "Host", "Content-Length", "Forwarded", "X-Real-IP", "Via"
    };

    readonly ILogger<BridgeWorker> _log;
    readonly HttpClient _http;
    readonly SemaphoreSlim _slots;
    readonly BridgeAuth _auth;

    public BridgeWorker(IConfiguration configuration, ILogger<BridgeWorker> log)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Server = configuration[nameof(Server)]!);
        ArgumentException.ThrowIfNullOrWhiteSpace(ClientId = configuration[nameof(ClientId)]!);
        ArgumentException.ThrowIfNullOrWhiteSpace(Token = configuration[nameof(Token)]!);
        ArgumentException.ThrowIfNullOrWhiteSpace(Upstream = configuration[nameof(Upstream)]!);
        ArgumentException.ThrowIfNullOrWhiteSpace(Secret = configuration[nameof(Secret)]!);
        AllowUntrustedCertificate = bool.TryParse(configuration[nameof(AllowUntrustedCertificate)], out var v1) && v1;
        PreserveHostHeader = bool.TryParse(configuration[nameof(PreserveHostHeader)], out var v2) && v2;
        _log = log;
        _slots = new SemaphoreSlim(MaxConcurrentStreams);
        _auth = new BridgeAuth(Secret);

        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
            ConnectTimeout = TimeSpan.FromSeconds(15),
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(60),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        };
        if (AllowUntrustedCertificate)
        {
            handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        }
        _http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => LoopAsync(stoppingToken);

    async Task LoopAsync(CancellationToken ct)
    {
        var backoff = TimeSpan.FromSeconds(1);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RunSessionAsync(ct);
                backoff = TimeSpan.FromSeconds(1);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (IsDuplicateRegistration(ex))
            {
                _log.LogWarning("client id {ClientId} 已在服务端在线，注册被拒绝（409），{Backoff} 后重试", ClientId, backoff);
            }
            catch (Exception ex)
            {
                _log.LogWarning("控制通道断开：{Error}", ex.Message);
            }

            try { await Task.Delay(backoff, ct); }
            catch (OperationCanceledException) { break; }
            backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, 15));
        }
    }

    // 服务端对重复注册返回 HTTP 409。ClientWebSocket 握手失败只抛 WebSocketException(NotAWebSocket)，
    // 拿不到 HttpRequestException.StatusCode，因此按 状态码枚举 + 消息中带引号的状态码 双重判定，
    // 不再用裸 Contains("409") 匹配，避免误命中端口号/耗时等数字。
    static bool IsDuplicateRegistration(Exception ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            if (e is HttpRequestException { StatusCode: HttpStatusCode.Conflict })
            {
                return true;
            }

            if (e is WebSocketException { WebSocketErrorCode: WebSocketError.NotAWebSocket }
                && e.Message.Contains("'409'", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    async Task RunSessionAsync(CancellationToken ct)
    {
        using var ws = new ClientWebSocket();
        if (AllowUntrustedCertificate)
        {
            ws.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        }

        var url = $"{Server}?id={ClientId}";
        if (!string.IsNullOrEmpty(Token))
        {
            url += $"&token={Uri.EscapeDataString(Token)}";
        }
        await ws.ConnectAsync(new Uri(url), ct);
        var transport = new WsTransport(ws, BodyBufferSize);
        using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = sessionCts.Token;

        _log.LogInformation("控制通道已连接 id={ClientId} server={Server}", ClientId, Server);

        var streams = new ConcurrentDictionary<uint, StreamContext>();
        try
        {
            while (!token.IsCancellationRequested)
            {
                var frame = await transport.ReadFrameAsync(token);
                if (frame is null) break;
                switch (frame.Type)
                {
                    case FrameType.Open:
                        {
                            OpenMessage? meta;
                            try
                            {
                                meta = JsonSerializer.Deserialize(frame.Payload, BridgeJsonContext.Default.OpenMessage);
                            }
                            catch (JsonException)
                            {
                                break;
                            }
                            if (meta is null) break;
                            var stream = new StreamContext(frame.Id, 64);
                            streams[frame.Id] = stream;
                            _ = HandleStreamAsync(streams, transport, stream, meta, token);
                            break;
                        }
                    case FrameType.Data:
                    case FrameType.Head:
                    case FrameType.End:
                    case FrameType.Reset:
                        if (streams.TryGetValue(frame.Id, out var target))
                        {
                            if (!target.Queue.Writer.TryWrite(frame))
                            {
                                target.Queue.Writer.TryComplete();
                                streams.TryRemove(frame.Id, out _);
                            }
                        }
                        break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log.LogWarning($"控制通道异常 {ex.Message}");
        }
        finally
        {
            try { sessionCts.Cancel(); } catch { }
            foreach (var kv in streams) kv.Value.Queue.Writer.TryComplete();
        }

        _log.LogInformation("控制通道已断开 id={ClientId}", ClientId);
    }

    async Task HandleStreamAsync(ConcurrentDictionary<uint, StreamContext> streams, WsTransport transport, StreamContext stream, OpenMessage meta, CancellationToken ct)
    {
        try
        {
            await DispatchAsync(transport, stream, meta, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning($"流 #{stream.Id} 收尾异常 {ex.Message}");
        }
        finally
        {
            streams.TryRemove(stream.Id, out _);
            stream.Queue.Writer.TryComplete();
        }
    }

    async Task DispatchAsync(WsTransport transport, StreamContext stream, OpenMessage meta, CancellationToken ct)
    {
        var id = stream.Id;
        await _slots.WaitAsync(ct);
        try
        {
            if (!_auth.IsTrusted(meta.ClientIp) && !_auth.Verify(meta))
            {
                if (meta.IsWebSocket)
                {
                    await transport.SendAsync(FrameCodec.EncodeJson(FrameType.Reset, id, new ResetMessage("未认证"), BridgeJsonContext.Default.ResetMessage), ct);
                }
                else
                {
                    await SendAuthPageAsync(transport, stream, ct);
                }
                return;
            }

            if (meta.IsWebSocket)
            {
                await HandleWebSocketAsync(transport, stream, meta, ct);
            }
            else
            {
                await HandleHttpAsync(transport, stream, meta, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log.LogWarning($"流 #{id} 处理失败 {ex.Message} {meta.Method} {meta.Path}");
            try
            {
                await transport.SendAsync(FrameCodec.EncodeJson(FrameType.Reset, id, new ResetMessage(ex.Message), BridgeJsonContext.Default.ResetMessage), ct);
            }
            catch { }
        }
        finally
        {
            _slots.Release();
        }
    }

    async Task SendAuthPageAsync(WsTransport transport, StreamContext stream, CancellationToken ct)
    {
        var id = stream.Id;
        var body = Encoding.UTF8.GetBytes(_auth.BuildPage());
        var head = new HeadMessage(401, new HeaderMap
        {
            ["Content-Type"] = ["text/html; charset=utf-8"],
            ["Content-Length"] = [body.Length.ToString()],
            ["Cache-Control"] = ["no-store"]
        });
        await transport.SendAsync(FrameCodec.EncodeJson(FrameType.Head, id, head, BridgeJsonContext.Default.HeadMessage), ct);
        await transport.SendAsync(FrameCodec.Encode(FrameType.Data, id, body), ct);
        await transport.SendAsync(FrameCodec.EncodeEmpty(FrameType.End, id), ct);
        _ = DrainAsync(stream.Queue.Reader);
    }

    // 已回响应后仍要排空请求体，否则 Server 侧注入端会卡在写帧
    static async Task DrainAsync(ChannelReader<BridgeFrame> reader)
    {
        try
        {
            while (await reader.WaitToReadAsync())
            {
                while (reader.TryRead(out _)) { }
            }
        }
        catch { }
    }

    async Task HandleHttpAsync(WsTransport transport, StreamContext stream, OpenMessage meta, CancellationToken ct)
    {
        var id = stream.Id;

        var uri = new Uri(Combine(Upstream!, meta.Path, meta.Query));
        using var request = new HttpRequestMessage(new HttpMethod(meta.Method), uri);

        var hasBody = meta.ContentLength is null or > 0;
        if (hasBody)
        {
            request.Content = new StreamContent(new FrameBodyStream(stream.Queue.Reader));
        }

        foreach (var kv in meta.Headers ?? [])
        {
            if (HopByHop.Contains(kv.Key) || kv.Key.StartsWith("X-Forwarded-", StringComparison.OrdinalIgnoreCase)) continue;
            if (!request.Headers.TryAddWithoutValidation(kv.Key, kv.Value))
            {
                request.Content?.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            }
        }

        if (PreserveHostHeader && !string.IsNullOrEmpty(meta.Host))
        {
            request.Headers.Host = meta.Host;
        }

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        var head = new HeadMessage((int)response.StatusCode, []);
        foreach (var h in response.Headers)
        {
            if (HopByHop.Contains(h.Key)) continue;
            head.Headers[h.Key] = [.. h.Value];
        }
        foreach (var h in response.Content.Headers)
        {
            if (HopByHop.Contains(h.Key)) continue;
            head.Headers[h.Key] = [.. h.Value];
        }
        await transport.SendAsync(FrameCodec.EncodeJson(FrameType.Head, id, head, BridgeJsonContext.Default.HeadMessage), ct);

        var buffer = ArrayPool<byte>.Shared.Rent(BodyBufferSize);
        try
        {
            var body = await response.Content.ReadAsStreamAsync(ct);
            while (true)
            {
                var n = await body.ReadAsync(buffer.AsMemory(0, BodyBufferSize), ct);
                if (n <= 0) break;
                await transport.SendAsync(FrameCodec.Encode(FrameType.Data, id, buffer.AsSpan(0, n)), ct);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        await transport.SendAsync(FrameCodec.EncodeEmpty(FrameType.End, id), ct);
    }

    async Task HandleWebSocketAsync(WsTransport transport, StreamContext stream, OpenMessage meta, CancellationToken ct)
    {
        var id = stream.Id;
        var uri = new Uri(Combine(ToWs(Upstream!), meta.Path, meta.Query));

        using var upstream = new ClientWebSocket();
        if (AllowUntrustedCertificate)
        {
            upstream.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        }
        if (meta.Headers is not null && meta.Headers.TryGetValue("Sec-WebSocket-Protocol", out var proto) && proto.Length > 0)
        {
            var first = proto.SelectMany(v => v.Split(',')).Select(v => v.Trim()).FirstOrDefault(v => v.Length > 0);
            if (first is not null) upstream.Options.AddSubProtocol(first);
        }

        await upstream.ConnectAsync(uri, ct);

        using var localCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var localToken = localCts.Token;

        var buffer = ArrayPool<byte>.Shared.Rent(BodyBufferSize);
        try
        {
            var up = PumpUpstreamAsync();

            async Task PumpUpstreamAsync()
            {
                try
                {
                    while (!localToken.IsCancellationRequested && upstream.State == WebSocketState.Open)
                    {
                        var r = await upstream.ReceiveAsync(new ArraySegment<byte>(buffer), localToken);
                        if (r.MessageType == WebSocketMessageType.Close)
                        {
                            await transport.SendAsync(FrameCodec.EncodeEmpty(FrameType.End, id), localToken);
                            return;
                        }
                        if (r.Count == 0) continue;
                        FrameFlags flags = 0;
                        if (r.MessageType == WebSocketMessageType.Text) flags |= FrameFlags.Text;
                        if (!r.EndOfMessage) flags |= FrameFlags.More;
                        await transport.SendAsync(FrameCodec.Encode(FrameType.Data, id, buffer.AsSpan(0, r.Count), flags), localToken);
                    }
                }
                catch { }
                finally
                {
                    try { localCts.Cancel(); } catch { }
                }
            }

            while (true)
            {
                var frame = await ReadStreamFrameAsync(stream, localToken);
                if (frame is null) break;
                if (frame.Type == FrameType.Data)
                {
                    if (upstream.State != WebSocketState.Open) break;
                    var type = (frame.Flags & FrameFlags.Text) != 0 ? WebSocketMessageType.Text : WebSocketMessageType.Binary;
                    var end = (frame.Flags & FrameFlags.More) == 0;
                    try
                    {
                        await upstream.SendAsync(new ArraySegment<byte>(frame.Payload), type, end, localToken);
                    }
                    catch { break; }
                }
                else if (frame.Type == FrameType.End || frame.Type == FrameType.Reset)
                {
                    break;
                }
            }

            await CloseWsAsync(upstream);
            try { await up; } catch { }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    static async Task CloseWsAsync(WebSocket ws)
    {
        try
        {
            if (ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bridge closed", cts.Token);
            }
        }
        catch { }
        finally
        {
            ws.Abort();
        }
    }

    static async Task<BridgeFrame?> ReadStreamFrameAsync(StreamContext stream, CancellationToken ct)
    {
        try
        {
            return await stream.Queue.Reader.ReadAsync(ct);
        }
        catch (ChannelClosedException)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    static string Combine(string target, string path, string query)
    {
        var t = target.TrimEnd('/');
        var p = string.IsNullOrEmpty(path) ? "/" : path;
        if (!p.StartsWith('/')) p = "/" + p;
        return t + p + query;
    }
    static string ToWs(string url)
    {
        var b = new UriBuilder(url);
        b.Scheme = b.Scheme.ToLowerInvariant() switch
        {
            "https" => "wss",
            "http" => "ws",
            var s => s
        };
        return b.Uri.ToString().TrimEnd('/');
    }
}

// 认证门：IP 白名单 → 认证头（ECDH P-256 + HKDF + HMAC-SHA256）→ 认证页
sealed class BridgeAuth(string secret)
{
    public const string HeaderName = "X-Bridge-Auth";
    static readonly TimeSpan TrustTtl = TimeSpan.FromHours(24);
    static readonly TimeSpan KeyTtl = TimeSpan.FromMinutes(5);
    static readonly byte[] Salt = Encoding.UTF8.GetBytes("bridge-auth-salt-v1");
    static readonly byte[] Info = Encoding.UTF8.GetBytes("bridge-auth-v1");

    readonly string _secret = secret;
    readonly ConcurrentDictionary<string, DateTimeOffset> _trusted = new(StringComparer.OrdinalIgnoreCase);
    readonly object _gate = new();
    ECDiffieHellman? _key;
    DateTimeOffset _keyExpire;
    public bool Enabled => _secret.Length > 0;
    static bool IsLocalAddress(string ip)
    {
        if (!IPAddress.TryParse(ip.Trim(), out var addr)) return false;
        if (addr.IsIPv4MappedToIPv6) addr = addr.MapToIPv4();
        if (IPAddress.IsLoopback(addr)) return true;
        return _localAddresses.Value.Contains(addr);
    }

    static readonly Lazy<HashSet<IPAddress>> _localAddresses = new(LoadLocalAddresses);

    static HashSet<IPAddress> LoadLocalAddresses()
    {
        var set = new HashSet<IPAddress>();
        try
        {
            foreach (var a in Dns.GetHostAddresses(Dns.GetHostName()))
                set.Add(a.IsIPv4MappedToIPv6 ? a.MapToIPv4() : a);
        }
        catch { }
        return set;
    }

    // 第 1 步：IP 是否已在白名单内（本机回环地址固定放行，便于本地 Mock/联调免认证）
    public bool IsTrusted(string? ip)
    {
        if (!Enabled) return true;
        if (string.IsNullOrEmpty(ip)) return false;
        if (IsLocalAddress(ip)) return true;
        if (!_trusted.TryGetValue(ip, out var expire)) return false;
        if (expire > DateTimeOffset.UtcNow) return true;
        _trusted.TryRemove(ip, out _);
        return false;
    }

    // 第 2 步：校验 X-Bridge-Auth: base64(客户端公钥点) . base64(HMAC)，通过则登记该 IP
    public bool Verify(OpenMessage meta)
    {
        if (!Enabled) return true;
        if (meta.Headers is null || !meta.Headers.TryGetValue(HeaderName, out var values) || values.Length == 0) return false;
        var raw = values[0];
        var dot = raw.IndexOf('.');
        if (dot <= 0 || dot == raw.Length - 1) return false;

        byte[] clientPublic;
        byte[] mac;
        try
        {
            clientPublic = Convert.FromBase64String(raw[..dot]);
            mac = Convert.FromBase64String(raw[(dot + 1)..]);
        }
        catch (FormatException) { return false; }

        var expected = SignClientKey(clientPublic);
        if (expected is null || !CryptographicOperations.FixedTimeEquals(expected, mac)) return false;

        if (!string.IsNullOrEmpty(meta.ClientIp))
        {
            _trusted[meta.ClientIp] = DateTimeOffset.UtcNow.Add(TrustTtl);
        }
        return true;
    }

    // 服务端私钥 × 客户端公钥 → 共享密钥 → HKDF → 对 secret 做 HMAC
    byte[]? SignClientKey(byte[] clientPublicKey)
    {
        if (clientPublicKey.Length != 65 || clientPublicKey[0] != 4) return null;
        lock (_gate)
        {
            EnsureKey();
            try
            {
                using var other = ECDiffieHellman.Create(new ECParameters
                {
                    Curve = ECCurve.NamedCurves.nistP256,
                    Q = new ECPoint { X = clientPublicKey[1..33], Y = clientPublicKey[33..] }
                });
                var shared = _key!.DeriveRawSecretAgreement(other.PublicKey);
                if (shared is null || shared.Length == 0) return null;
                return Mac(shared);
            }
            catch (CryptographicException) { return null; }
        }
    }

    byte[] Mac(byte[] shared)
    {
        var okm = HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, 32, Salt, Info);
        return HMACSHA256.HashData(okm, Encoding.UTF8.GetBytes(_secret));
    }

    void EnsureKey()
    {
        if (_key is not null && _keyExpire > DateTimeOffset.UtcNow) return;
        _key?.Dispose();
        _key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        _keyExpire = DateTimeOffset.UtcNow.Add(KeyTtl);
    }

    // 服务端公钥按未压缩点下发：0x04 || X(32) || Y(32)
    string PublicKeyPoint()
    {
        lock (_gate)
        {
            EnsureKey();
            var p = _key!.ExportParameters(false);
            var buf = new byte[65];
            buf[0] = 4;
            var x = p.Q.X ?? [];
            var y = p.Q.Y ?? [];
            x.CopyTo(buf, 33 - x.Length);
            y.CopyTo(buf, 65 - y.Length);
            return Convert.ToBase64String(buf);
        }
    }

    public string BuildPage() => Page.Replace("__POINT__", PublicKeyPoint(), StringComparison.Ordinal)
                                    .Replace("__SALT__", Convert.ToBase64String(Salt), StringComparison.Ordinal)
                                    .Replace("__INFO__", Convert.ToBase64String(Info), StringComparison.Ordinal);

    const string Page = """
<!doctype html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>需要认证</title>
<style>
  :root{color-scheme:dark}
  body{margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;
       font:15px/1.6 -apple-system,"Segoe UI",system-ui,sans-serif;background:#111417;color:#e6e8ea}
  .box{width:min(360px,calc(100vw - 48px));padding:28px;border-radius:14px;background:#1a1f24;
       border:1px solid #2a323a;box-shadow:0 12px 32px rgba(0,0,0,.45)}
  h1{margin:0 0 6px;font-size:17px;font-weight:600}
  p.sub{margin:0 0 20px;font-size:13px;color:#8b959e}
  input{width:100%;box-sizing:border-box;padding:10px 12px;border-radius:8px;border:1px solid #333d45;
        background:#12171b;color:#e6e8ea;font-size:15px;outline:none}
  input:focus{border-color:#3d7eff}
  button{margin-top:14px;width:100%;padding:10px;border:0;border-radius:8px;background:#3d7eff;
         color:#fff;font-size:15px;font-weight:600;cursor:pointer}
  button:disabled{opacity:.55;cursor:default}
  .err{margin-top:12px;min-height:18px;font-size:13px;color:#ff6b6b}
</style>
</head>
<body>
<div class="box">
  <h1>访问认证</h1>
  <p class="sub">输入访问密码以继续</p>
  <input id="p" type="password" autocomplete="current-password" placeholder="密码" autofocus>
  <button id="b">验证</button>
  <div class="err" id="e"></div>
</div>
<script>
/* HTTP 属非安全上下文，crypto.subtle 不可用，故自带 P-256 / SHA-256 / HMAC / HKDF 实现 */
/* 纯 JS：P-256 ECDH + SHA-256 + HMAC + HKDF（不依赖 crypto.subtle，HTTP 非安全上下文可用） */
var BC = (function () {
  const P = 0xffffffff00000001000000000000000000000000ffffffffffffffffffffffffn;
  const A = P - 3n;
  const GX = 0x6b17d1f2e12c4247f8bce6e563a440f277037d812deb33a0f4a13945d898c296n;
  const GY = 0x4fe342e2fe1a7f9b8ee7eb4a7c0f9e162bce33576b315ececbb6406837bf51f5n;
  const N = 0xffffffff00000000ffffffffffffffffbce6faada7179e84f3b9cac2fc632551n;

  const mod = (x, m) => { const r = x % (m || P); return r < 0n ? r + (m || P) : r; };
  function inv(a) {
    let r0 = mod(a), r1 = P, s0 = 1n, s1 = 0n;
    while (r1 !== 0n) { const q = r0 / r1; [r0, r1] = [r1, r0 - q * r1]; [s0, s1] = [s1, s0 - q * s1]; }
    return mod(s0);
  }
  function add(p1, p2) {
    if (!p1) return p2;
    if (!p2) return p1;
    const x1 = p1[0], y1 = p1[1], x2 = p2[0], y2 = p2[1];
    let l;
    if (x1 === x2) {
      if (mod(y1 + y2) === 0n) return null;
      l = mod((3n * x1 * x1 + A) * inv(2n * y1));
    } else {
      l = mod((y2 - y1) * inv(x2 - x1));
    }
    const x3 = mod(l * l - x1 - x2);
    return [x3, mod(l * (x1 - x3) - y1)];
  }
  function mul(k, p) {
    let r = null, acc = p, e = mod(k, N);
    while (e > 0n) {
      if (e & 1n) r = add(r, acc);
      acc = add(acc, acc);
      e >>= 1n;
    }
    return r;
  }

  const toBig = u8 => (u8.length ? BigInt('0x' + Array.from(u8, b => b.toString(16).padStart(2, '0')).join('')) : 0n);
  function toBytes(n, len) {
    let h = n.toString(16);
    if (h.length > len * 2) h = h.slice(h.length - len * 2);
    h = h.padStart(len * 2, '0');
    const out = new Uint8Array(len);
    for (let i = 0; i < len; i++) out[i] = parseInt(h.substr(i * 2, 2), 16);
    return out;
  }
  function rand(n) {
    const u = new Uint8Array(n);
    (window.crypto || window.msCrypto).getRandomValues(u);
    return u;
  }

  // 未压缩点：0x04 || X(32) || Y(32)
  function encodePoint(pt) { const out = new Uint8Array(65); out[0] = 4; out.set(toBytes(pt[0], 32), 1); out.set(toBytes(pt[1], 32), 33); return out; }
  function decodePoint(u8) {
    if (u8.length !== 65 || u8[0] !== 4) throw new Error('bad point');
    return [toBig(u8.slice(1, 33)), toBig(u8.slice(33, 65))];
  }

  // ECDH：返回共享 X 坐标（32 字节）
  function ecdh(privKey, peerPointBytes) {
    const pt = mul(toBig(privKey), decodePoint(peerPointBytes));
    if (!pt) throw new Error('bad shared point');
    return toBytes(pt[0], 32);
  }
  function genKeyPair() {
    let d;
    do { d = toBig(rand(32)) % N; } while (d === 0n);
    const priv = toBytes(d, 32);
    return { priv: priv, pub: encodePoint(mul(d, [GX, GY])) };
  }

  /* ---------- SHA-256 ---------- */
  const K = new Uint32Array([
    0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
    0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
    0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
    0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
    0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
    0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
    0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
    0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2]);
  function sha256(msg) {
    const ml = msg.length, words = Math.ceil((ml + 9) / 64) * 16;
    const buf = new Uint32Array(words);
    for (let i = 0; i < ml; i++) buf[i >> 2] |= msg[i] << (24 - (i % 4) * 8);
    buf[ml >> 2] |= 0x80 << (24 - (ml % 4) * 8);
    buf[words - 1] = ml * 8;
    const H = new Uint32Array([0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a, 0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19]);
    const W = new Uint32Array(64);
    for (let off = 0; off < words; off += 16) {
      for (let t = 0; t < 64; t++) {
        if (t < 16) W[t] = buf[off + t];
        else {
          const s0 = ((W[t - 15] >>> 7) | (W[t - 15] << 25)) ^ ((W[t - 15] >>> 18) | (W[t - 15] << 14)) ^ (W[t - 15] >>> 3);
          const s1 = ((W[t - 2] >>> 17) | (W[t - 2] << 15)) ^ ((W[t - 2] >>> 19) | (W[t - 2] << 13)) ^ (W[t - 2] >>> 10);
          W[t] = (W[t - 16] + s0 + W[t - 7] + s1) >>> 0;
        }
      }
      let [a, b, c, d, e, f, g, h] = H;
      for (let t = 0; t < 64; t++) {
        const S1 = ((e >>> 6) | (e << 26)) ^ ((e >>> 11) | (e << 21)) ^ ((e >>> 25) | (e << 7));
        const ch = (e & f) ^ (~e & g);
        const t1 = (h + S1 + ch + K[t] + W[t]) >>> 0;
        const S0 = ((a >>> 2) | (a << 30)) ^ ((a >>> 13) | (a << 19)) ^ ((a >>> 22) | (a << 10));
        const mj = (a & b) ^ (a & c) ^ (b & c);
        const t2 = (S0 + mj) >>> 0;
        h = g; g = f; f = e; e = (d + t1) >>> 0; d = c; c = b; b = a; a = (t1 + t2) >>> 0;
      }
      H[0] = (H[0] + a) >>> 0; H[1] = (H[1] + b) >>> 0; H[2] = (H[2] + c) >>> 0; H[3] = (H[3] + d) >>> 0;
      H[4] = (H[4] + e) >>> 0; H[5] = (H[5] + f) >>> 0; H[6] = (H[6] + g) >>> 0; H[7] = (H[7] + h) >>> 0;
    }
    const out = new Uint8Array(32);
    for (let i = 0; i < 8; i++) { out[i * 4] = H[i] >>> 24; out[i * 4 + 1] = (H[i] >>> 16) & 255; out[i * 4 + 2] = (H[i] >>> 8) & 255; out[i * 4 + 3] = H[i] & 255; }
    return out;
  }
  function hmac(key, msg) {
    if (key.length > 64) key = sha256(key);
    const k = new Uint8Array(64); k.set(key);
    const ipad = new Uint8Array(64 + msg.length), opad = new Uint8Array(64 + 32);
    for (let i = 0; i < 64; i++) { ipad[i] = k[i] ^ 0x36; opad[i] = k[i] ^ 0x5c; }
    ipad.set(msg, 64);
    opad.set(sha256(ipad), 64);
    return sha256(opad);
  }
  function hkdf(ikm, salt, info, len) {
    const prk = hmac(salt, ikm);
    const out = new Uint8Array(len);
    let prev = new Uint8Array(0), pos = 0, counter = 1;
    while (pos < len) {
      const t = new Uint8Array(prev.length + info.length + 1);
      t.set(prev); t.set(info, prev.length); t[prev.length + info.length] = counter++;
      prev = hmac(prk, t);
      out.set(prev.slice(0, Math.min(prev.length, len - pos)), pos);
      pos += prev.length;
    }
    return out;
  }
  return { genKeyPair: genKeyPair, ecdh: ecdh, sha256: sha256, hmac: hmac, hkdf: hkdf };
})();
if (typeof module !== 'undefined') module.exports = BC;


function u8(s){var b=atob(s),u=new Uint8Array(b.length);for(var i=0;i<b.length;i++)u[i]=b.charCodeAt(i);return u;}
function b64(u){var s='';for(var i=0;i<u.length;i++)s+=String.fromCharCode(u[i]);return btoa(s);}
var SERVER_PUB="__POINT__", SALT="__SALT__", INFO="__INFO__";

// 客户端生成密钥对 → 与服务端公钥算出共享密钥 → HKDF → 用密码做 HMAC
function buildHeader(pwd){
  var kp=BC.genKeyPair();
  var z=BC.ecdh(kp.priv,u8(SERVER_PUB));
  var okm=BC.hkdf(z,u8(SALT),u8(INFO),32);
  var mac=BC.hmac(okm,new TextEncoder().encode(pwd));
  return b64(kp.pub)+'.'+b64(mac);
}

var btn=document.getElementById('b'),inp=document.getElementById('p'),err=document.getElementById('e');
function submit(){
  var pwd=inp.value;
  if(!pwd){err.textContent='请输入密码';return;}
  btn.disabled=true;err.textContent='';
  var hdr;
  try{hdr=buildHeader(pwd);}catch(ex){err.textContent='认证失败：'+(ex.message||ex);btn.disabled=false;return;}
  fetch(location.href,{headers:{'X-Bridge-Auth':hdr},cache:'no-store'}).then(function(r){
    if(r.ok){location.reload();return;}
    err.textContent='密码错误或被拒绝（HTTP '+r.status+'）';btn.disabled=false;
  }).catch(function(ex){err.textContent='认证失败：'+(ex.message||ex);btn.disabled=false;});
}
btn.addEventListener('click',submit);
inp.addEventListener('keydown',function(ev){if(ev.key==='Enter')submit();});
</script>
</body>
</html>
""";
}
