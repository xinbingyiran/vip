using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static QRCoder.PayloadGenerator;

namespace AliHelper
{
    /// <summary>
    /// TianYiWindow.xaml 的交互逻辑
    /// </summary>
    public partial class TianYiWindow : Window
    {
        [GeneratedRegex("[^0-9]+")]
        private static partial Regex NonNumberRegex();

        private static readonly Encoding? Encoding;
        private static readonly ICryptoTransform des = DES.Create().CreateDecryptor(Encoding.UTF8.GetBytes("consmkey"), [18, 52, 86, 120, 144, 171, 205, 239]);
        private static readonly string h = "wg.MtbH&zvqS^!(d";
        private static readonly string c = "PTU18vZQEQka6CE2TOvwU/jsNrqWiGxPLm9u8oyznLFZbPOHTANpHM0yDKcB4J/bc6OS2RY2MuIKZS40b1ROZ+DbwG15s/m6ACXvz7L0Bnx5CyHuptfB+sR0JK3ljmsHlFDi2jh59r+pGNHho+st8AyS6ORMI/fNtTgOSUk8xTYHouvOdINg2EYdAuZaWttngunBnpawqYPmQQ+T0TYHtg==";
        private static readonly string f = "CWxXAmuJ0+QfAzurL4R8qZF5nUJq1YSyCO38gIlExuh66ZGv1k0De1yzZh13+agfe8oop8cS6UlsAK7DIHBbx8UMovijNBjiA/U2AuFNwbQ=";
        private static readonly string? cstr;
        private static readonly byte fb;
        private static readonly HttpClient _client = new();

        private static string Decrypt(string toDecrypt)
        {
            if (string.IsNullOrEmpty(toDecrypt))
            {
                return toDecrypt;
            }
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(h);
                byte[] array = Convert.FromBase64String(toDecrypt);
                var aes = Aes.Create();
                aes.Key = bytes;
                var bytes2 = aes.DecryptEcb(array, PaddingMode.PKCS7);
                return Encoding.UTF8.GetString(bytes2);
            }
            catch (Exception)
            {
                return toDecrypt;
            }
        }
        public static string? DESDecrypt(string? decryptString)
        {
            if (string.IsNullOrEmpty(decryptString))
            {
                return decryptString;
            }
            try
            {
                byte[] array = Convert.FromBase64String(decryptString);
                using var ms = new MemoryStream();
                using var cs = new CryptoStream(ms, des, CryptoStreamMode.Write);
                cs.Write(array, 0, array.Length);
                cs.FlushFinalBlock();
                return Encoding.UTF8.GetString(ms.ToArray());
            }
            catch
            {
                return decryptString;
            }
        }
        public static async Task<string> DecodecAsync(string path, byte password)
        {
            string result = string.Empty;
            try
            {
                byte[] bytes;
                if (path.StartsWith("http"))
                {
                    bytes = await _client.GetByteArrayAsync(path);
                }
                else
                {
                    bytes = await File.ReadAllBytesAsync(path);
                }
                result = Encoding!.GetString(bytes.Select(e => (byte)(e ^ password)).ToArray());
            }
            catch (Exception ex)
            {
                Console.WriteLine("解密失败: " + ex.Message);
            }
            return result;
        }

