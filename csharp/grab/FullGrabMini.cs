#:property TargetFramework=net8.0
#:property PublishAot=true

// FullGrabMini.cs
// 完整功能：
//  1) bootstrap 拉 host 列表，遍历 GET /api/client/startup，成功即停止，打印最新版本号与下载地址
//  2) ensure 拿 token → GET /api/client/download/data-snapshot/mini 下载 zip 到内存
//  3) 内存解压 games.dat → 用 games_dat_decrypt.cs 方法静态解密（AES-CBC + HMAC 校验）→ 全量游戏列表
//  4) GET /api/client/games/passwords 拿密码 → 按 GameCode 拼接 → 合成输出并保存
// 运行：dotnet run FullGrabMini.cs [-host <index>] [-out <path>] [-f]

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

string forcedHost = "-1";
string outPath = "grab_games.json";
bool force = false;
var cmdArgs = Environment.GetCommandLineArgs();
for (int i = 1; i < cmdArgs.Length; i++)
{
    if (cmdArgs[i] == "-host" && i + 1 < cmdArgs.Length) forcedHost = cmdArgs[++i];
    else if (cmdArgs[i] == "-out" && i + 1 < cmdArgs.Length) outPath = cmdArgs[++i];
    else if (cmdArgs[i] == "-f") force = true;
}

using var client = Api.NewClient();

// ===== 1) hosts = bootstrap / fallback =====
var hosts = await Api.FetchBootstrapRoutesAsync(client);
if (int.TryParse(forcedHost, out var fh) && fh >= 0 && fh < hosts.Count)
{
    hosts = [hosts[fh]];
    Console.WriteLine($"[forcehost] hosts={string.Join(", ", hosts)}");
}

// ===== 2) 遍历 startup，成功即停止 =====
string hostUsed = "";
Api.StartupInfo? info = null;
foreach (var host in hosts)
{
    info = await Api.ProbeStartupAsync(client, host);
    if (info == null) { Console.WriteLine($"[startup] {host} 失败，尝试下一节点"); continue; }
    //Console.WriteLine($"[startup] toolkitMiniLatestVersion={st.MiniLatestVersion}");
    Console.WriteLine($"[startup] {host} OK clientDownloadUrls={string.Join(" | ", info.ClientDownloadUrls)}");
    // if (st.MiniMirrorPath != null)
    //     Console.WriteLine($"[startup] miniUpdateFile={st.MiniUpdateFileName} hash={st.MiniUpdateHash} mirror={host}{st.MiniMirrorPath}");
    hostUsed = host;
    break;
}
if (info is null || hostUsed == "") { Console.WriteLine("所有节点 startup 均失败。"); return; }

string keyField = "GameCode";
string[] keepFields = [keyField, "Password", "ChineseName", "EnglishName", "Size", "GameVersion", "DownloadUrl1", "DownloadUrl2", "UpdateDate"];
var opt = Consts.NodeOpt;
var merged = new Dictionary<string, JsonNode>(StringComparer.OrdinalIgnoreCase);
if (!force)
{
    if (File.Exists(outPath))
    {
        try
        {
            var fileBytes = File.ReadAllBytes(outPath);
            if (JsonNode.Parse(fileBytes, opt) is JsonArray fdoc)
            {
                Console.WriteLine($"[file] games 条数={fdoc.Count}");
                foreach (var item in fdoc)
                    if (item is JsonObject fobj && Ji.Str(fobj, keyField) is { Length: > 0 } fcode) merged[fcode] = fobj;
            }
            else Console.WriteLine("[file] games 文件解析失败或不是数组，忽略本地文件");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[file] 读取失败 {ex.Message}");
            force = true;
        }
    }
}
//Console.WriteLine($"[force game dat] = {force}");


