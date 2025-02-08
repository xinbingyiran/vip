using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;

namespace AliHelper
{
    public static class Commands
    {
        public static readonly RoutedUICommand Login = new();
        public static readonly RoutedUICommand List = new();
        public static readonly RoutedUICommand Get = new();
        public static readonly RoutedUICommand Copy = new();
        public static readonly RoutedUICommand DownLoad = new();
        public static readonly RoutedUICommand Ali = new();
        public static readonly RoutedUICommand TianYi = new();
        public static readonly RoutedUICommand QrCode = new();
    }

    public class NullOrEmptyConverter : IValueConverter
    {
        public object? NullValue { get; set; } = null;
        public object? NotNullValue { get; set; } = null;
        public object? EmptyValue { get; set; } = null;
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is null || Object.Equals(value, EmptyValue) ? NullValue : NotNullValue;
        }

        public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
