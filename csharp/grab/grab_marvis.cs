#:property TargetFramework=net8.0
#:property PublishAot=true

// app2 主链路：bootstrap 路由 -> startup 探测 -> auto/ensure -> 文件装配（本地/快照）-> games 增量 -> passwords
// dotnet run grab_marvis.cs            复用本地
// dotnet run grab_marvis.cs -- -f      忽略本地文件，重新拉 data-snapshot
// dotnet run grab_marvis.cs -- -e 0    ensure 走裸 JSON
// 依据：静态还原 + 抓包校验；设备标识运行时随机生成，不含真机值。

using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

Options opt = Args.Parse(args);
using var client = new MiniClient(opt);
return await client.RunAsync();

// ============================== 1. 配置 ==============================

/// 端点与公共头
static class Api
{
    public const string Startup = "/api/client/startup";
    public const string AutoEnsure = "/api/client/activation/auto/ensure";   // clientKind 仅 mini / launcher
    public const string Games = "/api/client/games";                         // query 不参与 canonical，签名取 path
    public const string GamePasswords = "/api/client/games/passwords";
    public const string DataSnapshot = "/api/client/download/data-snapshot/mini";

    public const string EncHeader = "X-Enc", EncHeaderValue = "2";           // 值固定 "2"，缺失 -> 426
    public const string SigHeader = "X-Sig", SigHeaderValue = "3";           // 仅 /api/client/startup
    public const string DistributionHeader = "X-Mini-Distribution", DistributionValue = "Normal";
    public const string TokenHeader = "X-Entitlement-Token";
    public const string HmacSigHeader = "X-Hmac-Signature", HmacTsHeader = "X-Hmac-Timestamp";
    public const string EncKeyHeader = "X-Enc-Key";
}

/// 外部数据源
static class Sources
{
    public static readonly string[] Bootstrap =
    [
        "https://lkfv.oss-cn-shanghai.aliyuncs.com/bootstrap-routes-v1.json",
        "https://lkfv.oss-accelerate.aliyuncs.com/bootstrap-routes-v1.json",
    ];

    // 内置种子：bootstrap 双源全失败时主链路直接中断，暂未参与路由，保留备用
    // IP 兜底（42.240.170.167:28531 属 imageBed，不入列）
    public static readonly string[] DomainSeeds =
        ["http://corvanda.com", "http://jentrol.com", "http://javelto.com", "http://varlemo.com"];

    public static readonly string[] IpSeeds =
        ["http://156.225.28.133", "http://152.175.230.98", "http://152.175.230.40", "http://38.76.194.86"];
}

/// 协议密钥
static class Secrets
{
    public const string HmacKeyB64 = "yGuFtZLfTu0E6QSZ51uWSTAv1CoDPzv6qd4umJ7IqJ8=";

    // bootstrap 路由表 AES-256-CBC 密钥（IV = 密文头 16 字节）
    public static readonly byte[] BootstrapKey =
        Convert.FromHexString("98f00bbd4aa14359340a43fed8b7d8e602b6d190d6456703041b8fd74db0a867");

    // games.dat: head16=IV | AES-256-CBC(PKCS7) | tail32=HMAC-SHA256
    public const string GamesDatKeyB64 = "Fn4TMHwAujoc8zhaMoMtA8zrqbzpcoXlO2jsfNTwFxg=";
    public const string GamesDatHmacKeyHex = "b759aaefe0bd2ed3aa654cf8ed9632755ab4f92c9ff1a4fc5add1277e10b04b1";
}

/// 客户端身份与落盘位置
static class ClientInfo
{
    public const string Version = "1.1.4", SnapshotUserAgent = "App2.Mini/1.1.4";

    // 本地目录缓存（相对工作目录），-f 时忽略并重建
    public const string CatalogFile = "grab_games.json";
}

// ============================== 2. 编排 ==============================

/// 启动参数：-e 1（默认）加密信封 / -e 0 裸 JSON；-f 忽略本地目录、强制重取 data-snapshot
sealed class Options
{
    public static readonly JsonNodeOptions NodeOpt = new() { PropertyNameCaseInsensitive = true };
    public bool UseEnvelope { get; init; } = true;
    public bool Force { get; init; }
}

static class Args
{
    public static Options Parse(string[] argv)
    {
        bool envelope = true, force = false;
        for (int i = 0; i < argv.Length; i++)
        {
            string a = argv[i];
            if (a is "-f" or "--force") { force = true; continue; }

            string? val = a switch
            {
                "-e" or "--envelope" => i + 1 < argv.Length ? argv[i + 1] : null,   // 只写 -e 视作 1
                _ when a.StartsWith("-e=", StringComparison.Ordinal) => a[3..],
                _ when a.StartsWith("--envelope=", StringComparison.Ordinal) => a["--envelope=".Length..],
                _ => null,
            };
            if (val is not null)
                envelope = !int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) || v != 0;
        }
        return new Options { UseEnvelope = envelope, Force = force };
    }
}

