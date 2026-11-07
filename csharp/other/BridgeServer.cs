#:sdk Microsoft.NET.Sdk.Web
#:package Microsoft.Extensions.Hosting.WindowsServices@10.0.14
#:property TargetFramework=net8.0
#:property LangVersion=latest
#:property PublishAot=true

using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Primitives;

[assembly: SupportedOSPlatform("windows")]

Environment.CurrentDirectory = AppContext.BaseDirectory;

var builder = WebApplication.CreateEmptyBuilder(new()
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});
builder.Configuration.AddJsonFile("Server.json", optional: true, reloadOnChange: false);
builder.Configuration.AddCommandLine(args);

var hub = new BridgeHub(builder.Configuration);
builder.Services.AddSingleton(hub);
builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(3));
builder.WebHost.UseUrls(hub.Url);
builder.WebHost.UseHttpSys();

if (WindowsServiceHelpers.IsWindowsService())
{
    builder.Services.AddSingleton<IHostLifetime, WindowsServiceLifetime>();
}
else
{
    builder.Logging.AddSimpleConsole(o => o.TimestampFormat = "HH:mm:ss.fff ");
}

builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);
var app = builder.Build();
var bootLog = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Bridge");
bootLog.LogInformation("代理服务已启动：{Url}，控制域 {Host}", hub.Url, hub.ServerHost);
var life = app.Services.GetRequiredService<IHostApplicationLifetime>();
life.ApplicationStopping.Register(() => bootLog.LogInformation("代理服务正在停止"));
life.ApplicationStopped.Register(() => bootLog.LogInformation("代理服务已停止"));
app.UseWebSockets();
app.Use(_ => BridgeMiddleware.InvokeAsync);

await app.RunAsync();

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

    public static byte[] EncodeJson<T>(FrameType type, uint id, T value, JsonTypeInfo<T> info, FrameFlags flags = 0) => Encode(type, id, JsonSerializer.SerializeToUtf8Bytes(value, info), flags);

    public static byte[] EncodeEmpty(FrameType type, uint id) => Encode(type, id, []);

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

sealed class ClientConnection(string clientId, WsTransport transport, ILogger log, int queueCapacity)
{
    readonly WsTransport _transport = transport;
    readonly ConcurrentDictionary<uint, StreamContext> _streams = new();
    readonly ILogger _log = log;
    readonly int _queueCapacity = queueCapacity;
    int _streamSeq;

    public string ClientId { get; } = clientId;
    public DateTimeOffset ConnectedAt { get; } = DateTimeOffset.Now;
    public string RemoteIp { get; set; } = string.Empty;
    public int ActiveStreams => _streams.Count;

    public StreamContext CreateStream()
    {
        var id = unchecked((uint)Interlocked.Increment(ref _streamSeq));
        var stream = new StreamContext(id, _queueCapacity);
        _streams[id] = stream;
        return stream;
    }

    public void RemoveStream(uint id) => _streams.TryRemove(id, out _);
    public async Task RunAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var frame = await _transport.ReadFrameAsync(ct);
                if (frame is null)
                {
                    break;
                }
                if (_streams.TryGetValue(frame.Id, out var stream))
                {
                    if (!stream.Queue.Writer.TryWrite(frame))
                    {
                        stream.Queue.Writer.TryComplete();
                        _streams.TryRemove(frame.Id, out _);
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log.LogWarning($"client {ClientId} 通道异常 {ex.Message}");
        }
        finally
        {
            foreach (var s in _streams.Values) s.Queue.Writer.TryComplete();
            _streams.Clear();
        }
    }

