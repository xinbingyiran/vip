// muluInject.cs: 启动目标exe, 用 ClrMD 定位 JiHuo 实例地址, 注入后直接调用 JieYaGuoCheng
// 用法: dotnet run muluInject.cs <注入目标exe完整路径>
#:include FastWin32.cs
#:property TargetFramework=net48
#:property UseWPF=true
#:property PlatformTarget=x64
#:property LangVersion=latest
#:property PublishAot=false
#:property AllowUnsafeBlocks=true
#:package Microsoft.NETFramework.ReferenceAssemblies@1.0.3
#:package Microsoft.Diagnostics.Runtime@3.1.512801

using FastWin32.Diagnostics;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;

if (args.Length == 1 && args[0].EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
{
    var file = Path.GetFullPath(args[0]);
    var dllFile = Assembly.GetEntryAssembly().Location;

    var p = Process.Start(file);
    var time = DateTime.Now;
    while ((DateTime.Now - time).TotalSeconds < 10)
    {
        p.Refresh();
        if (p.HasExited) return;
        if (p.MainWindowHandle != IntPtr.Zero) break;
        p.WaitForInputIdle(1000);
    }

    Thread.Sleep(1000);

    using var dt = DataTarget.AttachToProcess(p.Id, suspend: true);
    using var rt = dt.ClrVersions[0].CreateRuntime();
    var addr = rt.Heap.EnumerateObjects().FirstOrDefault(o => o.Type?.Name == "JiHuoA.JiHuo").Address;

    Injector.InjectManaged((uint)p.Id, dllFile, "Trigger", "Inject", addr.ToString(CultureInfo.InvariantCulture));
}
else
{
    Console.WriteLine("用法: " + Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName) + " <注入目标exe路径>");
}

// 注入入口: 目标进程加载本程序集后, 用传入的实例地址直接调用 JieYaGuoCheng
public static class Trigger
{
    public static int Inject(string args)
    {
        try
        {
            var app = Application.Current;
            if (app == null) return -6; // 无 WPF Application
            if (!ulong.TryParse(args, out ulong addr)) return -5; // 无有效地址
            var raw = new IntPtr(unchecked((long)addr));
            var inst = Unsafe.As<IntPtr, object>(ref raw);
            if (inst == null) return -4;
            var m = inst.GetType().GetMethod("JieYaGuoCheng", BindingFlags.NonPublic | BindingFlags.Instance);
            if (m == null) return -3;
            app.Dispatcher.Invoke(() => m.Invoke(inst, null));
            return 1;
        }
        catch
        {
            return -1;
        }
    }
}
