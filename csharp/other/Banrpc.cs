#:package System.Diagnostics.EventLog@10.0.14
#:package Microsoft.Extensions.Hosting.WindowsServices@10.0.14
#:property TargetFramework=net8.0
#:property LangVersion=latest
#:property PublishAot=true

using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Net;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;

Environment.CurrentDirectory = AppContext.BaseDirectory;
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    ContentRootPath = Environment.CurrentDirectory
});
builder.Logging.ClearProviders();
if (WindowsServiceHelpers.IsWindowsService())
{
    builder.Services.AddWindowsService();
}
else
{
    builder.Logging.AddSimpleConsole();
}
builder.Services.AddHostedService<Program>();
var host = builder.Build();
await host.RunAsync();

[SupportedOSPlatform("windows")]
partial class Program(ILogger<Program> Logger) : BackgroundService
{
    private static readonly string ConfigFile = Path.Combine(AppContext.BaseDirectory, "config.ini");
    [GeneratedRegex(@"Accepted publickey for (?<user>\S+) from (?<ip>\S+) port", RegexOptions.Compiled)]
    private static partial Regex SshAcceptedRegexGetter();
    private static readonly Regex SshAcceptedRegex = SshAcceptedRegexGetter();
    [GeneratedRegex(@"Invalid user (?<user>\S+) from (?<ip>\S+) port", RegexOptions.Compiled)]
    private static partial Regex SshInvalidUserRegexGetter();
    private static readonly Regex SshInvalidUserRegex = SshInvalidUserRegexGetter();
    [GeneratedRegex(@"Failed password for invalid user (?<user>\S+) from (?<ip>\S+) port", RegexOptions.Compiled)]
    private static partial Regex SshFailedPwRegexGetter();
    private static readonly Regex SshFailedPwRegex = SshFailedPwRegexGetter();

    private int Fails = 3;
    private int Mins = 60;
    private string Ports = "";
    private const string BanRulePrefix = "IPBAN_";
    private readonly SemaphoreSlim _slim = new(1, 1);
    private readonly HttpListener _listener = new();
    private HashSet<string> Whites = [];
    private readonly Dictionary<string, BanItem> BanList = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ClearAllBanRules();

        await ReloadConfigAsync("Init", stoppingToken);

        string securityXPath = "*[System[(EventID=4624 or EventID=4625)]]";
        var securityQuery = new EventLogQuery("Security", PathType.LogName, securityXPath);
        using var securityWatcher = new EventLogWatcher(securityQuery);
        securityWatcher.EventRecordWritten += OnSecurityEvent;
        securityWatcher.Enabled = true;

        string sshXPath = "*";
        var sshQuery = new EventLogQuery("OpenSSH/Operational", PathType.LogName, sshXPath);
        using var sshWatcher = new EventLogWatcher(sshQuery);
        sshWatcher.EventRecordWritten += OnSshEvent;

        using var fw = new FileSystemWatcher(Path.GetDirectoryName(ConfigFile) ?? AppContext.BaseDirectory, Path.GetFileName(ConfigFile))
        {
            NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite,
            IncludeSubdirectories = false
        };
        fw.Deleted += FWChanged;
        fw.Changed += FWChanged;
        fw.Created += FWChanged;
        fw.Renamed += FWChanged;
        fw.EnableRaisingEvents = true;
        _listener.Start();