    public async Task ForwardHttpAsync(HttpContext ctx, BridgeHub hub, CancellationToken ct)
    {
        var stream = CreateStream();
        var id = stream.Id;
        var completed = false;
        try
        {
            var meta = new OpenMessage(
                ctx.Request.Method,
                ctx.Request.Scheme,
                ctx.Request.Host.Value ?? string.Empty,
                (ctx.Request.PathBase + ctx.Request.Path).Value ?? "/",
                ctx.Request.QueryString.Value ?? string.Empty,
                CopyHeaders(ctx.Request.Headers),
                false,
                ctx.Request.ContentLength,
                ctx.Connection.RemoteIpAddress?.ToString());
            await _transport.SendAsync(FrameCodec.EncodeJson(FrameType.Open, id, meta, BridgeJsonContext.Default.OpenMessage), ct);

            using var pumpCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var pump = PumpRequestBodyAsync(ctx, hub, id, pumpCts.Token);
            completed = await ConsumeResponseAsync(ctx, stream, ct);
            if (completed && !ct.IsCancellationRequested)
            {
                await Task.WhenAny(pump, Task.Delay(TimeSpan.FromSeconds(30), CancellationToken.None));
            }
            await pumpCts.CancelAsync();
            try { await pump; } catch { }
        }
        finally
        {
            RemoveStream(id);
            if (!completed) await ResetUpstreamAsync(id);
        }
    }