/// 主链路：路由 -> startup 探测 -> ensure -> 目录同步
sealed class MiniClient : IDisposable
{
    // 两个请求方共用同一连接池（快照只换 UA 与超时）
    internal static readonly HttpMessageHandler SharedHandler = new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
        UseProxy = false,
    };

    readonly HttpClient http = new(SharedHandler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(10) };
    readonly Identity identity = Identity.Create();
    readonly Options opt;

    long tsBatch;   // X-Hmac-Timestamp 批次缓存（60 秒窗口内复用）

    public MiniClient(Options opt) => this.opt = opt;

    public void Dispose() => http.Dispose();

    public async Task<int> RunAsync()
    {
        BootResult boot = await BootAsync();
        if (boot.Winner is null)
        {
            Console.Error.WriteLine("[bootstartup] 无节点返回 200 且可解密，主链路中断");
            return 1;
        }
        if (boot.Info.Req is null && opt.UseEnvelope)
        {
            Console.Error.WriteLine("[startup] 未取到 data.requestEncryption，信封无法构造（可用 -e 0 走裸 JSON）");
            return 1;
        }

        string kind = Net.IsIpOrigin(boot.Winner.Origin) ? "ip" : "domain";
        Console.WriteLine($"[startup] 胜出 {boot.Winner.Origin} ({kind}, {boot.ProbeMs}ms)  keyId={boot.Info.Req?.KeyId ?? "无"}");

        string bizBase = boot.Winner.Origin;
        string? token = await EnsureAsync(bizBase, boot.Info.Req);
        Console.WriteLine($"[ensure] token={(string.IsNullOrEmpty(token) ? "未取得" : token)}");

        long since = boot.Info.LastSyncTicks ?? 0;
        Console.WriteLine($"[startup] since={since}{(since > 0 ? "（lastSyncTicks）" : "（startup 未下发）")}");

        await new SyncPipeline(this, opt).RunAsync(bizBase, token, since);
        return 0;
    }

    // -------- 阶段 1：bootstrap + startup --------

    async Task<BootResult> BootAsync()
    {
        var sw = Stopwatch.StartNew();
        List<string> routes = await FetchRoutesAsync();

        ProbeResult? winner = null;
        foreach (var route in routes)
        {
            ProbeResult r = await ProbeStartupAsync(route);
            if (r.Ok) { winner = r; break; }
            Console.WriteLine($"[startup] 探测失败 {r.Origin} status={r.Status} {r.Fail}");
        }

        sw.Stop();
        return new BootResult(winner, winner is null ? new StartupInfo() : StartupParser.Parse(winner.PlainJson!), sw.ElapsedMilliseconds);
    }

    // bootstrap 路由表：{"d":"base64(IV|cipher)"} -> {"v":1,"r":["host",…]}
    async Task<List<string>> FetchRoutesAsync()
    {
        foreach (var src in Sources.Bootstrap)
        {
            try
            {
                using var resp = await http.GetAsync(src);
                byte[] raw = await resp.Content.ReadAsByteArrayAsync();
                if (!resp.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[bootstrap] {src} HTTP {(int)resp.StatusCode}，换源");
                    continue;
                }

                if (JsonNode.Parse(raw, Options.NodeOpt) is not JsonObject bdoc
                    || bdoc["d"] is not JsonValue dNode || !dNode.TryGetValue(out string? dStr) || string.IsNullOrEmpty(dStr))
                {
                    Console.WriteLine("[bootstrap] 响应无 d 字段，换源");
                    continue;
                }
                byte[] blob = Convert.FromBase64String(dStr);
                if (blob.Length < 32 || (blob.Length - 16) % 16 != 0)
                {
                    Console.WriteLine("[bootstrap] 密文长度非法，换源");
                    continue;
                }

                byte[] plain = Codec.AesCbcDecrypt(Secrets.BootstrapKey, blob[..16], blob[16..]);
                var pnode = JsonNode.Parse(plain, Options.NodeOpt);
                int v = pnode is JsonObject pv && pv["v"] is JsonValue vVal && vVal.TryGetValue(out int vInt) ? vInt : 0;

                var list = new List<string>();
                if (pnode is JsonObject pr && pr["r"] is JsonArray rArr)
                    foreach (var e in rArr)
                        if (e is JsonValue eVal && eVal.TryGetValue(out string? eStr) && eStr is { Length: > 0 })
                            list.Add(Net.Normalize(eStr));

                if (list.Count > 0)
                {
                    Console.WriteLine($"[bootstrap] v={v} 路由 {list.Count} 个");
                    return list;
                }
                Console.WriteLine("[bootstrap] 明文无 r 节点列表，换源");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[bootstrap] {src} 失败 {ex.GetType().Name}: {ex.Message}");
            }
        }
        Console.WriteLine("[bootstrap] 全部源失败，主链路中断");
        return [];
    }

    // startup 探测：X-Enc 必带，X-Sig 仅此接口；响应需能按 X-Enc-Key 解出
    async Task<ProbeResult> ProbeStartupAsync(string origin)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, origin + Api.Startup);
            AddHeaders(req, (Api.EncHeader, Api.EncHeaderValue), (Api.SigHeader, Api.SigHeaderValue));

            var (status, raw, encKey) = await SendAsync(req);
            var r = new ProbeResult { Origin = origin, Status = status };
            if (status != 200) { r.Fail = "status"; return r; }

            if (Proto.Decrypt(encKey, raw, out _) is { } plain)
            {
                r.Ok = true;
                r.PlainJson = Encoding.UTF8.GetString(plain);
            }
            else r.Fail = "解密失败";
            return r;
        }
        catch (Exception ex)
        {
            return new ProbeResult { Origin = origin, Fail = ex.GetType().Name };
        }
    }

    // -------- 阶段 2：auto/ensure --------

    async Task<string?> EnsureAsync(string baseUrl, RequestEncryption? reqEnc)
    {
        // 其余字段取 DTO 默认值（ProtocolVersion=2 / mini / Normal），序列化仍全量输出
        var payload = new AutoActivationEnsureRequest
        {
            InstallationIdHash = identity.InstallationIdHash,
            HardwareIdHash = identity.HardwareIdHash,
            ClientVersion = ClientInfo.Version,
        };
        string json = JsonSerializer.Serialize(payload, AppJson.Default.AutoActivationEnsureRequest);
        long ts = SignedTimestamp();

        byte[] reqBody;
        string reqContentType;
        byte[] dek = [];
        if (opt.UseEnvelope)
        {
            if (reqEnc is null)
            {
                Console.Error.WriteLine("[ensure] 信封模式缺少 data.requestEncryption，无法构造信封");
                return null;
            }
            try
            {
                reqBody = Encoding.UTF8.GetBytes(Envelope.Build(json, "application/json; charset=utf-8", reqEnc, Api.AutoEnsure, ts, out dek));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ensure] 信封构造失败: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
            reqContentType = Envelope.MediaType + "; charset=utf-8";
        }
        else
        {
            reqBody = Encoding.UTF8.GetBytes(json);
            reqContentType = "application/json; charset=utf-8";
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, baseUrl + Api.AutoEnsure);
            var content = new ByteArrayContent(reqBody);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(reqContentType);
            req.Content = content;
            AddHeaders(req, (Api.EncHeader, Api.EncHeaderValue));
            if (opt.UseEnvelope) req.Headers.TryAddWithoutValidation(Envelope.HeaderName, Envelope.HeaderValue);
            AddHeaders(req,
                (Api.DistributionHeader, Api.DistributionValue),
                (Api.HmacSigHeader, HmacSign("POST", Api.AutoEnsure, ts, reqBody)),
                (Api.HmacTsHeader, ts.ToString(CultureInfo.InvariantCulture)));

            var (status, raw, encKey) = await SendAsync(req);
            Console.WriteLine($"[ensure] HTTP {status}  {raw.Length}B  X-Enc-Key={encKey}");
            if (raw.Length == 0)
            {
                Console.Error.WriteLine("[ensure] 空响应体");
                return null;
            }

            return ExtractToken(ResolveEnsureBody(Codec.RestoreBody(encKey, raw), dek));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ensure] 失败 {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    // -e 1 先解响应信封，-e 0 直读明文；结果若仍是 descriptor 再 base64 解一层
    string ResolveEnsureBody(string text, byte[] dek)
    {
        if (opt.UseEnvelope)
        {
            if (RespEnv.TryOpen(text, dek, out _) is { } opened) text = Encoding.UTF8.GetString(opened);
        }
        else if (text.Contains("\"ciphertext\"", StringComparison.OrdinalIgnoreCase))
            Console.Error.WriteLine("[warn] 响应为加密信封，-e 0 无 DEK 无法解密，请改用 -e 1");
        return Codec.UnwrapDescriptor(text);
    }

    static string? ExtractToken(string business)
    {
        string? token = null;
        try
        {
            if (JsonNode.Parse(business, Options.NodeOpt) is not JsonObject bobj) return null;
            foreach (var (name, val) in bobj)
            {
                if (string.Equals(name, "entitlementToken", StringComparison.OrdinalIgnoreCase)
                    && val is JsonValue tVal && tVal.TryGetValue(out string? tStr))
                    token = tStr;
                else if (string.Equals(name, "action", StringComparison.OrdinalIgnoreCase))
                    Console.WriteLine($"[ensure] action={val}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ensure] 响应非 JSON: {ex.GetType().Name}");
        }
        return string.IsNullOrEmpty(token) ? null : token;
    }

    // -------- 阶段 3 取数：业务 GET（无请求体不套信封，canonical 走无体格式） --------

    public async Task<string> BizGetAsync(string baseUrl, string path, string token, string? query = null)
    {
        long ts = SignedTimestamp();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, baseUrl + path + (query is null ? "" : "?" + query));
            AddHeaders(req,
                (Api.EncHeader, Api.EncHeaderValue),
                (Api.DistributionHeader, Api.DistributionValue),
                (Api.TokenHeader, token),
                (Api.HmacSigHeader, HmacSign("GET", path, ts, null)),
                (Api.HmacTsHeader, ts.ToString(CultureInfo.InvariantCulture)));

            var (status, raw, encKey) = await SendAsync(req);
            Console.WriteLine($"[GET {path}] HTTP {status}  {raw.Length}B  X-Enc-Key={encKey}");
            return raw.Length == 0 ? "" : Codec.UnwrapDescriptor(Codec.RestoreBody(encKey, raw));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  [GET {path}] 失败 {ex.GetType().Name}: {ex.Message}");
            return "";
        }
    }

    // -------- 传输层 --------

    async Task<(int Status, byte[] Raw, string EncKey)> SendAsync(HttpRequestMessage req)
    {
        using var resp = await http.SendAsync(req);
        byte[] raw = await resp.Content.ReadAsByteArrayAsync();
        string encKey = resp.Headers.TryGetValues(Api.EncKeyHeader, out var v) ? (v.FirstOrDefault() ?? "") : "";
        return ((int)resp.StatusCode, raw, encKey);
    }

    static void AddHeaders(HttpRequestMessage req, params (string Name, string Value)[] headers)
    {
        foreach (var (name, value) in headers) req.Headers.TryAddWithoutValidation(name, value);
    }

    // X-Hmac-Timestamp：Unix 秒，取当前秒后缓存 60 秒
    long SignedTimestamp()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (tsBatch == 0 || now - tsBatch >= 60) tsBatch = now;
        return tsBatch;
    }

    // canonical：无体 / GET = "{METHOD}\n{path}\n{ts}\n"（query 不参与）
    //            有体 / POST = "{METHOD}\n{path}\n{ts}\n{sha256hex(body)}"（末尾无换行）
    static string HmacSign(string method, string path, long ts, byte[]? body)
    {
        string canonical = body is { Length: > 0 }
            ? $"{method}\n{path}\n{ts}\n{Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant()}"
            : $"{method}\n{path}\n{ts}\n";
        return Convert.ToBase64String(HMACSHA256.HashData(Convert.FromBase64String(Secrets.HmacKeyB64), Encoding.UTF8.GetBytes(canonical)));
    }
}

