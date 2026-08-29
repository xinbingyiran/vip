// muluInject.cs: 启动目标exe并注入自身, 反射触发其 JieYaGuoCheng 解压
// 用法: dotnet run muluInject.cs <注入目标exe完整路径>
#:include FastWin32.cs
#:property TargetFramework=net48
#:property UseWPF=true
#:property PlatformTarget=AnyCPU
#:property LangVersion=latest
#:property PublishAot=false
#:property AllowUnsafeBlocks=true
#:package Microsoft.NETFramework.ReferenceAssemblies@1.0.3

using FastWin32.Diagnostics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

// 顶级语句 = 编排器: 启动目标exe, 等待主窗口, 注入自身
if (args.Length == 1 && args[0].EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
{
    var file = Path.GetFullPath(args[0]);
    var dllFile = Assembly.GetEntryAssembly().Location;
    var p = Process.Start(file);
    var time = DateTime.Now;
    while ((DateTime.Now - time).TotalSeconds < 10)
    {
        if (p.HasExited) return;
        if (p.MainWindowHandle == IntPtr.Zero)
        {
            p.WaitForInputIdle(100);
            p.Refresh();
            continue;
        }
        break;
    }
    Injector.InjectManaged((uint)p.Id, dllFile, "Trigger", "Inject", string.Empty, out _);
}
else
{
    Console.WriteLine("用法: " + Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName) + " <注入目标exe路径>");
}

// 注入入口: 目标进程加载本程序集后反射调用 Trigger.Inject(string)
public static class Trigger
{
    public static int Inject(string args)
    {
        try
        {
            Thread.Sleep(3000);
            return (int)Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (Window win in Application.Current.Windows)
                    foreach (var item in Find(win))
                        foreach (var f in item.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                        {
                            var v = f.GetValue(item);
                            if (v is null) continue;
                            var m = v.GetType().GetMethod("JieYaGuoCheng", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (m is null) continue;
                            m.Invoke(v, null);
                            return 1;
                        }
                return 0;
            });
        }
        catch { return -1; }
    }

    private static IEnumerable<UserControl> Find(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is UserControl t) yield return t;
            foreach (var e in Find(child)) yield return e;
        }
    }
}
