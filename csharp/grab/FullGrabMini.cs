// FullGrabMini.cs
// 完整功能：
//  1) bootstrap 拉 host 列表，遍历 GET /api/client/startup，成功即停止，打印最新版本号与下载地址
//  2) ensure 拿 token → GET /api/client/download/data-snapshot/mini 下载 zip 到内存
//  3) 内存解压 games.dat → 用 games_dat_decrypt.cs 方法静态解密（AES-CBC + HMAC 校验）→ 全量游戏列表
//  4) GET /api/client/games/passwords 拿密码 → 按 GameCode 拼接 → 合成输出并保存
// 运行：dotnet run FullGrabMini.cs [--host <scheme://host[:port]>] [--out <path>]

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

string forcedHost = "";
string outPath = "grab_games.json";
var cmdArgs = Environment.GetCommandLineArgs();
for (int i = 1; i < cmdArgs.Length; i++)
{
    if (cmdArgs[i] == "--host" && i + 1 < cmdArgs.Length) forcedHost = cmdArgs[++i];
    else if (cmdArgs[i] == "--out" && i + 1 < cmdArgs.Length) outPath = cmdArgs[++i];
}

using var client = Api.NewClient();

// ===== 1) hosts = bootstrap / fallback =====
var hosts = !string.IsNullOrEmpty(forcedHost)
    ? new List<string> { forcedHost }
    : await Api.FetchBootstrapRoutesAsync(client);
Console.WriteLine($"[init] hosts={string.Join(", ", hosts)}");

// ===== 2) 遍历 startup，成功即停止 =====
string hostUsed = "";
foreach (var host in hosts)
{
    var st = await Api.ProbeStartupAsync(client, host);
    if (st == null) { Console.WriteLine($"[startup] {host} 失败，尝试下一节点"); continue; }
    hostUsed = host;
    Console.WriteLine($"[startup] {host} OK");
    Console.WriteLine($"[startup] toolkitMiniLatestVersion={st.MiniLatestVersion}");
    Console.WriteLine($"[startup] clientDownloadUrls={string.Join(" | ", st.ClientDownloadUrls)}");
    if (st.MiniMirrorPath != null)
        Console.WriteLine($"[startup] miniUpdateFile={st.MiniUpdateFileName} hash={st.MiniUpdateHash} mirror={host}{st.MiniMirrorPath}");
    break;
}
if (hostUsed == "") { Console.WriteLine("所有节点 startup 均失败。"); return; }

// ===== 3) ensure token =====
var (token, _, ok) = await Api.EnsureAsync(client, hostUsed);
if (!ok || string.IsNullOrEmpty(token)) { Console.WriteLine("[ensure] 未获取到 token，退出。"); return; }
Console.WriteLine($"[ensure] tokenLen={token.Length}");

// ===== 4) 快照 zip 下载到内存 =====
byte[]? zipBytes = await Api.GetSnapshotZipAsync(client, hostUsed, token);
Console.WriteLine($"[snapshot] zipBytes={zipBytes?.Length ?? 0}");
if (zipBytes == null || zipBytes.Length == 0) { Console.WriteLine("[snapshot] 快照为空，退出。"); return; }

// ===== 5) 内存解压 games.dat =====
byte[] gamesData;
long lastSyncTicks = 0;
using (var ms = new MemoryStream(zipBytes))
using (var zip = new ZipArchive(ms, ZipArchiveMode.Read))
{
    var entry = zip.GetEntry("games.dat");
    if (entry == null) { Console.WriteLine("[zip] 缺少 games.dat 条目"); return; }
    using var es = entry.Open();
    using var os = new MemoryStream();
    es.CopyTo(os);
    gamesData = os.ToArray();

    var syncEntry = zip.GetEntry("sync_state_mini.json");
    if (syncEntry != null)
    {
        using var ss = syncEntry.Open();
        using var sr = new StreamReader(ss);
        string syncJson = sr.ReadToEnd();
        try
        {
            using var sdoc = JsonDocument.Parse(syncJson);
            if (sdoc.RootElement.TryGetProperty("LastSyncTicks", out var t) && t.ValueKind == JsonValueKind.Number)
                lastSyncTicks = t.GetInt64();
        }
        catch { }
    }
}
Console.WriteLine($"[zip] games.dat bytes={gamesData.Length} lastSyncTicks={lastSyncTicks}");