// ============================== 3. 目录同步 ==============================

/// 文件装配：默认复用本地；不可用或带 -f 时取 data-snapshot。
/// 随后 games 增量 -> passwords 全量，按 gameCode 合并、裁剪字段后落盘。
sealed class SyncPipeline(MiniClient client, Options opt)
{
    const string KeyField = "GameCode";
    static readonly string[] KeepFields = [KeyField, "Password", "ChineseName", "EnglishName", "Size", "GameVersion", "DownloadUrl1", "DownloadUrl2", "UpdateDate"];

    public async Task RunAsync(string bizBase, string? token, long since)
    {
        Dictionary<string, JsonNode> catalog = opt.Force ? [] : LoadLocalCatalog();

        if (catalog.Count > 0)
        {
            Console.WriteLine($"[1/3] 本地目录 {catalog.Count} 条（{ClientInfo.CatalogFile}，-f 可强制重建）");
        }
        else
        {
            Console.WriteLine(opt.Force ? "[1/3] -f 强制重建：拉取 data-snapshot" : "[1/3] 本地目录不可用：拉取 data-snapshot");
            (byte[]? gamesDat, long snapSince) = UnpackSnapshot(await FetchSnapshotAsync(bizBase));
            Console.WriteLine($"[snapshot] games.dat={(gamesDat is null ? "缺失" : gamesDat.Length + "B")}  since={snapSince}");
            catalog = BuildCatalog(gamesDat);
            if (snapSince > 0) since = snapSince;          // 快照自带游标优先于 startup 下发的
        }

        if (string.IsNullOrEmpty(token))
        {
            Console.Error.WriteLine("  无 entitlementToken，只完成目录装配");
            return;
        }

        if (since > 0) await ApplyDeltaAsync(bizBase, token, since, catalog);
        else Console.Error.WriteLine("  无 since 游标，跳过 games 增量");

        Save(catalog, await LoadPasswordsAsync(bizBase, token));
    }

