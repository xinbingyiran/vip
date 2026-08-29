// 单文件版 mulu：游戏目录抓取与 SteamUI 解密工具
// 用法:
//   dotnet run mulu.cs                # 程序目录存在 SteamUI 文件时解密它；否则获取游戏列表(GameAll.json)
//   dotnet run mulu.cs <SteamUI路径>  # 解密指定 SteamUI 文件并打印激活码/解压密码
// 说明: 由 mulu 项目(Program.cs + Classes.cs)合并为单文件顶级语句脚本；TargetFramework / ImplicitUsings / Nullable / LangVersion 均使用 file-based app 默认值
#:package System.Text.Encoding.CodePages@6.0.0

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

Console.WriteLine("------------------## 开始 ##--------------------");
try
{
    ProgramHelper.Init();
    var uifile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SteamUI");
    if (args.Length == 1 && args[0].EndsWith("SteamUI", StringComparison.OrdinalIgnoreCase))
    {
        await ProgramHelper.DecryptStreamUIAsync(args[0]);
    }
    else if (File.Exists(uifile))
    {
        await ProgramHelper.DecryptStreamUIAsync(uifile);
    }
    else
    {
        Console.WriteLine("未发现SteamUI，任意键 获取列表");
        Console.ReadKey();
        await ProgramHelper.DownListAsync();
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"执行出错：{ex.Message}");
}
Console.WriteLine("------------------## 结束 ##--------------------");
Console.ReadKey();

public static partial class ProgramHelper
{

    [GeneratedRegex("[^0-9]+")]
    private static partial Regex NonNumberRegex();