        sshWatcher.Enabled = true;
        WriteLog("APP start");
        await Task.WhenAny(CheckBanExpire(stoppingToken), CheckListener(stoppingToken));
        fw.EnableRaisingEvents = false;
        sshWatcher.Enabled = false;
        securityWatcher.Enabled = false;
        ClearAllBanRules();
        WriteLog("APP exit");
        _slim.Dispose();
        (_listener as IDisposable)?.Dispose();
    }

    private async Task CheckListener(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var context = await _listener.GetContextAsync();
            var now = DateTime.Now;
            var ip = (context.Request.RawUrl is string str && str.LastIndexOf('/') is var v && v >= 0 && str[(v + 1)..] is string v2 && IPAddress.TryParse(v2, out var vip) ? vip : context.Request.RemoteEndPoint.Address).ToString();
            if (BanList.TryGetValue(ip.ToString(), out var item) && item.UnBanTime > now)
            {
                item.UnBanTime = now;
            }
            context.Response.OutputStream.Write(Encoding.UTF8.GetBytes(ip));
            context.Response.Close();
        }
    }

    private async Task ReloadConfigAsync(string reason, CancellationToken token = default)
    {
        HashSet<string> list = ["127.0.0.1"];
        var fails = 3;
        var mins = 60;
        var ports = "0-65535";
        if (!File.Exists(ConfigFile))
        {
            try
            {
                File.WriteAllLines(ConfigFile, [
                    $"listen={string.Join(",", _listener.Prefixes)}",
                    $"mins={mins}",
                    $"fails={fails}",
                    $"ports={ports}",
                    $"whites={string.Join(',',list)}",
                ], Encoding.UTF8);
            }
            catch { }
        }
        else
        {
            try
            {
                _listener.Prefixes.Clear();
            }
            catch { }
            try
            {
                foreach (var line in File.ReadAllLines(ConfigFile, Encoding.UTF8))
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }
                    var kv = line.Split("=", 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    if (kv.Length != 2)
                    {
                        continue;
                    }
                    switch (kv[0].ToLower())
                    {
                        case "listen":
                            foreach (var url in kv[1].Split([',', ';', '，', '；'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (string.IsNullOrWhiteSpace(url))
                                {
                                    continue;
                                }
                                try
                                {
                                    _listener.Prefixes.Add(url);
                                }
                                catch { }
                            }
                            break;
                        case "mins":
                            if (int.TryParse(kv[1], out var m) && m > 0)
                            {
                                mins = m;
                            }
                            break;
                        case "fails":
                            if (int.TryParse(kv[1], out var f) && f > 0)
                            {
                                fails = f;
                            }
                            break;
                        case "ports":
                            foreach (var port in kv[1].Split([',', ';', '，', '；'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (string.IsNullOrWhiteSpace(port))
                                {
                                    continue;
                                }
                                var newPorts = string.Join("-", port.Split('-', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(s => (int.TryParse(s, out var n) && n is >= 0 and <= 65535) ? n : -1).Where(s => s >= 0).Order());
                                if (newPorts.Length == 0)
                                {
                                    continue;
                                }
                                else if (ports is "0-65535")
                                {
                                    ports = newPorts;
                                }
                                else
                                {
                                    ports = $"{ports},{newPorts}";
                                }
                            }
                            break;
                        case "whites":
                            foreach (var ip in kv[1].Split([',', ';', '，', '；'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(s => IPAddress.TryParse(s, out var v) ? v : null))
                            {
                                if (ip is null)
                                {
                                    continue;
                                }
                                list.Add(ip.ToString());
                            }
                            break;
                    }
                }
            }
            catch { }
        }

        WriteLog($"Config load: [{reason}] listen={string.Join(",", _listener.Prefixes)} mins={mins} fails={fails} ports={ports} whites={string.Join(',', list)}");
        await _slim.WaitAsync();
        try
        {
            Fails = fails;
            Mins = mins;
            Ports = ports;
            Whites = list;
        }
        finally { _slim.Release(); }
    }

    private async void FWChanged(object sender, FileSystemEventArgs e)
    {
        await ReloadConfigAsync(e.ChangeType.ToString());
    }

    async Task CheckBanExpire(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(1000, token);
                await CheckBan(token);
            }
        }
        catch { }
    }

    async Task CheckBan(CancellationToken token)
    {
        var now = DateTime.Now;
        foreach (var (ip, info) in BanList.ToArray())
        {
            if (info.Name is not null && now >= info.UnBanTime)
            {
                var tcp = UnbanIp($"{info.Name}_tcp", out var tcpe);
                var udp = UnbanIp($"{info.Name}_udp", out var udpe);
                await _slim.WaitAsync(token);
                try
                {
                    BanList.Remove(ip, out _);
                }
                finally
                {
                    _slim.Release();
                }
                WriteLog($"Ban Delete: {ip} [{info.Name}_tcp[{tcp}|{tcpe}]/{info.Name}_udp[{udp}|{udpe}]]");
            }
        }
    }

    async void OnSecurityEvent(object? sender, EventRecordWrittenEventArgs e)
    {
        if (e.EventRecord == null) return;
        try
        {
            var xml = XElement.Parse(e.EventRecord.ToXml());
            var dataDict = ReadEventData(xml);

            int eventId = e.EventRecord.Id;
            string ip = Get(dataDict, "IpAddress");
            string user = Get(dataDict, "TargetUserName");
            string subStatus = Get(dataDict, "SubStatus");

            if (string.IsNullOrWhiteSpace(ip) || ip is "-") return;

            if (eventId == 4625)
            {
                var result = await AddFail(ip, "RDP");
                WriteLog($"RDP fail: {ip} | {user} | {ExplainSubStatus(subStatus)} | {result}");
            }
            else if (eventId == 4624)
            {
                WriteLog($"RDP success: {ip} | {user}");
            }
        }
        catch (Exception ex)
        {
            WriteLog($"RDP error: {ex.Message}");
        }
    }

    async void OnSshEvent(object? sender, EventRecordWrittenEventArgs e)
    {
        if (e.EventRecord == null) return;
        try
        {
            var xml = XElement.Parse(e.EventRecord.ToXml());
            var dataDict = ReadEventData(xml);
            string payload = Get(dataDict, "payload");

            if (payload.Contains("Connection reset by invalid user"))
                return;

            var user = "-";
            var ip = "-";
            var reason = "";

            Match mInvalid = SshInvalidUserRegex.Match(payload);
            if (mInvalid.Success)
            {
                user = mInvalid.Groups["user"].Value;
                ip = mInvalid.Groups["ip"].Value;
                reason = "无效用户名";
            }
            else
            {
                var mFailPw = SshFailedPwRegex.Match(payload);
                if (mFailPw.Success)
                {
                    user = mFailPw.Groups["user"].Value;
                    ip = mFailPw.Groups["ip"].Value;
                    reason = "无效用户名密码";
                }
            }

            if (string.IsNullOrWhiteSpace(ip) || ip is "-")
            {
                var mAccept = SshAcceptedRegex.Match(payload);
                if (mAccept.Success)
                {
                    WriteLog($"SSH success: {mAccept.Groups["ip"].Value} | {mAccept.Groups["user"].Value}");
                }
            }
            else
            {
                var result = await AddFail(ip, "SSH");
                WriteLog($"SSH fail: {ip} | {user} | {reason} | {result}");
            }
        }
        catch (Exception ex)
        {
            WriteLog($"SSH error: {ex.Message}");
        }
    }

    async Task<string> AddFail(string ip, string type, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(ip) || ip == "-" || !IPAddress.TryParse(ip, out var ipvalue))
        {
            return $"invalid IP";
        }
        ip = ipvalue.ToString();
        if (Whites.Contains(ip))
        {
            return $"white IP";
        }
        BanItem? ban;
        await _slim.WaitAsync(token);
        try
        {
            if (!BanList.TryGetValue(ip, out ban))
            {
                BanList[ip] = ban = new BanItem();
            }
        }
        finally
        {
            _slim.Release();
        }
        var now = DateTime.Now;
        var checkFailTime = now.AddMinutes(-Mins);
        var oldCount = 0;
        while (ban.List.TryPeek(out var p) && p.FailTime < checkFailTime && ban.List.TryDequeue(out p))
        {
            oldCount++;
        }
        ban.List.Enqueue((type, now));
        if (ban.Name is null && ban.List.Count >= Fails)
        {
            ban.Name = $"{BanRulePrefix}_{ip}";
            ban.UnBanTime = now.AddMinutes(Mins);
            var tcp = BanIp(ip, $"{ban.Name}_tcp", "tcp", Ports, out var tcpe);
            var udp = BanIp(ip, $"{ban.Name}_udp", "udp", Ports, out var udpe);
            return $"Ban IP: {ip} {ban.List.Count}/{Fails} -{oldCount}+1 [{ban.Name}_tcp[{tcp}|{tcpe}]/{ban.Name}_udp[{udp}|{udpe}]]";
        }
        else
        {
            return $"Count IP: {ip} {ban.List.Count}/{Fails} -{oldCount}+1";
        }
    }

    void ClearAllBanRules()
    {
        WriteLog("Ban delete batch");
        try
        {
            string output = Firewall("show rule name=all", out var err);
            var lines = output.Split(Environment.NewLine.Replace("\r", "\\r").Replace("\n", "\\n"), StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line) || line.IndexOf(':') < 0)
                {
                    continue;
                }
                var kv = line.Split(':', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (kv is ["Rule Name" or "规则名称", var rn] && rn.StartsWith(BanRulePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var r = UnbanIp(rn, out var re);
                    WriteLog($"Ban delete: {rn}[{r}|{re}]");
                }
            }
        }
        catch (Exception ex)
        {
            WriteLog($"Ban delete error: {ex.Message}");
        }
    }

    string BanIp(string ip, string ruleName, string protocol, string ports, out string error)
    {
        return Firewall($"add rule name=\"{ruleName}\" dir=in action=block remoteip={ip} protocol={protocol} localport={ports} enable=yes profile=any", out error);
    }

    string UnbanIp(string ruleName, out string error)
    {
        return Firewall($"delete rule name=\"{ruleName}\"", out error);
    }
    string Firewall(string args, out string err)
    {
        err = string.Empty;
        string result = string.Empty;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd",
                Arguments = $"/c chcp 65001 >nul && netsh advfirewall firewall {args}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            var pr = Process.Start(psi);
            if (pr == null) return result;
            result = pr.StandardOutput.ReadToEnd().Replace("\r", "\\r").Replace("\n", "\\n");
            err = pr.StandardError.ReadToEnd().Replace("\r", "\\r").Replace("\n", "\\n");
            pr.WaitForExit();
        }
        catch (Exception ex)
        {
            WriteLog($"Run netsh error: {ex.Message}");
        }
        return result;
    }

    void WriteLog(string text)
    {
        var now = DateTime.Now;
        string logLine = $"[{now:HH:mm:ss}] {text}";
        string logFile = Path.Combine(AppContext.BaseDirectory, $"Banrpc{now:yyyyMMdd}.log");
        try
        {
            Logger.LogInformation("{msg}", logLine);
            File.AppendAllText(logFile, logLine + Environment.NewLine);
        }
        catch
        {
        }
    }

    static Dictionary<string, string> ReadEventData(XElement eventXml)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var ns = eventXml.Name.Namespace;
        foreach (var dataNode in eventXml.Descendants(ns + "Data"))
        {
            var nameAttr = dataNode.Attribute("Name");
            string name = nameAttr?.Value ?? "";
            dict[name] = dataNode.Value?.Trim() ?? "";
        }
        return dict;
    }

    static string Get(Dictionary<string, string> dict, string key)
    {
        dict.TryGetValue(key, out var v);
        return v ?? "-";
    }

    static string ExplainSubStatus(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return code;
        return code.ToLower() switch
        {
            "0xc0000064" => "用户名不存在",
            "0xc000006a" => "用户名正确，密码错误",
            "0xc0000234" => "账号锁定",
            "0xc0000072" => "账号禁用",
            _ => code
        };
    }
    class BanItem
    {
        public Queue<(string Type, DateTime FailTime)> List = [];
        public DateTime? UnBanTime { get; set; }
        public string? Name { get; set; }
    }
}