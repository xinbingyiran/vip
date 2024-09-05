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

    private static bool FillPass(string pass)
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
                            return true;
                        }
                        else if (v is TextBox textBox)
                        {
                            Application.Current.Dispatcher.Invoke(new Action(() => { textBox.Text = pass; }));
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }

    private static bool FindCurrentPass(out int pass)
    {
        //开始启动
        pass = 0;
        try
        {
            string[] passlist = [];
            {
                var time = DateTime.Now;
                while ((DateTime.Now - time).TotalSeconds < 10 && passlist.Length == 0)
                {
                    Thread.Sleep(100);
                    passlist = Application.Current.Dispatcher.Invoke(() => FindPass().ToArray()) as string[];
                }
            }
            if (passlist == null || passlist.Length == 0)
            {
                return false;
            }
            if (passlist.Length == 1)
            {
                var time = DateTime.Now;
                var firstPass = passlist[0];
                while ((DateTime.Now - time).TotalSeconds < 10)
                {
                    Thread.Sleep(100);
                    if (Application.Current.Dispatcher.Invoke(() => FillPass(firstPass)) is bool b && b)
                    {
                        break;
                    }
                }
            }
            else if (passlist.Length > 1)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageBox.Show(string.Join("，", passlist), "可能的密码", MessageBoxButton.OK, MessageBoxImage.Information);
                }));
            }
            return int.TryParse(passlist.FirstOrDefault() ?? "0", out pass) && pass != 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool EnableAllPass(out int pass)
    {
        //打开我
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
            //var jhm_f = main.GetType().GetField("jhm", BindingFlags.Instance | BindingFlags.NonPublic);
            //if (jhm_f is not null)
            //{
            //    var find = false;
            //    Hyperlink link = null;
            //    foreach (Window win in Application.Current.Windows)
            //    {
            //        foreach (var c in Find<TextBlock>(win))
            //        {
            //            foreach (Hyperlink hl in c.Inlines.Where(e => e is Hyperlink il && il.Inlines.Any(s => s is Run r && r.Text == "查看旧密码")))
            //            {
            //                hl.DoClick();
            //                link = hl;
            //                find = true;
            //            }
            //            //var click_f = c.GetType().GetMethod("Jiu_kan_Click", BindingFlags.Instance | BindingFlags.NonPublic);
            //            //if (click_f is not null)
            //            //{
            //            //    click_f.Invoke(c, [null, null]);
            //            //    find = true;
            //            //}
            //        }
            //    }
            //    if (link != null)
            //    {
            //        var btn = new Button { Content = "点我！", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            //        btn.Click += (s, e) =>
            //        {
            //            link.DoClick();
            //        };
            //        var win = new Window { WindowStyle = WindowStyle.ToolWindow, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = Application.Current.MainWindow, Width = 100, MaxWidth = 100, Height = 60, MaxHeight = 60, Content = btn };
            //        win.Show();
            //    }
            //    if (!find)
            //    {
            //        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            //        {
            //            MessageBox.Show("当前游戏的激活码是：" + jhm_f.GetValue(main));
            //        }));
            //    }
            //}
            var jhm1_f = main.GetType().GetField("jhm1", BindingFlags.Instance | BindingFlags.NonPublic);
            if (jhm1_f is not null)
            {
                int.TryParse(jhm1_f.GetValue(main) as string ?? "0", out pass);
            }
            shijian3.Start();
            if (pass == 0)
            {
                pass = -2;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
    public static int Inject(string _)
    {
        return EnableAllPass(out var pass) || FindCurrentPass(out pass) ? pass : -1;

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
                throw new Exception("进程已退出！");
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
        if (result == -1)
        {
            throw new Exception("目标未找到！");
        }
        if (result == 0)
        {
            throw new Exception("注入失败了！");
        }
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