    // 本地落盘目录（已裁剪字段）-> gameCode -> 原始 JSON；不存在/解析为空即视为不可用
    static Dictionary<string, JsonNode> LoadLocalCatalog()
    {
        var catalog = new Dictionary<string, JsonNode>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!File.Exists(ClientInfo.CatalogFile)) return catalog;
            foreach (var item in JsonNode.Parse(File.ReadAllBytes(ClientInfo.CatalogFile), Options.NodeOpt)!.AsArray())
                catalog[(string)item![KeyField]!] = item!;
            if (catalog.Count == 0) Console.Error.WriteLine($"  本地目录解析为空：{ClientInfo.CatalogFile}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  本地目录读取失败：{ex.Message}");
            catalog.Clear();
        }
        return catalog;
    }

    // 快照：不签名、不带 X-Enc，只靠 UA
    static async Task<byte[]?> FetchSnapshotAsync(string bizBase)
    {
        using var http = new HttpClient(MiniClient.SharedHandler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(60) };
        using var req = new HttpRequestMessage(HttpMethod.Get, bizBase + Api.DataSnapshot);
        req.Headers.TryAddWithoutValidation("User-Agent", ClientInfo.SnapshotUserAgent);
        using var resp = await http.SendAsync(req);
        byte[] raw = await resp.Content.ReadAsByteArrayAsync();
        Console.WriteLine($"[snapshot] HTTP {(int)resp.StatusCode}  {raw.Length}B");

        bool isZip = raw.Length > 2 && raw[0] == 0x50 && raw[1] == 0x4b;
        return resp.IsSuccessStatusCode && isZip ? raw : null;
    }

    // ZIP 内取 games.dat 与 since（sync_state_mini.json 的 LastSyncTicks）
    static (byte[]? GamesDat, long Since) UnpackSnapshot(byte[]? zip)
    {
        if (zip is null) return (null, 0);

        byte[]? gamesDat = null;
        long since = 0;
        using var archive = new ZipArchive(new MemoryStream(zip, writable: false), ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            if (entry.Name.Equals("games.dat", StringComparison.OrdinalIgnoreCase))
            {
                using var ms = new MemoryStream();
                using (var es = entry.Open()) es.CopyTo(ms);
                gamesDat = ms.ToArray();
            }
            else if (entry.Name.StartsWith("sync_state", StringComparison.OrdinalIgnoreCase))
            {
                using var rd = new StreamReader(entry.Open(), Encoding.UTF8);
                if (JsonNode.Parse(rd.ReadToEnd().Trim().TrimStart('\uFEFF').Trim(), Options.NodeOpt) is JsonObject sd && sd["LastSyncTicks"] is JsonValue lv && lv.TryGetValue(out long lt)) since = lt;
            }
        }
        return (gamesDat, since);
    }

    // games.dat 解密 -> 目录（gameCode -> 原始 JSON）
    static Dictionary<string, JsonNode> BuildCatalog(byte[]? gamesDat)
    {
        var catalog = new Dictionary<string, JsonNode>(StringComparer.OrdinalIgnoreCase);
        if (gamesDat is null)
        {
            Console.Error.WriteLine("  games.dat 不可用：快照未取到");
            return catalog;
        }
        if (!GamesDatCodec.TryDecrypt(gamesDat, out string json, out string note))
        {
            Console.Error.WriteLine($"  games.dat 不可用：{note}");
            return catalog;
        }
        foreach (var item in JsonNode.Parse(json, Options.NodeOpt)!.AsArray())
            catalog[(string)item![KeyField]!] = item!;
        Console.WriteLine($"  快照目录 {catalog.Count} 条（{note}）");
        return catalog;
    }

    async Task ApplyDeltaAsync(string bizBase, string token, long since, Dictionary<string, JsonNode> catalog)
    {
        string incJson = await client.BizGetAsync(bizBase, Api.Games, token, "since=" + since.ToString(CultureInfo.InvariantCulture));
        int added = 0, updated = 0;
        var ij = JsonNode.Parse(incJson, Options.NodeOpt)!.AsObject();
        foreach (var it in ij["games"]!.AsArray())
        {
            var code = (string)it![KeyField]!;
            if (catalog.ContainsKey(code)) updated++; else added++;
            catalog[code] = it!;
        }
        Console.WriteLine($"  增量 新增 {added} 覆盖 {updated}，目录 {catalog.Count} 条");
        if ((ij["serverTime"] ?? ij["lastSyncTicks"]) is { } st)
            Console.WriteLine($"  新游标 lastSyncTicks = {st}");
    }

    async Task<Dictionary<string, JsonNode>> LoadPasswordsAsync(string bizBase, string token)
    {
        string pwdJson = await client.BizGetAsync(bizBase, Api.GamePasswords, token);
        var map = new Dictionary<string, JsonNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var it in JsonNode.Parse(pwdJson, Options.NodeOpt)!.AsObject()["passwords"]!.AsArray())
            map[(string)it![KeyField]!] = it!;
        return map;
    }

    // 密码回填进目录，按 KeepFields 裁剪后落盘
    static void Save(Dictionary<string, JsonNode> catalog, Dictionary<string, JsonNode> pwdMap)
    {
        int matched = 0;
        var outArr = new JsonArray();
        foreach (var (code, value) in catalog.OrderBy(s => s.Key, StringComparer.OrdinalIgnoreCase))
        {
            var game = value.AsObject();
            if (pwdMap.TryGetValue(code, out var pwd))
            {
                matched++;
                foreach (var (k, v) in pwd.AsObject()) game[k] = v?.DeepClone();
            }
            var si = new JsonObject(Options.NodeOpt);
            foreach (var k in KeepFields)
                if (game.TryGetPropertyValue(k, out var v) && v is { }) si.Add(k, v.DeepClone());
            outArr.Add((JsonNode)si);
        }
        var outOptions = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, WriteIndented = true };
        string outJson = DecodeUnicodeEscapes(outArr.ToJsonString(outOptions));
        File.WriteAllBytes(ClientInfo.CatalogFile, Encoding.UTF8.GetBytes(outJson));
        Console.WriteLine($"[out] 保存 {ClientInfo.CatalogFile} 条数={outArr.Count} 密码匹配={matched}");
    }

    // \uXXXX 还原为真实字符（含代理对合并）
    static string DecodeUnicodeEscapes(string s)
    {
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (i + 5 < s.Length && s[i] == '\\' && s[i + 1] == 'u'
                && uint.TryParse(s.AsSpan(i + 2, 4), NumberStyles.HexNumber, null, out uint cp))
            {
                if (cp is >= 0xD800 and <= 0xDBFF && i + 11 < s.Length && s[i + 6] == '\\' && s[i + 7] == 'u'
                    && uint.TryParse(s.AsSpan(i + 8, 4), NumberStyles.HexNumber, null, out uint low) && low is >= 0xDC00 and <= 0xDFFF)
                {
                    sb.Append(char.ConvertFromUtf32(0x10000 + ((int)cp - 0xD800) * 0x400 + ((int)low - 0xDC00)));
                    i += 11;
                }
                else
                {
                    sb.Append(char.ConvertFromUtf32((int)cp));
                    i += 5;
                }
                continue;
            }
            sb.Append(s[i]);
        }
        return sb.ToString();
    }
}

