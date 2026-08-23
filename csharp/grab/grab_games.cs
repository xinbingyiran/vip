// 一键注入采集全量游戏列表
// 用法: dotnet run grab_games.cs <注入目标exe完整路径> [sync]
// sync: 可选参数，注入后先调用 ApiService.FetchPasswordsWithStatusAsync 刷新密码，再导出（适合本地缓存密码缺失/过期场景）
// 流程: 复制自身为注入器 -> 设置环境变量 APPDOMAIN_MANAGER_ASM/APPDOMAIN_MANAGER_TYPE(+GRAB_SYNC_PW) -> 启动目标(不碰exe.config) -> 注入完成在客户端目录生成 grab_games.json -> 结束
// 说明: 不杀进程、不清理旧目录；程序集完整名(Version/Culture/PublicKeyToken)从程序集自身获取；完全不修改目标 config
#:property TargetFramework=net48
#:property PlatformTarget=x86
#:property LangVersion=latest
#:property OutputType=Exe
#:property PublishAot=false
#:property Nullable=disable
#:package Microsoft.NETFramework.ReferenceAssemblies@1.0.3

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

if (args.Length < 1) { Console.Error.WriteLine("用法: dotnet run grab_games.cs <注入目标exe完整路径> [sync]"); return 1; }
string clientExe = args[0];
if (!File.Exists(clientExe)) { Console.Error.WriteLine("未找到目标: " + clientExe); return 1; }

string clientDir = Path.GetDirectoryName(clientExe);

// 注入器在客户端进程内直接写固定名 grab_games.json，落点=客户端目录（注入器自身所在目录）
string outPath = Path.Combine(clientDir, "grab_games.json");

string selfExe = Assembly.GetEntryAssembly().Location;
string asmFullName = Assembly.GetEntryAssembly().GetName().FullName; // 完整程序集名(含 Version/Culture/PublicKeyToken)
string dest = Path.Combine(clientDir, Path.GetFileName(selfExe));

// 1) 复制自身为注入器（不清理旧目录）
try { File.Copy(selfExe, dest, true); Console.WriteLine("部署注入器: " + dest); }
catch (Exception ex) { Console.Error.WriteLine("部署失败(目标可能仍在占用本注入器,请先关闭它): " + ex.Message); return 1; }

// 2) 通过环境变量注入 AppDomainManager（完全不碰目标 config）
string oldAsm = Environment.GetEnvironmentVariable("APPDOMAIN_MANAGER_ASM");
string oldType = Environment.GetEnvironmentVariable("APPDOMAIN_MANAGER_TYPE");
Environment.SetEnvironmentVariable("APPDOMAIN_MANAGER_ASM", asmFullName);
Environment.SetEnvironmentVariable("APPDOMAIN_MANAGER_TYPE", "KeyGrabber");

// 可选参数 [sync]：先刷新密码再导出（开关经环境变量传给注入器子进程）
string oldSync = Environment.GetEnvironmentVariable("GRAB_SYNC_PW");
if (args.Length > 1 && (args[1] == "sync" || args[1] == "--sync" || args[1] == "1" || args[1].ToLower() == "true"))
{
    Environment.SetEnvironmentVariable("GRAB_SYNC_PW", "1");
    Console.WriteLine("已启用: 注入后先调用 FetchPasswordsWithStatusAsync 刷新密码再导出");
}

// 3) 启动目标（不预先结束目标进程）
Console.WriteLine("启动目标: " + clientExe);
Process client = Process.Start(new ProcessStartInfo(clientExe) { WorkingDirectory = clientDir });

// 记录启动前状态：结果文件的修改时间（用于判定是否本次新生成）
DateTime oldMtime = File.Exists(outPath) ? File.GetLastWriteTime(outPath) : DateTime.MinValue;

// 恢复当前进程环境变量
Environment.SetEnvironmentVariable("APPDOMAIN_MANAGER_ASM", oldAsm);
Environment.SetEnvironmentVariable("APPDOMAIN_MANAGER_TYPE", oldType);
Environment.SetEnvironmentVariable("GRAB_SYNC_PW", oldSync);

// 4) 等待注入采集完成：以目标进程退出为准（注入器采集完成后会 Environment.Exit(0) 结束客户端）
Console.WriteLine("等待注入采集（目标进程退出后校验结果文件）...");
bool exited = false;
for (int i = 0; i < 240; i++) // 最长约 240s，覆盖注入器轮询上限
{
    Thread.Sleep(1000);
    client.Refresh();
    if (client.HasExited) { exited = true; break; }
}

if (!exited) { Console.Error.WriteLine("超时：目标进程未退出（注入可能未完成或登录阻塞）"); return 1; }

// 进程已退出：校验结果文件是否本次新生成（防止命中旧文件误判成功）
if (!File.Exists(outPath))
{
    Console.Error.WriteLine("目标进程已退出，但未生成 " + outPath);
    return 1;
}
if (File.GetLastWriteTime(outPath) <= oldMtime)
{
    Console.Error.WriteLine("结果文件非本次新生成（mtime 未更新），注入可能失败: " + outPath);
    return 1;
}

string text = File.ReadAllText(outPath);
if (text.TrimStart().StartsWith("ERROR"))
{
    Console.Error.WriteLine("注入失败: " + text);
    return 1;
}
int total = Regex.Matches(text, "\"GameCode\"\\s*:\\s*\"").Count;
int withPw = Regex.Matches(text, "\"Password\"\\s*:\\s*\"(?!\")").Count;
Console.WriteLine("总计 " + total + " 条，带密码 " + withPw + " 条，无密码 " + (total - withPw) + " 条");
Console.WriteLine("结果文件: " + outPath);
return 0;

