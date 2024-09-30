using Org.BouncyCastle.Asn1.X509;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AliHelper
{
    /// <summary>
    /// Interaction logic for AliWindow.xaml
    /// </summary>
    public partial class AliWindow : Window
    {
        public string? Status
        {
            get { return (string?)GetValue(StatusProperty); }
            set { SetValue(StatusProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Status.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register("Status", typeof(string), typeof(AliWindow), new PropertyMetadata(null));




        public string? Source
        {
            get { return (string?)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Source.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(string), typeof(AliWindow), new PropertyMetadata(""));



        public AliFolderItem? Root
        {
            get { return (AliFolderItem?)GetValue(RootProperty); }
            set { SetValue(RootProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Root.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty RootProperty =
            DependencyProperty.Register("Root", typeof(AliFolderItem), typeof(AliWindow), new PropertyMetadata(null));



        public AliDriverItem[]? Drivers
        {
            get { return (AliDriverItem[]?)GetValue(DriversProperty); }
            set { SetValue(DriversProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Drivers.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DriversProperty =
            DependencyProperty.Register("Drivers", typeof(AliDriverItem[]), typeof(AliWindow), new PropertyMetadata(null));



        public AliDriverItem? CurrentDriver
        {
            get { return (AliDriverItem?)GetValue(CurrentDriverProperty); }
            set { SetValue(CurrentDriverProperty, value); }
        }

        // Using a DependencyProperty as the backing store for CurrentDriver.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CurrentDriverProperty =
            DependencyProperty.Register("CurrentDriver", typeof(AliDriverItem), typeof(AliWindow), new PropertyMetadata(null));




        private string? _access_token;
        private string? _refresh_token;
        private int? _expires_in;
        private bool _isLoading = false;

        public AliWindow()
        {
            InitializeComponent();
            CommandBindings.Add(new CommandBinding(Commands.Login, OnLogin));
            CommandBindings.Add(new CommandBinding(Commands.List, OnList, CanList));
            CommandBindings.Add(new CommandBinding(Commands.Get, OnGet, CanGet));
            CommandBindings.Add(new CommandBinding(Commands.Copy, OnCopy, CanCopy));
            CommandBindings.Add(new CommandBinding(Commands.DownLoad, OnDownLoad, CanDownLoad));
        }
        private static IEnumerable<string> CollectFileUrls(AliViewItem item, string folder)
        {
            if (item is AliFileItem fi)
            {
                if (string.IsNullOrEmpty(fi.Url))
                {
                    yield break;
                }
                yield return $"{folder}{fi.Name}： {fi.Url}";
            }
            else if (item is AliFolderItem di && di.Items is ObservableCollection<AliViewItem> items)
            {
                var subFolder = $"{folder}{di.Name}/";
                foreach (var subItem in items)
                {
                    foreach (var result in CollectFileUrls(subItem, subFolder))
                    {
                        yield return result;
                    }
                }
            }
        }

        private async Task TryGetFileUrl(AliFileItem item)
        {
            using var result = await AliExtends.AliOpenQueryAsync("/adrive/v1.0/openFile/getDownloadUrl", _access_token, new Dictionary<string, object?>
            {
                { "drive_id", item.Driver },
                { "file_id",item.Current }
            });
            if (result is null)
            {
                throw new Exception($"获取下载地址失败：{item.Name} 【返回内容无效】");
            }
            if (result.RootElement.TryGetProperty("url", out var url))
            {
                item.Url = url.GetString();
                return;
            }
            throw new Exception($"获取下载地址失败：{item.Name} 【返回内容无URL】");
        }

        private void CanList(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _access_token is not null && !_isLoading && ((e.Parameter is AliFolderItem item && (!string.IsNullOrWhiteSpace(item.Marker) || item.Items is null)) || CurrentDriver is not null);
        }
        private void CanGet(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _access_token is not null && !_isLoading;
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
            //if (!await Step0())
            //{
            //    return;
            //}
            //if (!await Step1())
            //{
            //    return;
            //}
            if (!await Step2())
            {
                return;
            }
            await LoadDrivers();
        }
        private async void OnList(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Parameter is not AliFolderItem item)
            {
                if (CurrentDriver is not AliDriverItem di)
                {
                    UpdateStatus("未指定源。。。");
                    return;
                }
                Root = item = new AliFolderItem("root", di.Id, "", "root") { RefreshTag = "need" };
            }
            try
            {
                _isLoading = true;
                UpdateStatus("正在获取列表。。。");
                if (string.IsNullOrWhiteSpace(item.Marker) && item.Items is ObservableCollection<AliViewItem> items)
                {
                    UpdateStatus("已完成获取。。。");
                    return;
                }
                using var result = await AliExtends.AliOpenQueryAsync("/adrive/v1.0/openFile/list", _access_token, new Dictionary<string, object?>
                {
                    { "drive_id", item.Driver },
                    { "parent_file_id",item.Current },
                    { "marker",item.Marker }
                });
                if (result is null)
                {
                    UpdateStatus("返回内容无效。。。");
                    return;
                }
                var element = result.RootElement;
                item.Marker = element.GetProperty("next_marker").GetString();
                item.RefreshTag = item.Marker;
                var all = element.GetProperty("items");
                var subItems = item.Items ??= [];
                if (all.ValueKind == JsonValueKind.Array)
                {
                    foreach (var citem in all.EnumerateArray())
                    {
                        AliViewItem viewItem;
                        var name = citem.GetProperty("name").ToString();
                        var file_id = citem.GetProperty("file_id").ToString();
                        if (citem.GetProperty("type").ToString() == "file")
                        {
                            var size = citem.GetProperty("size").GetInt64();
                            viewItem = new AliFileItem(name, size, item.Driver, item.Current, file_id)
                            {
                                Url = citem.GetProperty("url").ToString(),
                                //DownloadUrl = citem.GetProperty("download_url").ToString()
                            };
                        }
                        else
                        {
                            viewItem = new AliFolderItem(name, item.Driver, item.Current, file_id)
                            {
                                RefreshTag = "need"
                            };
                        }
                        subItems.Add(viewItem);
                    }
                }
                Source = string.Join(Environment.NewLine, CollectFileUrls(item, "/"));
                UpdateStatus("获取列表成功");
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


        private TaskCompletionSource? _batchSource = null;
        private async void OnGet(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Parameter is AliFileItem item)
            {
                Source = Root is null ? "" : string.Join(Environment.NewLine, CollectFileUrls(item, "/"));
                if (!string.IsNullOrEmpty(item.Url))
                {
                    UpdateStatus("已经获取过。。。");
                    return;
                }
                try
                {
                    _isLoading = true;
                    UpdateStatus($"正在获取地址：{item.Name}");
                    await TryGetFileUrl(item);
                    Source = Root is null ? "" : string.Join(Environment.NewLine, CollectFileUrls(item, "/"));
                    UpdateStatus($"获取地址成功：{item.Name}");
                }
                catch (Exception ex)
                {
                    UpdateStatus($"获取地址失败：{item.Name} 【{ex.Message}】");
                }
                finally
                {
                    _isLoading = false;
                    CommandManager.InvalidateRequerySuggested();
                }
            }
            else if (e.Parameter is AliFolderItem folder)
            {
                var oldCts = Interlocked.Exchange(ref _batchSource, null);
                if (oldCts != null)
                {
                    oldCts.TrySetCanceled();
                    await Task.Delay(10);
                }
                Source = Root is null ? "" : string.Join(Environment.NewLine, CollectFileUrls(folder, "/"));
                if (folder.Items is not ObservableCollection<AliViewItem> items)
                {
                    UpdateStatus("无需获取。。。");
                    return;
                }
                var findItems = items.Where(s => s is AliFileItem fi && string.IsNullOrEmpty(fi.Url)).Cast<AliFileItem>().ToArray();
                if (findItems.Length <= 0)
                {
                    UpdateStatus("无需获取。。。");
                    return;
                }
                var cts = new TaskCompletionSource();
                if(Interlocked.CompareExchange(ref _batchSource, cts, null) != null)
                {
                    cts.TrySetCanceled();
                    UpdateStatus("操作冲突，稍后再试。。。");
                    return;
                }
                AliFileItem? currentFile = null;
                try
                {
                    foreach (var fi in findItems)
                    {
                        currentFile = fi;
                        UpdateStatus($"正在获取地址：{fi.Name}");
                        await TryGetFileUrl(fi);
                        Source = Root is null ? "" : string.Join(Environment.NewLine, CollectFileUrls(folder, "/"));
                        await Task.WhenAny(Task.Delay(1000), cts.Task).Unwrap();
                    }
                    UpdateStatus($"批量获取地址成功：{findItems.Length}");
                }
                catch (Exception ex)
                {
                    cts.TrySetException(ex);
                    UpdateStatus($"获取地址失败：{currentFile?.Name} 【{ex.Message}】");
                }
                finally
                {
                    cts.TrySetResult();
                }
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


        private async Task<bool> Step0()
        {
            var signObj = new Dictionary<string, string> { { "key", "woniu.login.tips" } };
            var param = await AliExtends.WoniuQueryAsync("/api/woniu/getParam", signObj);
            return true;
        }


        private async Task<bool> Step1()
        {
            async Task<(string ck, string t, string codeContent)> RefreshQrcode()
            {
                using var result = await AliExtends.GetAliWebQrCodeAsync();
                if (result is null)
                {
                    UpdateStatus("返回内容无效。。。");
                    return default;
                }
                var contentData = result.RootElement.GetProperty("content").GetProperty("data");
                var ck = contentData.GetProperty("ck").GetString() ?? string.Empty;
                var t = contentData.GetProperty("t").GetRawText() ?? string.Empty;
                var codeContent = contentData.GetProperty("codeContent").GetString() ?? string.Empty;
                return (ck, t, codeContent);
            }
            var (ck, t, codeContent) = await RefreshQrcode();
            if (ck is null)
            {
                return false;
            }
            return await QrWindow.OpenAsync(win => win.UpdateFromContent(codeContent), async (win) =>
            {
                using var result = await AliExtends.CheckAliWebQrcodeAsync(ck, t, default);
                if (result is null)
                {
                    UpdateStatus("返回内容无效。。。");
                    return false;
                }
                var status = result.RootElement.GetProperty("content").GetProperty("data").GetProperty("qrCodeStatus").GetString();
                switch (status)
                {
                    case "NEW":
                        win.UpdateStatus("二维码状态：等待扫描");
                        await Task.Delay(500);
                        return false;
                    case "SCANED":
                        win.UpdateStatus("二维码状态：扫描成功，请确认登录");
                        await Task.Delay(500);
                        return false;
                    case "EXPIRED":
                        win.UpdateStatus("二维码状态：已过期，重新获取二维码");
                        await RefreshQrcode();
                        win.UpdateFromContent(codeContent);
                        return false;
                    case "CANCELED":
                        win.UpdateStatus("二维码状态：取消登录，重新获取二维码");
                        await RefreshQrcode();
                        win.UpdateFromContent(codeContent);
                        return false;
                    case "CONFIRMED":
                        win.UpdateStatus("二维码状态：登录成功");
                        return true;
                    default:
                        win.UpdateStatus($"未知状态：{status}");
                        await Task.Delay(500);
                        return false;
                }

            });
        }

        private async Task<bool> Step2()
        {
            var authCode = string.Empty;
            var signObj = new Dictionary<string, object> { { "width", 250 }, { "height", 250 }, { "access_token", "" } };
            async Task<(string openCodeUrl, string openCodeSid)> RefreshQrcode()
            {
                using var result = await AliExtends.WoniuQueryAsync("/api/woniu/getQrcode", signObj);
                if (result is null)
                {
                    UpdateStatus("返回内容无效。。。");
                    return default;
                }
                var openCodeUrl = result.RootElement.GetProperty("qrCodeUrl").GetString() ?? string.Empty;
                var openCodeSid = result.RootElement.GetProperty("sid").GetString() ?? string.Empty;
                return (openCodeUrl, openCodeSid);
            }
            var (openCodeUrl, openCodeSid) = await RefreshQrcode();
            if (openCodeUrl is null)
            {
                return false;
            }
            if (!await QrWindow.OpenAsync(win => win.UpdateFromUrl(openCodeUrl), async (win) =>
            {
                if(!this.IsVisible)
                {
                    return true;
                }
                using var result = await AliExtends.CheckAliOpenQrcodeAsync(openCodeSid, default);
                if (result is null)
                {
                    UpdateStatus("返回内容无效。。。");
                    return false;
                }
                var re = result.RootElement;
                var status = re.GetProperty("status").GetString();
                authCode = re.GetProperty("authCode").GetString() ?? string.Empty;
                switch (status)
                {
                    case "WaitLogin":
                        win.UpdateStatus("二维码状态：等待扫描");
                        await Task.Delay(500);
                        return false;
                    case "ScanSuccess":
                        win.UpdateStatus("二维码状态：扫描成功，请确认登录");
                        await Task.Delay(500);
                        return false;
                    case "LoginSuccess":
                        win.UpdateStatus("二维码状态：登录成功");
                        return true;
                    default:
                        win.UpdateStatus($"未知状态：{status}");
                        await Task.Delay(500);
                        return false;
                }
            }) || string.IsNullOrWhiteSpace(authCode))
            {
                UpdateStatus("取消登录！");
                return false;
            }

            signObj = new Dictionary<string, object>() { { "authCode", authCode }, { "access_token", "" } };
            using var param = await AliExtends.WoniuQueryAsync("/api/woniu/loginByCode", signObj);
            if (param is null)
            {
                UpdateStatus("返回内容无效。。。");
                return false;
            }
            var element = param.RootElement;
            _access_token = element.GetProperty("access_token").GetString() ?? string.Empty;
            _refresh_token = element.GetProperty("refresh_token").GetString() ?? string.Empty;
            _expires_in = element.GetProperty("expires_in").GetInt32();
            return true;
        }

        private async Task<bool> LoadDrivers()
        {
            using var result = await AliExtends.AliOpenQueryAsync("/adrive/v1.0/user/getDriveInfo", _access_token);
            if (result is null)
            {
                UpdateStatus("返回内容无效。。。");
                return false;
            }
            var element = result.RootElement;
            this.Title = result.RootElement.GetProperty("name").GetString();
            var drivers = new List<AliDriverItem>();
            Drivers = null;
            CurrentDriver = null;
            AliDriverItem? current = null;
            var defaultid = element.GetProperty("default_drive_id").GetString();
            var driverMap = new Dictionary<string, string>
            {
                { "backup_drive_id","备份盘" },
                { "resource_drive_id","资源库" },
                { "album_drive_id","相册" },
            };
            foreach (var driver in driverMap)
            {
                if (element.TryGetProperty(driver.Key, out var value))
                {
                    var id = value.GetString();
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        var item = new AliDriverItem(driver.Value, id);
                        drivers.Add(item);
                        if (id == defaultid)
                        {
                            current = item;
                        }
                    }
                }
            }
            this.Drivers = [.. drivers];
            CurrentDriver = current;
            CommandManager.InvalidateRequerySuggested();
            return true;
        }


        private async Task<bool> RefreshToken()
        {
            if (_refresh_token is null)
            {
                return false;
            }
            var signObj = new Dictionary<string, object> { { "refresh_token", _refresh_token }, { "access_token", "" }, { "user_id", "" }, { "decvice_type", 3 }, { "app_version", AliExtends.versionName } };
            using var result = await AliExtends.WoniuQueryAsync("/api/woniu/refreshToken", signObj);
            if (result is null)
            {
                UpdateStatus("返回内容无效。。。");
                return false;
            }
            var element = result.RootElement;
            _access_token = element.GetProperty("access_token").GetString() ?? string.Empty;
            _refresh_token = element.GetProperty("refresh_token").GetString() ?? string.Empty;
            _expires_in = element.GetProperty("expires_in").GetInt32();
            return true;

        }
    }
}