// ============================== 4. 设备标识 ==============================

/// fp = "2|<CPUID 16hex>|<4×4hex 下划线连接>."；hardwareId = SHA256(fp) 大写，
/// hardwareIdHash = SHA256(hardwareId) 小写；installationId = base64url(32 随机字节)，不持久化。
readonly record struct Identity(string HardwareIdHash, string InstallationIdHash)
{
    public static Identity Create()
    {
        string fp = $"2|{Convert.ToHexString(RandomNumberGenerator.GetBytes(8))}|"
            + string.Join("_", Enumerable.Range(0, 4).Select(_ => Convert.ToHexString(RandomNumberGenerator.GetBytes(2)))) + ".";
        string hardwareId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fp)));
        string installationId = Codec.Base64Url(RandomNumberGenerator.GetBytes(32));
        return new Identity(Codec.Sha256HexLower(hardwareId), Codec.Sha256HexLower(installationId));
    }
}

// ============================== 5. 协议 ==============================

/// 请求信封：每次随机 DEK，enc/mac 子密钥 HKDF 简化派生；DEK 用 startup 给的 RSA-OAEP 公钥包裹
static class Envelope
{
    public const string MediaType = "application/vnd.app2.client-envelope+json";
    public const string HeaderName = "X-Client-Envelope", HeaderValue = "1";

