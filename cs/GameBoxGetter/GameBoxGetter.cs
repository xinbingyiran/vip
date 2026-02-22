using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

//string sourceString = """
//POST /gamebox/?sign=1771728550-123456-0-1b0396510da13032d708263fd9b332b5 HTTP/1.1
//Host: www.uy5.net
//Connection: keep-alive
//Content-Length: 19
//Accept: application/json, text/plain, */*
//Accept-Encoding: gzip, deflate, br
//Accept-Language: zh-CN
//Content-Type: application/x-www-form-urlencoded
//Cookie: session_prefix=66469a353374a83f19abd2250f550719
//Referer: https://www.uy5.net
//Sec-Fetch-Dest: empty
//Sec-Fetch-Mode: cors
//Sec-Fetch-Site: cross-site
//User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) gamebox/2.3.2 Chrome/91.0.4472.164 Electron/13.6.9 Safari/537.36

//action=announcement
//""";


string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) gamebox/2.3.2 Chrome/91.0.4472.164 Electron/13.6.9 Safari/537.36";
var cd = Environment.CurrentDirectory;
Console.WriteLine($"当前目录：{cd}");
string[] urls = ["https://www.uy5.net", "http://103.39.221.38:8885"];
var client = new HttpClient()
{
    Timeout = TimeSpan.FromSeconds(5)
};

var full = args.Length > 0;

var tags = new ConcurrentDictionary<int, string>();

var check = await WithRetry(MainLoopAsync, urls.Length, default);

Console.WriteLine($"输出目录：{cd}");

async Task<RetryResult> MainLoopAsync(int urlIndex, CancellationToken token)
{
    var url = urls[urlIndex];
    Console.WriteLine($"当前地址：{url} - 绑定版本: 2.3.2");
    try
    {
        var message = new HttpRequestMessage(HttpMethod.Get, $"{url}/gamebox/update.php");
        await FixMessageAsync(message, token);
        using var result = await client.SendAsync(message, token);
        var updateinfo = await result.Content.ReadAsStringAsync(token);
        Console.WriteLine(updateinfo);
        Console.WriteLine(JsonSerializer.Serialize(JsonSerializer.Deserialize(updateinfo, JsonElementContext.Custom.GameBoxUpdateInfo), JsonElementContext.Custom.GameBoxUpdateInfo));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[{url}]检测升级失败了：{ex.Message}");
        return RetryResult.Failure;
    }
    int[] cats = [527, 5];
    for (var ci = 0; ci < cats.Length; ci++)
    {
        var cat = cats[ci];
        Console.WriteLine($"当前类目： --------------------{cat}--------------------");
        var filename = Path.Combine(cd, $"GameBox_{cat}_All.json");
        var items = new ConcurrentDictionary<int, GameBoxItem>();
        if (File.Exists(filename))
        {
            var text = await File.ReadAllTextAsync(filename, token);
            var readItems = JsonSerializer.Deserialize(text, JsonElementContext.Custom.GameBoxItemArray)!;
            foreach (var readItem in readItems)
            {
                if (readItem.GetFileInfo?.Length > 0)
                {
                    items.TryAdd(readItem.ID, readItem);
                }
            }

        }
        var index = 0;
        var total = 1;
        while (index++ < total)
        {
            async Task<RetryResult> SubLoopAsync(int urlIndex, CancellationToken token)
            {
                try
                {
                    var content = new StringContent($"action=get_posts_list&cat={cat}&newPage={index}&s={""}&ranking={"latest"}");
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");
                    var postUrl = GetBoxHash(url);
                    var message = new HttpRequestMessage(HttpMethod.Post, postUrl)
                    {
                        Content = content
                    };
                    await FixMessageAsync(message, token);
                    using var result = await client.SendAsync(message, token);
                    var str = await result.Content.ReadAsStringAsync(token);
                    var resultItem = JsonSerializer.Deserialize(str, JsonElementContext.Custom.GameBoxList)!;
                    var list = resultItem.List;
                    total = resultItem.AllPage;
                    var len = list?.Length ?? 0;
                    if (len <= 0)
                    {
                        return RetryResult.Success;
                    }
                    Console.WriteLine($"当前页：----------------[{len}] {index}  / {total}  {cat}----------------");
                    var tasks = list!.Select(listi => AddListi(url, items, listi));
                    var rarray = await Task.WhenAll(tasks);
                    return rarray.Max();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"获取当前页：{index}  / {total}  {cat}【异常：{ex.Message}】");
                    return RetryResult.Failure;
                }
            }
            var result = await WithRetry(SubLoopAsync, 3, default);
            if (result is RetryResult.Break)
            {
                break;
            }
        }
        if (items.IsEmpty)
        {
            Console.WriteLine($"未获取到内容：{cat}");
            return RetryResult.Failure;
        }
        await File.WriteAllTextAsync(filename, JsonSerializer.Serialize([.. items.Values.OrderBy(s => s.PostTitle)], JsonElementContext.Custom.GameBoxItemArray), token);
        Console.WriteLine($"结果保存到以下文件：{filename}");
    }
    return RetryResult.Success;
}


