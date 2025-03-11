using System.Net.Http;
using System.Net.Http.Json;
using System.Security.AccessControl;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Controls;

namespace GameBox.Providers;

class CaiNiaoGameProvider : WebGameProvider
{
    //https://tb.huifangkuai.com/p1/?dlid=377
    //https://tb.huifangkuai.com/p1/App%25/?dlid=377
    private const string SearchUrl = "https://bvip.huifangkuai.com/index/Apix/tbsearchlist";
    private const string InfoUrl = "https://bvip.huifangkuai.com/index/Apix/getGameInfo";

    private static readonly string[] baseAddress = ["http://1.117.71.138:8813", "http://101.35.254.55:8814", "http://101.35.254.55:8813", "http://1.117.71.138:8814"];

    private static int urlIndex = 0;
    public override string Name => "菜鸟盒子";

    private static async Task<string> GetTrueUrlAsync(string name, string gameId, JsonElement fileId, CancellationToken token)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"{baseAddress[urlIndex]}/geturl/{gameId}?fileid={fileId}");
        using var result = await DefaultClient.SendAsync(message, token);
        result.EnsureSuccessStatusCode();
        var resultStr = await result.Content.ReadAsStringAsync(token);
        var items = JsonSerializer.Deserialize<JsonElement>(resultStr);
        if (!items.TryGetProperty("maps", out var list) || list.ValueKind is not JsonValueKind.Array)
        {
            throw new Exception("解析地址失败！");
        }
        //{"f5ntk":"","fileId":"","fileSize":0,"sha1":"","url":"","utf_name":""},
        foreach (var obj in list.EnumerateArray())
        {
            if (obj.ValueKind is JsonValueKind.Object && obj.TryGetProperty("url", out var url))
            {
                try
                {
                    return $"{Encoding.UTF8.GetString(Convert.FromBase64String($"{url}"))}#|#{fileId}#|#{name}#|#";
                }
                catch
                {
                    return $"{url}#|#{fileId}#|#{name}#|#";
                }
            }
        }
        throw new Exception(resultStr);
    }

    private (string name, int nextPage, bool hasNext, Game[] games) currentStatus = new("", 0, false, []);
    public override async Task<Game[]> SearchGame(Action<string> logger, string name, CancellationToken token)
    {
        int page = 1;
        Game[] preGames = [];
        if (name == currentStatus.name)
        {
            if (currentStatus.hasNext)
            {
                page = currentStatus.nextPage;
                preGames = currentStatus.games;
            }
        }
        var content = JsonContent.Create(new { page = page, num = 20, gamename = name, flag = "", dlid = "377" });
        await content.LoadIntoBufferAsync();
        using var message = new HttpRequestMessage(HttpMethod.Post, SearchUrl) { Content = content };
        message.Headers.Add("x-requested-with", "XMLHttpRequest");
        logger($"搜索第 {page} 页");
        using var result = await DefaultClient.SendAsync(message, token);
        result.EnsureSuccessStatusCode();
        var resultStr = await result.Content.ReadAsStringAsync(token);
        var items = JsonSerializer.Deserialize<JsonElement>(resultStr);
        var nodes = new List<Game>();
        if (items.ValueKind is JsonValueKind.Object && items.TryGetProperty("list", out var list) && list.ValueKind is JsonValueKind.Array)
        {
            foreach (var obj in list.EnumerateArray())
            {
                if (obj.ValueKind is not JsonValueKind.Object)
                {
                    continue;
                }
                nodes.Add(CreateGame(obj));
            }
        }
        else
        {
            throw new Exception(resultStr);
        }
        Game[] games = [.. preGames, .. nodes];
        var count = items.GetProperty("num")[0].GetProperty("count(*)").GetInt32();
        currentStatus = (name, page + 1, page * 20 < count, games);
        logger($"当前显示第 1 - {page * 20} 个结果，共 {count} 个！");
        return games;
    }
    private static Game CreateGame(JsonElement obj)
    {
        var dict = new Dictionary<string, string>();
        return new Game
        {
            Id = $"{obj.GetProperty("gameid")}",
            Name = $"{obj.GetProperty("gamename_zh")}[{obj.GetProperty("gamename_en")}]",
            Ext = dict,
            InfoGetter = CreateInfoGetter(obj, dict),
            UrlGetter = CreateUrlGetter(obj, dict)
        };
    }
    static Func<Action<string>, CancellationToken, Task<string[]>> CreateInfoGetter(JsonElement obj, Dictionary<string, string> gameDict)
    {
        string[]? cacheResult = null;
        return async (logger, tk) =>
        {
            if (cacheResult is null)
            {
                foreach (var item in obj.EnumerateObject())
                {
                    gameDict[$"{item.Name}"] = $"{item.Value}";
                }
                var content = JsonContent.Create(new { gameid = gameDict["gameid"], platform = gameDict["platform"], dlid = "377" });
                await content.LoadIntoBufferAsync();
                using var message = new HttpRequestMessage(HttpMethod.Post, InfoUrl) { Content = content };
                message.Headers.Add("x-requested-with", "XMLHttpRequest");
                logger("正在更新。。。");
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(tk);
                cts.CancelAfter(3000);
                using var result = await DefaultClient.SendAsync(message, cts.Token);
                result.EnsureSuccessStatusCode();
                var resultStr = await result.Content.ReadAsStringAsync(cts.Token);
                var items = JsonSerializer.Deserialize<JsonElement>(resultStr);
                //alone_file: "1,app.7z.001,1778820v20250222"
                foreach (var item in items.EnumerateObject())
                {
                    gameDict[$"{item.Name}"] = $"{item.Value}";
                }
                cacheResult = [.. gameDict.Select(e => $"{e.Key}={e.Value}")];
            }
            return cacheResult;
        };
    }
    static Func<Action<string>, CancellationToken, Task<string[]>> CreateUrlGetter(JsonElement obj, Dictionary<string, string> gameDict)
    {
        Func<CancellationToken, Task<string>>[]? cacheGetter = null;
        return async (logger, tk) =>
        {
            if (cacheGetter is null)
            {
                var gameId = gameDict["gameid"];
                if (gameDict.TryGetValue("alone_file", out var alone_file) && !string.IsNullOrWhiteSpace(alone_file))
                {
                    var alonefiles = alone_file.Split(',');
                    if (alonefiles.Length != 3)
                    {
                        throw new KeyNotFoundException("未正确解析文件描述！");
                    }
                    gameId = alonefiles[2];
                }
                string resultStr = string.Empty;
                for (var i = 0; i < baseAddress.Length; i++)
                {
                    try
                    {
                        using var tcs = CancellationTokenSource.CreateLinkedTokenSource(tk);
                        tcs.CancelAfter(5000);
                        using var message = new HttpRequestMessage(HttpMethod.Get, $"{baseAddress[urlIndex]}/geturl/{gameId}");
                        logger($"正在获取列表【第{i + 1}次尝试】。。。");
                        using var getresult = await DefaultClient.SendAsync(message, tcs.Token);
                        getresult.EnsureSuccessStatusCode();
                        resultStr = await getresult.Content.ReadAsStringAsync(tk);
                        break;
                    }
                    catch
                    {
                        urlIndex = (urlIndex + 1) % baseAddress.Length;
                    }
                }
                if (string.IsNullOrWhiteSpace(resultStr))
                {
                    throw new Exception("无法获取资源");
                }
                var items = JsonSerializer.Deserialize<JsonElement>(resultStr);
                if (items.TryGetProperty("maps", out var list) && list.ValueKind is JsonValueKind.Array)
                {
                    //{"f5ntk":"","fileId":"","fileSize":0,"sha1":"","utf_name":""},
                    var getter = new List<Func<CancellationToken, Task<string>>>();
                    foreach (var item in list.EnumerateArray())
                    {
                        if (item.ValueKind is JsonValueKind.Object && item.TryGetProperty("fileId", out var fileId)
                            && item.TryGetProperty("utf_name", out var utf_name))
                        {
                            getter.Add(async t => await GetTrueUrlAsync($"{utf_name}", gameId, fileId, t));
                        }
                        else
                        {
                            throw new Exception("解析失败！");
                        }
                    }
                    cacheGetter = [.. getter];
                }
                else
                {
                    throw new Exception(resultStr);
                }
            }
            var count = cacheGetter.Length;
            var result = new string[count];
            var current = 0;
            logger($"获取地址【0 / {count}】。。。");
            await Parallel.ForAsync(0, count, tk, async (index, token) =>
            {
                using var tcs = CancellationTokenSource.CreateLinkedTokenSource(token);
                tcs.CancelAfter(5000);
                result[index] = await cacheGetter[index].Invoke(tcs.Token);
                var cur = Interlocked.Increment(ref current);
                logger($"获取地址【{cur} / {count}】。。。");
            });
            return await Task.WhenAll(cacheGetter.Select(e => e.Invoke(tk)));
        };
    }
}
