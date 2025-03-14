using GameBox.Providers;
using System.ComponentModel;
using System.Diagnostics;
using System.Formats.Tar;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GameBox;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{

    public string Info
    {
        get { return (string)GetValue(InfoProperty); }
        set { SetValue(InfoProperty, value); }
    }

    // Using a DependencyProperty as the backing store for Info.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty InfoProperty =
        DependencyProperty.Register("Info", typeof(string), typeof(MainWindow), new PropertyMetadata(string.Empty));


    public string GameName
    {
        get { return (string)GetValue(GameNameProperty); }
        set { SetValue(GameNameProperty, value); }
    }

    // Using a DependencyProperty as the backing store for GameName.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty GameNameProperty =
        DependencyProperty.Register("GameName", typeof(string), typeof(MainWindow), new PropertyMetadata(string.Empty));



    public IGameProvider[] Providers
    {
        get { return (IGameProvider[])GetValue(ProvidersProperty); }
        set { SetValue(ProvidersProperty, value); }
    }

    // Using a DependencyProperty as the backing store for Providers.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty ProvidersProperty =
        DependencyProperty.Register("Providers", typeof(IGameProvider[]), typeof(MainWindow), new PropertyMetadata(null));



    public Game[]? Games
    {
        get { return (Game[]?)GetValue(GamesProperty); }
        set { SetValue(GamesProperty, value); }
    }

    // Using a DependencyProperty as the backing store for Games.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty GamesProperty =
        DependencyProperty.Register("Games", typeof(Game[]), typeof(MainWindow), new PropertyMetadata(null));



    public Game? CurrentGame
    {
        get { return (Game?)GetValue(CurrentGameProperty); }
        set { SetValue(CurrentGameProperty, value); }
    }

    // Using a DependencyProperty as the backing store for CurrentGame.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty CurrentGameProperty =
        DependencyProperty.Register("CurrentGame", typeof(Game), typeof(MainWindow), new PropertyMetadata(null));




    public GameFileInfo[]? GameFiles
    {
        get { return (GameFileInfo[]?)GetValue(GameFilesProperty); }
        set { SetValue(GameFilesProperty, value); }
    }

    // Using a DependencyProperty as the backing store for GameFiles.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty GameFilesProperty =
        DependencyProperty.Register("GameFiles", typeof(GameFileInfo[]), typeof(MainWindow), new PropertyMetadata(null));



    public IGameProvider? CurrentProvider
    {
        get { return (IGameProvider?)GetValue(CurrentProviderProperty); }
        set { SetValue(CurrentProviderProperty, value); }
    }

    // Using a DependencyProperty as the backing store for CurrentProvider.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty CurrentProviderProperty =
        DependencyProperty.Register("CurrentProvider", typeof(IGameProvider), typeof(MainWindow), new PropertyMetadata(null));


    private CancellationTokenSource? lastCts;

    [GeneratedRegex("^https?://")]
    private static partial Regex UrlRegex();

    public MainWindow()
    {
        InitializeComponent();
        CommandBindings.Add(new CommandBinding(Commands.Search, OnSearch, CanSearch));
        CommandBindings.Add(new CommandBinding(Commands.GetUrl, OnUrl, CanUrl));
        CommandBindings.Add(new CommandBinding(Commands.GetFile, OnFile, CanFile));
        CommandBindings.Add(new CommandBinding(Commands.Download, OnDownload));
        DependencyPropertyDescriptor.FromProperty(CurrentProviderProperty, this.GetType()).AddValueChanged(this, CurrentProviderChanged);
        DependencyPropertyDescriptor.FromProperty(CurrentGameProperty, this.GetType()).AddValueChanged(this, CurrentGameChanged);
        this.Providers = [new CaiNiaoGameProvider(), new MuluGameProvider()];
        this.CurrentProvider = this.Providers.First();
    }

    private void OnDownload(object sender, ExecutedRoutedEventArgs e)
    {
        if(e.Parameter is string url && !string.IsNullOrEmpty(url) && UrlRegex().IsMatch(url))
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                UseShellExecute = true,
                FileName = url,
            });
        }
    }

    private CancellationTokenSource NewCts()
    {
        using var cts = Interlocked.Exchange(ref lastCts, new CancellationTokenSource());
        try
        {
            cts?.Cancel(false);
        }
        catch { }
        return lastCts;
    }

    private async void CurrentGameChanged(object? sender, EventArgs e)
    {
        var getter = this.CurrentGame?.InfoGetter;
        if (getter is not null)
        {
            try
            {
                var lines = await getter.Invoke(TellInfo, NewCts().Token);
                this.Info = string.Join(Environment.NewLine, lines.Select(e => $"{e.Key}： {e.Value}"));
            }
            catch (Exception ex)
            {
                this.Info = $"{ex}";
            }
        }
        else
        {
            this.Info = string.Empty;
        }
    }

    private void CurrentProviderChanged(object? sender, EventArgs e)
    {
        this.GameFiles = null;
        this.CurrentGame = null;
        this.Games = null;
    }

    private void CanUrl(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = this.CurrentGame is not null;
    }

    private async void OnUrl(object sender, ExecutedRoutedEventArgs e)
    {
        var getter = this.CurrentGame?.UrlGetter;
        this.GameFiles = null;
        if (getter is not null)
        {
            try
            {
                this.GameFiles = await getter.Invoke(TellInfo, NewCts().Token);
                this.Info = "获取地址成功！";
            }
            catch (Exception ex)
            {
                this.Info = $"{ex}";
            }
        }
        else
        {
            this.Info = string.Empty;
        }
    }
    private void CanFile(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = e.Parameter is System.Collections.IList list && list.Count > 0;
    }

    private void OnFile(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is System.Collections.IList list && list.Count > 0)
        {
            this.Info = string.Join(Environment.NewLine, list.OfType<GameFileInfo>().Select(e => e.Url));
        }
    }
    private void TellInfo(string info)
    {
        if (!CheckAccess())
        {
            Dispatcher.Invoke(TellInfo, info);
        }
        else
        {
            this.Info = info;
        }
    }

    private void CanSearch(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = this.CurrentProvider is not null;
    }

    private async void OnSearch(object sender, ExecutedRoutedEventArgs e)
    {
        try
        {
            this.Games = await this.CurrentProvider!.SearchGame(TellInfo, this.GameName, NewCts().Token);
        }
        catch (Exception ex)
        {
            this.Info = $"{ex}";
        }
    }
}