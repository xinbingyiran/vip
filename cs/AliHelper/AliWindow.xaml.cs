using System.Collections.ObjectModel;
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
        private void CanList(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _access_token is not null && !_isLoading && ((e.Parameter is AliFolderItem item && (!string.IsNullOrWhiteSpace(item.Marker) || item.Items is null)) || CurrentDriver is not null);
        }
        private void CanGet(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _access_token is not null && !_isLoading && e.Parameter is AliFileItem item && string.IsNullOrEmpty(item.Url);
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
                var result = await AliExtends.AliOpenQueryAsync("/adrive/v1.0/openFile/list", _access_token, new Dictionary<string, object?>
                {
                    { "drive_id", item.Driver },
                    { "parent_file_id",item.Current },
                    { "marker",item.Marker }
                });
                if (result is not JsonElement element)
                {
                    UpdateStatus("返回内容无效。。。");
                    return;
                }
                item.Marker = element.GetProperty("next_marker").GetString();
                item.RefreshTag = item.Marker;
                var all = element.GetProperty("items");
                var subItems = item.Items ??= new ObservableCollection<AliViewItem>();
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

        private async void OnGet(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Parameter is not AliFileItem item)
            {
                UpdateStatus("未指定源。。。");
                return;
            }
            try
            {
                _isLoading = true;
                UpdateStatus("正在获取地址。。。");
                var result = await AliExtends.AliOpenQueryAsync("/adrive/v1.0/openFile/getDownloadUrl", _access_token, new Dictionary<string, object?>
                {
                    { "drive_id", item.Driver },
                    { "file_id",item.Current }
                });
                if (result.Value.TryGetProperty("url", out var url))
                {
                    item.Url = url.GetString();
                }
                UpdateStatus("获取地址成功");
            }
            catch (Exception ex)
            {
                UpdateStatus($"获取地址失败：{ex.Message}");
            }
            finally
            {
                _isLoading = false;
                CommandManager.InvalidateRequerySuggested();
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
            string ck = string.Empty, t = string.Empty, codeContent = string.Empty;
            async Task RefreshQrcode()
            {
                var code = await AliExtends.GetAliWebQrCodeAsync();
                var contentData = code.Value.GetProperty("content").GetProperty("data");
                ck = contentData.GetProperty("ck").GetString() ?? string.Empty;
                t = contentData.GetProperty("t").GetRawText() ?? string.Empty;
                codeContent = contentData.GetProperty("codeContent").GetString() ?? string.Empty;
            }
            await RefreshQrcode();
            return await QrWindow.OpenAsync(win => win.UpdateFromContent(codeContent), async (win) =>
            {
                var result = await AliExtends.CheckAliWebQrcodeAsync(ck, t, default);
                var status = result.Value.GetProperty("content").GetProperty("data").GetProperty("qrCodeStatus").GetString();
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
            string openCodeUrl = string.Empty, openCodeSid = string.Empty, authCode = string.Empty;
            var signObj = new Dictionary<string, object> { { "width", 250 }, { "height", 250 }, { "access_token", "" } };
            async Task RefreshQrcode()
            {
                var openCode = await AliExtends.WoniuQueryAsync("/api/woniu/getQrcode", signObj);
                openCodeUrl = openCode.Value.GetProperty("qrCodeUrl").GetString() ?? string.Empty;
                openCodeSid = openCode.Value.GetProperty("sid").GetString() ?? string.Empty;
            }
            await RefreshQrcode();
            if (!await QrWindow.OpenAsync(win => win.UpdateFromUrl(openCodeUrl), async (win) =>
            {
                var result = await AliExtends.CheckAliOpenQrcodeAsync(openCodeSid, default);
                var status = result.Value.GetProperty("status").GetString();
                authCode = result.Value.GetProperty("authCode").GetString() ?? string.Empty;
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
            }))
            {
                UpdateStatus("取消登录！");
                return false;
            }

            signObj = new Dictionary<string, object>() { { "authCode", authCode }, { "access_token", "" } };
            var param = await AliExtends.WoniuQueryAsync("/api/woniu/loginByCode", signObj);

            _access_token = param.Value.GetProperty("access_token").GetString() ?? string.Empty;
            _refresh_token = param.Value.GetProperty("refresh_token").GetString() ?? string.Empty;
            _expires_in = param.Value.GetProperty("expires_in").GetInt32();
            return true;
        }

        private async Task<bool> LoadDrivers()
        {
            var result = await AliExtends.AliOpenQueryAsync("/adrive/v1.0/user/getDriveInfo", _access_token);
            this.Title = result.Value.GetProperty("name").GetString();
            var drivers = new List<AliDriverItem>();
            Drivers = null;
            CurrentDriver = null;
            AliDriverItem? current = null;
            var defaultid = result.Value.GetProperty("default_drive_id").GetString();
            var driverMap = new Dictionary<string, string>
            {
                { "backup_drive_id","备份盘" },
                { "resource_drive_id","资源库" },
                { "album_drive_id","相册" },
            };
            foreach (var driver in driverMap)
            {
                if (result.Value.TryGetProperty(driver.Key, out var value))
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
            var param = await AliExtends.WoniuQueryAsync("/api/woniu/refreshToken", signObj);
            _access_token = param.Value.GetProperty("access_token").GetString() ?? string.Empty;
            _refresh_token = param.Value.GetProperty("refresh_token").GetString() ?? string.Empty;
            _expires_in = param.Value.GetProperty("expires_in").GetInt32();
            return true;

        }
    }
}