    const string DerivePrefix = "app2:client-envelope:v1:";
    const string MacTag = "app2-client-envelope-v1";
    const string Purpose = "request";
    const int DekSize = 32, NonceSize = 16, IvSize = 16, MaxCipherSize = 0x10000;

    static RSAEncryptionPadding Padding => RSAEncryptionPadding.OaepSHA256;

    // relativePath 为 MAC canonical 中的 url 元素（小写相对 path）
    public static string Build(string bodyJson, string bodyMediaType, RequestEncryption rsa,
                               string relativePath, long ts, out byte[] dek)
    {
        dek = RandomNumberGenerator.GetBytes(DekSize);
        byte[] encKey = HMACSHA256.HashData(dek, Encoding.UTF8.GetBytes(DerivePrefix + "enc"));
        byte[] macKey = HMACSHA256.HashData(dek, Encoding.UTF8.GetBytes(DerivePrefix + "mac"));
        string nonce = Codec.Base64Url(RandomNumberGenerator.GetBytes(NonceSize));
        byte[] iv = RandomNumberGenerator.GetBytes(IvSize);

        var descriptor = new EnvelopeDescriptor
        {
            ContentType = bodyMediaType,
            BodyBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(bodyJson)),
            Headers = new Dictionary<string, string> { [Api.DistributionHeader] = Api.DistributionValue },
        };
        byte[] plaintext = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(descriptor, AppJson.Default.EnvelopeDescriptor));

        byte[] cipherBlob;
        using (var aes = Aes.Create())
        {
            aes.Key = encKey;
            cipherBlob = aes.EncryptCbc(plaintext, iv, PaddingMode.PKCS7);
        }
        if (cipherBlob.Length == 0 || cipherBlob.Length > MaxCipherSize)
            throw new InvalidOperationException($"envelope ciphertext size out of range: {cipherBlob.Length}");

        string wrappedKey = Convert.ToBase64String(WrapDek(dek, rsa));

        // MAC 输入：tag\nrequest\nMETHOD\npath\n0\n\nkeyId\nts\nnonce\nwrappedKey\n + iv + ciphertext
        string canonical = string.Join("\n", MacTag, Purpose, "POST", relativePath.ToLowerInvariant(), "0", "",
            rsa.KeyId, ts.ToString(CultureInfo.InvariantCulture), nonce, wrappedKey) + "\n";
        byte[] macInput = [.. Encoding.UTF8.GetBytes(canonical), .. iv, .. cipherBlob];

        var envelope = new ClientEnvelope
        {
            KeyId = rsa.KeyId,
            Timestamp = ts,
            Nonce = nonce,
            WrappedKey = wrappedKey,
            Iv = Convert.ToBase64String(iv),
            Ciphertext = Convert.ToBase64String(cipherBlob),
            Mac = Convert.ToBase64String(HMACSHA256.HashData(macKey, macInput)),
        };
        return JsonSerializer.Serialize(envelope, AppJson.Default.ClientEnvelope);
    }

    static byte[] WrapDek(byte[] dek, RequestEncryption rsa)
    {
        using var r = RSA.Create();
        r.ImportParameters(new RSAParameters
        {
            Modulus = Convert.FromBase64String(rsa.ModulusBase64),
            Exponent = Convert.FromBase64String(rsa.ExponentBase64),
        });
        return r.Encrypt(dek, Padding);
    }
}

/// 响应信封：keyId / wrappedKey 为空串，复用本次请求 DEK 派生密钥；
/// 派生 label 未确定，按候选逐个试探，解出 JSON 即命中
static class RespEnv
{
    const string DerivePrefix = "app2:client-envelope:v1:";
    static readonly string[] Labels =
        ["resp-enc", "response-enc", "resp", "response", "enc", "data", "resp-aes", "response-aes"];

    public static byte[]? TryOpen(string json, byte[] dek, out string hit)
    {
        hit = "";
        if (JsonNode.Parse(json, Options.NodeOpt) is not JsonObject doc) return null;
        byte[] iv, ct;
        try
        {
            iv = Convert.FromBase64String((string)doc["iv"]!);
            ct = Convert.FromBase64String((string)doc["ciphertext"]!);
        }
        catch { return null; }
        if (iv.Length != 16 || ct.Length == 0 || ct.Length % 16 != 0) return null;

        foreach (var (label, key) in Candidates(dek))
        {
            if (Codec.TryAesCbcDecrypt(key, iv, ct) is not { } dec) continue;
            string txt = Encoding.UTF8.GetString(dec).TrimStart();
            if (txt.StartsWith('{') || txt.StartsWith('[')) { hit = label; return dec; }
        }
        return null;
    }