// ===== 6) 内存静态解密（games_dat_decrypt.cs 方法） =====
byte[] plainBytes = GamesDat.DecryptToBytes(gamesData);
string gamesJson = Encoding.UTF8.GetString(plainBytes);
Console.WriteLine($"[decrypt] plainJsonBytes={plainBytes.Length}");
int gameCount = -1;
using (var doc = JsonDocument.Parse(gamesJson))
    gameCount = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.GetArrayLength() : -1;
Console.WriteLine($"[decrypt] games 条数={gameCount}");

// ===== 6.5) GetGamesAsync 获取最新游戏列表，与快照合并（接口数据覆盖/新增） =====
static JsonObject MakeEntry(JsonElement item)
{
    var obj = new JsonObject();
    obj["GameCode"] = GetProp(item, "gameCode", "GameCode") ?? "";
    obj["ChineseName"] = GetProp(item, "chineseName", "ChineseName") ?? "";
    obj["EnglishName"] = GetProp(item, "englishName", "EnglishName") ?? "";
    obj["Size"] = GetProp(item, "size", "Size") ?? "";
    obj["GameVersion"] = GetProp(item, "gameVersion", "GameVersion") ?? "";
    obj["DownloadUrl1"] = GetProp(item, "downloadUrl1", "DownloadUrl1") ?? "";
    obj["DownloadUrl2"] = GetProp(item, "downloadUrl2", "DownloadUrl2") ?? "";
    obj["UpdateDateTooltip"] = BuildTooltip(GetProp(item, "updateDate", "UpdateDate"), GetProp(item, "releaseDate", "ReleaseDate"));
    return obj;
}

var merged = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
using (var gdoc = JsonDocument.Parse(gamesJson))
{
    foreach (var item in gdoc.RootElement.EnumerateArray())
    {
        string code = GetProp(item, "gameCode", "GameCode") ?? "";
        if (!string.IsNullOrEmpty(code)) merged[code] = MakeEntry(item);
    }
}
string latestGamesJson = await Api.GetGamesAsync(client, hostUsed, token, lastSyncTicks);
int covered = 0, added = 0, latestCount = 0;
using (var ldoc = JsonDocument.Parse(latestGamesJson))
{
    JsonElement arr = ldoc.RootElement;
    if (arr.TryGetProperty("games", out var garr) && garr.ValueKind == JsonValueKind.Array) arr = garr;
    if (arr.ValueKind == JsonValueKind.Array)
    {
        foreach (var item in arr.EnumerateArray())
        {
            string code = GetProp(item, "gameCode", "GameCode") ?? "";
            if (string.IsNullOrEmpty(code)) continue;
            latestCount++;
            if (merged.ContainsKey(code)) covered++; else added++;
            merged[code] = MakeEntry(item);
        }
    }
}
Console.WriteLine($"[merge] 接口最新条数={latestCount} 覆盖={covered} 新增={added} 合并后总条数={merged.Count}");

// ===== 7) passwords 接口 =====
string passwordsJson = await Api.GetPasswordsAsync(client, hostUsed, token);
var pwdMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
using (var pdoc = JsonDocument.Parse(passwordsJson))
{
    JsonElement arr = pdoc.RootElement;
    if (arr.TryGetProperty("passwords", out var pw) && pw.ValueKind == JsonValueKind.Array) arr = pw;
    if (arr.ValueKind == JsonValueKind.Array)
    {
        foreach (var item in arr.EnumerateArray())
        {
            string code = GetProp(item, "GameCode", "gameCode") ?? "";
            string pwd = GetProp(item, "Password", "password") ?? "";
            if (!string.IsNullOrEmpty(code)) pwdMap[code] = pwd ?? "";
        }
    }
}
Console.WriteLine($"[passwords] 映射条数={pwdMap.Count}");