var lastSyncTicks = info.MiniDataSnapshot_LastSyncTicks;
if (force)
{
    // ===== 4) 快照 zip 下载到内存 =====
    byte[]? zipBytes = await Api.GetSnapshotZipAsync(hostUsed, info.MiniDataSnapshot_DownloadPath);//Consts.RouteSnapshot
    //Console.WriteLine($"[snapshot] zipBytes={zipBytes?.Length ?? 0}");
    if (zipBytes == null || zipBytes.Length == 0) { Console.WriteLine("[snapshot] 快照为空，退出。"); return; }

    // ===== 5) 内存解压 games.dat =====
    byte[] gamesData;
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
                if (JsonNode.Parse(syncJson.Trim().TrimStart('\uFEFF'), opt) is JsonObject sdoc)
                    lastSyncTicks = Ji.Long(sdoc, "LastSyncTicks");
            }
            catch { }
        }
    }
    Console.WriteLine($"[zip] games.dat bytes={gamesData.Length} lastSyncTicks={lastSyncTicks}");

    // ===== 6) 内存静态解密（games_dat_decrypt.cs 方法） =====
    byte[] plainBytes = GamesDat.DecryptToBytes(gamesData);
    string gamesJson = Encoding.UTF8.GetString(plainBytes);
    //Console.WriteLine($"[down] plainJsonBytes={plainBytes.Length}");

    if (JsonNode.Parse(gamesJson.Trim().TrimStart('\uFEFF'), opt) is not JsonArray garr)
        throw new InvalidDataException("games.dat 解密产物不是数组");
    Console.WriteLine($"[down] games 条数={garr.Count}");
    foreach (var item in garr)
        if (item is JsonObject dobj && Ji.Str(dobj, keyField) is { Length: > 0 } dcode) merged[dcode] = dobj;
}

// ===== 6.5) GetGamesAsync 获取最新游戏列表，与快照合并（接口数据覆盖/新增） =====

string latestGamesJson = await Api.GetGamesAsync(client, hostUsed, lastSyncTicks);
int covered = 0, added = 0, latestCount = 0;
var arr = ((JsonNode.Parse(latestGamesJson.Trim().TrimStart('\uFEFF'), opt) as JsonObject)?["games"] as JsonArray) ?? new JsonArray();
if (arr.Count == 0) Console.WriteLine("[merge] games 接口未返回 games 数组，跳过合并");
foreach (var item in arr)
{
    if (item is not JsonObject gobj || Ji.Str(gobj, keyField) is not { Length: > 0 } code) continue;
    latestCount++;
    if (merged.ContainsKey(code)) covered++; else added++;
    merged[code] = gobj;
}
Console.WriteLine($"[merge] 接口最新条数={latestCount} 覆盖={covered} 新增={added} 合并后总条数={merged.Count}");

// ===== 3) ensure token =====
var (token, _, ok) = await Api.EnsureAsync(client, hostUsed);
if (!ok || string.IsNullOrEmpty(token)) { Console.WriteLine("[ensure] 未获取到 token，退出。"); return; }
Console.WriteLine($"[ensure] token.Length = {token.Length}");

// ===== 7) passwords 接口 =====
string passwordsJson = await Api.GetPasswordsAsync(client, hostUsed, token);
var pwdMap = new Dictionary<string, JsonNode>(StringComparer.OrdinalIgnoreCase);
arr = ((JsonNode.Parse(passwordsJson.Trim().TrimStart('\uFEFF'), opt) as JsonObject)?["passwords"] as JsonArray) ?? new JsonArray();
if (arr.Count == 0) Console.WriteLine("[merge] passwords 接口未返回 passwords 数组，跳过合并");
foreach (var item in arr)
    if (item is JsonObject pwobj && Ji.Str(pwobj, keyField) is { Length: > 0 } pwcode) pwdMap[pwcode] = pwobj;
Console.WriteLine($"[passwords] 映射条数={pwdMap.Count}");

// ===== 8) 合成输出（按 GameCode 升序） =====
int matched = 0;
var outArr = new JsonArray();
foreach (var (code, mvalue) in merged.OrderBy(s => s.Key, StringComparer.OrdinalIgnoreCase))
{
    if (mvalue is not JsonObject game) continue;
    if (pwdMap.TryGetValue(code, out var pwd) && pwd is JsonObject pwdObj)
    {
        matched++;
        foreach (var (k, v) in pwdObj) game[k] = v?.DeepClone();
    }
    var si = new JsonObject(opt);
    foreach (var k in keepFields)
        if (game.TryGetPropertyValue(k, out var v) && v is { }) si.Add(k, v.DeepClone());
    outArr.Add((JsonNode)si);
}

