#:package Microsoft.Diagnostics.Runtime@3.1.512801
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Diagnostics.Runtime;

// =====================================================================
// 用法:
//   dotnet run grab_dump.cs -- --pid <pid>
//   dotnet run grab_dump.cs -- <exe路径> [等待毫秒]
// ClrMD 挂起附加 -> 枚举全部类型静态字段运行时值 -> detach(进程继续运行)
// =====================================================================

if (args.Length == 0)
{
    Console.Error.WriteLine("用法: dotnet run grab_dump.cs -- --pid <pid> | <exe路径> [等待毫秒]");
    return;
}

int pid;
if (args[0] == "--pid")
{
    pid = int.Parse(args[1]);
}
else
{
    var exe = Path.GetFullPath(args[0]);
    if (!File.Exists(exe)) { Console.Error.WriteLine($"文件不存在: {exe}"); return; }
    int waitMs = args.Length > 1 ? int.Parse(args[1]) : 6000;
    Console.WriteLine($"[启动] {exe}");
    var psi = new ProcessStartInfo(exe)
    {
        WorkingDirectory = Path.GetDirectoryName(exe)!,
        UseShellExecute = false,
    };
    var p = Process.Start(psi);
    if (p == null) { Console.Error.WriteLine("启动失败"); return; }
    pid = p.Id;
    Console.WriteLine($"[PID] {pid}, 等待 {waitMs}ms 初始化...");
    Thread.Sleep(waitMs);
}

try
{
    using var dt = DataTarget.AttachToProcess(pid, suspend: true);
    Console.WriteLine($"[附加] PID={pid}");
    using var runtime = dt.ClrVersions[0].CreateRuntime();
    var heap = runtime.Heap;
    var ad = runtime.SharedDomain ?? runtime.AppDomains.FirstOrDefault();
    if (ad == null) { Console.Error.WriteLine("无可用 AppDomain"); return; }

    var modules = runtime.EnumerateModules()
        .Where(m => !string.IsNullOrEmpty(m.Name))
        .ToList();

    int total = 0, ok = 0, fail = 0;
    foreach (var mod in modules)
    {
        var mname = Path.GetFileName(mod.Name);
        // 只处理业务模块,排除框架/系统 dll。
        // 注意:不再按 exe 文件名前缀过滤——DNGuard 壳(KaiShiYouXi.exe)与业务程序集(douyljq.dll)可能不同名,
        // 之前按 exe 名过滤会把 douyljq.dll 整个跳过导致 exe 模式无输出。
        if (mname == null || mname.StartsWith("System.", StringComparison.OrdinalIgnoreCase)
            || mname.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)
            || mname.StartsWith("Presentation", StringComparison.OrdinalIgnoreCase)
            || mname.StartsWith("WindowsBase", StringComparison.OrdinalIgnoreCase)
            || mname.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase)
            || mname.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase)
            || mname.StartsWith("vcruntime", StringComparison.OrdinalIgnoreCase)
            || mname.StartsWith("clr", StringComparison.OrdinalIgnoreCase)
            || mname.StartsWith("concrt", StringComparison.OrdinalIgnoreCase)
            || mname.StartsWith("ucrtbase", StringComparison.OrdinalIgnoreCase)) continue;

        try
        {
            foreach (var kv in mod.EnumerateTypeDefToMethodTableMap())
            {
                ClrType? type;
                try { type = runtime.GetTypeByMethodTable(kv.MethodTable); }
                catch { continue; }
                if (type == null) continue;

                var sfs = type.StaticFields;
                if (sfs.Length == 0) continue;

                var lines = new List<string>();
                foreach (var sf in sfs)
                {
                    total++;
                    if (sf == null || sf.Type == null || !sf.Type.IsString) continue; // 只处理 string 类型
                    try
                    {
                        var s = sf.ReadString(ad);
                        if (s == null || string.IsNullOrWhiteSpace(s)) continue; // 空/null/空白字符串跳过
                        ok++;
                        lines.Add($"      {sf.Name} = \"{s}\"");
                    }
                    catch (Exception ex)
                    {
                        fail++;
                        Console.Error.WriteLine($"      {sf.Name} = <读值异常: {ex.GetType().Name}>");
                    }
                }
                if (lines.Count == 0) continue; // 无输出项则不打印类型头
                Console.WriteLine($"== {type.Name} ({mname})");
                foreach (var line in lines) Console.WriteLine(line);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[模块异常] {mname}: {ex.GetType().Name}: {ex.Message}");
            break; // ClrMD 内部状态可能已损坏,继续枚举有 native 崩溃风险,业务数据已到手则提前收尾
        }
    }
    Console.WriteLine($"[统计] 字段总数 {total}, 成功 {ok}, 失败 {fail}");
    Console.WriteLine("[完成] detach,目标进程继续运行");
}
catch (Exception ex)
{
    Console.Error.WriteLine("[崩溃] " + ex);
}
