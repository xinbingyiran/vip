using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

namespace CaiShuZi2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static RoutedUICommand Start = new RoutedUICommand();
        public static RoutedUICommand Reset = new RoutedUICommand();


        public int MinValue
        {
            get { return (int)GetValue(MinValueProperty); }
            set { SetValue(MinValueProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MinValue.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MinValueProperty =
            DependencyProperty.Register("MinValue", typeof(int), typeof(MainWindow), new PropertyMetadata(1));



        public int MaxValue
        {
            get { return (int)GetValue(MaxValueProperty); }
            set { SetValue(MaxValueProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MaxValue.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MaxValueProperty =
            DependencyProperty.Register("MaxValue", typeof(int), typeof(MainWindow), new PropertyMetadata(100));



        public int GuessValue
        {
            get { return (int)GetValue(GuessValueProperty); }
            set { SetValue(GuessValueProperty, value); }
        }

        // Using a DependencyProperty as the backing store for GuessValue.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty GuessValueProperty =
            DependencyProperty.Register("GuessValue", typeof(int), typeof(MainWindow), new PropertyMetadata(1));


        private int targetValue = 55;
        private Random random = new Random();



        public string Status
        {
            get { return (string)GetValue(StatusProperty); }
            set { SetValue(StatusProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Status.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register("Status", typeof(string), typeof(MainWindow), new PropertyMetadata(""));


        public MainWindow()
        {
            InitializeComponent();
            CommandBindings.Add(new CommandBinding(Start, OnStart));
            CommandBindings.Add(new CommandBinding(Reset, OnReset));
            CommandBindings.Add(new CommandBinding(App.Guess, OnGuess));
            ResetAll();
        }

        private void OnGuess(object sender, ExecutedRoutedEventArgs e)
        {
            if (GuessValue == targetValue)
            {
                Status = "恭喜你答对了！";
            }
            else if (GuessValue < targetValue)
            {
                if (GuessValue >= MinValue)
                {
                    MinValue = GuessValue + 1;
                }
                Status = "你猜小了，请重猜一次！";
            }
            else
            {
                if (GuessValue <= MaxValue)
                {
                    MaxValue = GuessValue - 1;
                }
                Status = "你猜大了，请重猜一次！";
            }
        }

        private void OnReset(object sender, ExecutedRoutedEventArgs e)
        {
            GuessValue = 1;
        }

        private void ResetAll()
        {
            MinValue = 1;
            MaxValue = 100;
            //GuessValue = 50;
            targetValue = random.Next(1, 100);
            Status = "请开始你的猜测。";
        }

        private void OnStart(object sender, ExecutedRoutedEventArgs e)
        {
            ResetAll();
        }
    }
}