async Task FixMessageAsync(HttpRequestMessage message, CancellationToken token)
{
    if (message.Content is HttpContent content)
    {
        await content.LoadIntoBufferAsync(token);
    }
    message.Headers.UserAgent.TryParseAdd(userAgent);
}

string GetBoxHash(string ApiUrl)
{
    //获取请求密钥
    //ApiUrl是请求的链接 比如 https://www.uy5.net/gamebox/
    // 更新要加密的数据
    var timeStamp = (long)(DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
    var url = $"{ApiUrl}/gamebox/?sign={timeStamp}-123456-0-{Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes($"/gamebox/-{timeStamp}-123456-0-WTCrudbtHLYDu4LZ"))).ToLower()}";
    return url;// 输出 MD5 哈希值
}


async Task<RetryResult> WithRetry(Func<int, CancellationToken, Task<RetryResult>> task, int retryTimes, CancellationToken token)
{
    for (var i = 0; i < retryTimes; i++)
    {
        var result = await task.Invoke(i, token);
        if (result == RetryResult.Failure)
        {
            continue;
        }
        return result;
    }
    return RetryResult.Failure;
}


async Task<RetryResult> AddListi(string url, ConcurrentDictionary<int, GameBoxItem> items, GameBoxItem newItem)
{
    items.TryGetValue(newItem.ID,out var findItem);
    async Task<RetryResult> AddListSubAsync(int i, CancellationToken token)
    {
        try
        {
            var content = new StringContent($"action=GetFileInfo&get_post_id={newItem.ID}");
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded", "UTF-8");
            await content.LoadIntoBufferAsync(token);
            var postUrl = GetBoxHash(url);
            var message = new HttpRequestMessage(HttpMethod.Post, postUrl)
            {
                Content = content
            };
            await FixMessageAsync(message, token);
            using var result = await client.SendAsync(message, token);
            var str = await result.Content.ReadAsStringAsync(default);
            var element = JsonSerializer.Deserialize(str, JsonElementContext.Custom.GameBoxItem)!;
            var fileInfo = element.GetFileInfo;
            if (fileInfo is not null)
            {
                if (fileInfo.Length > 0)
                {
                    if (findItem.GetFileInfo is not null)
                    {
                        fileInfo = [.. findItem.GetFileInfo.Concat(fileInfo).Distinct()];
                        if (fileInfo.SequenceEqual(findItem.GetFileInfo))
                        {
                            var tag = fileInfo.MaxBy(info => DateTime.TryParseExact(info.LinkCreateTime, "yyyy.mm.dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : DateTime.MinValue).LinkCreateTime ?? string.Empty;
                            var lastTag = tags.GetOrAdd(newItem.CategoryParent, tag);
                            if (lastTag != tag)
                            {
                                Console.WriteLine($"{newItem.ID} - {newItem.PostTitle}【数据已获取！】");
                                return full ? RetryResult.Success : RetryResult.Break;
                            }
                            Console.WriteLine($"{newItem.ID} - {newItem.PostTitle}【数据一致！】");
                            return RetryResult.Success;
                        }
                    }
                    newItem.GetFileInfo = fileInfo;
                    Console.WriteLine($"{newItem.ID} - {newItem.PostTitle}【{fileInfo.Length}已添加！】");
                }
                else
                {
                    Console.WriteLine($"{newItem.ID} - {newItem.PostTitle}【空数据！】");
                }
                return RetryResult.Success;
            }
            else
            {
                Console.WriteLine($"{newItem.ID} - {newItem.PostTitle}【无数据！】");
                return RetryResult.Success;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{newItem.ID} - {newItem.PostTitle}【异常{ex.Message}】");
            return RetryResult.Failure;
        }
    }
    var result = await WithRetry(AddListSubAsync, 3, default);
    if(result is RetryResult.Success && newItem.GetFileInfo is not null)
    {
        items[newItem.ID] = newItem;
    }
    return result;
}

enum RetryResult : int
{
    Success,
    Break,
    Failure,
}

record struct GameBoxUpdateInfo
{
    [JsonPropertyName("version")]
    public string? Version { get; set; }
    [JsonPropertyName("asar")]
    public string? Asar { get; set; }
    [JsonPropertyName("content")]
    public string? Content { get; set; }
    [JsonPropertyName("forceUp")]
    public bool ForceUp { get; set; }
    [JsonPropertyName("updateUrl")]
    public string? UpdateUrl { get; set; }
    [JsonPropertyName("watermark")]
    public string? Watermark { get; set; }

}

record struct GameBoxList
{
    [JsonPropertyName("rankinglist")]
    public GameBoxRanking[]? RankingList { get; set; }
    [JsonPropertyName("list")]
    public GameBoxItem[]? List { get; set; }
    [JsonPropertyName("all_page")]
    public int AllPage { get; set; }
    [JsonPropertyName("state")]
    public int State { get; set; }
    [JsonPropertyName("watermark")]
    public string? Watermark { get; set; }
    [JsonPropertyName("child_categories")]
    public GameBoxCategories[]? ChildCategories { get; set; }

}

record struct GameBoxRanking
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("id")]
    public string? ID { get; set; }
}
record struct GameBoxCategories
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("id")]
    public int? ID { get; set; }

}

