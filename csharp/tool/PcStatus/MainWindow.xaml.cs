using System.Reflection.Metadata;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PcStatus
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static readonly ICommand Check = new RoutedUICommand();

        public string? Status
        {
            get { return (string?)GetValue(StatusProperty); }
            set { SetValue(StatusProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Status.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register(nameof(Status), typeof(string), typeof(MainWindow), new PropertyMetadata("点击下方检测开始。"));



        public string? CheckText
        {
            get { return (string?)GetValue(CheckTextProperty); }
            set { SetValue(CheckTextProperty, value); }
        }

        // Using a DependencyProperty as the backing store for CheckText.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CheckTextProperty =
            DependencyProperty.Register(nameof(CheckText), typeof(string), typeof(MainWindow), new PropertyMetadata("检测"));



        private bool _isChecking = false;


        public MainWindow()
        {
            InitializeComponent();
            CommandBindings.Add(new CommandBinding(Check, OnCheck, CanCheck));
        }

        private void CanCheck(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = !_isChecking;
        }

        private async void OnCheck(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                _isChecking = true;
                for(var i = 0;i < 20;i ++)
                {
                    Status = $"正在检测：{new string('|',i)}";
                    await Task.Delay(100);
                }
                Status = "当前电脑状态：“未关机”！";
                CheckText = "重新检测";
            }
            finally
            {
                _isChecking = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }
}