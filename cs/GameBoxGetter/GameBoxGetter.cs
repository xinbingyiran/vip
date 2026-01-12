using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

Console.WriteLine($"当前程序目标：{AppContext.BaseDirectory}");
string[] urls = ["https://www.uy5.net", "http://103.39.221.38:8885"];
var client = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(5)
};
var check = await WithRetry(async (i, token) =>
{
    var url = urls[i];
    Console.WriteLine($"当前地址：{url} - 绑定版本: 2.3.2");
    try
    {
        var updateinfo = await client.GetStringAsync($"{url}/gamebox/update.php");
        Console.WriteLine(JsonSerializer.Serialize(JsonSerializer.Deserialize(updateinfo, JsonElementContext.Custom.JsonElement), JsonElementContext.Custom.JsonElement));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[{url}]检测升级失败了：{ex.Message}");
        return false;
    }
    int[] cats = [527, 5];
    for (var ci = 0; ci < cats.Length; ci++)
    {
        var cat = cats[ci];
        Console.WriteLine($"当前类目： --------------------{cat}--------------------");
        var index = 0;
        var total = 1;
        var items = new ConcurrentDictionary<int, Dictionary<string, object>>();
        while (index++ < total)
        {
            var result = await WithRetry(async (i, token) =>
            {
                try
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
                    if (len <= 0)
                    {
                        return false;
                    }
                    Console.WriteLine($"当前页：----------------[{len}] {index}  / {total}  {cat}----------------");
                    var tasks = list.EnumerateArray().Select(listi => AddListi(url, client, items, listi));
                    var rarray = await Task.WhenAll(tasks);
                    return rarray.All(s => s);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"获取当前页：{index}  / {total}  {cat}【异常：{ex.Message}】");
                    return false;
                }
            }, 3, default);
            if (!result)
            {
                break;
            }
        }
        if (items.IsEmpty)
        {
            Console.WriteLine($"未获取到内容：{cat}");
            return false;
        }
        var filename = Path.Combine(AppContext.BaseDirectory, $"GameBox_{cat}_All.json");
        File.WriteAllText(filename, JsonSerializer.Serialize(items.Values.OrderBy(s => s["post_title"].ToString()).ToArray(), JsonElementContext.Custom.Array));
        Console.WriteLine($"结果保存到以下文件：{filename}");
    }
    return true;
}, urls.Length, default);

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


static async Task<bool> WithRetry(Func<int, CancellationToken, Task<bool>> task, int retryTimes, CancellationToken token)
{
    for (var i = 0; i < retryTimes; i++)
    {
        if (await task.Invoke(i, token))
        {
            return true;
        }
    }
    return false;
}


static async Task<bool> AddListi(string url, HttpClient client, ConcurrentDictionary<int, Dictionary<string, object>> items, JsonElement listi)
{
    var newItem = new Dictionary<string, object>();
    foreach (var p in listi.EnumerateObject())
    {
        newItem.TryAdd(p.Name, p.Value);
    }
    var id = int.Parse(newItem["id"].ToString() ?? string.Empty);
    return await WithRetry(async (i, token) =>
    {
        try
        {
            var newContent = new StringContent($"action=GetFileInfo&get_post_id={id}");
            newContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded", "UTF-8");
            await newContent.LoadIntoBufferAsync();
            var newPostUrl = GetBoxHash(url);
            using var newResult = await client.PostAsync(newPostUrl, newContent, token);
            var str = await newResult.Content.ReadAsStringAsync(default);
            var element = JsonSerializer.Deserialize(str, JsonElementContext.Default.JsonElement);
            var fileInfo = element.GetProperty("GetFileInfo");
            if (fileInfo.ValueKind == JsonValueKind.Array)
            {
                var alen = fileInfo.GetArrayLength();
                if (alen > 0)
                {
                    newItem["GetFileInfo"] = fileInfo;
                    if (items.TryAdd(id, newItem))
                    {
                        Console.WriteLine($"{id} - {newItem["post_title"]}【{alen}已添加！】");
                    }
                    else
                    {
                        Console.WriteLine($"{id} - {newItem["post_title"]}【ID重复！】");
                    }
                }
                else
                {
                    Console.WriteLine($"{id} - {newItem["post_title"]}【空数据！】");
                }
                return true;
            }
            else if (fileInfo.ValueKind == JsonValueKind.Null)
            {
                Console.WriteLine($"{id} - {newItem["post_title"]}【无数据！】");
                return true;
            }
            else
            {
                Console.WriteLine($"{id} - {newItem["post_title"]}【无效数据！】");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{id} - {newItem["post_title"]}【异常{ex.Message}】");
            return false;
        }
    }, 3, default);
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