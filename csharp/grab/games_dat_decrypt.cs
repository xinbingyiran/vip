// games.dat 独立离线解密器（C# 单文件脚本，顶级语句）
// 用法: dotnet run games_dat_decrypt.cs <games.dat> [out.json]
// 不依赖客户端程序；密钥取自客户端程序集静态字段。
// 布局: [16B IV][AES-256-CBC 密文][32B HMAC-SHA256 校验尾]
// 解密 -> gzip -> UTF-8 JSON

using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

if (args.Length < 1)
{
    Console.Error.WriteLine("用法: dotnet run games_dat_decrypt.cs <games.dat> [out.json]");
    return 1;
}

byte[] AesKey =
{
    22,126,19,48,124,0,186,58,28,243,56,90,50,131,45,3,
    204,235,169,188,233,114,133,229,59,104,236,124,212,240,23,24,
};
byte[] HmacKey =
{
    183,89,170,239,224,189,46,211,170,101,76,248,237,150,50,117,
    90,180,249,44,159,241,164,252,90,221,18,119,225,11,4,177,
};

string datPath = args[0];
string outPath = args.Length > 1 ? args[1] : Path.ChangeExtension(datPath, ".decrypted.json");

byte[] data = File.ReadAllBytes(datPath);
if (data.Length < 48)
{
    Console.Error.WriteLine("文件过小，不像是 games.dat");
    return 1;
}

// 1) 尾部 32B = HMAC-SHA256(HmacKey, 前部)
byte[] body = data[..^32];
byte[] mac = data[^32..];
using (var h = new HMACSHA256(HmacKey))
{
    byte[] expect = h.ComputeHash(body);
    if (!CryptographicOperations.FixedTimeEquals(expect, mac))
    {
        Console.Error.WriteLine("HMAC 校验失败：文件被篡改或密钥不匹配（可能非本版本生成）");
        return 1;
    }
}

// 2) 前 16B = IV，其余 = 密文
byte[] iv = body[..16];
byte[] ciphertext = body[16..];
if (ciphertext.Length % 16 != 0)
{
    Console.Error.WriteLine("密文长度不是 16 的倍数，格式异常");
    return 1;
}

// 3) AES-256-CBC 解密
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

// 4) gzip 解压
byte[] jsonBytes;
using (var gz = new GZipStream(new MemoryStream(plain), CompressionMode.Decompress))
using (var ms = new MemoryStream())
{
    gz.CopyTo(ms);
    jsonBytes = ms.ToArray();
}

string json = Encoding.UTF8.GetString(jsonBytes);

// 原始 JSON 中非 ASCII 以 \uXXXX 转义存储，重新序列化并放开转义，输出可读中文。
var outOptions = new JsonSerializerOptions
{
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    WriteIndented = true,
    IndentSize = 2,
};
string outJson = JsonNode.Parse(json) is { } root
    ? root.ToJsonString(outOptions)
    : json;
// 序列化器仍可能把 nbsp / emoji 代理对等输出为 \uXXXX，再做一次完整反转义（含代理对配对）。
outJson = DecodeUnicodeEscapes(outJson);
File.WriteAllText(outPath, outJson, new UTF8Encoding(false));

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

int count = -1;
try
{
    using var doc = JsonDocument.Parse(jsonBytes);
    count = doc.RootElement.ValueKind == JsonValueKind.Array
        ? doc.RootElement.GetArrayLength()
        : doc.RootElement.EnumerateObject().Count();
}
catch { /* 非标准 JSON 数组时仅提示字节数 */ }

Console.WriteLine($"OK 已解密 {datPath} -> {outPath}");
Console.WriteLine(count >= 0 ? $"明文 {jsonBytes.Length} 字节，JSON 顶层条数: {count}" : $"明文 {jsonBytes.Length} 字节");
return 0;