    private static Encoding? Encoding;
    private static readonly ICryptoTransform des = DES.Create().CreateDecryptor(Encoding.UTF8.GetBytes("consmkey"), [18, 52, 86, 120, 144, 171, 205, 239]);
    private static readonly string h = "wg.MtbH&zvqS^!(d";
    private static readonly string c = "PTU18vZQEQka6CE2TOvwU/jsNrqWiGxPLm9u8oyznLFZbPOHTANpHM0yDKcB4J/bc6OS2RY2MuIKZS40b1ROZ+DbwG15s/m6ACXvz7L0Bnx5CyHuptfB+sR0JK3ljmsHlFDi2jh59r+pGNHho+st8AyS6ORMI/fNtTgOSUk8xTYHouvOdINg2EYdAuZaWttngunBnpawqYPmQQ+T0TYHtg==";
    private static readonly string f = "CWxXAmuJ0+QfAzurL4R8qZF5nUJq1YSyCO38gIlExuh66ZGv1k0De1yzZh13+agfe8oop8cS6UlsAK7DIHBbx8UMovijNBjiA/U2AuFNwbQ=";
    private static string? cstr;
    private static byte fb;
    private static readonly HttpClient _client = new();
    private static readonly SYS _sys = new(new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    });

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

    public static async Task<string> DecodecAsync(string path, byte password)
    {
        string result = string.Empty;
        try
        {
            byte[] bytes;
            if (path.StartsWith("http"))
            {
                bytes = await _client.GetByteArrayAsync(path);
            }
            else
            {
                bytes = await File.ReadAllBytesAsync(path);
            }
            result = Encoding!.GetString(bytes.Select(e => (byte)(e ^ password)).ToArray());
        }
        catch (Exception ex)
        {
            Console.WriteLine("解密失败: " + ex.Message);
        }
        return result;
    }

    public static void Init()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Encoding = Encoding.GetEncoding("GBK");

        cstr = Decrypt(Decrypt(Decrypt(Decrypt(c))));
        fb = (byte)int.Parse(Decrypt(Decrypt(Decrypt(Decrypt(f)))));

    }

    public static async Task SyncGameAsync(Game game, string tt1a002A)
    {
        var filecode = await DecodecAsync($"{tt1a002A}{game.Code}.txt", fb);
        var ini1z = Ini.ParseString(filecode);
        string yxbb = ini1z.GetStringValue("zhu", "yxbb", "WLCW");
        game.YXBB = yxbb == "WLCW" ? null : yxbb;
        string xiazai1_ming = ini1z.GetStringValue("xiazai", "xiazai1_ming", "WLCW");
        string xiazai1_dizhi = ini1z.GetStringValue("xiazai", "xiazai1_dizhi", "WLCW");
        string xiazai1_fwm = ini1z.GetStringValue("xiazai", "xiazai1_fwm", "WLCW");
        game.Addr1 = xiazai1_ming == "WLCW" ? null : $"{xiazai1_ming}: {xiazai1_dizhi}{(xiazai1_fwm == "WLCW" ? "" : $"#{xiazai1_fwm}")}";
        string xiazai2_ming = ini1z.GetStringValue("xiazai", "xiazai2_ming", "WLCW");
        string xiazai2_dizhi = ini1z.GetStringValue("xiazai", "xiazai2_dizhi", "WLCW");
        string xiazai2_fwm = ini1z.GetStringValue("xiazai", "xiazai2_fwm", "WLCW");
        game.Addr2 = xiazai2_ming == "WLCW" ? null : $"{xiazai2_ming}: {xiazai2_dizhi}{(xiazai2_fwm == "WLCW" ? "" : $"#{xiazai2_fwm}")}";
        string xiazai3_ming = ini1z.GetStringValue("xiazai", "xiazai3_ming", "WLCW");
        string xiazai3_dizhi = ini1z.GetStringValue("xiazai", "xiazai3_dizhi", "WLCW");
        string xiazai3_fwm = ini1z.GetStringValue("xiazai", "xiazai3_fwm", "WLCW");
        game.Addr3 = xiazai3_ming == "WLCW" ? null : $"{xiazai3_ming}: {xiazai3_dizhi}{(xiazai3_fwm == "WLCW" ? "" : $"#{xiazai3_fwm}")}";
    }

    private static string SHA1_Encrypt(string str) => BitConverter.ToString(SHA1.HashData(Encoding.Default.GetBytes(str))).Replace("-", "");

    public static async Task DecryptStreamUIAsync(string file)
    {
        Console.WriteLine("## 解密SteamUI文件 ##");
        Console.WriteLine(file);
        var str = await DecodecAsync(file, fb);
        Console.WriteLine("------------------------------------------------------------------------");
        await File.WriteAllTextAsync($"{file}.ini", str);
        Console.WriteLine("------------------------------------------------------------------------");

        var ini = Ini.ParseString(str);
        var MiMa__ = ini.GetStringValue("zhu", "mm", "WLCW");

        var km = NonNumberRegex().Replace(SHA1_Encrypt(MiMa__ + DateTime.Now.ToString("yyyyMM")) + "123456", "")[..6];

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine($"data_steam 解压密码： {MiMa__}");
        Console.WriteLine($"激活码： {MiMa__}");
        Console.WriteLine($"激活码： {km}");
        Console.ResetColor();
        Console.WriteLine();

        Console.WriteLine("------------------------------------------------------------------------");

        return;
    }

    public static async Task DownListAsync()
    {
        Console.WriteLine("##  获取列表  ##");

        Console.WriteLine(cstr + "a/a.txt");
        var str = await DecodecAsync(cstr + "a/a.txt", fb);
        Console.WriteLine("------------------------------------------------------------------------");

        Console.WriteLine(str);
        Console.WriteLine("------------------------------------------------------------------------");

        Ini ini = Ini.ParseString(str);
        var tt1a001 = ini.GetStringValue("zhu", "wj", "WLCW");
        var tt1a002A = ini.GetStringValue("zhu", "shuju1", "WLCW");

        var wjurl = tt1a001 + "WenJian.json";
        Console.WriteLine(wjurl);
        var wjtext = await _client.GetStringAsync(wjurl);
        var ct = JsonSerializer.Deserialize(wjtext, _sys.Sys_content_version)!;
        var datas = ct.Content!.Select(p =>
            new Game
            {
                Code = DESDecrypt(p.BH),
                MMSS = DESDecrypt(p.MM),
                Describe = DESDecrypt(p.Name2),
                Name = DESDecrypt(p.Name1),
                RLzz = DESDecrypt(p.RongL),
                Types = DESDecrypt(p.BiaoQ)
            }).OrderBy(s => s.Code).ToArray();
        var l = datas.Length;
        var i = 0;
        var time = DateTime.Now;
        var suc = 0;
        var fai = 0;
        await Parallel.ForEachAsync(datas, async (g, t) =>
        {
            var retry = 0;
            var c = Interlocked.Increment(ref i);
            while (retry < 3)
            {
                retry++;
                try
                {
                    await SyncGameAsync(g, tt1a002A);
                }
                catch
                {
                }
                if (!string.IsNullOrEmpty(g.Addr1) || !string.IsNullOrEmpty(g.Addr2) || !string.IsNullOrEmpty(g.Addr3))
                {
                    break;
                }
            }
            if (!string.IsNullOrEmpty(g.Addr1) || !string.IsNullOrEmpty(g.Addr2) || !string.IsNullOrEmpty(g.Addr3))
            {
                Interlocked.Increment(ref suc);
            }
            else
            {
                Interlocked.Increment(ref fai);
            }
            if (DateTime.Now - time > TimeSpan.FromSeconds(3))
            {
                time = DateTime.Now;
                Console.WriteLine($"链接获取：获取成功 {suc} / {l} ,失败 {fai} / {l} 。");
            }
        });
        Console.WriteLine($"获取完成：获取成功 {suc} / {l} ,失败 {fai} / {l} 。");
        await File.WriteAllTextAsync("GameAll.json", JsonSerializer.Serialize(datas, _sys.GameArray));
        Console.WriteLine("------------------------------------------------------------------------");
    }
}