// ===== 8) 合成输出（按 GameCode 升序） =====
var outArr = new JsonArray();
int matched = 0;
foreach (var code in merged.Keys.OrderBy(k => k, StringComparer.Ordinal))
{
    var obj = new JsonObject();
    string pwd = pwdMap.TryGetValue(code, out var v) ? v : "";
    if (pwdMap.ContainsKey(code)) matched++;
    var src = merged[code];
    obj["GameCode"] = code;
    obj["Password"] = pwd;
    obj["ChineseName"] = src["ChineseName"]?.ToString() ?? "";
    obj["EnglishName"] = src["EnglishName"]?.ToString() ?? "";
    obj["Size"] = src["Size"]?.ToString() ?? "";
    obj["GameVersion"] = src["GameVersion"]?.ToString() ?? "";
    obj["DownloadUrl1"] = src["DownloadUrl1"]?.ToString() ?? "";
    obj["DownloadUrl2"] = src["DownloadUrl2"]?.ToString() ?? "";
    obj["UpdateDateTooltip"] = src["UpdateDateTooltip"]?.ToString() ?? "";
    outArr.Add(obj);
}

var outOptions = new JsonSerializerOptions
{
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    WriteIndented = true,
    IndentSize = 2,
};
string outJson = DecodeUnicodeEscapes(outArr.ToJsonString(outOptions));
File.WriteAllText(outPath, outJson, new UTF8Encoding(false));
Console.WriteLine($"[out] 保存 {outPath} bytes={outJson.Length} 条数={outArr.Count} 密码匹配={matched}");

static string? GetProp(JsonElement e, params string[] names)
{
    foreach (var n in names)
        if (e.TryGetProperty(n, out var p) && p.ValueKind == JsonValueKind.String)
            return p.GetString();
    return null;
}

static string BuildTooltip(string? updateDate, string? releaseDate)
{
    string? upd = FmtDate(updateDate);
    string? rel = FmtDate(releaseDate);
    var parts = new List<string>();
    if (!string.IsNullOrWhiteSpace(upd)) parts.Add("更新日期: " + upd);
    if (!string.IsNullOrWhiteSpace(rel)) parts.Add("发行日期: " + rel);
    return string.Join(" / ", parts);
}

static string? FmtDate(string? d)
{
    return DateTime.TryParseExact(d, "yyyyMMdd", DateTimeFormatInfo.CurrentInfo, DateTimeStyles.None, out var dt) ? dt.ToString("yyyy年MM月dd日") : d;
}

static string DecodeUnicodeEscapes(string input)
{
    var sb = new StringBuilder(input.Length);
    for (int i = 0; i < input.Length; i++)
    {
        if (input[i] == '\\' && i + 5 < input.Length && input[i + 1] == 'u'
            && uint.TryParse(input.AsSpan(i + 2, 4), System.Globalization.NumberStyles.HexNumber, null, out uint cp))
        {
            if (cp >= 0xD800 && cp <= 0xDBFF
                && i + 11 < input.Length
                && input[i + 6] == '\\' && input[i + 7] == 'u'
                && uint.TryParse(input.AsSpan(i + 8, 4), System.Globalization.NumberStyles.HexNumber, null, out uint low)
                && low >= 0xDC00 && low <= 0xDFFF)
            {
                int scalar = 0x10000 + ((int)cp - 0xD800) * 0x400 + ((int)low - 0xDC00);
                sb.Append(char.ConvertFromUtf32(scalar));
                i += 11;
                continue;
            }
            sb.Append(char.ConvertFromUtf32((int)cp));
            i += 5;
            continue;
        }
        sb.Append(input[i]);
    }
    return sb.ToString();
}