    async Task ResetUpstreamAsync(uint id)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await _transport.SendAsync(
                FrameCodec.EncodeJson(FrameType.Reset, id, new ResetMessage("browser gone"), BridgeJsonContext.Default.ResetMessage),
                cts.Token);
        }
        catch { }
    }

    internal static async Task CloseWsAsync(WebSocket ws)
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

    async Task PumpRequestBodyAsync(HttpContext ctx, BridgeHub hub, uint id, CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(hub.BodyBufferSize);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var read = await ctx.Request.Body.ReadAsync(buffer.AsMemory(0, hub.BodyBufferSize), ct);
                if (read <= 0) break;
                await _transport.SendAsync(FrameCodec.Encode(FrameType.Data, id, buffer.AsSpan(0, read)), ct);
            }
            await _transport.SendAsync(FrameCodec.EncodeEmpty(FrameType.End, id), ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log.LogWarning($"流 #{id} 请求体转发中止 {ex.Message}");
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    static async Task<bool> ConsumeResponseAsync(HttpContext ctx, StreamContext stream, CancellationToken ct)
    {
        var started = false;
        try
        {
            while (true)
            {
                BridgeFrame frame;
                try
                {
                    frame = await stream.Queue.Reader.ReadAsync(ct);
                }
                catch (ChannelClosedException)
                {
                    if (!started)
                    {
                        ctx.Response.StatusCode = 502;
                        ctx.Response.ContentType = "text/plain; charset=utf-8";
                        await ctx.Response.WriteAsync("bridge channel closed", CancellationToken.None);
                    }
                    return false;
                }
                catch (OperationCanceledException)
                {
                    return false;
                }

                switch (frame.Type)
                {
                    case FrameType.Head:
                        {
                            var head = JsonSerializer.Deserialize(frame.Payload, BridgeJsonContext.Default.HeadMessage);
                            if (head is null) break;
                            ctx.Response.StatusCode = head.Status <= 0 ? 502 : head.Status;
                            foreach (var kv in head.Headers ?? [])
                            {
                                if (BridgeMiddleware.IsHopByHop(kv.Key)) continue;
                                if (string.Equals(kv.Key, "Content-Length", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (long.TryParse(kv.Value.FirstOrDefault(), out var len)) ctx.Response.ContentLength = len;
                                    continue;
                                }
                                ctx.Response.Headers[kv.Key] = new StringValues(kv.Value);
                            }
                            await ctx.Response.StartAsync(ct);
                            started = true;
                            break;
                        }
                    case FrameType.Data:
                        if (frame.Payload.Length > 0)
                        {
                            await ctx.Response.Body.WriteAsync(frame.Payload, ct);
                            await ctx.Response.Body.FlushAsync(ct);
                        }
                        break;
                    case FrameType.End:
                        await ctx.Response.CompleteAsync();
                        return true;
                    case FrameType.Reset:
                        {
                            var reset = JsonSerializer.Deserialize(frame.Payload, BridgeJsonContext.Default.ResetMessage);
                            if (!started)
                            {
                                ctx.Response.StatusCode = 502;
                                ctx.Response.ContentType = "text/plain; charset=utf-8";
                                await ctx.Response.WriteAsync($"bridge upstream error: {reset?.Reason}", ct);
                            }
                            return true;
                        }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            if (!started) { ctx.Response.StatusCode = 502; }
        }
        return false;
    }

    public async Task ForwardWebSocketAsync(HttpContext ctx, BridgeHub hub, CancellationToken ct)
    {
        var requested = ctx.WebSockets.WebSocketRequestedProtocols;
        using var ws = requested.Count > 0
            ? await ctx.WebSockets.AcceptWebSocketAsync(requested[0])
            : await ctx.WebSockets.AcceptWebSocketAsync();
        var stream = CreateStream();
        var id = stream.Id;
        var finished = false;
        var endSent = false;
        using var localCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var localToken = localCts.Token;
        try
        {
            var meta = new OpenMessage(
                "GET",
                ctx.Request.Scheme,
                ctx.Request.Host.Value ?? string.Empty,
                (ctx.Request.PathBase + ctx.Request.Path).Value ?? "/",
                ctx.Request.QueryString.Value ?? string.Empty,
                CopyHeaders(ctx.Request.Headers),
                true,
                null,
                ctx.Connection.RemoteIpAddress?.ToString());
            await _transport.SendAsync(FrameCodec.EncodeJson(FrameType.Open, id, meta, BridgeJsonContext.Default.OpenMessage), localToken);

            var upstream = PumpUpstreamAsync();

            while (true)
            {
                BridgeFrame frame;
                try { frame = await stream.Queue.Reader.ReadAsync(localToken); }
                catch (ChannelClosedException) { break; }
                catch (OperationCanceledException) { break; }

                if (frame.Type == FrameType.Data)
                {
                    if (ws.State != WebSocketState.Open) break;
                    var type = (frame.Flags & FrameFlags.Text) != 0 ? WebSocketMessageType.Text : WebSocketMessageType.Binary;
                    var endOfMessage = (frame.Flags & FrameFlags.More) == 0;
                    try
                    {
                        await ws.SendAsync(new ArraySegment<byte>(frame.Payload), type, endOfMessage, localToken);
                    }
                    catch { break; }
                }
                else if (frame.Type == FrameType.End || frame.Type == FrameType.Reset)
                {
                    finished = true;
                    break;
                }
            }

            await CloseWsAsync(ws);
            try { await upstream; } catch { }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log.LogWarning($"WebSocket 流 #{id} 异常 {ex.Message}");
        }
        finally
        {
            RemoveStream(id);
            if (!finished && !endSent) await ResetUpstreamAsync(id);
        }

        async Task PumpUpstreamAsync()
        {
            var buffer = ArrayPool<byte>.Shared.Rent(hub.BodyBufferSize);
            try
            {
                while (!localToken.IsCancellationRequested)
                {
                    var r = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), localToken);
                    if (r.MessageType == WebSocketMessageType.Close)
                    {
                        await _transport.SendAsync(FrameCodec.EncodeEmpty(FrameType.End, id), localToken);
                        endSent = true;
                        return;
                    }
                    if (r.Count == 0) continue;
                    FrameFlags flags = 0;
                    if (r.MessageType == WebSocketMessageType.Text) flags |= FrameFlags.Text;
                    if (!r.EndOfMessage) flags |= FrameFlags.More;
                    await _transport.SendAsync(FrameCodec.Encode(FrameType.Data, id, buffer.AsSpan(0, r.Count), flags), localToken);
                }
            }
            catch (Exception) { }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
                try { localCts.Cancel(); } catch { }
            }
        }
    }

    static HeaderMap CopyHeaders(IHeaderDictionary headers)
    {
        var result = new HeaderMap();
        foreach (var kv in headers)
        {
            result[kv.Key] = [.. kv.Value.Where(v => v is not null).Select(v => v!)];
        }
        return result;
    }
}

sealed record class ClientInfo(string ClientId, int ActiveStreams, string RemoteIp, DateTimeOffset Since);

// ?format=json 的响应体（前端按 camelCase 读取，缩进输出保持原样）
sealed record class StatusClient(string ClientId, string? Domain, string Url, int Streams, string RemoteIp, DateTimeOffset Since, long UptimeSeconds);

