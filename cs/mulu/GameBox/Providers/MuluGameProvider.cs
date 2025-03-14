using System.IO;
using System.Net.Http;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace GameBox.Providers;

class MuluGameProvider : WebGameProvider
{
    private static Encoding? Encoding;
    private static readonly ICryptoTransform des = DES.Create().CreateDecryptor(Encoding.UTF8.GetBytes("consmkey"), [18, 52, 86, 120, 144, 171, 205, 239]);
    private static readonly string h = "wg.MtbH&zvqS^!(d";
    private static readonly string c = "PTU18vZQEQka6CE2TOvwU/jsNrqWiGxPLm9u8oyznLFZbPOHTANpHM0yDKcB4J/bc6OS2RY2MuIKZS40b1ROZ+DbwG15s/m6ACXvz7L0Bnx5CyHuptfB+sR0JK3ljmsHlFDi2jh59r+pGNHho+st8AyS6ORMI/fNtTgOSUk8xTYHouvOdINg2EYdAuZaWttngunBnpawqYPmQQ+T0TYHtg==";
    private static readonly string f = "CWxXAmuJ0+QfAzurL4R8qZF5nUJq1YSyCO38gIlExuh66ZGv1k0De1yzZh13+agfe8oop8cS6UlsAK7DIHBbx8UMovijNBjiA/U2AuFNwbQ=";
    private static string? cstr;
    private static byte fb;
    //private static readonly string zxxzdz1 = "https://m.189.ly93.cc/share/";
    private static string SHA1_Encrypt(string str) => BitConverter.ToString(SHA1.HashData(Encoding.Default.GetBytes(str))).Replace("-", "");
    private static string Decrypt(string toDecrypt)
    {
        if (string.IsNullOrEmpty(toDecrypt))
        {
            return toDecrypt;
        }
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(h);
            byte[] array = Convert.FromBase64String(toDecrypt);
            var aes = Aes.Create();
            aes.Key = bytes;
            var bytes2 = aes.DecryptEcb(array, PaddingMode.PKCS7);
            return Encoding.UTF8.GetString(bytes2);
        }
        catch (Exception)
        {
            return toDecrypt;
        }
    }
    public static string? DESDecrypt(string? decryptString)
    {
        if (string.IsNullOrEmpty(decryptString))
        {
            return decryptString;
        }
        try
        {
            byte[] array = Convert.FromBase64String(decryptString);
            using var ms = new MemoryStream();
            using var cs = new CryptoStream(ms, des, CryptoStreamMode.Write);
            cs.Write(array, 0, array.Length);
            cs.FlushFinalBlock();
            return Encoding.UTF8.GetString(ms.ToArray());
        }
        catch
        {
            return decryptString;
        }
    }
    public static async Task<string> DecodecAsync(string path, byte password, CancellationToken token)
    {
        string result = string.Empty;
        try
        {
            byte[] bytes;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(5000);
            if (path.StartsWith("http"))
            {
                bytes = await DefaultClient.GetByteArrayAsync(path, cts.Token);
            }
            else
            {
                bytes = await File.ReadAllBytesAsync(path, cts.Token);
            }
            result = Encoding!.GetString(bytes.Select(e => (byte)(e ^ password)).ToArray());
        }
        catch (Exception ex)
        {
            Console.WriteLine("解密失败: " + ex.Message);
        }
        return result;
    }
    public override string Name => "目录合集";
    private static Game[]? AllGames;
    private static DateTime _asyncTime = DateTime.MinValue;

    private static string? tt1a001;

    private static string? tt1a002A;
    private static string? apiurl;
    private static async Task AsyncAllGamesAsync(Action<string> logger, CancellationToken token)
    {
        if (AllGames is not null && _asyncTime.AddHours(1) > DateTime.Now)
        {
            return;
        }
        if (Encoding is null)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Encoding = Encoding.GetEncoding("GBK");
            cstr = Decrypt(Decrypt(Decrypt(Decrypt(c))));
            fb = (byte)int.Parse(Decrypt(Decrypt(Decrypt(Decrypt(f)))));
        }
        logger("正在初始化参数。。。");
        var str = await DecodecAsync(cstr + "a/a.txt", fb, token);
        //Console.WriteLine("------------------------------------------------------------------------");
        //await File.WriteAllTextAsync("a.txt", str);

        //Console.WriteLine(str);
        //Console.WriteLine("------------------------------------------------------------------------");

        var dict = ParseIni(str);

        dict.TryGetValue(("zhu", "wj"), out tt1a001);
        if (tt1a001 is null)
        {
            throw new Exception("未获取zhu-wj");
        }
        dict.TryGetValue(("zhu", "shuju1"), out tt1a002A);
        if (tt1a001 is null)
        {
            throw new Exception("未获取zhu-shuju1");
        }
        dict.TryGetValue(("C3", "ZaiXian_DiZhi"), out apiurl);
        if (tt1a001 is null)
        {
            throw new Exception("未获取C3-ZaiXian_DiZhi");
        }
        var wjurl = tt1a001 + "WenJian.json";
        //Console.WriteLine(wjurl);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(3000);
        logger("正在获取列表。。。");
        var wjtext = await DefaultClient.GetStringAsync(wjurl, cts.Token);
        var ct = JsonSerializer.Deserialize<JsonElement>(wjtext);
        if (ct.ValueKind is not JsonValueKind.Object || !ct.TryGetProperty("Content", out var content) || content.ValueKind is not JsonValueKind.Array)
        {
            throw new Exception("未找到Content");
        }
        var games = new List<Game>();
        logger("解析中。。。");
        foreach (var obj in content.EnumerateArray())
        {
            if (obj.ValueKind is not JsonValueKind.Object)
            {
                continue;
            }
            games.Add(CreateGame(obj));
        }
        AllGames = [.. games];
        _asyncTime = DateTime.Now;
    }
    private static Game CreateGame(JsonElement obj)
    {
        var dict = new Dictionary<string, string>();
        return new Game
        {
            Id = DESDecrypt($"{obj.GetProperty("BH")}")!,
            Name = DESDecrypt($"{obj.GetProperty("Name1")}")!,
            Ext = dict,
            InfoGetter = CreateInfoGetter(obj, dict),
            UrlGetter = CreateUrlGetter(obj, dict)
        };
    }
    static Func<Action<string>, CancellationToken, Task<GameInfo[]>> CreateInfoGetter(JsonElement obj, Dictionary<string, string> gameDict)
    {
        GameInfo[]? cacheResult = null;
        return async (logger, tk) =>
        {
            if (cacheResult is null)
            {
                foreach (var item in obj.EnumerateObject())
                {
                    gameDict[$"{item.Name}"] = $"{DESDecrypt($"{item.Value}")}";
                }
                logger("正在更新。。。");
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(tk);
                cts.CancelAfter(3000);
                var filecode = await DecodecAsync($"{tt1a002A}{DESDecrypt($"{obj.GetProperty("BH")}")}.txt", fb, cts.Token);
                var ini1z = ParseIni(filecode);
                foreach (var (k, v) in ini1z)
                {
                    gameDict[$"{(k.Item1 == "zhu" ? k.Item2 : k)}"] = v;
                }
                cacheResult = [.. gameDict.Select(e => new GameInfo(e.Key, e.Value))];
            }
            return cacheResult;
        };
    }
    static Func<Action<string>, CancellationToken, Task<GameFileInfo[]>> CreateUrlGetter(JsonElement _, Dictionary<string, string> gameDict)
    {
        GameFileInfo[]? cacheResult = null;
        return async (logger, tk) =>
        {
            if (cacheResult is null)
            {
                if (!gameDict.TryGetValue($"{("xiazai", "xiazai2_dizhi")}", out var dizi) || !gameDict.TryGetValue($"{("xiazai", "xiazai2_fwm")}", out var fwm))
                {
                    throw new KeyNotFoundException("未找到下载地址！");
                }
                var code = dizi[(dizi.LastIndexOf('/') + 1)..];
                var sharePara = $"code={code}";
                if (!string.IsNullOrEmpty(fwm) && fwm != "WLCW")
                {
                    sharePara += $"&pwd={fwm}";
                }
                var url = apiurl;
                logger("正在获取。。。");
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(tk);
                cts.CancelAfter(3000);
                var result = await DefaultClient.GetStringAsync($"{url}/share/list?{sharePara}", cts.Token);
                var idUrl = $"{url}/share/url?{sharePara}&id=";
                var element = JsonSerializer.Deserialize<JsonElement>(result);
                var rootItems = GetItems(element, FileUrlGetter);
                string FileUrlGetter(string id)
                {
                    return idUrl + id;
                }

                cacheResult = rootItems;
            }
            return cacheResult;
        };
    }

    private static Dictionary<(string, string), string> ParseIni(string str)
    {
        Dictionary<(string, string), string> dict = [];
        string prefix = string.Empty;
        foreach (var line in str.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var validLine = line.Trim();
            if (validLine.Length > 1 && validLine[0] == '[' && validLine[^1] == ']')
            {
                prefix = validLine[1..^1];
                continue;
            }
            var kv = validLine.Split('=', 2);
            if (kv.Length != 2)
            {
                continue;
            }
            dict[(prefix, kv[0].Trim())] = kv[1].Trim();
        }

        return dict;
    }

    private static GameFileInfo[] GetItems(JsonElement element, Func<string, string> urlGetter)
    {
        if (element.TryGetProperty("code", out var code))
        {
            var codeStr = code.GetRawText();
            if (codeStr != "0")
            {
                throw new Exception($"【{codeStr}】：{(element.TryGetProperty("msg", out var msg) ? msg.GetString() : "未知错误")}");
            }
            else if (element.TryGetProperty("data", out var newElement))
            {
                element = newElement;
            }
            else
            {
                throw new KeyNotFoundException("未找到有效数据data");
            }
        }
        if (element.ValueKind == JsonValueKind.Array)
        {
            return [.. element.EnumerateArray().SelectMany(e => GetItemCore(e, "", urlGetter))];
        }
        else
        {
            return GetItemCore(element, "", urlGetter);
        }
    }
    private static GameFileInfo[] GetItemCore(JsonElement element, string path, Func<string, string> urlGetter)
    {
        var name = element.GetProperty("filename").GetString() ?? "";
        var id = element.GetProperty("id").GetRawText().Trim('\"');
        var folder = element.GetProperty("isFolder").GetBoolean();
        return folder ? [.. (element.TryGetProperty("children", out var children) ? children.EnumerateArray() : []).SelectMany(e => GetItemCore(e, Path.Combine(path, name), urlGetter))]
         : [new GameFileInfo(path, name, urlGetter.Invoke(id))];
    }

    public override async Task<Game[]> SearchGame(Action<string> logger, string name, CancellationToken token)
    {
        await AsyncAllGamesAsync(logger, token);
        ArgumentNullException.ThrowIfNull(AllGames);
        Game[] result = [.. AllGames.Where(s => s.Name is string gname && gname.Contains(name))];
        logger($"搜索到 {result.Length} / {AllGames.Length}");
        return result;
    }
}