var outOptions = new JsonSerializerOptions
{
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    WriteIndented = true
};
string outJson = DecodeUnicodeEscapes(outArr.ToJsonString(outOptions));
File.WriteAllBytes(outPath, Encoding.UTF8.GetBytes(outJson));
Console.WriteLine($"[out] 保存 {outPath} 条数={outArr.Count} 密码匹配={matched}");


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

//Console.WriteLine("[done]");

// ============ JsonNode 取值辅助：全部走 Consts.NodeOpt（不区分大小写），取不到返回空值不抛 ============
static class Ji
{
    static JsonNode? N(JsonNode? o, string name) => o is JsonObject obj ? obj[name] : null;

    public static string Str(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue(out string? s) ? s : "";

    public static string Str(JsonNode? o, string name) => Str(N(o, name));

    public static long Long(JsonNode? o, string name) =>
        N(o, name) is JsonValue v && v.TryGetValue(out long l) ? l : 0L;

    public static int Int(JsonNode? o, string name) =>
        N(o, name) is JsonValue v && v.TryGetValue(out int i) ? i : 0;

    public static bool Bool(JsonNode? o, string name) =>
        N(o, name) is JsonValue v && v.TryGetValue(out bool b) && b;
}

// ============ 常量区 ============
static class Consts
{
    public static readonly JsonNodeOptions NodeOpt = new() { PropertyNameCaseInsensitive = true };
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
    public const string BOOTSTRAP_B = "https://lkfv.oss-accelerate.aliyuncs.com/bootstrap-routes-v1.json";
    public static readonly string[] FallbackHosts =
    {
        "http://42.240.170.167:28531", "http://156.225.28.133",
        "http://38.76.194.86", "http://39.109.63.108", "http://39.109.63.45",
        "http://ad5fkruhhd.uk", "http://corvanda.com", "http://javelto.com",
        "http://jentrol.com", "http://varlemo.com"
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
        string b64 = Encoding.UTF8.GetString(step1).Trim();
        var ct = Convert.FromBase64String(b64);
        var key = Consts.ResponseAESKey;
        var plain = TryGzip(AesCbcDecrypt(key, ct));
        return Encoding.UTF8.GetString(plain);
    }

    public static List<string> DecryptBootstrapEnvelope(string json)
    {
        // bootstrap-routes-v1.json 信封：
        // {"v": 1,"k": "r1","z": 0,"d": "..." }
        if (JsonNode.Parse(json.Trim().TrimStart('\uFEFF'), Consts.NodeOpt) is not JsonObject root)
            throw new InvalidDataException("bootstrap 信封根节点不是对象");
        if (root["d"] is JsonValue dEl)
        {
            var ct = Convert.FromBase64String(Ji.Str(dEl));
            var keyName = Ji.Str(root, "k") is { Length: > 0 } kn ? kn : "r1";
            var key = Consts.Keys[keyName];
            var plain = Util.TryGzip(Util.AesCbcDecrypt(key, ct));
            if (Ji.Int(root, "z") == 1) plain = Util.TryGzip(plain);
            return ParseRouteArray(plain);
        }
        // 旧结构 { data: <base64 AES 密文> }
        if (root["data"] is JsonValue dataEl)
        {
            var ct = Convert.FromBase64String(Ji.Str(dataEl));
            var keyName = Ji.Str(root, "k") is { Length: > 0 } kn ? kn : "r1";
            var key = Consts.Keys[keyName];
            var plain = Util.TryGzip(Util.AesCbcDecrypt(key, ct));
            return ParseRouteArray(plain);
        }
        // 兜底：明文 { r: [...] } 或数组
        if (root["r"] is JsonArray r2)
        {
            var list = new List<string>();
            foreach (var item in r2) list.Add(Ji.Str(item));
            return list;
        }
        throw new InvalidDataException("bootstrap 信封解析失败");
    }

