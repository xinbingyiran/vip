using FastWin32.Diagnostics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace muluInject;

public class Program
{

    private static IEnumerable<T> Find<T>(DependencyObject root)
        where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t)
            {
                yield return t;
            }
            foreach (var e in Find<T>(child))
            {
                yield return e;
            }
        }

    }

    private static int Extract()
    {
        foreach (Window win in Application.Current.Windows)
        {
            foreach (var item in Find<UserControl>(win))
            {
                foreach (var f in item.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                {
                    var value = f.GetValue(item);
                    if (value is null)
                    {
                        continue;
                    }
                    var method = value.GetType().GetMethod("JieYaGuoCheng", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (method is null)
                    {
                        continue;

                    }
                    method.Invoke(value, null);
                    return 1;
                }
            }
        }
        return 0;
    }

    public static int Inject(string args)
    {
        try
        {
            Thread.Sleep(3000);
            var result = (int)Application.Current.Dispatcher.Invoke(() => Extract());
            return result;
        }
        catch
        {
            return -1;
        }

    }
    private static void DecryptProgress(string file)
    {
        file = Path.GetFullPath(file);
        var ass = Assembly.GetEntryAssembly();
        var dllFile = ass.Location;
        var p = Process.Start(file);
        var time = DateTime.Now;
        while ((DateTime.Now - time).TotalSeconds < 10)
        {
            if (p.HasExited)
            {
                return;
            }
            if (p.MainWindowHandle == IntPtr.Zero)
            {
                p.WaitForInputIdle(100);
                p.Refresh();
                continue;
            }
            break;
        }
        Injector.InjectManaged((uint)p.Id, dllFile, typeof(Program).FullName, nameof(Inject), string.Empty, out var result);
    }


    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0].EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                DecryptProgress(args[0]);
            }
            else
            {
                MessageBox.Show($"用法：{Environment.NewLine}    {Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName)} 要注入的exe文件路径", "用法", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "出错了！", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
