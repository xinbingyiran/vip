using System.Buffers;
using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var cd = Environment.CurrentDirectory;
string[] urls = ["https://www.uy5.net", "http://103.39.221.38:8885"];
var client = new HttpClient() { Timeout = TimeSpan.FromSeconds(5) };
string prestr = "ODE?????";
string poststr = "ODE11111";
string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) ODE?????/1.4.0 Chrome/91.0.4472.164 Electron/13.6.9 Safari/537.36".Replace(prestr, poststr);
var full = args.Length > 0;
var tags = new ConcurrentDictionary<int, string>();
string[] cats = ["538", "527", "5"];
var check = await WithRetry(MainLoopAsync, urls.Length, default);
Console.WriteLine($"输出目录：{cd}");
var apiUrl = urls[0];

async Task<RetryResult> MainLoopAsync(int urlIndex, CancellationToken token)
{
    apiUrl = urls[urlIndex];
    Console.WriteLine($"当前地址：{apiUrl} - 绑定版本: 1.4.1");
    try
    {
        var message = new HttpRequestMessage(HttpMethod.Get, $"{apiUrl}/wodown/update.php");
        message.Headers.UserAgent.TryParseAdd(userAgent);
        using var result = await client.SendAsync(message, token);
        var updateinfo = await result.Content.ReadAsStringAsync(token);
        Console.WriteLine(updateinfo);
        var updateItem = JsonSerializer.Deserialize(updateinfo, JsonElementContext.Custom.ODEUpdateInfo);
        Console.WriteLine(JsonSerializer.Serialize(updateItem, JsonElementContext.Custom.ODEUpdateInfo));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[{apiUrl}]检测升级失败了：{ex.Message}");
        return RetryResult.Failure;
    }
    for (var ci = 0; ci < cats.Length; ci++)
    {
        var cat = cats[ci];
        var (page, format) = cat == "538" ? (24, "yyyyMMddHHmm") : (12, "yyyy-MM-dd");
        Console.WriteLine($"当前类目： --------------------{cat}--------------------");
        var filename = Path.Combine(cd, $"ODE_{cat}_All.json");
        var items = new ConcurrentDictionary<int, ODEItem>();
        if (File.Exists(filename))
        {
            var text = await File.ReadAllTextAsync(filename, token);
            var readItems = JsonSerializer.Deserialize(text, JsonElementContext.Custom.ODEItemArray)!;
            foreach (var readItem in readItems)
            {
                if (readItem.List?.Length > 0)
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
                    var resultItem = await GetAppList(cat, index, numberposts: page, token: token);
                    var list = resultItem.List;
                    var len = list?.Length ?? 0;
                    if (len <= 0)
                    {
                        return RetryResult.Success;
                    }
                    total = (resultItem.Numberofarticlescount + resultItem.PageSize - 1) / resultItem.PageSize;
                    Console.WriteLine($"当前页：----------------[{len}] {index}  / {total}  {cat}----------------");
                    var tasks = list!.Select(listi => AddListi(items, listi, format));
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
        await File.WriteAllTextAsync(filename, JsonSerializer.Serialize([.. items.Values.OrderBy(s => s.ID)], JsonElementContext.Custom.ODEItemArray), token);
        Console.WriteLine($"结果保存到以下文件：{filename}");
    }
    return RetryResult.Success;
}
async Task<RetryResult> AddListi(ConcurrentDictionary<int, ODEItem> items, ODEItem newItem, string timeFormat)
{
    items.TryGetValue(newItem.ID, out var findItem);
    async Task<RetryResult> AddListSubAsync(int i, CancellationToken token)
    {
        try
        {
            var element = await GetAppDownList(newItem.ID.ToString(), null, token)!;
            var list = element.List;
            if (list is not null)
            {
                if (list.Length > 0)
                {
                    if (element.Type != "game")
                    {
                        await FillListIfNeedAsync(list, token);
                    }
                    if (findItem.List is not null)
                    {
                        list = [.. findItem.List.Concat(list).Distinct()];
                        if (JsonSerializer.Serialize(list, JsonElementContext.Request.ODEFileArray) == JsonSerializer.Serialize(findItem.List, JsonElementContext.Request.ODEFileArray))
                        {
                            var tag = list.MaxBy(info => DateTime.TryParseExact(info.UdTime, timeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : DateTime.MinValue).UdTime ?? string.Empty;
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
                    newItem.List = list;
                    Console.WriteLine($"{newItem.ID} - {newItem.PostTitle}【{list.Length}已添加！】");
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
    if (result is RetryResult.Success && newItem.List is not null)
    {
        items[newItem.ID] = newItem;
    }
    return result;
}

async Task FillListIfNeedAsync(ODEFile[] list, CancellationToken token)
{
    for (var i = 0;i < list.Length;i ++)
    {
        var item = list[i];
        if (item.Type is "file")
        {
            item.CoID = $"{apiUrl}/download/?path={item.CoID}";
        }
        else
        {
            item.CoID = $"{apiUrl}/download/list/?path={item.CoID}";
            //var subList = await GetAppDownList(null, item.CoID, token)!;
            //if(subList.List?.Length > 0)
            //{
            //    item.List = subList.List;
            //    await FillListIfNeedAsync(item.List, token);
            //}
        }
        list[i] = item;
    }
}

async Task<JsonElement> SaveDownFileSetting(string fileID, bool isPackageInstallable, string fileOpenExePath, CancellationToken token = default)
{
    var postdata = $"action=saveDownFileSetting&fileID={fileID}&isPackageInstallable={isPackageInstallable}&fileOpenExePath={fileOpenExePath}";

    return await GetResourceAsync<JsonElement>(postdata, token);
}
async Task<JsonElement> GetDownFileSetting(string fileID, CancellationToken token = default)
{
    var postdata = $"action=getDownFileSetting&fileID={fileID}";
    return await GetResourceAsync<JsonElement>(postdata, token);
}
async Task<JsonElement> IdentifyLogin(string code, CancellationToken token = default)
{
    var postdata = $"action=identifyLogin&code={code}";
    return await GetResourceAsync<JsonElement>(postdata, token);
}
async Task<JsonElement> HandleCommentLike(string id, CancellationToken token = default)
{
    var postdata = $"action=handleCommentLike&id={id}";
    return await GetResourceAsync<JsonElement>(postdata, token);
}
async Task<JsonElement> SubmitComment(string content, string id, string replyId, CancellationToken token = default)
{
    var postdata = $"action=submitComment&content={content}&id={id}&ReplyId={replyId}";
    return await GetResourceAsync<JsonElement>(postdata, token);
}
async Task<JsonElement> IfLogin(CancellationToken token = default)
{
    var postdata = $"action=IfLogin";
    return await GetResourceAsync<JsonElement>(postdata, token);
}
async Task<JsonElement> GetPostArticle(string id, CancellationToken token = default)
{
    var postdata = $"action=getPostArticle&id={id}";
    return await GetResourceAsync<JsonElement>(postdata, token);
}
async Task<JsonElement> GetResourceDownload(string path, string route, CancellationToken token = default)
{
    var postdata = $"action=getResourceDownload&path={path}&route={route}";
    return await GetResourceAsync<JsonElement>(postdata, token);
}
async Task<JsonElement> GetResourceComment(string id, string fileID, CancellationToken token = default)
{
    var postdata = $"action=getResourceComment&id={id}&fileID={fileID}";
    return await GetResourceAsync<JsonElement>(postdata, token);
}
async Task<ODEDownList> GetAppDownList(string? id, string? path, CancellationToken token = default)
{
    var postdata = $"action=getAppDownList&id={id ?? "null"}&path={path ?? "null"}&version=1";
    return await GetResourceAsync<ODEDownList>(postdata, token);
}
async Task<ODEList> GetAppList(string cat, int newPage, string s = "", string ranking = "", int numberposts = 12, CancellationToken token = default)
{
    var postdata = $"action=getAppList&cat={cat}&newPage={newPage}&ranking={ranking}&s={s}&numberposts={numberposts}&topicsId={cat}";
    return await GetResourceAsync<ODEList>(postdata, token);
}
async Task<JsonElement> GetHomeAppList(string homeSearchContent, CancellationToken token = default)
{
    var postdata = $"action=getHomeAppList&search={homeSearchContent}";
    return await GetResourceAsync<JsonElement>(postdata, token);
}
async Task<T> GetResourceAsync<T>(string postdata, CancellationToken token = default)
{
    var data = new PostEData { EData = EncryptData(postdata, 3) };
    var content = JsonContent.Create(data, JsonElementContext.Request.PostEData);
    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
    await content.LoadIntoBufferAsync(token);
    var message = new HttpRequestMessage(HttpMethod.Post, $"{apiUrl}/wodown/resources/") { Content = content };
    message.Headers.UserAgent.TryParseAdd(userAgent);
    //var p = message.Headers.UserAgent.FirstOrDefault(a => a.Product is ProductHeaderValue v && v.Name == poststr)?.Product;
    //if (p is not null)
    //{
    //    var m = typeof(ProductHeaderValue).GetField("_name", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
    //    m.SetValue(p, prestr);
    //}
    using var result = await client.SendAsync(message, token);
    var str = await result.Content.ReadAsStringAsync(token);
    return (T?)JsonSerializer.Deserialize(str, JsonElementContext.Custom.GetTypeInfo(typeof(T))!) ?? throw new NullReferenceException();
}

static string EncryptData(string input, int key = 3)
{
    if (string.IsNullOrEmpty(input))
        return string.Empty;
    var span = input.AsSpan();
    var result = ArrayPool<char>.Shared.Rent(span.Length);
    try
    {
        for (int i = 0; i < span.Length; i++)
        {
            var charCode = span[i];
            result[i] = charCode switch
            {
                >= 'A' and <= 'Z' => (char)((charCode - 'A' + key) % 26 + 'A'),
                >= 'a' and <= 'z' => (char)((charCode - 'a' + key) % 26 + 'a'),
                _ => charCode
            };
        }
        return new(result, 0, span.Length);
    }
    finally
    {
        ArrayPool<char>.Shared.Return(result);
    }
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

enum RetryResult : int
{
    Success,
    Break,
    Failure,
}

record struct PostEData
{
    [JsonPropertyName("EData")]
    public string? EData { get; set; }
}

record struct ODEUpdateInfo
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

record struct ODEList
{
    [JsonPropertyName("list")]
    public ODEItem[]? List { get; set; }
    [JsonPropertyName("parentList")]
    public ODEParent[]? ParentList { get; set; }
    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }
    [JsonPropertyName("Numberofarticlescount")]
    public int Numberofarticlescount { get; set; }
    [JsonPropertyName("erron")]
    public int? erron { get; set; }

}

record struct ODEParent
{
    [JsonPropertyName("id")]
    public int ID { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

record struct ODEItem
{
    [JsonPropertyName("id")]
    public int ID { get; set; }
    [JsonPropertyName("category_parent")]
    public int CategoryParent { get; set; }
    [JsonPropertyName("categories")]
    public string? Categories { get; set; }
    [JsonPropertyName("post_title")]
    public string? PostTitle { get; set; }
    [JsonPropertyName("Subtitle")]
    public string? Subtitle { get; set; }
    [JsonPropertyName("list")]
    public ODEFile[]? List { get; set; }
}

record struct ODEDownList
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    [JsonPropertyName("list")]
    public ODEFile[]? List { get; set; }

}

record struct ODEFile
{
    [JsonPropertyName("coSize")]
    public string? CoSize { get; set; }
    [JsonPropertyName("udTime")]
    public string? UdTime { get; set; }
    [JsonPropertyName("coName")]
    public string? CoName { get; set; }
    [JsonPropertyName("coID")]
    public string? CoID { get; set; }
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    [JsonPropertyName("path")]
    public string? Path { get; set; }
    [JsonPropertyName("openPath")]
    public string? OpenPath { get; set; }
    [JsonPropertyName("list")]

    public ODEFile[]? List { get; set; }
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

[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(PostEData))]
[JsonSerializable(typeof(ODEUpdateInfo))]
[JsonSerializable(typeof(ODEList))]
[JsonSerializable(typeof(ODEDownList))]

internal partial class JsonElementContext : JsonSerializerContext
{
    static JsonElementContext()
    {
        var jsonOption = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        };
        jsonOption.Converters.Add(new NumberToStringConverter());
        Custom = new(jsonOption);
        var jsonOption2 = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        };
        jsonOption2.Converters.Add(new NumberToStringConverter());
        Request = new(jsonOption2);
    }
    public static JsonElementContext Request;
    public static JsonElementContext Custom;
}