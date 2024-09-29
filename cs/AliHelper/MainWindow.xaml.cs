using System.Windows;
using System.Windows.Input;

namespace AliHelper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            CommandBindings.Add(new CommandBinding(Commands.Ali, OnAli));
            CommandBindings.Add(new CommandBinding(Commands.TianYi, OnTianYi));
        }

        private Dictionary<Type,Window> _windows = [];

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
            OpenWindow(()=>new AliWindow());
        }
    }
}