Console.WriteLine("[done]");

// ============ 常量区 ============
static class Consts
{
    public static readonly byte[] ResponseAESKey = Convert.FromBase64String("Tq8vR2xLm5Nw3pYjKdF7hA0sBcEgUiOlZrXnMtWfDqI=");
    public static readonly byte[] R1Key = Convert.FromBase64String("mPALvUqhQ1k0CkP+2LfY5gK20ZDWRWcDBBuP102wqGc=");
    public static readonly byte[] StartupAESKey = Convert.FromBase64String("Osz/rv3p5/q/3XzLQ28SPt23zIuTkoNHurftcOVV8tk=");
    public static readonly Dictionary<string, byte[]> Keys = new()
    {
        ["startup-bootstrap-v1"] = StartupAESKey,  // ← StartupAESKey
        ["r1"] = R1Key,  // BootstrapAESKey
        ["aes-v1"] = ResponseAESKey,
        ["client-api-response-v1"] = ResponseAESKey,  // ResponseAESKey
    };
    public const string BootstrapUrl = "https://lkfv.oss-cn-shanghai.aliyuncs.com/bootstrap-routes-v1.json";
    public static readonly string[] FallbackHosts =
    {
        "http://42.240.170.167:28531", "http://156.225.28.133",
        "http://38.76.194.86", "http://39.109.63.108", "http://39.109.63.45",
        "http://ad5fkruhhd.uk",
    };
    public const string RouteStartup = "/api/client/startup";
    public const string RouteEnsure = "/api/client/activation/auto/ensure";
    public const string RouteGames = "/api/client/games";
    public const string RouteSnapshot = "/api/client/download/data-snapshot/mini";
    public const string RoutePasswords = "/api/client/games/passwords";
    public const string HdrEntitlement = "X-Entitlement-Token";
    public const string HdrDistribution = "X-Mini-Distribution";
    public const string HdrHmacSig = "X-Hmac-Signature";
    public const string HdrHmacTs = "X-Hmac-Timestamp";
    public const string HdrEnc = "X-Enc";
    public const string HdrEncKey = "X-Enc-Key";
    public const string HdrEncZip = "X-Enc-Zip";
}

// ============ 基础工具 ============
static class Util
{
    public static readonly byte[] HmacKey = Convert.FromBase64String("yGuFtZLfTu0E6QSZ51uWSTAv1CoDPzv6qd4umJ7IqJ8=");
    public static string Sha256Hex(string s) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();