sealed record class StatusPayload(bool Ok, DateTimeOffset Time, int Count, int Streams, StatusClient[] Clients);

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(StatusPayload))]
partial class BridgeStatusJsonContext : JsonSerializerContext
{
}

sealed class BridgeHub
{
    public readonly int StreamQueueCapacity = 64;
    public readonly int BodyBufferSize = 65536;
    public readonly string Url;
    public readonly string Token;
    public readonly string Suffix;
    private readonly string ServerPrefix = "s";
    public readonly string ServerHost;
    readonly ConcurrentDictionary<string, ClientConnection> _clients = new(StringComparer.OrdinalIgnoreCase);
    public BridgeHub(IConfiguration configuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Url = configuration[nameof(Url)]!);
        ArgumentException.ThrowIfNullOrWhiteSpace(Token = configuration[nameof(Token)]!);
        var wildcardAt = Url.IndexOf("*.", StringComparison.Ordinal);
        if (wildcardAt < 0)
        {
            throw new Exception("Url 必须是泛域名，例如 http://*.abc.com/");
        }
        var domainStart = wildcardAt + 2;
        var domainEnd = Url.IndexOfAny([':', '/'], domainStart);
        var suffix = (domainEnd < 0 ? Url[domainStart..] : Url[domainStart..domainEnd]).ToLowerInvariant();
        Suffix = $".{suffix}";
        ServerHost = $"{ServerPrefix}.{suffix}";
    }
    public bool IsValidClientId(string? clientId) => !string.IsNullOrEmpty(clientId) && !string.Equals(clientId, ServerPrefix, StringComparison.OrdinalIgnoreCase) && clientId.All(ch => ch is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_');
    public string? ProxyHost(string clientId) => IsValidClientId(clientId) ? $"{clientId}{Suffix}" : null;
    public string? GetClientId(string host)
    {
        if (string.IsNullOrEmpty(host)) return null;
        if (!host.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase)) return null;
        if (string.Equals(host, ServerHost, StringComparison.OrdinalIgnoreCase)) return null;
        var id = host[..^Suffix.Length].ToLowerInvariant();
        return IsValidClientId(id) ? id : null;
    }

    // id 全局唯一：已在线则拒绝本次注册
    public bool TryRegister(ClientConnection conn) => _clients.TryAdd(conn.ClientId, conn);

    public void Unregister(ClientConnection conn)
        => _clients.TryRemove(new KeyValuePair<string, ClientConnection>(conn.ClientId, conn));

    public ClientInfo? Online(string clientId)
        => _clients.TryGetValue(clientId, out var conn)
           ? new ClientInfo(conn.ClientId, conn.ActiveStreams, conn.RemoteIp, conn.ConnectedAt)
           : null;

    public ClientConnection? Pick(string clientId)
        => _clients.TryGetValue(clientId, out var conn) ? conn : null;

    public IReadOnlyList<ClientInfo> Snapshot()
        => [.. _clients.Values
                   .Select(c => new ClientInfo(c.ClientId, c.ActiveStreams, c.RemoteIp, c.ConnectedAt))
                   .OrderBy(x => x.ClientId, StringComparer.OrdinalIgnoreCase)];

    // 由 Url 推导客户端访问的完整 URL（保留 scheme / 端口）
    public string ProxyUrl(string clientId)
    {
        var host = ProxyHost(clientId);
        if (host is null) return string.Empty;
        var probe = Url.Replace("*.", "wildcard.", StringComparison.Ordinal);
        if (!Uri.TryCreate(probe, UriKind.Absolute, out var uri)) return "http://" + host + "/";
        var port = uri.IsDefaultPort ? string.Empty : ":" + uri.Port.ToString();
        return $"{uri.Scheme}://{host}{port}/";
    }
}

static class BridgeMiddleware
{
    static readonly HashSet<string> HopByHopHeaders = new(StringComparer.OrdinalIgnoreCase) {
        "Connection", "Keep-Alive", "Transfer-Encoding", "Upgrade",
        "Proxy-Connection", "Proxy-Authenticate", "Proxy-Authorization", "TE", "Trailer", "Host"
    };

    public static bool IsHopByHop(string name) => HopByHopHeaders.Contains(name);