record struct GameBoxItem
{
    [JsonPropertyName("id")]
    public int ID { get; set; }
    [JsonPropertyName("category_parent")]
    public int CategoryParent { get; set; }
    [JsonPropertyName("categories")]
    public string? Categories { get; set; }
    [JsonPropertyName("post_title")]
    public string? PostTitle { get; set; }
    [JsonPropertyName("wp_get_attachment_image_src")]
    public string? WpGetAttachmentImageSrc { get; set; }
    [JsonPropertyName("filesize")]
    public string? FileSize { get; set; }
    [JsonPropertyName("GetFileInfo")]
    public GameBoxFile[]? GetFileInfo { get; set; }
}

record struct GameBoxFile
{
    [JsonPropertyName("DownUrl")]
    public string? DownUrl { get; set; }
    [JsonPropertyName("openpath")]
    public string? OpenPath { get; set; }
    [JsonPropertyName("filesize")]
    public string? FileSize { get; set; }
    [JsonPropertyName("link_ctime")]
    public string? LinkCreateTime { get; set; }
    [JsonPropertyName("filesize_z")]
    public long FileSizeZone { get; set; }
    [JsonPropertyName("filename")]
    public string? FileName { get; set; }
    [JsonPropertyName("server")]
    public string? Server { get; set; }
}

// 优化后的转换器：直接用ValueSpan转字符串，极简且通用
public class NumberToStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
    {
        // 字符串类型：直接返回
        JsonTokenType.String => reader.GetString(),
        // 数字类型：直接取ValueSpan转字符串（核心优化）
        JsonTokenType.Number => Encoding.UTF8.GetString(reader.ValueSpan),
        // Null类型：返回null
        JsonTokenType.Null => null,
        // 其他类型：抛明确异常
        _ => throw new JsonException($"不支持将 {reader.TokenType} 类型转换为字符串")
    };

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}

[JsonSerializable(typeof(GameBoxUpdateInfo))]
[JsonSerializable(typeof(GameBoxList))]

internal partial class JsonElementContext : JsonSerializerContext
{
    static JsonElementContext()
    {
        var jsonOption = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true,
        };
        jsonOption.Converters.Add(new NumberToStringConverter());
        Custom = new(jsonOption);
    }
    public static JsonElementContext Custom;
}