    static List<string> ParseRouteArray(byte[] plain)
    {
        var root = JsonNode.Parse(Encoding.UTF8.GetString(plain).Trim().TrimStart('\uFEFF'), Consts.NodeOpt);
        var list = new List<string>();
        if (root is JsonObject robj)
        {
            if (robj["r"] is JsonArray r)
                foreach (var item in r)
                {
                    string v = Ji.Str(item);
                    if (v.Length > 0) list.Add(v.StartsWith("http") ? v : "http://" + v);
                }
        }
        else if (root is JsonArray rarr)
        {
            foreach (var item in rarr)
            {
                string v = Ji.Str(item);
                if (v.Length > 0) list.Add(v);
            }
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
        using var gz = new GZipStream(new MemoryStream(plain), CompressionMode.Decompress);
        using var ms = new MemoryStream();
        gz.CopyTo(ms);
        return ms.ToArray();
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
            Console.WriteLine($"[bootstrap]: {string.Join(", ", routes)}");
            for (int i = 0; i < routes.Count; i++)
                if (!routes[i].StartsWith("http")) routes[i] = "http://" + routes[i];
            if (routes.Count > 0) return routes;
        }
        catch (Exception ex) { Console.WriteLine($"[bootstrap] 失败: {ex.Message}"); }
        return [.. Consts.FallbackHosts];
    }

    public sealed class StartupInfo
    {
        public string MiniLatestVersion = "";
        public List<string> ClientDownloadUrls = [];
        public string MiniUpdateFileName = "";
        public string MiniUpdateHash = "";
        public string MiniMirrorPath = "";
        public string MiniDataSnapshot_DownloadPath = "";
        public long MiniDataSnapshot_LastSyncTicks = 0L;
    }

    public static async Task<StartupInfo?> ProbeStartupAsync(HttpClient c, string host)
    {
        try
        {
            string path = Consts.RouteStartup;
            //long ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var req = new HttpRequestMessage(HttpMethod.Get, host + path);
            //req.Headers.TryAddWithoutValidation(Consts.HdrDistribution, "Normal");
            req.Headers.TryAddWithoutValidation(Consts.HdrEnc, "2");
            //req.Headers.TryAddWithoutValidation(Consts.HdrHmacTs, ts.ToString());
            //req.Headers.TryAddWithoutValidation(Consts.HdrHmacSig, Util.HmacSign("GET", path, ts, ""));
            using var resp = await c.SendAsync(req);
            byte[] raw = await resp.Content.ReadAsByteArrayAsync();
            // string encKeyHint = "startup-bootstrap-v1";
            // if (resp.Headers.TryGetValues(Consts.HdrEncKey, out var ev)) encKeyHint = string.Join(",", ev);
            //Console.WriteLine($"[startup] {resp.StatusCode} bodyLen={raw.Length} encKey={encKeyHint}");
            if (!resp.IsSuccessStatusCode) return null;

            // 响应解密：gzip → JSON | base64 → AES-CBC(startup-bootstrap-v1 key) → gzip
            byte[] step1 = Util.TryGzip(raw);
            string s1 = Encoding.UTF8.GetString(step1);
            if (!s1.TrimStart().StartsWith("{"))
            {
                string b64 = Encoding.ASCII.GetString(step1).Trim();
                var ct = Convert.FromBase64String(b64);
                var key = Consts.StartupAESKey; //Consts.Keys[encKeyHint];
                var plain = Util.TryGzip(Util.AesCbcDecrypt(key, ct));
                s1 = Encoding.UTF8.GetString(plain);
            }
            if (JsonNode.Parse(s1.Trim().TrimStart('\uFEFF'), Consts.NodeOpt) is not JsonObject sroot)
                throw new InvalidDataException("startup 响应不是 JSON 对象");
            var data = sroot["data"] as JsonObject ?? sroot;
            var info = new StartupInfo
            {
                MiniLatestVersion = Ji.Str(data, "toolkitMiniLatestVersion"),
                MiniUpdateFileName = Ji.Str(data, "miniUpdateFileName"),
                MiniUpdateHash = Ji.Str(data, "miniUpdateHash"),
                MiniMirrorPath = Ji.Str(data, "miniMirrorDownloadPath"),
            };
            if (data["clientDownloadUrls"] is JsonArray urls)
                foreach (var u in urls)
                {
                    string url = Ji.Str(u);
                    if (url.Length > 0) info.ClientDownloadUrls.Add(url);
                }
            if (data["miniDataSnapshot"] is JsonObject mds)
            {
                info.MiniDataSnapshot_DownloadPath = Ji.Str(mds, "downloadPath");
                info.MiniDataSnapshot_LastSyncTicks = Ji.Long(mds, "lastSyncTicks");
            }
            return info;
        }
        catch
        {
            return null;
        }
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
            ["InstallationIdHash"] = Convert.ToHexString(InstallationIdBytes),
            ["HardwareIdHash"] = Convert.ToHexString(HardwareIdHashBytes),
            ["ClientKind"] = "mini",
            ["ClientVersion"] = "1.1.4",
            ["Distribution"] = "Normal",
        };
        string body = bodyObj.ToJsonString();
        //Console.WriteLine(body);
        string bodyHash = Util.Sha256Hex(body);