    public static async Task InvokeAsync(HttpContext ctx)
    {
        var hub = ctx.RequestServices.GetRequiredService<BridgeHub>();
        var log = ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Bridge");

        var host = (ctx.Request.Host.Host ?? string.Empty).ToLowerInvariant();
        var controlHost = hub.ServerHost;

        if (string.Equals(host, controlHost, StringComparison.OrdinalIgnoreCase))
        {
            var providedToken = ctx.Request.Query["token"].ToString();
            if (ctx.WebSockets.IsWebSocketRequest)
            {
                if (!string.Equals(providedToken, hub.Token, StringComparison.Ordinal))
                {
                    ctx.Response.StatusCode = 401;
                    ctx.Response.ContentType = "text/plain; charset=utf-8";
                    await ctx.Response.WriteAsync("invalid token");
                    return;
                }
                await HandleControlAsync(ctx, hub, log);
                return;
            }
            if (!string.Equals(providedToken, hub.Token, StringComparison.Ordinal))
            {
                await WriteTokenPageAsync(ctx, providedToken.Length > 0);
                return;
            }
            var format = ctx.Request.Query["format"].ToString();
            var isJson = string.Equals(format, "json", StringComparison.OrdinalIgnoreCase)
                         || (ctx.Request.Path.Value ?? string.Empty).EndsWith(".json", StringComparison.OrdinalIgnoreCase);
            if (isJson)
            {
                await WriteStatusAsync(ctx, hub);
            }
            else
            {
                await WriteDashboardAsync(ctx, hub);
            }
            return;
        }

        var clientId = hub.GetClientId(host);
        if (clientId is null)
        {
            ctx.Response.StatusCode = 421;
            ctx.Response.ContentType = "text/plain; charset=utf-8";
            await ctx.Response.WriteAsync($"host not bridged: {host}{Environment.NewLine}expected: {controlHost} (channel) or {hub.ProxyHost("<id>")} (proxy, e.g. {hub.ProxyHost("abc")})");
            return;
        }

        var conn = hub.Pick(clientId);
        if (conn is null)
        {
            ctx.Response.StatusCode = 502;
            ctx.Response.ContentType = "text/plain; charset=utf-8";
            await ctx.Response.WriteAsync($"client '{clientId}' offline ({host})");
            return;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            ctx.RequestAborted,
            ctx.RequestServices.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping);
        if (ctx.WebSockets.IsWebSocketRequest)
        {
            await conn.ForwardWebSocketAsync(ctx, hub, cts.Token);
        }
        else
        {
            await conn.ForwardHttpAsync(ctx, hub, cts.Token);
        }
    }

    static async Task WriteTokenPageAsync(HttpContext ctx, bool invalid)
    {
        var action = ctx.Request.Path.Value;
        if (string.IsNullOrEmpty(action))
        {
            action = "/";
        }
        var hint = invalid
            ? "<p class=\"err\">token 不正确，请重新输入</p>"
            : "<p>请输入访问 token</p>";
        var html = $$"""
        <!doctype html>
        <html lang="zh-CN">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width,initial-scale=1">
        <title>Bridge 访问验证</title>
        <style>
        body{margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;background:#0f1115;color:#e6e8ec;font:14px/1.6 "Segoe UI",-apple-system,sans-serif}
        .box{width:320px;padding:28px;border:1px solid #262b36;border-radius:12px;background:#161a21}
        h1{margin:0 0 6px;font-size:16px;font-weight:600}
        p{margin:0 0 16px;color:#8b93a1;font-size:13px}
        p.err{margin:0 0 16px;color:#ff6b6b;font-size:13px}
        input{width:100%;box-sizing:border-box;padding:10px 12px;border:1px solid #2b3240;border-radius:8px;background:#0f1115;color:#e6e8ec;font-size:14px;outline:none}
        input:focus{border-color:#3d7eff}
        button{width:100%;margin-top:12px;padding:10px;border:0;border-radius:8px;background:#3d7eff;color:#fff;font-size:14px;cursor:pointer}
        </style>
        </head>
        <body>
        <form class="box" method="get" action="{{action}}">
        <h1>Bridge 访问验证</h1>
        {{hint}}
        <input name="token" type="password" autofocus autocomplete="off" placeholder="token">
        <button type="submit">验证</button>
        </form>
        </body>
        </html>
        """;
        ctx.Response.ContentType = "text/html; charset=utf-8";
        await ctx.Response.WriteAsync(html);
    }

