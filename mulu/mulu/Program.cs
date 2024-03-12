using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace mulu;
public class Program
{

    private static Encoding? Encoding;
    private static readonly ICryptoTransform des = DES.Create().CreateDecryptor(Encoding.UTF8.GetBytes("consmkey"), new byte[] { 18, 52, 86, 120, 144, 171, 205, 239 });
    private static readonly string h = "wg.MtbH&zvqS^!(d";
    private static readonly string c = "PTU18vZQEQka6CE2TOvwU/jsNrqWiGxPLm9u8oyznLFZbPOHTANpHM0yDKcB4J/bc6OS2RY2MuIKZS40b1ROZ+DbwG15s/m6ACXvz7L0Bnx5CyHuptfB+sR0JK3ljmsHlFDi2jh59r+pGNHho+st8AyS6ORMI/fNtTgOSUk8xTYHouvOdINg2EYdAuZaWttngunBnpawqYPmQQ+T0TYHtg==";
    private static readonly string f = "CWxXAmuJ0+QfAzurL4R8qZF5nUJq1YSyCO38gIlExuh66ZGv1k0De1yzZh13+agfe8oop8cS6UlsAK7DIHBbx8UMovijNBjiA/U2AuFNwbQ=";
    private static string? cstr;
    private static byte fb;
    private static readonly string zxxzdz1 = "https://m.189.ly93.cc/share/";
    private static readonly HttpClient _client = new();
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
    private static void Init()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Encoding = Encoding.GetEncoding("GBK");


        cstr = Decrypt(Decrypt(Decrypt(Decrypt(c))));
        fb = (byte)int.Parse(Decrypt(Decrypt(Decrypt(Decrypt(f)))));
        Console.WriteLine(cstr);
        Console.WriteLine(fb);

    }
    private static async Task jiancha(Ini ini)
    {
        try
        {
            string text = zxxzdz1 + "list";
            if ((await _client.GetAsync(text)).IsSuccessStatusCode)
            {
                var ZXXZ_To = ini.GetStringValue("C3", "ZaiXian_To", "WLCW");
                var toPath = ZXXZ_To + "To.txt";
                Console.WriteLine(toPath);
                string stringValue = Ini.ParseString(await _client.GetStringAsync(toPath)).GetStringValue("To", "On1", "WLCW");
                Console.WriteLine("------------------------------------------------------------------------");
                //Console.WriteLine(stringValue);
                await File.WriteAllTextAsync("to.txt", stringValue);
                Console.WriteLine("------------------------------------------------------------------------");
            }
        }
        catch (HttpRequestException)
        {
        }
    }

    public static async Task huoquyx(Game game, string tt1a002A)
    {
        var ini1z = Ini.ParseString(await DecodecAsync($"{tt1a002A}{game.Code}.txt", fb));
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

    public static async Task Main()
    {
        var serial = new SYS(new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        Init();

        Console.WriteLine(cstr + "a/a.txt");
        var str = await DecodecAsync(cstr + "a/a.txt", fb);
        Console.WriteLine("------------------------------------------------------------------------");
        await File.WriteAllTextAsync("a.txt", str);

        //Console.WriteLine(str);
        Console.WriteLine("------------------------------------------------------------------------");

        Ini ini = Ini.ParseString(str);
        var tt1a001 = ini.GetStringValue("zhu", "wj", "WLCW");
        var tt1a002A = ini.GetStringValue("zhu", "shuju1", "WLCW");
        await jiancha(ini);

        var wjurl = tt1a001 + "WenJian.json";
        Console.WriteLine(wjurl);
        var wjtext = await _client.GetStringAsync(wjurl);
        Console.WriteLine("------------------------------------------------------------------------");
        var ct = JsonSerializer.Deserialize(wjtext, serial.Sys_content_version)!;
        var datas = ct.Content!.Select(p =>
            new Game
            {
                Code = DESDecrypt(p.BH),
                MMSS = DESDecrypt(p.MM),
                Describe = DESDecrypt(p.Name2),
                Name = DESDecrypt(p.Name1),
                RLzz = DESDecrypt(p.RongL),
                Types = DESDecrypt(p.BiaoQ)
            }).OrderBy(s=>s.Code).ToArray();
        await File.WriteAllTextAsync("Game.json", JsonSerializer.Serialize(datas, serial.GameArray));
        Console.WriteLine("------------------------------------------------------------------------");

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
                    await huoquyx(g, tt1a002A);
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
        await File.WriteAllTextAsync("GameAll.json", JsonSerializer.Serialize(datas, serial.GameArray));
        Console.WriteLine("------------------------------------------------------------------------");
    }
}