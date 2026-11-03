using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

namespace JinZhiZhuanHuan
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {



        public long Value
        {
            get { return (long)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Value.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(long), typeof(MainWindow), new PropertyMetadata(0L));




        public MainWindow()
        {
            InitializeComponent();
        }
    }
    public class ValueConverter2 : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long l)
            {
                return System.Convert.ToString(l, 2);
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && Regex.IsMatch(s, "^[01]+$"))
            {
                return System.Convert.ToInt64(value as string, 2);
            }
            return 0;
        }
    }
    public class ValueConverter8 : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long l)
            {
                return System.Convert.ToString(l, 8);
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && Regex.IsMatch(s, "^[0-7]+$"))
            {
                return System.Convert.ToInt64(value as string, 8);
            }
            return 0;
        }
    }
    public class ValueConverter10 : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long l)
            {
                return (ulong)l;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && ulong.TryParse(s,out var ul))
            {
                return (long)ul;
            }
            return 0;
        }
    }

    public class ValueConverterL : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long l)
            {
                return System.Convert.ToString(l, 10);
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && Regex.IsMatch(s, "^[+-]?[0-9]+$"))
            {
                return System.Convert.ToInt64(value as string, 10);
            }
            return 0;
        }
    }
    public class ValueConverter16 : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long l)
            {
                return System.Convert.ToString(l, 16);
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && Regex.IsMatch(s, "^[0-9a-fA-F]+$"))
            {
                return System.Convert.ToInt64(value as string, 16);
            }
            return 0;
        }
    }
}
