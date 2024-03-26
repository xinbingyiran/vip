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
        return int.Parse(passlist.FirstOrDefault() ?? "0");
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
        Console.WriteLine("## 进程注入 ##");
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
            Injector.InjectManaged((uint)p.Id, dllFile, typeof(Program).FullName, nameof(Inject), string.Empty, out var result);
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"解密结果：{result}");
            Console.ResetColor();
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
            Console.WriteLine("请指定参数");
        }

        Console.WriteLine("------------------## 结束 ##--------------------");
        Console.ReadKey();
    }
}
