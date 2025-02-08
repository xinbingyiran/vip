using QRCoder;
using System.Windows;
using System.Windows.Input;

namespace AliHelper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {



        public string TextStr
        {
            get { return (string)GetValue(TextStrProperty); }
            set { SetValue(TextStrProperty, value); }
        }

        // Using a DependencyProperty as the backing store for TextStr.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TextStrProperty =
            DependencyProperty.Register("TextStr", typeof(string), typeof(MainWindow), new PropertyMetadata(string.Empty));


        public MainWindow()
        {
            InitializeComponent();
            CommandBindings.Add(new CommandBinding(Commands.Ali, OnAli));
            CommandBindings.Add(new CommandBinding(Commands.TianYi, OnTianYi));
            CommandBindings.Add(new CommandBinding(Commands.QrCode, OnQrCode));
        }

        private Dictionary<Type, Window> _windows = [];

        private void OpenWindow<T>(Func<T> creator) where T : Window
        {
            if (!_windows.TryGetValue(typeof(T), out var window))
            {
                _windows[typeof(T)] = window = creator.Invoke();
                window.Closing += (s, e) =>
                {
                    e.Cancel = true;
                    (s as Window)!.Hide();
                };
            }
            window.Show();
            window.Activate();
        }

        private void OnTianYi(object sender, ExecutedRoutedEventArgs e)
        {
            OpenWindow(() => new TianYiWindow());
        }

        private void OnAli(object sender, ExecutedRoutedEventArgs e)
        {
            OpenWindow(() => new AliWindow());
        }
        private async void OnQrCode(object sender, ExecutedRoutedEventArgs e)
        {
            await QrWindow.OpenAsync(win => win.UpdateFromContent(this.TextStr), async (win) =>
            {
                if (!this.IsVisible)
                {
                    return true;
                }
                await Task.Delay(500);
                return false;
            });
        }
    }
}