#:property TargetFramework=net8.0-windows
#:property UseWPF=true
#:property PublishTrimmed=false
#:property PublishAot=false
#:property AllowUnsafeBlocks=true
#:property LangVersion=latest
#:property Nullable=disable

using System;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.WriteLine("usage: dotnet run run_install.cs -- <targetExe>");
    return;
}
var targetExe = Path.GetFullPath(args[0]);
if (!File.Exists(targetExe))
{
    Console.WriteLine("target not found: " + targetExe);
    return;
}

// 自身托管程序集作 startup hook（apphost .exe 改取同目录 .dll）
var selfAsm = typeof(StartupHook).Assembly.Location;
if (Path.GetExtension(selfAsm).Equals(".exe", StringComparison.OrdinalIgnoreCase))
{
    var alt = Path.ChangeExtension(selfAsm, ".dll");
    if (File.Exists(alt)) selfAsm = alt;
}

var psi = new ProcessStartInfo(targetExe)
{
    WorkingDirectory = Path.GetDirectoryName(targetExe)!,
    UseShellExecute = false,
    CreateNoWindow = true,
};
psi.Environment["DOTNET_STARTUP_HOOKS"] = selfAsm;
Process.Start(psi);
Console.WriteLine("injected: " + selfAsm);

public class StartupHook
{
    public static async void Initialize()
    {
        // 注入前：等待主窗口出现（已创建、已加载、可见）
        var p = Process.GetCurrentProcess();
        var time = DateTime.Now;
        while ((DateTime.Now - time).TotalSeconds < 10)
        {
            p.Refresh();
            if (p.HasExited) return;
            if (p.MainWindowHandle != IntPtr.Zero) break;
            await Task.Delay(100);
        }
        await Task.Delay(1000);
        var app = Application.Current;
        if (app is null) return;

        // 在 UI 线程启动 ExtractGameAsync
        var task = app.Dispatcher.Invoke(async () =>
        {
            var mw = app.MainWindow;
            var mi = mw.GetType().GetMethod("ExtractGameAsync",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            if (mi != null)
            {
                var t = (Task)mi.Invoke(mw, null);
                await t;
            }
        });
        await task;
    }
}