    public static string HmacSign(string method, string path, long ts, string bodyHash)
    {
        string msg = method + "\n" + path + "\n" + ts.ToString() + "\n";
        if (!string.IsNullOrEmpty(bodyHash)) msg += bodyHash;
        using var h = new HMACSHA256(HmacKey);
        return Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(msg)));
    }

    public static byte[] TryGzip(byte[] data)
    {
        if (data.Length > 2 && data[0] == 0x1F && data[1] == 0x8B)
        {
            using var ms = new MemoryStream(data);
            using var gz = new GZipStream(ms, CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            gz.CopyTo(outMs);
            return outMs.ToArray();
        }
        return data;
    }

    public static byte[] AesCbcDecrypt(byte[] key, byte[] ct)
    {
        if (ct.Length < 16) throw new InvalidDataException("ciphertext too short");
        var iv = ct.AsSpan(0, 16).ToArray();
        var body = ct.AsSpan(16).ToArray();
        using var aes = Aes.Create();
        aes.Key = key; aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
        using var dec = aes.CreateDecryptor();
        return dec.TransformFinalBlock(body, 0, body.Length);
    }

    public static string DecryptResponseBody(byte[] raw)
    {
        var step1 = TryGzip(raw);
        string b64 = Encoding.ASCII.GetString(step1).Trim();
        var ct = Convert.FromBase64String(b64);
        var key = Consts.ResponseAESKey;
        var plain = TryGzip(AesCbcDecrypt(key, ct));
        return Encoding.UTF8.GetString(plain);
    }

    public static List<string> DecryptBootstrapEnvelope(string json)
    {
        // bootstrap-routes-v1.json 信封：
        // {"v": 1,"k": "r1","z": 0,"d": "..." }
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("d", out var dEl) && dEl.ValueKind == JsonValueKind.String)
        {
            var ct = Convert.FromBase64String(dEl.GetString() ?? "");
            var keyName = root.TryGetProperty("k", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() ?? "r1" : "r1";
            var key = Consts.Keys[keyName];
            var plain = Util.TryGzip(Util.AesCbcDecrypt(key, ct));
            bool zipped = root.TryGetProperty("z", out var z) && z.GetInt32() == 1;
            if (zipped) plain = Util.TryGzip(plain);
            return ParseRouteArray(plain);
        }
        // 旧结构 { data: <base64 AES 密文> }
        if (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.String)
        {
            var ct = Convert.FromBase64String(dataEl.GetString() ?? "");
            var keyName = root.TryGetProperty("k", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() ?? "r1" : "r1";
            var key = Consts.Keys[keyName];
            var plain = Util.TryGzip(Util.AesCbcDecrypt(key, ct));
            return ParseRouteArray(plain);
        }
        // 兜底：明文 { r: [...] } 或数组
        if (root.TryGetProperty("r", out var r2) && r2.ValueKind == JsonValueKind.Array)
        {
            var list = new List<string>();
            foreach (var item in r2.EnumerateArray()) list.Add(item.GetString() ?? "");
            return list;
        }
        throw new InvalidDataException("bootstrap 信封解析失败");
    }

    static List<string> ParseRouteArray(byte[] plain)
    {
        using var pdoc = JsonDocument.Parse(Encoding.UTF8.GetString(plain));
        var root = pdoc.RootElement;
        var list = new List<string>();
        if (root.TryGetProperty("r", out var r) && r.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in r.EnumerateArray())
                if (item.ValueKind == JsonValueKind.String)
                {
                    string v = item.GetString() ?? "";
                    list.Add(v.StartsWith("http") ? v : "http://" + v);
                }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
                list.Add(item.GetString() ?? "");
        }
        return list;
    }
}

// ============ games.dat 静态解密（games_dat_decrypt.cs 移植，内存版） ============
static class GamesDat
{
    // _aesKey = Fn4TMHwAujoc8zhaMoMtA8zrqbzpcoXlO2jsfNTwFxg=
    static readonly byte[] AesKey =
    {
        22,126,19,48,124,0,186,58,28,243,56,90,50,131,45,3,
        204,235,169,188,233,114,133,229,59,104,236,124,212,240,23,24,
    };
    // _hmacKey = t1mq7+C9LtOqZUz47ZYydVq0+Syf8aT8Wt0Sd+ELBLE=
    static readonly byte[] HmacKey =
    {
        183,89,170,239,224,189,46,211,170,101,76,248,237,150,50,117,
        90,180,249,44,159,241,164,252,90,221,18,119,225,11,4,177,
    };

    public static byte[] DecryptToBytes(byte[] data)
    {
        if (data.Length < 48) throw new InvalidDataException("文件过小，不像是 games.dat");
        byte[] body = data.AsSpan(0, data.Length - 32).ToArray();
        byte[] mac = data.AsSpan(data.Length - 32).ToArray();
        using (var h = new HMACSHA256(HmacKey))
        {
            byte[] expect = h.ComputeHash(body);
            if (!CryptographicOperations.FixedTimeEquals(expect, mac))
                throw new InvalidDataException("HMAC 校验失败：文件被篡改或密钥不匹配");
        }
        byte[] iv = body.AsSpan(0, 16).ToArray();
        byte[] ciphertext = body.AsSpan(16).ToArray();
        if (ciphertext.Length % 16 != 0) throw new InvalidDataException("密文长度不是 16 的倍数");
        byte[] plain;
        using (var aes = Aes.Create())
        {
            aes.Key = AesKey;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            using var dec = aes.CreateDecryptor();
            plain = dec.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
        }
        using (var gz = new GZipStream(new MemoryStream(plain), CompressionMode.Decompress))
        using (var ms = new MemoryStream())
        {
            gz.CopyTo(ms);
            return ms.ToArray();
        }
    }
}

