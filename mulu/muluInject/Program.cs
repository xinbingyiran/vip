using FastWin32.Diagnostics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace muluInject;

public class Program
{
    public static int Inject(string _)
    {
        try
        {
            return (int)Application.Current.Dispatcher.Invoke(InjectMethod);
        }
        catch (Exception ex)
        {
            MessageBox.Show("1:" + ex.Message);
        }

        return 0;
    }

    private static int InjectMethod()
    {
        return EnableAllPass(out var pass) || FindCurrentPass(out pass) ? pass : 0;

    }

    private static bool FindCurrentPass(out int pass)
    {
        //测试 开始启动
        var passlist = FindPass().ToArray();
        if (passlist.Length == 1)
        {
            FillPass(passlist[0]);
        }
        else if (passlist.Length > 1)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                MessageBox.Show("可能的密码是：" + string.Join(",", passlist));
            }));
        }
        return int.TryParse(passlist.FirstOrDefault() ?? "0", out pass) && pass != 0;
    }

    private static bool EnableAllPass(out int pass)
    {
        //测试 打开我
        pass = 0;
        try
        {
            if (!Application.Current.Resources.Contains("Locator"))
            {
                return false;
            }
            var locator = Application.Current.Resources["Locator"];
            if (locator is null)
            {
                return false;
            }
            var method = locator.GetType().GetProperty("Main");
            if (method is null)
            {
                return false;
            }
            var main = method.GetValue(locator, null);
            if (main == null)
            {
                return false;
            }
            var shijian3_f = main.GetType().GetField("shijian3", BindingFlags.Static | BindingFlags.NonPublic);
            if (shijian3_f is null)
            {
                return false;
            }
            var shijian3 = shijian3_f.GetValue(null) as DispatcherTimer;
            if (shijian3 is null)
            {
                return false;
            }

            var jhm_f = main.GetType().GetField("jhm", BindingFlags.Instance | BindingFlags.NonPublic);
            if (jhm_f is not null)
            {
                var find = false;
                foreach (Window win in Application.Current.Windows)
                {
                    foreach (var c in Find<UserControl>(win))
                    {
                        var click_f = c.GetType().GetMethod("Jiu_kan_Click", BindingFlags.Instance | BindingFlags.NonPublic);
                        if (click_f is not null)
                        {
                            click_f.Invoke(c, [null, null]);
                            find = true;
                        }
                    }
                }
                if (!find)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        MessageBox.Show("当前游戏的激活码是：" + jhm_f.GetValue(main));
                    }));
                }
            }

            var jhm1_f = main.GetType().GetField("jhm1", BindingFlags.Instance | BindingFlags.NonPublic);
            if (jhm1_f is not null)
            {
                int.TryParse(jhm1_f.GetValue(main) as string ?? "0", out pass);
            }


            shijian3.Start();
            return true;
        }
        catch
        {
            return false;
        }
    }

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

    private static IEnumerable<string> FindPass()
    {
        var strType = typeof(string);
        foreach (Window win in Application.Current.Windows)
        {
            foreach (var item in Find<UserControl>(win))
            {
                foreach (var f in item.GetType().GetFields(BindingFlags.Static | BindingFlags.Public))
                {
                    if (f.FieldType == strType)
                    {
                        if (f.GetValue(null) is string v && v.Length == 6 && int.TryParse(v, out var _))
                        {
                            yield return v;
                        }
                    }
                }
            }
        }
    }

    private static void FillPass(string pass)
    {
        var strType = typeof(string);
        foreach (Window win in Application.Current.Windows)
        {
            foreach (var item in Find<ContentControl>(win))
            {
                foreach (var f in item.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (f.Name == "MiMaKuang")
                    {
                        var v = f.GetValue(item);
                        if (v is PasswordBox password)
                        {
                            Application.Current.Dispatcher.Invoke(new Action(() => { password.Password = pass; }));
                        }
                        else if (v is TextBox textBox)
                        {
                            Application.Current.Dispatcher.Invoke(new Action(() => { textBox.Text = pass; }));
                        }
                    }
                }
            }
        }
    }

    private static void DecryptProgress(string file)
    {
        Console.WriteLine("------------------------------------------------------------------------");
        file = Path.GetFullPath(file);
        Console.WriteLine(file);
        var ass = Assembly.GetEntryAssembly();
        var dllFile = ass.Location;
        try
        {
            var p = Process.Start(file);
            Console.WriteLine("准备好后，按任意键继续。");
            Console.ReadKey();
            p.Refresh();
            if(p.HasExited)
            {
                Console.WriteLine("Emmm....目标已退出！");
            }
            else
            {
                Injector.InjectManaged((uint)p.Id, dllFile, typeof(Program).FullName, nameof(Inject), string.Empty, out var result);
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"结果：{result}");
                Console.ResetColor();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"发生错误：{ex.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("------------------------------------------------------------------------");
    }

    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length == 1)
        {
            DecryptProgress(args[0]);
        }
        else
        {
            Console.WriteLine("请给我一个可执行文件。");
        }

        Console.WriteLine("------------------## 结束 ##--------------------");
        Console.ReadKey();
    }
}
