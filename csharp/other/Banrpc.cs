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
partial class Program(ILogger<Program> Logger, IHostApplicationLifetime Lifetime) : BackgroundService
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

    private const string BanRulePrefix = "IPBAN_";
    // -c 清理模式的实例判活互斥体：命名互斥体存在即视为已有进程在运行
    // Global\ 前缀保证跨会话（服务 Session 0 与控制台会话）可见
    private const string InstanceGateName = @"Global\Banrpc.InstanceGate";
    private const int FirewallTimeoutMs = 30000;
    private const int MaxPendingEvents = 512;
    private const int DefaultWindowMins = 60;
    private const int DefaultBanMins = 60;

    // 规则名统一为 IPBAN_<ip>，TCP/UDP 由后缀 _tcp/_udp 区分
    private int Fails = 3;
    // #12 失败统计窗口与封禁时长拆分为两个独立配置：mins=统计窗口，banmins=封禁时长
    private int WindowMins = DefaultWindowMins;
    private int BanMins = DefaultBanMins;
    private string Ports = "0-65535";
    // #15 SSH 仅订阅失败/成功登录事件，避免全量订阅把连接类高频事件全部派发进来
    private int[] SshEventIds = [2, 4];

    // #3 内存结构的唯一同步点：BanList/BanItem.List/配置快照全部在 _gate 内访问
    private readonly object _gate = new();
    // #17 日志文件串行写入
    private readonly object _logGate = new();
    // #6 配置热重载串行化，避免 FileSystemWatcher 多事件并发重入
    private readonly SemaphoreSlim _configSlim = new(1, 1);
    // #6 事件处理串行化，形成背压
    private readonly SemaphoreSlim _eventSlim = new(1, 1);
    private int _pendingEvents;
    private int _droppedEvents;
    private readonly HttpListener _listener = new();
    private HashSet<string> Whites = [];
    private readonly Dictionary<string, BanItem> BanList = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // -c 清理模式只探测不创建，绝不占用 InstanceGate：清理是逐条 netsh，
        // 规则多时可能持续数十秒，期间若服务重启会被误判为"已有实例"而启动失败
        if (Environment.GetCommandLineArgs().Any(a => string.Equals(a, "-c", StringComparison.OrdinalIgnoreCase)))
        {
            if (IsInstanceRunning())
            {
                WriteLog("Cleanup mode: 检测到运行中的实例(互斥体存在或无法判定)，直接退出");
            }
            else
            {
                WriteLog("Cleanup mode: 无运行实例，开始清理规则");
                await ClearAllBanRulesAsync(stoppingToken);
                WriteLog("Cleanup mode: exit");
            }
            Lifetime.StopApplication();
            return;
        }

        // initiallyOwned:false —— 只要句柄不要所有权，不做 WaitOne/ReleaseMutex 配对，
        // 仅凭"命名对象是否存在"判活；createdNew=false 即已有同名对象，说明有实例在跑
        Mutex? instanceGate = null;
        bool gateCreated;
        try
        {
            instanceGate = new Mutex(false, InstanceGateName, out gateCreated);
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // 同名内核对象已存在但不是 Mutex（历史版本遗留的其它类型对象）：
            // 无法共用判活语义，按"已有实例"处理，直接退出且绝不动防火墙规则
            WriteLog("Instance: 同名内核对象类型不匹配(非互斥体)，本实例退出");
            Lifetime.StopApplication();
            return;
        }
        catch (UnauthorizedAccessException ex)
        {
            // 缺 SeCreateGlobalPrivilege 时建不出 Global\ 对象；若放任继续运行，
            // -c 会误判"无实例"而删掉正在服务的规则，故拒绝启动并明确指出权限原因
            WriteLog($"Instance: 创建全局互斥体被拒绝({ex.Message})，需管理员或服务账户运行，本实例退出");
            Lifetime.StopApplication();
            return;
        }
        // 提前退出路径统一走静默释放；只有拉起清理子进程前才显式 Dispose
        using var instanceGateScope = instanceGate;
        if (!gateCreated)
        {
            WriteLog("Instance: 检测到运行中的实例(互斥体存在)，本实例退出");
            Lifetime.StopApplication();
            return;
        }

        await ClearAllBanRulesAsync(stoppingToken);

        await ReloadConfigAsync("Init", stoppingToken);

        // #9 内部 CTS 与外部停止令牌联动：任一循环退出后可主动取消另一循环，
        // 待两任务真正结束后再释放共享资源，避免 ObjectDisposedException
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var token = linked.Token;

        string securityXPath = "*[System[(EventID=4624 or EventID=4625)]]";
        var securityQuery = new EventLogQuery("Security", PathType.LogName, securityXPath);
        using var securityWatcher = new EventLogWatcher(securityQuery);
        securityWatcher.EventRecordWritten += OnSecurityEvent;
        securityWatcher.Enabled = true;

        // #15 从 "*" 收敛为登录成功(2)/失败(4)两类事件；若本机 OpenSSH 事件 ID 不同，
        // 可改 config.ini 的 sshids= 覆盖，payload 正则仍做二次校验
        int[] sshIds;
        lock (_gate)
        {
            sshIds = SshEventIds;
        }
        string sshXPath = sshIds.Length == 0
            ? "*"
            : "*[System[(" + string.Join(" or ", sshIds.Select(id => $"EventID={id}")) + ")]]";
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

        try
        {
            fw.EnableRaisingEvents = true;
            _listener.Start();
            sshWatcher.Enabled = true;
            WriteLog($"APP start: sshXPath={sshXPath}");
            var finished = await Task.WhenAny(CheckBanExpire(token), CheckListener(token));
            WriteLog($"APP loop exit!");
        }
        catch (Exception ex)
        {
            WriteLog($"APP loop error: {ex.Message}");
        }
        finally
        {
            fw.EnableRaisingEvents = false;
            try { sshWatcher.Enabled = false; } catch { }
            try { securityWatcher.Enabled = false; } catch { }
            try { _listener.Stop(); } catch { }
            linked.Cancel();
            // 唯一显式释放点：必须先放掉 gate，-c 子进程才能探测到"无实例"而执行清理
            instanceGate?.Dispose();
            SpawnCleanupProcess();
            WriteLog("APP exit");
            _listener.Close();
            Lifetime.StopApplication();
        }
    }

    // #5 循环内异常必须就地捕获并继续，不能让异常逃出导致后台任务"正常"结束
    private async Task CheckListener(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            HttpListenerContext? context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }
                WriteLog($"Listen error: {ex.Message}");
                if (!await SafeDelay(500, token))
                {
                    break;
                }
                continue;
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                var now = DateTime.Now;
                var ip = ResolveRequestIp(context);
                // 查询与放行为原子，避免与 AddFail/CheckBan 并发操作同一 BanItem
                lock (_gate)
                {
                    if (BanList.TryGetValue(ip, out var item) && item.UnBanTime > now)
                    {
                        item.UnBanTime = now;
                    }
                }
                context.Response.OutputStream.Write(Encoding.UTF8.GetBytes(ip));
            }
            catch (Exception ex)
            {
                WriteLog($"Request error: {ex.Message}");
            }
            finally
            {
                try { context.Response.Close(); } catch { }
            }
        }
    }

    private static string ResolveRequestIp(HttpListenerContext context)
    {
        if (context.Request.RawUrl is string raw)
        {
            var query = raw.IndexOf('?');
            if (query >= 0)
            {
                raw = raw[..query];
            }
            var idx = raw.LastIndexOf('/');
            if (idx >= 0 && IPAddress.TryParse(raw[(idx + 1)..], out var parsed))
            {
                return parsed.ToString();
            }
        }
        return context.Request.RemoteEndPoint?.Address?.ToString() ?? "-";
    }

    private async Task ReloadConfigAsync(string reason, CancellationToken token = default)
    {
        try
        {
            await _configSlim.WaitAsync(token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        try
        {
            await LoadConfigCoreAsync(reason);
        }
        finally
        {
            _configSlim.Release();
        }
    }

    // #14 配置解析先落局部变量，全部校验通过后再一次性生效；
    // Prefixes 应用失败时回滚到原有前缀，避免写错配置丢失全部监听地址
    private async Task LoadConfigCoreAsync(string reason)
    {
        HashSet<string> list = ["127.0.0.1"];
        var fails = 3;
        var windowMins = DefaultWindowMins;
        // 未配置 banmins 时沿用 mins，保证旧 config.ini 升级后封禁时长语义不变
        int? banMinsCfg = null;
        var ports = "0-65535";
        int[] sshIds = [2, 4];
        var newPrefixes = new List<string>();
        var configParsed = false;

        if (!File.Exists(ConfigFile))
        {
            try
            {
                File.WriteAllLines(ConfigFile, [
                    $"listen={string.Join(",", _listener.Prefixes)}",
                    $"mins={windowMins}",
                    $"banmins={DefaultBanMins}",
                    $"fails={fails}",
                    $"ports={ports}",
                    $"whites={string.Join(',',list)}",
                    $"sshids={string.Join(',', sshIds)}",
                ], Encoding.UTF8);
            }
            catch { }
        }
        else
        {
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
                                // 语法合法性统一交给 HttpListener 校验（Uri 不接受 http://+:port/ 这类通配前缀）
                                newPrefixes.Add(url);
                            }
                            break;
                        case "mins":
                            if (int.TryParse(kv[1], out var m) && m > 0)
                            {
                                windowMins = m;
                            }
                            break;
                        case "banmins":
                            if (int.TryParse(kv[1], out var bm) && bm > 0)
                            {
                                banMinsCfg = bm;
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
                        case "sshids":
                            var parsedIds = kv[1].Split([',', ';', '，', '；'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                                .Select(s => int.TryParse(s, out var id) && id is > 0 and <= 65535 ? id : -1)
                                .Where(id => id > 0)
                                .Distinct()
                                .Take(16)
                                .ToArray();
                            if (parsedIds.Length > 0)
                            {
                                sshIds = parsedIds;
                            }
                            break;
                    }
                }
                configParsed = true;
            }
            catch (Exception ex)
            {
                WriteLog($"Config read error: [{reason}] {ex.Message}");
            }
        }

        // 前缀应用阶段：配置读取成功且全部前缀校验通过才替换，任何异常回滚旧值
        if (configParsed)
        {
            var oldPrefixes = _listener.Prefixes.Cast<string>().ToList();
            var invalid = newPrefixes.Count == 0 ? "未解析到 listen" : ValidatePrefixes(newPrefixes);
            if (invalid is not null)
            {
                WriteLog($"Config warn: [{reason}] {invalid}，保留现有前缀 {string.Join(",", oldPrefixes)}");
            }
            else if (!oldPrefixes.OrderBy(x => x).SequenceEqual(newPrefixes.OrderBy(x => x)))
            {
                try
                {
                    _listener.Prefixes.Clear();
                    foreach (var prefix in newPrefixes)
                    {
                        _listener.Prefixes.Add(prefix);
                    }
                }
                catch (Exception ex)
                {
                    WriteLog($"Config error: [{reason}] listen 应用失败({ex.Message})，回滚为 {string.Join(",", oldPrefixes)}");
                    try
                    {
                        _listener.Prefixes.Clear();
                        foreach (var prefix in oldPrefixes)
                        {
                            _listener.Prefixes.Add(prefix);
                        }
                    }
                    catch (Exception ex2)
                    {
                        WriteLog($"Config error: [{reason}] listen 回滚失败 {ex2.Message}");
                    }
                }
            }
        }

        var banMins = banMinsCfg ?? windowMins;

        lock (_gate)
        {
            Fails = fails;
            WindowMins = windowMins;
            BanMins = banMins;
            Ports = ports;
            Whites = list;
            SshEventIds = sshIds;
        }
        WriteLog($"Config load: [{reason}] listen={string.Join(",", _listener.Prefixes)} mins={windowMins} banmins={banMins} fails={fails} ports={ports} whites={string.Join(',', list)} sshids={string.Join(',', sshIds)}");
    }

    // 用独立 HttpListener 试加前缀做校验：等价于运行期语法规则，且支持 + / * 通配 host；不 Start 不占端口
    private static string? ValidatePrefixes(List<string> prefixes)
    {
        var probe = new HttpListener();
        try
        {
            foreach (var prefix in prefixes)
            {
                probe.Prefixes.Add(prefix);
            }
            return null;
        }
        catch (Exception ex)
        {
            return $"listen 校验失败({ex.Message})";
        }
        finally
        {
            try { probe.Close(); } catch { }
        }
    }

    // #6 FileSystemWatcher 会并发触发同一处理器，重载已由 _configSlim 串行化，异常不外逃
    private async void FWChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            await ReloadConfigAsync(e.ChangeType.ToString());
        }
        catch (Exception ex)
        {
            WriteLog($"Config reload error: {ex.Message}");
        }
    }

    // #5 取消属正常退出，其余异常记录后继续，不能静默吞掉
    private async Task CheckBanExpire(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, token);
                await CheckBanAsync(token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                WriteLog($"Ban check error: {ex.Message}");
                if (!await SafeDelay(1000, token))
                {
                    break;
                }
            }
        }
    }

    // #3 遍历与回收都在 _gate 内完成，不再做全量 ToArray 快照
    // #4 未达阈值的条目在统计窗口清空后即回收，字典不会无界增长
    private async Task CheckBanAsync(CancellationToken token)
    {
        var now = DateTime.Now;
        List<(string Ip, string Name)> expired = [];
        lock (_gate)
        {
            var checkFailTime = now.AddMinutes(-WindowMins);
            List<string> idle = [];
            foreach (var (ip, info) in BanList)
            {
                if (info.Name is not null)
                {
                    if (now >= info.UnBanTime)
                    {
                        expired.Add((ip, info.Name));
                    }
                    continue;
                }
                while (info.List.TryPeek(out var p) && p.FailTime < checkFailTime)
                {
                    info.List.Dequeue();
                }
                if (info.List.Count == 0)
                {
                    idle.Add(ip);
                }
            }
            foreach (var ip in idle)
            {
                BanList.Remove(ip);
            }
        }

        foreach (var (ip, name) in expired)
        {
            var (tcp, tcpe, tcpx) = await UnbanIpAsync($"{name}_tcp", token);
            var (udp, udpe, udpx) = await UnbanIpAsync($"{name}_udp", token);
            lock (_gate)
            {
                BanList.Remove(ip, out _);
            }
            WriteLog($"Ban Delete: {ip} {name}_tcp[{tcp}|{tcpe}|{tcpx}] {name}_udp[{udp}|{udpe}|{udpx}]");
        }
    }

    // #6 事件回调必须保持 void 签名，内部经 _eventSlim 串行化并限制积压上限，避免无界并发与内存堆积
    async void OnSecurityEvent(object? sender, EventRecordWrittenEventArgs e)
    {
        if (e.EventRecord == null || !TryEnterEvent())
        {
            return;
        }
        try
        {
            await _eventSlim.WaitAsync();
            try
            {
                await HandleSecurityEventAsync(e.EventRecord);
            }
            finally
            {
                _eventSlim.Release();
            }
        }
        catch (Exception ex)
        {
            WriteLog($"RDP error: {ex.Message}");
        }
        finally
        {
            ExitEvent();
        }
    }

    async void OnSshEvent(object? sender, EventRecordWrittenEventArgs e)
    {
        if (e.EventRecord == null || !TryEnterEvent())
        {
            return;
        }
        try
        {
            await _eventSlim.WaitAsync();
            try
            {
                await HandleSshEventAsync(e.EventRecord);
            }
            finally
            {
                _eventSlim.Release();
            }
        }
        catch (Exception ex)
        {
            WriteLog($"SSH error: {ex.Message}");
        }
        finally
        {
            ExitEvent();
        }
    }

    private bool TryEnterEvent()
    {
        var pending = Interlocked.Increment(ref _pendingEvents);
        if (pending <= MaxPendingEvents)
        {
            return true;
        }
        Interlocked.Decrement(ref _pendingEvents);
        var dropped = Interlocked.Increment(ref _droppedEvents);
        if (dropped % 100 == 1)
        {
            WriteLog($"Event backlog full({MaxPendingEvents}), dropped={dropped}");
        }
        return false;
    }

    private void ExitEvent()
    {
        Interlocked.Decrement(ref _pendingEvents);
    }

    // #11 只统计网络登录(3)与远程交互登录(10)，并排除账号策略类失败，避免本地/内网登录误封
    private async Task HandleSecurityEventAsync(EventRecord record, CancellationToken token = default)
    {
        var xml = XElement.Parse(record.ToXml());
        var dataDict = ReadEventData(xml);

        int eventId = record.Id;
        string ip = Get(dataDict, "IpAddress");
        string user = Get(dataDict, "TargetUserName");
        string subStatus = Get(dataDict, "SubStatus");
        string logonType = Get(dataDict, "LogonType");

        if (string.IsNullOrWhiteSpace(ip) || ip is "-")
        {
            return;
        }

        if (eventId == 4625)
        {
            if (logonType is not ("3" or "10"))
            {
                WriteLog($"RDP skip: {ip} | {user} | logonType={logonType}");
                return;
            }
            if (!IsCountableFailure(subStatus))
            {
                WriteLog($"RDP skip: {ip} | {user} | {ExplainSubStatus(subStatus)}");
                return;
            }
            var result = await AddFailAsync(ip, "RDP", token);
            WriteLog($"RDP fail: {ip} | {user} | logonType={logonType} | {ExplainSubStatus(subStatus)} | {result}");
        }
        else if (eventId == 4624)
        {
            WriteLog($"RDP success: {ip} | {user} | logonType={logonType}");
        }
    }

    // 账号锁定/禁用/未授予登录类型属配置或策略问题，不计入爆破次数
    private static bool IsCountableFailure(string subStatus) => subStatus.ToLowerInvariant() switch
    {
        "0xc000015b" => false,
        "0xc0000234" => false,
        "0xc0000072" => false,
        "0xc000006e" => false,
        _ => true
    };

    private async Task HandleSshEventAsync(EventRecord record, CancellationToken token = default)
    {
        var xml = XElement.Parse(record.ToXml());
        var dataDict = ReadEventData(xml);
        string payload = Get(dataDict, "payload");

        if (payload.Contains("Connection reset by invalid user"))
        {
            return;
        }

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
            return;
        }

        var result = await AddFailAsync(ip, "SSH", token);
        WriteLog($"SSH fail: {ip} | {user} | {reason} | {result}");
    }

    private async Task<string> AddFailAsync(string ip, string type, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(ip) || ip == "-" || !IPAddress.TryParse(ip, out var ipvalue))
        {
            return "invalid IP";
        }
        ip = ipvalue.ToString();

        int fails;
        int windowMins;
        int banMins;
        string ports;
        lock (_gate)
        {
            if (Whites.Contains(ip))
            {
                return "white IP";
            }
            fails = Fails;
            windowMins = WindowMins;
            banMins = BanMins;
            ports = Ports;
        }

        var now = DateTime.Now;
        var checkFailTime = now.AddMinutes(-windowMins);
        int count;
        int oldCount = 0;
        string? banName = null;
        DateTime unbanTime = default;
        // 计数、老化与封禁判定在同一临界区内完成，杜绝并发下重复封禁/计数错乱
        lock (_gate)
        {
            if (!BanList.TryGetValue(ip, out var ban))
            {
                BanList[ip] = ban = new BanItem();
            }
            while (ban.List.TryPeek(out var p) && p.FailTime < checkFailTime)
            {
                ban.List.Dequeue();
                oldCount++;
            }
            ban.List.Enqueue((type, now));
            count = ban.List.Count;
            if (ban.Name is null && count >= fails)
            {
                // #10 规则名去掉多余下划线，统一为 IPBAN_<ip>
                ban.Name = $"{BanRulePrefix}{ip}";
                ban.UnBanTime = now.AddMinutes(banMins);
                banName = ban.Name;
                unbanTime = ban.UnBanTime.Value;
            }
        }

        if (banName is null)
        {
            return $"Count IP: {ip} {count}/{fails} -{oldCount}+1";
        }

        var (tcp, tcpe, tcpx) = await BanIpAsync(ip, $"{banName}_tcp", "tcp", ports, token);
        var (udp, udpe, udpx) = await BanIpAsync(ip, $"{banName}_udp", "udp", ports, token);
        if (tcpx != 0 || udpx != 0)
        {
            // 规则写入失败则回滚标记，下一轮失败仍可重新封禁，避免长期"假封禁"
            lock (_gate)
            {
                if (BanList.TryGetValue(ip, out var item) && item.Name == banName)
                {
                    item.Name = null;
                    item.UnBanTime = null;
                }
            }
        }
        return $"Ban IP: {ip} {count}/{fails} -{oldCount}+1 until {unbanTime:HH:mm:ss} {banName}_tcp[{tcp}|{tcpe}|{tcpx}] {banName}_udp[{udp}|{udpe}|{udpx}]";
    }

    // 判活：能打开命名互斥体即代表有实例在运行。OpenExisting 只打开不创建，
    // 探测全程不占用 gate，清理期间服务可正常重启。
    // 只认自身使用的 Mutex；对象不存在时抛 WaitHandleCannotBeOpenedException 即判无实例。
    // 权限不足时无法判定，按"在运行"处理——宁可不清理，
    // 也不能误删正在服务的实例所依赖的规则
    private static bool IsInstanceRunning()
    {
        try
        {
            using var probe = Mutex.OpenExisting(InstanceGateName);
            return true;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    private void SpawnCleanupProcess()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe))
            {
                WriteLog("Cleanup process: 无法解析自身路径，跳过");
                return;
            }

            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory,
            };

            // 宿主为 dotnet（dotnet xxx.dll / runfile）时命令行带入口程序集，原样补回；
            // 原生 apphost（Banrpc.exe / AOT）命令行无 .dll，此处自然跳过，不做任何名字判断
            var entry = Environment.GetCommandLineArgs()
                .FirstOrDefault(a => a.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(entry))
            {
                psi.ArgumentList.Add(entry);
            }
            psi.ArgumentList.Add("-c");

            using var p = Process.Start(psi);
            if (p is null)
            {
                WriteLog("Cleanup process: 启动失败");
                return;
            }
            // 不等待子进程结束：清可能是数十秒的逐条 netsh，
            // 在此阻塞会拖慢停止响应（SCM 默认 WaitToKillServiceTimeout 仅 5s），
            // 子进程已独立于父进程运行，父进程退出不影响其完成清理
            WriteLog($"Cleanup process start: pid={p.Id}");
        }
        catch (Exception ex)
        {
            WriteLog($"Cleanup process error: {ex.Message}");
        }
    }

    // #13 不再依赖 "Rule Name"/"规则名称" 等本地化字面量：
    // netsh 输出以空行分块，每块首行固定为"<标签>:<值>"，直接取值做前缀判断，与系统语言无关
    private async Task ClearAllBanRulesAsync(CancellationToken token = default)
    {
        await Task.Delay(100, token);
        WriteLog("Ban delete batch");
        try
        {
            var (output, err, exitCode) = await FirewallAsync("show rule name=all", token);
            if (exitCode != 0)
            {
                WriteLog($"Ban delete query: exit={exitCode} err={err}");
            }
            var lines = output.Split(Environment.NewLine.Replace("\r", "\\r").Replace("\n", "\\n"), StringSplitOptions.None);
            bool expectName = true;
            int deleted = 0;
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0)
                {
                    expectName = true;
                    continue;
                }
                // 块内分隔线（全为 - 或 =）不改变块状态
                if (line.TrimStart('-', '=').Length == 0)
                {
                    continue;
                }
                if (!expectName)
                {
                    continue;
                }
                expectName = false;
                var sep = line.IndexOf(':');
                if (sep <= 0)
                {
                    continue;
                }
                var ruleName = line[(sep + 1)..].Trim();
                if (ruleName.Length == 0 || !ruleName.StartsWith(BanRulePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var (r, re, rx) = await UnbanIpAsync(ruleName, token);
                deleted++;
                WriteLog($"Ban delete: {ruleName}[{r}|{re}|{rx}]");
            }
            WriteLog($"Ban delete done: {deleted}");
        }
        catch (Exception ex)
        {
            WriteLog($"Ban delete error: {ex.Message}");
        }
    }

    private Task<(string result, string err, int exitCode)> BanIpAsync(string ip, string ruleName, string protocol, string ports, CancellationToken token)
    {
        return FirewallAsync($"add rule name=\"{ruleName}\" dir=in action=block remoteip={ip} protocol={protocol} localport={ports} enable=yes profile=any", token);
    }

    private Task<(string result, string err, int exitCode)> UnbanIpAsync(string ruleName, CancellationToken token)
    {
        return FirewallAsync($"delete rule name=\"{ruleName}\"", token);
    }

    // #8 增加执行超时、读取退出码并释放 Process；netsh 删除不存在规则返回 exit=1，调用方需据此判断
    private async Task<(string result, string err, int exitCode)> FirewallAsync(string args, CancellationToken token)
    {
        var result = string.Empty;
        var exitCode = -1;
        string? err;
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
            using var pr = Process.Start(psi);
            if (pr is null)
            {
                err = "process start failed";
                return (result, err, exitCode);
            }
            var outTask = pr.StandardOutput.ReadToEndAsync();
            var errTask = pr.StandardError.ReadToEndAsync();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(FirewallTimeoutMs);
            try
            {
                await pr.WaitForExitAsync(cts.Token);
            }
            catch
            {
                try { pr.Kill(entireProcessTree: true); } catch { }
                using var cts2 = CancellationTokenSource.CreateLinkedTokenSource(token);
                cts2.CancelAfter(5000);
                try { await pr.WaitForExitAsync(cts2.Token); } catch { }
                err = "timeout";
                WriteLog($"Run netsh timeout({FirewallTimeoutMs}ms): {args}");
            }
            result = (await outTask ?? string.Empty).Replace("\r", "\\r").Replace("\n", "\\n");
            err = (await errTask ?? string.Empty).Replace("\r", "\\r").Replace("\n", "\\n");
            exitCode = pr.HasExited ? pr.ExitCode : -1;
        }
        catch (Exception ex)
        {
            err = $"Run netsh error: {ex.Message}";
            WriteLog(err);
        }
        return (result, err, exitCode);
    }

    // #17 多线程并发 AppendAllText 会互相覆盖丢行，改为串行写入
    private void WriteLog(string text)
    {
        var now = DateTime.Now;
        string logLine = $"[{now:HH:mm:ss}] {text}";
        string logFile = Path.Combine(AppContext.BaseDirectory, $"Banrpc{now:yyyyMMdd}.log");
        try
        {
            Logger.LogInformation("{msg}", logLine);
            lock (_logGate)
            {
                File.AppendAllText(logFile, logLine + Environment.NewLine);
            }
        }
        catch
        {
        }
    }

    private static async Task<bool> SafeDelay(int ms, CancellationToken token)
    {
        try
        {
            await Task.Delay(ms, token);
            return !token.IsCancellationRequested;
        }
        catch (OperationCanceledException)
        {
            return false;
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
            "0xc000015b" => "未授予该登录类型",
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