// ============ HTTP 客户端与请求组装 ============
static class Api
{
    public static HttpClient NewClient()
    {
        var h = new HttpClient(new SocketsHttpHandler { AutomaticDecompression = System.Net.DecompressionMethods.None });
        h.Timeout = TimeSpan.FromSeconds(20);
        h.DefaultRequestHeaders.UserAgent.ParseAdd("App2.Toolkit.Mini/1.0.249.0");
        h.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return h;
    }

    public static async Task<List<string>> FetchBootstrapRoutesAsync(HttpClient c)
    {
        try
        {
            using var resp = await c.GetAsync(Consts.BootstrapUrl);
            resp.EnsureSuccessStatusCode();
            string json = await resp.Content.ReadAsStringAsync();
            var routes = Util.DecryptBootstrapEnvelope(json);
            for (int i = 0; i < routes.Count; i++)
                if (!routes[i].StartsWith("http")) routes[i] = "http://" + routes[i];
            if (routes.Count > 0) return routes;
        }
        catch (Exception ex) { Console.WriteLine($"[bootstrap] 失败: {ex.Message}"); }
        return new List<string>(Consts.FallbackHosts);
    }

    public sealed class StartupInfo
    {
        public string MiniLatestVersion = "";
        public List<string> ClientDownloadUrls = new();
        public string MiniUpdateFileName = "";
        public string MiniUpdateHash = "";
        public string MiniMirrorPath = "";
    }

    public static async Task<StartupInfo?> ProbeStartupAsync(HttpClient c, string host)
    {
        string path = Consts.RouteStartup;
        long ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var req = new HttpRequestMessage(HttpMethod.Get, host + path);
        req.Headers.TryAddWithoutValidation(Consts.HdrDistribution, "Normal");
        req.Headers.TryAddWithoutValidation(Consts.HdrEnc, "2");
        req.Headers.TryAddWithoutValidation(Consts.HdrHmacTs, ts.ToString());
        req.Headers.TryAddWithoutValidation(Consts.HdrHmacSig, Util.HmacSign("GET", path, ts, ""));
        using var resp = await c.SendAsync(req);
        byte[] raw = await resp.Content.ReadAsByteArrayAsync();
        string encKeyHint = "";
        if (resp.Headers.TryGetValues(Consts.HdrEncKey, out var ev)) encKeyHint = string.Join(",", ev);
        Console.WriteLine($"[startup] {resp.StatusCode} bodyLen={raw.Length} encKey={encKeyHint}");
        if (!resp.IsSuccessStatusCode) return null;

        // 响应解密：gzip → JSON | base64 → AES-CBC(startup-bootstrap-v1 key) → gzip
        byte[] step1 = Util.TryGzip(raw);
        string s1 = Encoding.UTF8.GetString(step1);
        JsonDocument doc;
        if (!s1.TrimStart().StartsWith("{"))
        {
            string b64 = Encoding.ASCII.GetString(step1).Trim();
            var ct = Convert.FromBase64String(b64);
            var key = Consts.StartupAESKey;
            var plain = Util.TryGzip(Util.AesCbcDecrypt(key, ct));
            s1 = Encoding.UTF8.GetString(plain);
        }
        doc = JsonDocument.Parse(s1);
        var info = new StartupInfo();
        using (doc)
        {
            var data = doc.RootElement;
            if (data.TryGetProperty("data", out var d)) data = d;
            if (data.TryGetProperty("toolkitMiniLatestVersion", out var v)) info.MiniLatestVersion = v.GetString() ?? "";
            if (data.TryGetProperty("clientDownloadUrls", out var urls) && urls.ValueKind == JsonValueKind.Array)
                foreach (var u in urls.EnumerateArray()) info.ClientDownloadUrls.Add(u.GetString() ?? "");
            if (data.TryGetProperty("miniUpdateFileName", out var f)) info.MiniUpdateFileName = f.GetString() ?? "";
            if (data.TryGetProperty("miniUpdateHash", out var hh)) info.MiniUpdateHash = hh.GetString() ?? "";
            if (data.TryGetProperty("miniMirrorDownloadPath", out var mp)) info.MiniMirrorPath = mp.GetString() ?? "";
        }
        return info;
    }

