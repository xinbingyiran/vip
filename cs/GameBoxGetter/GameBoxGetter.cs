using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

Console.WriteLine($"当前程序目标：{AppContext.BaseDirectory}");
string[] urls = ["https://www.uy5.net", "http://103.39.221.38:8885"];
var client = new HttpClient();
var urlIndex = args.Length;
var url = urls[urlIndex % urls.Length];
Console.WriteLine($"当前地址：{url} - 绑定版本: 2.3.2");
try
{
    var updateinfo = await client.GetStringAsync($"{url}/gamebox/update.php");
    Console.WriteLine(JsonSerializer.Serialize(JsonSerializer.Deserialize(updateinfo, JsonElementContext.Custom.JsonElement), JsonElementContext.Custom.JsonElement));

}
catch (Exception ex)
{
    Console.WriteLine($"检测升级失败了：{ex.Message}");
    return;
}
foreach (var cat in (int[])[527, 5])
{
    Console.WriteLine($"当前类目： --------------------{cat}--------------------");
    var index = 0;
    var total = 1;
    var items = new ConcurrentDictionary<int, Dictionary<string, object>>();
    try
    {
        while (index++ < total)
        {
            var content = new StringContent($"action=get_posts_list&cat={cat}&newPage={index}&s={""}&ranking={"latest"}");
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded", "UTF-8");
            await content.LoadIntoBufferAsync();
            var postUrl = GetBoxHash(url);
            using var result = await client.PostAsync(postUrl, content, default);
            var str = await result.Content.ReadAsStringAsync(default);
            var resultItem = JsonSerializer.Deserialize(str, JsonElementContext.Custom.JsonElement);
            var list = resultItem.GetProperty("list");
            total = resultItem.TryGetProperty("all_page", out var value) && value.TryGetInt32(out var t) ? t : 0;
            var len = list.GetArrayLength();

            Console.WriteLine($"当前页：----------------[{len}] {index}  / {total}----------------");
            var tasks = list.EnumerateArray().Select(listi => AddListi(url, client, items, listi));
            await Task.WhenAll(tasks);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"获取发现异常：{ex.Message}");
    }
    var filename = Path.Combine(AppContext.BaseDirectory, $"GameBox_{cat}_All.json");
    File.WriteAllText(filename, JsonSerializer.Serialize(items.Values.OrderBy(s => s["post_title"].ToString()).ToArray(), JsonElementContext.Custom.Array));
    Console.WriteLine($"结果保存到以下文件：{filename}");
}

Console.WriteLine($"当前程序目标：{AppContext.BaseDirectory}");



static string GetBoxHash(string ApiUrl)
{
    //获取请求密钥
    //ApiUrl是请求的链接 比如 https://www.uy5.net/gamebox/
    // 更新要加密的数据
    var timeStamp = (long)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
    var url = $"{ApiUrl}/gamebox/?sign={timeStamp}-123456-0-{Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes($"/gamebox/-{timeStamp}-123456-0-WTCrudbtHLYDu4LZ"))).ToLower()}";
    return url;// 输出 MD5 哈希值
}


static async Task AddListi(string url, HttpClient client, ConcurrentDictionary<int, Dictionary<string, object>> items, JsonElement listi)
{
    var newItem = new Dictionary<string, object>();
    foreach (var p in listi.EnumerateObject())
    {
        newItem.TryAdd(p.Name, p.Value);
    }
    var id = int.Parse(newItem["id"].ToString() ?? string.Empty);
    var newContent = new StringContent($"action=GetFileInfo&get_post_id={id}");
    newContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded", "UTF-8");
    await newContent.LoadIntoBufferAsync();
    var newPostUrl = GetBoxHash(url);
    using var newResult = await client.PostAsync(newPostUrl, newContent, default);
    var str = await newResult.Content.ReadAsStringAsync(default);
    var element = JsonSerializer.Deserialize(str, JsonElementContext.Default.JsonElement);
    var fileInfo = element.GetProperty("GetFileInfo");
    if (fileInfo.ValueKind == JsonValueKind.Array && newItem.TryAdd("GetFileInfo", fileInfo) && items.TryAdd(id, newItem))
    {
        Console.WriteLine($"添加成功[{fileInfo.GetArrayLength()}]：{id} - {newItem["post_title"]}");
    }
    else
    {
        Console.WriteLine($"添加失败[{fileInfo.ValueKind}]：{id} - {newItem["post_title"]}");
    }
}

[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(Array))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(Dictionary<string, object>))]
internal partial class JsonElementContext : JsonSerializerContext
{
    private static readonly JsonSerializerOptions jsonOption = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
    };
    public static JsonElementContext Custom = new(jsonOption);
}