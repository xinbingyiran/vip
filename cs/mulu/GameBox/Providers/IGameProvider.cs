using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Security.Policy;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace GameBox.Providers;
public class Game
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public Dictionary<string, string>? Ext { get; init; }
    public Func<Action<string>, CancellationToken, Task<string[]>>? InfoGetter { get; init; }
    public Func<Action<string>, CancellationToken, Task<string[]>>? UrlGetter { get; init; }
}
public interface IGameProvider
{
    string Name { get; }
    Task<Game[]> SearchGame(Action<string> logger, string name, CancellationToken token);
}


abstract class WebGameProvider : IGameProvider
{
    protected static readonly HttpClient DefaultClient = new();
    protected static readonly JsonSerializerOptions DefaultSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    public abstract string Name { get; }
    public abstract Task<Game[]> SearchGame(Action<string> logger, string name, CancellationToken token);
}