    public static async Task<(string token, string raw, bool ok)> EnsureAsync(HttpClient c, string host)
    {
        var InstallationIdBytes = new byte[32];
        var HardwareIdHashBytes = new byte[32];
        //var rand = new Random();
        //rand.NextBytes(InstallationIdBytes);
        //rand.NextBytes(HardwareIdHashBytes);
        long ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var bodyObj = new JsonObject
        {
            ["ProtocolVersion"] = 2,
            ["InstallationIdHash"] = Util.Sha256Hex(Convert.ToBase64String(InstallationIdBytes)),
            ["HardwareIdHash"] = Convert.ToHexString(HardwareIdHashBytes),
            ["ClientKind"] = "mini",
            ["ClientVersion"] = "1.0.250.0",
            ["Distribution"] = "Normal",
        };
        string body = bodyObj.ToJsonString();
        Console.WriteLine(body);
        string bodyHash = Util.Sha256Hex(body);

        var req = new HttpRequestMessage(HttpMethod.Post, host + Consts.RouteEnsure);
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");
        req.Headers.TryAddWithoutValidation(Consts.HdrDistribution, "Normal");
        req.Headers.TryAddWithoutValidation(Consts.HdrEnc, "2");
        req.Headers.TryAddWithoutValidation(Consts.HdrHmacTs, ts.ToString());
        req.Headers.TryAddWithoutValidation(Consts.HdrHmacSig, Util.HmacSign("POST", Consts.RouteEnsure, ts, bodyHash));
        // if (!string.IsNullOrEmpty(oldToken))
        //     req.Headers.TryAddWithoutValidation(Consts.HdrEntitlement, oldToken);

        using var resp = await c.SendAsync(req);
        byte[] raw = await resp.Content.ReadAsByteArrayAsync();
        Console.WriteLine($"[ensure] {resp.StatusCode} host={host}");
        string plain;
        try { plain = Util.DecryptResponseBody(raw); }
        catch { plain = Encoding.UTF8.GetString(Util.TryGzip(raw)); }
        using var doc = JsonDocument.Parse(plain);
        var root = doc.RootElement;
        bool success = root.TryGetProperty("success", out var s) && s.GetBoolean();
        string token = root.TryGetProperty("entitlementToken", out var t) ? (t.GetString() ?? "") : "";
        return (token, plain, success);
    }