    // 候选：直接 DEK + 各 label 派生
    static IEnumerable<(string Label, byte[] Key)> Candidates(byte[] dek) =>
        [("dek", dek), .. Labels.Select(l => (l, HMACSHA256.HashData(dek, Encoding.UTF8.GetBytes(DerivePrefix + l))))];
}

/// 响应解密：X-Enc-Key 查表 -> 前 16B IV -> AES-256-CBC(PKCS7) -> gzip
static class Proto
{
    public static readonly Dictionary<string, string> RespKeyTable = new(StringComparer.Ordinal)
    {
        ["startup-bootstrap-v1"] = "Osz/rv3p5/q/3XzLQ28SPt23zIuTkoNHurftcOVV8tk=",
        ["r1"] = "mPALvUqhQ1k0CkP+2LfY5gK20ZDWRWcDBBuP102wqGc=",
        ["aes-v1"] = "Tq8vR2xLm5Nw3pYjKdF7hA0sBcEgUiOlZrXnMtWfDqI=",
        ["client-api-response-v1"] = "Tq8vR2xLm5Nw3pYjKdF7hA0sBcEgUiOlZrXnMtWfDqI=",
    };

    public static byte[]? Decrypt(string encKeyLabel, byte[] raw, out string hit)
    {
        hit = "";
        if (encKeyLabel.Length == 0 || !RespKeyTable.TryGetValue(encKeyLabel, out var keyB64)) return null;

        byte[] blob = raw;
        string asText = Encoding.UTF8.GetString(raw).Trim();
        if (asText.Length > 44 && asText.All(c => char.IsLetterOrDigit(c) || c is '+' or '/' or '='))
        {
            try { blob = Convert.FromBase64String(asText); } catch { return null; }
        }
        if (blob.Length <= 32 || (blob.Length - 16) % 16 != 0) return null;

        if (Codec.TryAesCbcDecrypt(Convert.FromBase64String(keyB64), blob[..16], blob[16..]) is not { } dec) return null;
        hit = $"CBC / table:{encKeyLabel} / iv=head16";
        return dec;
    }
}

// ============================== 6. 编解码 / 工具 ==============================

/// 通用编解码：哈希、base64url、AES-CBC、gzip、响应体还原
static class Codec
{
    public static string Sha256HexLower(string s) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();

    // base64url（无 padding），与原客户端 installationId 编码一致
    public static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static bool IsGzip(byte[] data) => data.Length > 2 && data[0] == 0x1f && data[1] == 0x8b;

    public static byte[] Gunzip(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var gz = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gz.CopyTo(output);
        return output.ToArray();
    }

    // 解密失败抛异常（调用方需要区分失败原因时用）；明文为 gzip 时自动解压
    public static byte[] AesCbcDecrypt(byte[] key, byte[] iv, byte[] cipher)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        byte[] plain = aes.DecryptCbc(cipher, iv, PaddingMode.PKCS7);
        return IsGzip(plain) ? Gunzip(plain) : plain;
    }

    public static byte[]? TryAesCbcDecrypt(byte[] key, byte[] iv, byte[] cipher)
    {
        try { return AesCbcDecrypt(key, iv, cipher); } catch { return null; }
    }

    public static byte[] HmacConcat(byte[] key, byte[] a, byte[] b) => HMACSHA256.HashData(key, (byte[])[.. a, .. b]);

    // 响应体还原：X-Enc-Key 查表解密；裸 gzip 解压；否则原样
    public static string RestoreBody(string encKey, byte[] raw)
    {
        if (encKey.Length > 0 && Proto.Decrypt(encKey, raw, out _) is { } plain) return Encoding.UTF8.GetString(plain);
        if (IsGzip(raw))
        {
            try { return Encoding.UTF8.GetString(Gunzip(raw)); } catch { }
        }
        return Encoding.UTF8.GetString(raw);
    }

    // descriptor 包裹 {"contentType":"…","bodyBase64":"…","headers":{…}} -> 业务 JSON；否则原样返回
    public static string UnwrapDescriptor(string json)
    {
        string t = json.TrimStart();
        if (t.Length < 2 || t[0] != '{' || !t.Contains("bodyBase64", StringComparison.OrdinalIgnoreCase)) return json;
        try
        {
            var doc = JsonNode.Parse(json, Options.NodeOpt)!.AsObject();
            if (doc["bodyBase64"] is { } bb && bb.GetValueKind() == JsonValueKind.String)
                return Encoding.UTF8.GetString(Convert.FromBase64String((string)bb!));
        }
        catch
        {
            // 非法 JSON / 非法 base64 一律按非 descriptor 处理
        }
        return json;
    }
}