#if NETFRAMEWORK
public class KeyGrabber : AppDomainManager
{
    private int _try = 0;

    public override void InitializeNewDomain(AppDomainSetup appDomainInfo)
    {
        base.InitializeNewDomain(appDomainInfo);
        try
        {
            new Thread(Loop) { IsBackground = true }.Start();
        }
        catch { }
    }

    void Loop()
    {
        Thread.Sleep(12000);
        while (_try++ < 40)
        {
            try { if (RunAll()) return; } catch { }
            Thread.Sleep(5000);
        }
        WriteResult("ERROR: timeout");
    }

    void WriteResult(string content)
    {
        // 结果直接写固定名 grab_games.json，落点=注入器所在目录(客户端目录)
        string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "grab_games.json");
        try { File.WriteAllText(p, content, new UTF8Encoding(false)); } catch { }
    }

    bool RunAll()
    {
        object app = null;
        try
        {
            var pf = Assembly.Load("PresentationFramework");
            var cur = pf.GetType("System.Windows.Application").GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
            app = cur.GetValue(null);
        }
        catch { }
        if (app == null) return false;

        object vm = null;
        try { vm = app.GetType().GetProperty("MainViewModel", BindingFlags.Public | BindingFlags.Instance).GetValue(app); } catch { }
        if (vm == null) return false;

        // 获取 Dispatcher/Invoke（UI 线程执行）
        object disp = null;
        MethodInfo invoke = null;
        try
        {
            disp = app.GetType().GetProperty("Dispatcher", BindingFlags.Public | BindingFlags.Instance).GetValue(app);
            invoke = disp.GetType().GetMethods().First(x => x.Name == "Invoke" && x.GetParameters().Length == 1 && x.GetParameters()[0].ParameterType.Name == "Action");
        }
        catch { }
        if (invoke == null) return false;

        // 可选: 先调用 ApiService.FetchPasswordsWithStatusAsync 刷新密码，再导出
        // ApiService 实例取自 App 的 public 属性 ApiService（Application.Current），不依赖 MainViewModel 私有字段
        if (Environment.GetEnvironmentVariable("GRAB_SYNC_PW") == "1")
        {
            try
            {
                object api = app.GetType().GetProperty("ApiService", BindingFlags.Public | BindingFlags.Instance).GetValue(app);
                if (api != null)
                {
                    var m = api.GetType().GetMethod("FetchPasswordsWithStatusAsync", BindingFlags.Public | BindingFlags.Instance);
                    if (m != null)
                    {
                        object ptask = null;
                        Action pact = () => { ptask = m.Invoke(api, null); };
                        invoke.Invoke(disp, new object[] { pact });
                        if (ptask != null)
                        {
                            var awaiter = ptask.GetType().GetMethod("GetAwaiter").Invoke(ptask, null);
                            awaiter.GetType().GetMethod("GetResult").Invoke(awaiter, null);
                        }
                    }
                }
            }
            catch { }
        }

        object task = null;
        try
        {
            var m = vm.GetType().GetMethod("GetAllGamesForExportAsync", BindingFlags.Public | BindingFlags.Instance);
            if (m == null) return false;
            Action act = () => { task = m.Invoke(vm, null); };
            invoke.Invoke(disp, new object[] { act });
        }
        catch { return false; }
        if (task == null) return false;

        try
        {
            object awaiter = task.GetType().GetMethod("GetAwaiter").Invoke(task, null);
            awaiter.GetType().GetMethod("GetResult").Invoke(awaiter, null);
            object result = task.GetType().GetProperty("Result").GetValue(task);
            var list = result as System.Collections.IEnumerable;
            if (list == null) return false;

            var sb = new StringBuilder();
            sb.Append("[\n");
            int n = 0;
            foreach (var item in list)
            {
                if (n > 0) sb.Append(",\n");
                sb.Append(SerializeObj(item, 1));
                n++;
            }
            sb.Append("\n]\n");
            WriteResult(sb.ToString());

            // 注入完成、生成结果后结束进程
            Environment.Exit(0);
            return true;
        }
        catch { return false; }
    }

    static readonly string[] KeepFields =
    {
        "GameCode", "Password", "ChineseName", "EnglishName",
        "UpdateDateTooltip", "GameVersion", "Size", "DownloadUrl1", "DownloadUrl2"
    };

    static string SerializeObj(object o, int indent)
    {
        var sb = new StringBuilder();
        sb.Append("{\n");
        int i = 0;
        foreach (var pi in o.GetType().GetProperties())
        {
            if (Array.IndexOf(KeepFields, pi.Name) < 0) continue;
            object v;
            try { v = pi.GetValue(o); } catch { v = null; }
            if (i > 0) sb.Append(",\n");
            sb.Append(new string(' ', indent * 2)).Append('"').Append(Esc(pi.Name)).Append("\": ");
            sb.Append(Scalar(v));
            i++;
        }
        sb.Append("\n").Append(new string(' ', (indent - 1) * 2)).Append('}');
        return sb.ToString();
    }

    static string Scalar(object v)
    {
        if (v == null) return "null";
        if (v is bool b) return b ? "true" : "false";
        if (v is string s) return "\"" + Esc(s) + "\"";
        if (v is DateTime dt) return "\"" + dt.ToString("yyyy-MM-dd HH:mm:ss") + "\"";
        var tc = Type.GetTypeCode(v.GetType());
        if (tc == TypeCode.Object) return "\"" + Esc(v.ToString()) + "\"";
        return v.ToString().Replace(",", ".");
    }

    static string Esc(string s)
    {
        if (s == null) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
    }
}
#endif