    public static async Task<byte[]?> GetSnapshotZipAsync(HttpClient c, string host, string token)
    {
        long ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string path = Consts.RouteSnapshot; // 签名路径不含 query
        var req = new HttpRequestMessage(HttpMethod.Get, host + path);
        req.Headers.TryAddWithoutValidation(Consts.HdrEntitlement, token);
        req.Headers.TryAddWithoutValidation(Consts.HdrDistribution, "Normal");
        req.Headers.TryAddWithoutValidation(Consts.HdrEnc, "2");
        req.Headers.TryAddWithoutValidation(Consts.HdrHmacTs, ts.ToString());
        req.Headers.TryAddWithoutValidation(Consts.HdrHmacSig, Util.HmacSign("GET", path, ts, ""));

        byte[]? raw = null;
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                using var slow = new HttpClient(new SocketsHttpHandler { AutomaticDecompression = System.Net.DecompressionMethods.None })
                {
                    Timeout = TimeSpan.FromSeconds(120)
                };
                long ts2 = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var req2 = new HttpRequestMessage(HttpMethod.Get, host + path);
                req2.Headers.TryAddWithoutValidation(Consts.HdrEntitlement, token);
                req2.Headers.TryAddWithoutValidation(Consts.HdrDistribution, "Normal");
                req2.Headers.TryAddWithoutValidation(Consts.HdrEnc, "2");
                req2.Headers.TryAddWithoutValidation(Consts.HdrHmacTs, ts2.ToString());
                req2.Headers.TryAddWithoutValidation(Consts.HdrHmacSig, Util.HmacSign("GET", path, ts2, ""));
                using var resp = await slow.SendAsync(req2);
                raw = await resp.Content.ReadAsByteArrayAsync();
                Console.WriteLine($"[snapshot] 尝试{attempt} {resp.StatusCode} bodyLen={raw.Length}");
                if (!resp.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[snapshot] bodyHead={Encoding.UTF8.GetString(Util.TryGzip(raw), 0, Math.Min(300, Util.TryGzip(raw).Length))}");
                    return null;
                }
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[snapshot] 尝试{attempt} 失败: {ex.Message}");
                if (attempt == 3) return null;
                await Task.Delay(2000);
            }
        }
        // zip: PK\x03\x04 开头；若非 zip 尝试解包（历史出现 gzip 包裹）
        if (raw != null && raw.Length > 2 && raw[0] == 0x1F && raw[1] == 0x8B) raw = Util.TryGzip(raw);
        return raw;
    }

    public static async Task<string> GetGamesAsync(HttpClient c, string host, string token, long since)
    {
        long ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string path = Consts.RouteGames; // 签名路径不含 query
        var req = new HttpRequestMessage(HttpMethod.Get, host + path + $"?since={since}");
        req.Headers.TryAddWithoutValidation(Consts.HdrEntitlement, token);
        req.Headers.TryAddWithoutValidation(Consts.HdrDistribution, "Normal");
        req.Headers.TryAddWithoutValidation(Consts.HdrEnc, "2");
        req.Headers.TryAddWithoutValidation(Consts.HdrHmacTs, ts.ToString());
        req.Headers.TryAddWithoutValidation(Consts.HdrHmacSig, Util.HmacSign("GET", path, ts, ""));

        using var resp = await c.SendAsync(req);
        byte[] raw = await resp.Content.ReadAsByteArrayAsync();
        Console.WriteLine($"[games] {resp.StatusCode} bodyLen={raw.Length}");
        if (!resp.IsSuccessStatusCode)
        {
            try { Console.WriteLine($"[games] rawBody={Encoding.UTF8.GetString(Util.TryGzip(raw))}"); }
            catch (Exception ex) { Console.WriteLine($"[games] rawBodyErr={ex.Message}"); }
            return "{}";
        }
        return Util.DecryptResponseBody(raw);
    }

    public static async Task<string> GetPasswordsAsync(HttpClient c, string host, string token)
    {
        long ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var req = new HttpRequestMessage(HttpMethod.Get, host + Consts.RoutePasswords);
        req.Headers.TryAddWithoutValidation(Consts.HdrEntitlement, token);
        req.Headers.TryAddWithoutValidation(Consts.HdrDistribution, "Normal");
        req.Headers.TryAddWithoutValidation(Consts.HdrEnc, "2");
        req.Headers.TryAddWithoutValidation(Consts.HdrHmacTs, ts.ToString());
        req.Headers.TryAddWithoutValidation(Consts.HdrHmacSig, Util.HmacSign("GET", Consts.RoutePasswords, ts, ""));

        using var resp = await c.SendAsync(req);
        byte[] raw = await resp.Content.ReadAsByteArrayAsync();
        Console.WriteLine($"[passwords] {resp.StatusCode} bodyLen={raw.Length}");
        return Util.DecryptResponseBody(raw);
    }
}