public class Game
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? MMSS { get; set; }
    public string? Describe { get; set; }
    public string? RLzz { get; set; }
    public string? Types { get; set; }
    public string? YXBB { get; set; }
    public string? Addr1 { get; set; }
    public string? Addr2 { get; set; }
    public string? Addr3 { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Sys_content_version))]
[JsonSerializable(typeof(Sys_content_more))]
[JsonSerializable(typeof(Game[]))]
internal partial class SYS : JsonSerializerContext
{
}

public class Sys_content_more
{
    public long Id { get; set; }

    public string? BH { get; set; }

    public string? MM { get; set; }

    public string? Name1 { get; set; }

    public string? Name2 { get; set; }

    public string? BiaoQ { get; set; }

    public string? XingJ { get; set; }

    public string? MageA { get; set; }

    public string? RongL { get; set; }

    public string? RiQ { get; set; }

    public string? Category { get; set; }
}

public class Sys_content_version
{
    public string? Version { get; set; }

    public Sys_content_more[]? Content { get; set; }
}

public class Ini
{

    public static Encoding Encoding { get; set; } = Encoding.GetEncoding("gbk");

    public Dictionary<string, Dictionary<string, object>> Data { get; set; } = new();

    public static Ini ParseString(string context)
    {
        Ini ini = new();
        var array = context.Split('\n');
        string? text = null;
        foreach (string? text2 in array)
        {
            if (string.IsNullOrEmpty(text2))
            {
                continue;
            }
            string text3 = text2.Trim();
            if (IsAnnotation(text3))
            {
                continue;
            }
            if (text3.StartsWith('[') && text3.EndsWith(']') && text3.Length >= 3)
            {
                text = text3[1..^1];
                continue;
            }
            int num = text3.IndexOf('=');
            if (num > 0 && num + 1 < text3.Length && text != null)
            {
                string key = text3[..num].Trim();
                string value = text3[(num + 1)..].Trim();
                ini.Add(text, key, value);
            }
        }
        return ini;
    }

    private string? GetString(string section, string key)
    {
        try
        {
            return Data[section][key].ToString();
        }
        catch
        {
            return null;
        }
    }

    public string GetStringValue(string section, string key, string defaultValue = "")
    {
        string? text = GetString(section, key);
        if (string.IsNullOrEmpty(text))
        {
            text = defaultValue;
        }
        return text;
    }

    public bool GetBooleanValue(string section, string key, bool defaultValue)
    {
        if (bool.TryParse(GetString(section, key), out var result))
        {
            return result;
        }
        return defaultValue;
    }

    public int GetIntegerValue(string section, string key, int defaultValue = 0)
    {
        if (int.TryParse(GetString(section, key), out var result))
        {
            return result;
        }
        return defaultValue;
    }

    public double GetDoubleValue(string section, string key, double defaultValue = 0.0)
    {
        if (double.TryParse(GetString(section, key), out var result))
        {
            return result;
        }
        return defaultValue;
    }

    public float GetFloatValue(string section, string key, float defaultValue = 0f)
    {
        if (float.TryParse(GetString(section, key), out var result))
        {
            return result;
        }
        return defaultValue;
    }

    public short GetShortValue(string section, string key, short defaultValue = 0)
    {
        if (short.TryParse(GetString(section, key), out var result))
        {
            return result;
        }
        return defaultValue;
    }

    public long GetLongValue(string section, string key, long defaultValue = 0L)
    {
        if (long.TryParse(GetString(section, key), out var result))
        {
            return result;
        }
        return defaultValue;
    }

    public byte GetByteValue(string section, string key, byte defaultValue = 0)
    {
        if (byte.TryParse(GetString(section, key), out var result))
        {
            return result;
        }
        return defaultValue;
    }

    private static bool IsAnnotation(string line)
    {
        if (line != null && line.Length != 0 && !line.StartsWith(';'))
        {
            return line.StartsWith('#');
        }
        return true;
    }

    public void Add(string section, string key, object value)
    {
        if (!Data.ContainsKey(section) || Data[section] == null)
        {
            Data[section] = new Dictionary<string, object>();
        }
        if (value != null && section != null && key != null)
        {
            Data[section][key] = value;
        }
    }

    public override string ToString()
    {
        StringBuilder stringBuilder = new StringBuilder();
        foreach (KeyValuePair<string, Dictionary<string, object>> datum in Data)
        {
            stringBuilder.Append("[").Append(datum.Key).Append("]\r\n");
            foreach (KeyValuePair<string, object> item in datum.Value)
            {
                stringBuilder.Append(item.Key).Append(" = ").Append(item.Value)
                    .Append("\r\n");
            }
            stringBuilder.Append("\r\n");
        }
        return stringBuilder.ToString();
    }
}