/// games.dat 解密：head16=IV | cipher | tail32=HMAC-SHA256；HMAC 覆盖范围两种写法都试
static class GamesDatCodec
{
    public static bool TryDecrypt(byte[] file, out string json, out string note)
    {
        json = "";
        note = "";
        if (file.Length < 16 + 16 + 32)
        {
            note = $"文件长度非法 {file.Length}B";
            return false;
        }
        byte[] iv = file[..16];
        byte[] cipher = file[16..^32];
        byte[] tailMac = file[^32..];
        if (cipher.Length % 16 != 0)
        {
            note = $"密文未块对齐 {cipher.Length}B";
            return false;
        }

        byte[] aesKey = Convert.FromBase64String(Secrets.GamesDatKeyB64);
        byte[] hmacKey = Convert.FromHexString(Secrets.GamesDatHmacKeyHex);
        bool hitIvCt = CryptographicOperations.FixedTimeEquals(Codec.HmacConcat(hmacKey, iv, cipher), tailMac);
        bool hitCt = !hitIvCt && CryptographicOperations.FixedTimeEquals(Codec.HmacConcat(hmacKey, [], cipher), tailMac);
        note = hitIvCt ? "HMAC(iv|cipher) 命中" : hitCt ? "HMAC(cipher) 命中" : "HMAC 不匹配（仍尝试解密）";

        if (Codec.TryAesCbcDecrypt(aesKey, iv, cipher) is not { } plain)
        {
            note += "/解密失败";
            return false;
        }

        json = Encoding.UTF8.GetString(plain);
        string head = json.TrimStart();
        return head.StartsWith('[') || head.StartsWith('{');
    }
}

/// startup 响应解析：data.requestEncryption + data.startupSignatureV3
static class StartupParser
{
    public static StartupInfo Parse(string json)
    {
        var info = new StartupInfo();
        try
        {
            if (JsonNode.Parse(json, Options.NodeOpt) is not JsonObject root) return info;
            if (root["data"] is not JsonObject data) { Console.Error.WriteLine("[startup] 响应缺 data 对象"); return info; }
            if (data["miniDataSnapshot"] is { } sn && sn["lastSyncTicks"] is JsonValue t && t.TryGetValue(out long tl)) info.LastSyncTicks = tl;
            if (data["requestEncryption"] is JsonObject re)
            {
                string? keyId = (string)re["keyId"]!;
                string? mod = (string)re["modulusBase64"]!;
                string? exp = (string)re["exponentBase64"]!;
                if (!string.IsNullOrEmpty(keyId) && !string.IsNullOrEmpty(mod) && !string.IsNullOrEmpty(exp))
                    info.Req = new RequestEncryption { KeyId = keyId, ModulusBase64 = mod, ExponentBase64 = exp };
            }
        }
        catch { }
        return info;
    }
}

/// 路由地址规整
static class Net
{
    public static string Normalize(string s)
    {
        s = s.Trim();
        return (s.Contains("://") ? s : "http://" + s).TrimEnd('/');
    }

    public static bool IsIpOrigin(string origin)
    {
        string host = origin[(origin.IndexOf("://", StringComparison.Ordinal) + 3)..];
        int colon = host.IndexOf(':');
        if (colon >= 0) host = host[..colon];
        return IPAddress.TryParse(host, out _);
    }
}

// ============================== 7. 数据结构 ==============================

sealed record BootResult(ProbeResult? Winner, StartupInfo Info, long ProbeMs);

sealed class ProbeResult
{
    public string Origin { get; set; } = "";
    public int Status { get; set; }
    public bool Ok { get; set; }
    public string? PlainJson { get; set; }
    public string? Fail { get; set; }
}

sealed class StartupInfo
{
    public RequestEncryption? Req { get; set; }
    public long? LastSyncTicks { get; set; }
}

sealed class RequestEncryption
{
    [JsonPropertyName("keyId")] public string KeyId { get; set; } = "";
    [JsonPropertyName("modulusBase64")] public string ModulusBase64 { get; set; } = "";
    [JsonPropertyName("exponentBase64")] public string ExponentBase64 { get; set; } = "";
}

// 信封内层 payload（静态还原）
sealed class EnvelopeDescriptor
{
    public string ContentType { get; set; } = "application/json";
    public string BodyBase64 { get; set; } = "";
    public Dictionary<string, string> Headers { get; set; } = new();
}

// 外层信封（camelCase，字段顺序固定）
sealed class ClientEnvelope
{
    [JsonPropertyName("version")] public int Version { get; set; } = 1;
    [JsonPropertyName("keyId")] public string KeyId { get; set; } = "";
    [JsonPropertyName("timestamp")] public long Timestamp { get; set; }
    [JsonPropertyName("nonce")] public string Nonce { get; set; } = "";
    [JsonPropertyName("wrappedKey")] public string WrappedKey { get; set; } = "";
    [JsonPropertyName("iv")] public string Iv { get; set; } = "";
    [JsonPropertyName("ciphertext")] public string Ciphertext { get; set; } = "";
    [JsonPropertyName("mac")] public string Mac { get; set; } = "";
}

// auto/ensure 内层明文 PascalCase，6 字段顺序固定
sealed class AutoActivationEnsureRequest
{
    [JsonPropertyName("ProtocolVersion")] public int ProtocolVersion { get; set; } = 2;
    [JsonPropertyName("InstallationIdHash")] public string InstallationIdHash { get; set; } = "";
    [JsonPropertyName("HardwareIdHash")] public string HardwareIdHash { get; set; } = "";
    [JsonPropertyName("ClientKind")] public string ClientKind { get; set; } = "mini";
    [JsonPropertyName("ClientVersion")] public string ClientVersion { get; set; } = "1.1.4";
    [JsonPropertyName("Distribution")] public string Distribution { get; set; } = "Normal";
}

// ============================== 8. AOT JSON 上下文 ==============================
// NativeAOT 下不反射序列化，统一走 source generator：AppJson.Default.<Type>
[JsonSerializable(typeof(AutoActivationEnsureRequest))]
[JsonSerializable(typeof(EnvelopeDescriptor))]
[JsonSerializable(typeof(ClientEnvelope))]
partial class AppJson : JsonSerializerContext
{
}