    static async Task WriteStatusAsync(HttpContext ctx, BridgeHub hub)
    {
        var now = DateTimeOffset.Now;
        var clients = hub.Snapshot();
        var payload = new StatusPayload(
            true,
            now,
            clients.Count,
            clients.Sum(c => c.ActiveStreams),
            [.. clients.Select(c => new StatusClient(
                c.ClientId,
                hub.ProxyHost(c.ClientId),
                hub.ProxyUrl(c.ClientId),
                c.ActiveStreams,
                c.RemoteIp,
                c.Since,
                (long)(now - c.Since).TotalSeconds))]);
        ctx.Response.Headers.CacheControl = "no-store";
        ctx.Response.ContentType = "application/json; charset=utf-8";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(payload, BridgeStatusJsonContext.Default.StatusPayload));
    }

    static string FormatUptime(TimeSpan span)
    {
        if (span < TimeSpan.Zero)
        {
            span = TimeSpan.Zero;
        }
        if (span.TotalDays >= 1) return $"{(int)span.TotalDays}天{span.Hours}时{span.Minutes}分";
        if (span.TotalHours >= 1) return $"{span.Hours}时{span.Minutes}分{span.Seconds}秒";
        if (span.TotalMinutes >= 1) return $"{span.Minutes}分{span.Seconds}秒";
        return $"{span.Seconds}秒";
    }

    static string BuildRowsHtml(BridgeHub hub, IReadOnlyList<ClientInfo> clients, DateTimeOffset now)
    {
        if (clients.Count == 0)
        {
            return "<tr class=\"empty\"><td colspan=\"5\">暂无在线客户端</td></tr>";
        }
        var sb = new StringBuilder();
        foreach (var c in clients)
        {
            var domain = hub.ProxyHost(c.ClientId) ?? string.Empty;
            sb.Append("<tr>")
              .Append("<td class=\"id\">").Append(WebUtility.HtmlEncode(c.ClientId)).Append("</td>")
              .Append("<td><a href=\"").Append(WebUtility.HtmlEncode(hub.ProxyUrl(c.ClientId))).Append("\" target=\"_blank\" rel=\"noreferrer\">")
              .Append(WebUtility.HtmlEncode(domain)).Append("</a></td>")
              .Append("<td class=\"num\">").Append(c.ActiveStreams.ToString()).Append("</td>")
              .Append("<td class=\"mono\">").Append(WebUtility.HtmlEncode(c.RemoteIp.Length == 0 ? "-" : c.RemoteIp)).Append("</td>")
              .Append("<td class=\"num\">").Append(WebUtility.HtmlEncode(FormatUptime(now - c.Since))).Append("</td>")
              .Append("</tr>");
        }
        return sb.ToString();
    }

    static async Task WriteDashboardAsync(HttpContext ctx, BridgeHub hub)
    {
        var now = DateTimeOffset.Now;
        var clients = hub.Snapshot();
        var rows = BuildRowsHtml(hub, clients, now);
        var online = clients.Count;
        var streams = clients.Sum(c => c.ActiveStreams);
        var html = $$"""
        <!doctype html>
        <html lang="zh-CN">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width,initial-scale=1">
        <title>Bridge 在线列表</title>
        <style>
        *{box-sizing:border-box}
        body{margin:0;background:#0f1115;color:#e6e8ec;font:14px/1.6 "Segoe UI",-apple-system,sans-serif}
        .wrap{max-width:1040px;margin:0 auto;padding:28px 20px 48px}
        header{display:flex;align-items:flex-start;justify-content:space-between;gap:16px;flex-wrap:wrap}
        h1{margin:0;font-size:18px;font-weight:600}
        .sub{margin:2px 0 0;color:#8b93a1;font-size:12px}
        .right{display:flex;align-items:center;gap:10px;color:#8b93a1;font-size:12px}
        button{padding:6px 12px;border:1px solid #2b3240;border-radius:8px;background:#161a21;color:#e6e8ec;font-size:12px;cursor:pointer}
        button:hover{border-color:#3d7eff}
        .stats{display:grid;grid-template-columns:repeat(2,1fr);gap:12px;margin:20px 0}
        .card{padding:14px 16px;border:1px solid #262b36;border-radius:12px;background:#161a21;display:flex;flex-direction:column;gap:4px}
        .k{color:#8b93a1;font-size:12px}
        .v{font-size:22px;font-weight:600}
        table{width:100%;border-collapse:collapse;border:1px solid #262b36;border-radius:12px;overflow:hidden;background:#161a21}
        th,td{padding:11px 14px;text-align:left;border-bottom:1px solid #21262f;font-size:13px;white-space:nowrap}
        th{background:#12161c;color:#8b93a1;font-weight:500;font-size:12px}
        tbody tr:last-child td{border-bottom:0}
        tbody tr:hover{background:#191e26}
        th.num,td.num{text-align:right}
        td.id{font-family:ui-monospace,Consolas,monospace;color:#7ee0a8}
        td.mono{font-family:ui-monospace,Consolas,monospace;color:#a9b1bf}
        a{color:#3d7eff;text-decoration:none}
        a:hover{text-decoration:underline}
        tr.empty td{text-align:center;color:#6b7280;padding:28px}
        .foot{margin:14px 2px 0;color:#6b7280;font-size:12px}
        @media (max-width:640px)
        {
        .stats{grid-template-columns:1fr}
        table{display:block;overflow-x:auto}
        }
        </style>
        </head>
        <body>
        <div class="wrap">
        <header>
        <div>
        <h1>Bridge 在线列表</h1>
        <p class="sub">{{WebUtility.HtmlEncode(hub.ServerHost)}}</p>
        </div>
        <div class="right">
        <span id="stamp">{{now:HH:mm:ss}}</span>
        <button id="toggle" type="button">暂停刷新</button>
        </div>
        </header>
        <section class="stats">
        <div class="card"><span class="k">在线客户端</span><span class="v" id="s-clients">{{online}}</span></div>
        <div class="card"><span class="k">活跃流</span><span class="v" id="s-streams">{{streams}}</span></div>
        </section>
        <table>
        <thead>
        <tr><th>客户端 ID</th><th>代理域名</th><th class="num">活跃流</th><th>来源 IP</th><th class="num">在线时长</th></tr>
        </thead>
        <tbody id="rows">{{rows}}</tbody>
        </table>
        <p class="foot">每 2 秒自动刷新 · JSON 接口：<a id="jsonlink" href="?format=json">?format=json</a></p>
        </div>
        <script>
        (function () {
          var token = new URLSearchParams(location.search).get('token') || '';
          var rowsEl = document.getElementById('rows');
          var stamp = document.getElementById('stamp');
          var toggle = document.getElementById('toggle');
          var paused = false;
          var loading = false;
          var endpoint = location.pathname + '?format=json&token=' + encodeURIComponent(token);
          document.getElementById('jsonlink').href = endpoint;

          function uptime(sec) {
            sec = Math.max(0, Math.floor(sec || 0));
            var d = Math.floor(sec / 86400);
            var h = Math.floor(sec % 86400 / 3600);
            var m = Math.floor(sec % 3600 / 60);
            var s = sec % 60;
            if (d > 0) return d + '天' + h + '时' + m + '分';
            if (h > 0) return h + '时' + m + '分' + s + '秒';
            if (m > 0) return m + '分' + s + '秒';
            return s + '秒';
          }

          function cell(text, cls) {
            var td = document.createElement('td');
            if (cls) td.className = cls;
            td.textContent = text;
            return td;
          }

          function emptyRow() {
            var tr = document.createElement('tr');
            tr.className = 'empty';
            var td = document.createElement('td');
            td.colSpan = 5;
            td.textContent = '暂无在线客户端';
            tr.appendChild(td);
            rowsEl.appendChild(tr);
          }

          function render(data) {
            document.getElementById('s-clients').textContent = data.count;
            var total = 0;
            data.clients.forEach(function (c) { total += c.streams; });
            document.getElementById('s-streams').textContent = total;
            rowsEl.textContent = '';
            if (!data.clients.length) { emptyRow(); return; }
            data.clients.forEach(function (c) {
              var tr = document.createElement('tr');
              tr.appendChild(cell(c.clientId, 'id'));
              var td = document.createElement('td');
              var a = document.createElement('a');
              a.href = c.url;
              a.target = '_blank';
              a.rel = 'noreferrer';
              a.textContent = c.domain || '-';
              td.appendChild(a);
              tr.appendChild(td);
              tr.appendChild(cell(c.streams, 'num'));
              tr.appendChild(cell(c.remoteIp || '-', 'mono'));
              tr.appendChild(cell(uptime(c.uptimeSeconds), 'num'));
              rowsEl.appendChild(tr);
            });
          }

          function refresh() {
            if (loading) return;
            loading = true;
            fetch(endpoint, { cache: 'no-store' })
              .then(function (r) {
                if (r.status === 401) { location.replace(location.pathname); return null; }
                return r.json();
              })
              .then(function (d) {
                if (!d) return;
                render(d);
                stamp.textContent = new Date().toLocaleTimeString('zh-CN', { hour12: false });
              })
              .catch(function () { stamp.textContent = '刷新失败'; })
              .then(function () { loading = false; });
          }

          toggle.addEventListener('click', function () {
            paused = !paused;
            toggle.textContent = paused ? '继续刷新' : '暂停刷新';
            if (!paused) refresh();
          });

          setInterval(function () {
            if (!paused && !document.hidden) refresh();
          }, 2000);
        })();
        </script>
        </body>
        </html>
        """;
        ctx.Response.Headers.CacheControl = "no-store";
        ctx.Response.ContentType = "text/html; charset=utf-8";
        await ctx.Response.WriteAsync(html);
    }

    static async Task HandleControlAsync(HttpContext ctx, BridgeHub hub, ILogger log)
    {
        if (!ctx.WebSockets.IsWebSocketRequest)
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.WriteAsync("websocket upgrade required");
            return;
        }

        var clientId = (ctx.Request.Query["id"].ToString() ?? string.Empty).Trim().ToLowerInvariant();
        if (clientId.Length == 0)
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.WriteAsync("missing id");
            return;
        }
        if (!hub.IsValidClientId(clientId))
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.WriteAsync($"invalid id '{clientId}': only lowercase letters, digits and '_' are allowed");
            return;
        }

        if (hub.Online(clientId) is { } online)
        {
            log.LogWarning($"重复注册被拒 id={clientId} 现有连接 ip={online.RemoteIp} since={online.Since:HH:mm:ss}");
            ctx.Response.StatusCode = 409;
            ctx.Response.ContentType = "text/plain; charset=utf-8";
            await ctx.Response.WriteAsync($"client id '{clientId}' already online ({online.RemoteIp}, since {online.Since:HH:mm:ss}), duplicate registration rejected");
            return;
        }

        using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
        var lifetime = ctx.RequestServices.GetRequiredService<IHostApplicationLifetime>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted, lifetime.ApplicationStopping);
        ClientConnection? conn = null;
        var transport = new WsTransport(ws, hub.BodyBufferSize, cts.Token);
        try
        {
            var remoteIp = ctx.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
            conn = new ClientConnection(clientId, transport, log, hub.StreamQueueCapacity)
            {
                RemoteIp = remoteIp
            };
            if (!hub.TryRegister(conn))
            {
                conn = null;
                log.LogWarning("client {ClientId} 重复上线，已拒绝", clientId);
                try
                {
                    await ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "duplicate client id", CancellationToken.None);
                }
                catch { }
                return;
            }
            log.LogInformation("client {ClientId} 已上线（{Ip}）", conn.ClientId, remoteIp);
            await conn.RunAsync(cts.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            log.LogWarning($"控制通道异常 {ex.Message}");
        }
        finally
        {
            if (conn is not null)
            {
                hub.Unregister(conn);
                log.LogInformation("client {ClientId} 已下线", conn.ClientId);
            }
        }
    }

}