        static TianYiWindow()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Encoding = Encoding.GetEncoding("GBK");
            cstr = Decrypt(Decrypt(Decrypt(Decrypt(c))));
            fb = (byte)int.Parse(Decrypt(Decrypt(Decrypt(Decrypt(f)))));
        }

        private JsonSerializerOptions serializerOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };


        public string? Status
        {
            get { return (string?)GetValue(StatusProperty); }
            set { SetValue(StatusProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Status.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register("Status", typeof(string), typeof(TianYiWindow), new PropertyMetadata(""));



        public string? Code
        {
            get { return (string?)GetValue(CodeProperty); }
            set { SetValue(CodeProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Code.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CodeProperty =
            DependencyProperty.Register("Code", typeof(string), typeof(TianYiWindow), new PropertyMetadata(""));



        public string? Pwd
        {
            get { return (string?)GetValue(PwdProperty); }
            set { SetValue(PwdProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Pwd.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PwdProperty =
            DependencyProperty.Register("Pwd", typeof(string), typeof(TianYiWindow), new PropertyMetadata(""));



        public string? Url
        {
            get { return (string?)GetValue(UrlProperty); }
            set { SetValue(UrlProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Url.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty UrlProperty =
            DependencyProperty.Register("Url", typeof(string), typeof(TianYiWindow), new PropertyMetadata(""));




        public string? Source
        {
            get { return (string?)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Source.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(string), typeof(TianYiWindow), new PropertyMetadata(""));



        public TianYiFolderItem? Root
        {
            get { return (TianYiFolderItem?)GetValue(RootProperty); }
            set { SetValue(RootProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Root.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty RootProperty =
            DependencyProperty.Register("Root", typeof(TianYiFolderItem), typeof(TianYiWindow), new PropertyMetadata(null));

        private bool _isLoading;
        public TianYiWindow()
        {
            InitializeComponent();
            CommandBindings.Add(new CommandBinding(Commands.Login, OnLogin));
            CommandBindings.Add(new CommandBinding(Commands.List, OnList, CanList));
            CommandBindings.Add(new CommandBinding(Commands.Get, OnGet));
            CommandBindings.Add(new CommandBinding(Commands.Copy, OnCopy, CanCopy));
            CommandBindings.Add(new CommandBinding(Commands.DownLoad, OnDownLoad, CanDownLoad));
        }
        private void CanList(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = !_isLoading && !string.IsNullOrEmpty(Url) && !string.IsNullOrEmpty(Code);
        }
        private void CanCopy(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = e.Parameter is not null;
        }
        private void CanDownLoad(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = e.Parameter is not null;
        }
        private async void OnLogin(object sender, RoutedEventArgs e)
        {
            try
            {
                _isLoading = true;
                UpdateStatus($"登录中。。。");
                var str = await DecodecAsync(cstr + "a/a.txt", fb);
                var ini = Ini.ParseString(str);
                Url = ini.GetStringValue("C3", "ZaiXian_DiZhi", "");
                UpdateStatus($"登录成功！");
            }
            catch (Exception ex)
            {

                UpdateStatus($"登录失败：{ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }

        }

        private TianYiViewItem[] GetItems(Func<string, string> urlGetter, JsonElement element)
        {
            if (element.TryGetProperty("code", out var code))
            {
                var codeStr = code.GetRawText();
                if (codeStr != "0")
                {
                    throw new Exception($"【{codeStr}】：{(element.TryGetProperty("msg",out var msg) ? msg.GetString() : "未知错误")}");
                }
                else if (element.TryGetProperty("data", out var newElement))
                {
                    element = newElement;
                }
                else
                {
                    throw new KeyNotFoundException("未找到有效数据data");
                }
            }
            if (element.ValueKind == JsonValueKind.Array)
            {
                return [.. element.EnumerateArray().Select(e => GetItemCore(urlGetter, e))];
            }
            else
            {
                return [GetItemCore(urlGetter, element)];
            }
        }
        private TianYiViewItem GetItemCore(Func<string, string> urlGetter, JsonElement element)
        {
            var name = element.GetProperty("filename").GetString() ?? "";
            var id = element.GetProperty("id").GetRawText().Trim('\"');
            var folder = element.GetProperty("isFolder").GetBoolean();
            return folder ? new TianYiFolderItem(name)
            {
                Items = element.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array
                ? [.. children.EnumerateArray().Select(e => GetItemCore(urlGetter, e))]
                : null
            } : new TianYiFileItem(name, element.GetProperty("rongliang").GetInt64())
            {
                Url = urlGetter.Invoke(id)
            };
        }

        private static IEnumerable<string> CollectFileUrls(TianYiViewItem[] items, string folder)
        {
            foreach (var item in items)
            {
                if (item is TianYiFileItem fi)
                {
                    yield return $"{folder}{fi.Name}： {fi.Url}";
                }
                else if (item is TianYiFolderItem di && di.Items is TianYiViewItem[] subItems)
                {
                    var subFolder = $"{folder}{di.Name}/";
                    foreach (var result in CollectFileUrls(subItems, subFolder))
                    {
                        yield return result;
                    }
                }
            }
        }


        private async void OnList(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                _isLoading = true;
                UpdateStatus("获取列表中。。。");
                var sharePara = $"code={Code}";
                if (Pwd is string pwd && !string.IsNullOrEmpty(pwd))
                {
                    sharePara += $"&pwd={pwd}";
                }
                var url = Url;
                var result = await _client.GetStringAsync($"{url}/share/list?{sharePara}");
                var idUrl = $"{url}/share/url?{sharePara}&id=";
                using var document = JsonDocument.Parse(result);
                var root = document.RootElement;
                var rootItems = GetItems(FileUrlGetter, root);
                Root = new TianYiFolderItem("root")
                {
                    Items = rootItems
                };
                Source = string.Join(Environment.NewLine, CollectFileUrls(rootItems, "/"));
                UpdateStatus("获取列表成功！");
                string FileUrlGetter(string id)
                {
                    return idUrl + id;
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"获取列表失败：{ex.Message}");
            }
            finally
            {
                _isLoading = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void OnGet(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Parameter is TianYiViewItem item)
            {
                Source = string.Join(Environment.NewLine, CollectFileUrls([item], "/"));
            }
        }

        private void OnCopy(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Parameter is not string str)
            {
                UpdateStatus("未指定源。。。");
                return;
            }
            if (!string.IsNullOrWhiteSpace(str))
            {
                try
                {
                    Clipboard.SetText(str);
                    UpdateStatus($"已复制到剪切板。");
                }
                catch (Exception ex)
                {
                    UpdateStatus($"复制到剪切板失败：{ex.Message}");
                }
                return;
            }
        }
        private void OnDownLoad(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Parameter is not string url)
            {
                UpdateStatus("未指定源。。。");
                return;
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = url,
                });
            }
        }


        private void UpdateStatus(string status)
        {
            if (!CheckAccess())
            {
                Dispatcher.Invoke(() => UpdateStatus(status));
                return;
            }
            this.Status = status;
        }

        public class Ini
        {

            public static Encoding Encoding { get; set; } = Encoding.GetEncoding("gbk");

            public Dictionary<string, Dictionary<string, object>> Data { get; set; } = new();

            public static Ini ParseString(string context)
            {
                Ini ini = new();
                var array = context.Split('\n');
                string? text = null;
                foreach (string? text2 in array)
                {
                    if (string.IsNullOrEmpty(text2))
                    {
                        continue;
                    }
                    string text3 = text2.Trim();
                    if (IsAnnotation(text3))
                    {
                        continue;
                    }
                    if (text3.StartsWith('[') && text3.EndsWith(']') && text3.Length >= 3)
                    {
                        text = text3[1..^1];
                        continue;
                    }
                    int num = text3.IndexOf('=');
                    if (num > 0 && num + 1 < text3.Length && text != null)
                    {
                        string key = text3[..num].Trim();
                        string value = text3[(num + 1)..].Trim();
                        ini.Add(text, key, value);
                    }
                }
                return ini;
            }

            private string? GetString(string section, string key)
            {
                //Discarded unreachable code: IL_0005
                //IL_0006: Incompatible stack heights: 0 vs 1
                try
                {
                    return Data[section][key].ToString();
                }
                catch
                {
                    //Console.WriteLine("获取值失败:section = " + section + ", key = " + key + " " + ex.Message);
                    return null;
                }
            }

            public string GetStringValue(string section, string key, string defaultValue = "")
            {
                //Discarded unreachable code: IL_0005
                //IL_0006: Incompatible stack heights: 0 vs 1
                string? text = GetString(section, key);
                if (string.IsNullOrEmpty(text))
                {
                    text = defaultValue;
                }
                return text;
            }

            public bool GetBooleanValue(string section, string key, bool defaultValue)
            {
                //Discarded unreachable code: IL_0005
                //IL_0006: Incompatible stack heights: 0 vs 1
                if (bool.TryParse(GetString(section, key), out var result))
                {
                    return result;
                }
                return defaultValue;
            }

            public int GetIntegerValue(string section, string key, int defaultValue = 0)
            {
                //Discarded unreachable code: IL_0005
                //IL_0006: Incompatible stack heights: 0 vs 1
                if (int.TryParse(GetString(section, key), out var result))
                {
                    return result;
                }
                return defaultValue;
            }

            public double GetDoubleValue(string section, string key, double defaultValue = 0.0)
            {
                //Discarded unreachable code: IL_0005
                //IL_0006: Incompatible stack heights: 0 vs 1
                if (double.TryParse(GetString(section, key), out var result))
                {
                    return result;
                }
                return defaultValue;
            }

            public float GetFloatValue(string section, string key, float defaultValue = 0f)
            {
                //Discarded unreachable code: IL_0005
                //IL_0006: Incompatible stack heights: 0 vs 1
                if (float.TryParse(GetString(section, key), out var result))
                {
                    return result;
                }
                return defaultValue;
            }

            public short GetShortValue(string section, string key, short defaultValue = 0)
            {
                //Discarded unreachable code: IL_0005
                //IL_0006: Incompatible stack heights: 0 vs 1
                if (short.TryParse(GetString(section, key), out var result))
                {
                    return result;
                }
                return defaultValue;
            }

            public long GetLongValue(string section, string key, long defaultValue = 0L)
            {
                //Discarded unreachable code: IL_0005
                //IL_0006: Incompatible stack heights: 0 vs 1
                if (long.TryParse(GetString(section, key), out var result))
                {
                    return result;
                }
                return defaultValue;
            }

            public byte GetByteValue(string section, string key, byte defaultValue = 0)
            {
                //Discarded unreachable code: IL_0005
                //IL_0006: Incompatible stack heights: 0 vs 1
                if (byte.TryParse(GetString(section, key), out var result))
                {
                    return result;
                }
                return defaultValue;
            }

            private static bool IsAnnotation(string line)
            {
                if (line != null && line.Length != 0 && !line.StartsWith(';'))
                {
                    return line.StartsWith('#');
                }
                return true;
            }

            public void Add(string section, string key, object value)
            {
                if (!Data.ContainsKey(section) || Data[section] == null)
                {
                    Data[section] = new Dictionary<string, object>();
                }
                if (value != null && section != null && key != null)
                {
                    Data[section][key] = value;
                }
            }

            public override string ToString()
            {
                StringBuilder stringBuilder = new StringBuilder();
                foreach (KeyValuePair<string, Dictionary<string, object>> datum in Data)
                {
                    stringBuilder.Append("[").Append(datum.Key).Append("]\r\n");
                    foreach (KeyValuePair<string, object> item in datum.Value)
                    {
                        stringBuilder.Append(item.Key).Append(" = ").Append(item.Value)
                            .Append("\r\n");
                    }
                    stringBuilder.Append("\r\n");
                }
                return stringBuilder.ToString();
            }
        }

    }
}