        var req = new HttpRequestMessage(HttpMethod.Post, host + Consts.RouteEnsure)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        //req.Headers.TryAddWithoutValidation(Consts.HdrDistribution, "Normal");
        //req.Headers.TryAddWithoutValidation("X-Client-Envelope", "1");
        req.Headers.TryAddWithoutValidation(Consts.HdrEnc, "2");
        req.Headers.TryAddWithoutValidation(Consts.HdrHmacTs, ts.ToString());
        req.Headers.TryAddWithoutValidation(Consts.HdrHmacSig, Util.HmacSign("POST", Consts.RouteEnsure, ts, bodyHash));
        // if (!string.IsNullOrEmpty(oldToken))
        //     req.Headers.TryAddWithoutValidation(Consts.HdrEntitlement, oldToken);

        using var resp = await c.SendAsync(req);
        byte[] raw = await resp.Content.ReadAsByteArrayAsync();
        //Console.WriteLine($"[ensure] {resp.StatusCode} host={host}");
        string plain;
        try { plain = Util.DecryptResponseBody(raw); }
        catch { plain = Encoding.UTF8.GetString(Util.TryGzip(raw)); }
        var root = JsonNode.Parse(plain.Trim().TrimStart('\uFEFF'), Consts.NodeOpt) as JsonObject;
        bool success = Ji.Bool(root, "success");
        string token = Ji.Str(root, "entitlementToken");
        return (token, plain, success);
    }

    public static async Task<byte[]?> GetSnapshotZipAsync(string host, string path)
    {
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
                using var resp = await slow.SendAsync(req2);
                raw = await resp.Content.ReadAsByteArrayAsync();
                //Console.WriteLine($"[snapshot] 尝试{attempt} {resp.StatusCode} bodyLen={raw.Length}");
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

    public static async Task<string> GetGamesAsync(HttpClient c, string host, long since)
    {
        long ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string path = Consts.RouteGames; // 签名路径不含 query
        var req = new HttpRequestMessage(HttpMethod.Get, host + path + $"?since={since}");
        req.Headers.TryAddWithoutValidation(Consts.HdrEnc, "2");
        req.Headers.TryAddWithoutValidation(Consts.HdrHmacTs, ts.ToString());
        req.Headers.TryAddWithoutValidation(Consts.HdrHmacSig, Util.HmacSign("GET", path, ts, ""));

        using var resp = await c.SendAsync(req);
        byte[] raw = await resp.Content.ReadAsByteArrayAsync();
        //Console.WriteLine($"[new games] {resp.StatusCode} bodyLen={raw.Length}");
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
        //Console.WriteLine($"[passwords] {resp.StatusCode} bodyLen={raw.Length}");
        if (!resp.IsSuccessStatusCode)
        {
            Console.WriteLine($"[passwords] HTTP {(int)resp.StatusCode}，跳过合并");
            return "{}";
        }
        try { return Util.DecryptResponseBody(raw); }
        catch (Exception ex) { Console.WriteLine($"[passwords] 响应解密失败：{ex.Message}，跳过合并"); return "{}"; }
    }
}
