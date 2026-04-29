using ExCSS;
using System.Xml.Serialization;

namespace Scarab.Services;

public class ModDatabase : IModDatabase
{
    private static readonly string[] MODLINKS_URIS = new string[3] { "https://lgithub.xyz/hk-modding/modlinks/raw/refs/heads/main/ModLinks.xml", "https://raw.githubusercontent.com/hk-modding/modlinks/main/ModLinks.xml", "https://cdn.jsdelivr.net/gh/hk-modding/modlinks@latest/ModLinks.xml" };

    private static readonly string[] APILINKS_URIS = new string[3] { "https://lgithub.xyz/hk-modding/modlinks/raw/refs/heads/main/ApiLinks.xml", "https://raw.githubusercontent.com/hk-modding/modlinks/main/ApiLinks.xml", "https://cdn.jsdelivr.net/gh/hk-modding/modlinks@latest/ApiLinks.xml" };

    public (string Url, int Version, string SHA256) Api { get; }

    public IEnumerable<ModItem> Items => _items;

    private readonly List<ModItem> _items = new();

    private ModDatabase(IModSource mods, ModLinks ml, ApiLinks al)
    {
        foreach (var mod in ml.Manifests)
        {
            var tags = mod.Tags.Select(x => Enum.TryParse(x, out Tag tag) ? (Tag?) tag : null)
                          .OfType<Tag>()
                          .ToImmutableArray();
                
            var item = new ModItem
            (
                link: mod.Links.OSUrl,
                version: mod.Version.Value,
                name: mod.Name,
                shasum: mod.Links.SHA256,
                description: mod.Description,
                repository: mod.Repository,
                dependencies: mod.Dependencies,
                    
                tags: tags,
                integrations: mod.Integrations,
                authors: mod.Authors,
                    
                state: mods.FromManifest(mod)
                    
            );
                
            _items.Add(item);
        }

        _items.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

        Api = (al.Manifest.Links.OSUrl, al.Manifest.Version, al.Manifest.Links.SHA256);
    }

    public ModDatabase(IModSource mods, (ModLinks ml, ApiLinks al) links) : this(mods, links.ml, links.al) { }

    public ModDatabase(IModSource mods, string modlinks, string apilinks) : this(mods, FromString<ModLinks>(modlinks), FromString<ApiLinks>(apilinks)) { }
        
    public static async Task<(ModLinks, ApiLinks)> FetchContent(HttpClient hc)
    {
        var ml = FetchModLinks(hc);
        var al = FetchApiLinks(hc);

        await Task.WhenAll(ml, al);

        return (await ml, await al);
    }
        
    private static T FromString<T>(string xml)
    {
        var serializer = new XmlSerializer(typeof(T));
            
        using TextReader reader = new StringReader(xml);

        var obj = (T?) serializer.Deserialize(reader);

        if (obj is null)
            throw new InvalidDataException();

        return obj;
    }

    private static async Task<ApiLinks> FetchApiLinks(HttpClient hc)
    {
        string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ApiLinks.xml");
        return FromString<ApiLinks>(await FetchWithFallback(hc, APILINKS_URIS, file));
    }
        
    private static async Task<ModLinks> FetchModLinks(HttpClient hc)
    {
        string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ModLinks.xml");
        return FromString<ModLinks>(await FetchWithFallback(hc, MODLINKS_URIS, file));
    }

    private static async Task<string> FetchWithFallback(HttpClient hc, string[] urls, string? file)
    {
        if (!string.IsNullOrWhiteSpace(file) && File.Exists(file))
        {
            return await File.ReadAllTextAsync(file);
        }
        foreach (string url in urls)
        {
            try
            {
                CancellationTokenSource cts = new CancellationTokenSource(3000);
                string ret = (await hc.GetStringAsync(url, cts.Token)).Replace("CDATA[https://github.com/", "CDATA[https://lgithub.xyz/");
                if (!string.IsNullOrWhiteSpace(file))
                {
                    await File.WriteAllTextAsync(file, ret);
                }
                return ret;
            }
            catch (Exception ex) when (((ex is TaskCanceledException || ex is HttpRequestException) ? 1 : 0) != 0)
            {
            }
        }
        throw new HttpRequestException("未获取到内容！");
    }
}