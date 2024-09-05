using QRCoder;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
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
using System.Windows.Shapes;
using static System.Net.Mime.MediaTypeNames;

namespace AliHelper
{
    /// <summary>
    /// QrWindow.xaml 的交互逻辑
    /// </summary>
    public partial class QrWindow : Window
    {
        public ImageSource? QrSource
        {
            get { return (ImageSource?)GetValue(QrSourceProperty); }
            set { SetValue(QrSourceProperty, value); }
        }

        // Using a DependencyProperty as the backing store for QrSource.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty QrSourceProperty =
            DependencyProperty.Register("QrSource", typeof(ImageSource), typeof(QrWindow), new PropertyMetadata(null));



        public string? Status
        {
            get { return (string?)GetValue(StatusProperty); }
            set { SetValue(StatusProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Status.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register("Status", typeof(string), typeof(QrWindow), new PropertyMetadata(null));

        private bool _isClosed = false;

        private QrWindow()
        {
            InitializeComponent();
        }

        protected override void OnClosed(EventArgs e)
        {
            this._isClosed = true;
            base.OnClosed(e);
        }
        public void UpdateFromContent(string content)
        {
            var generator = new QRCodeGenerator();
            var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
            var code = new QRCode(data);
            Bitmap qrImage = code.GetGraphic(10);
            UpdateImage(qrImage);
        }

        public void UpdateFromUrl(string url)
        {
            QrSource = new BitmapImage(new Uri(url));
        }

        public void UpdateImage(Bitmap image)
        {
            using var ms = new MemoryStream();
            image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = ms;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            QrSource = bitmapImage;
        }

        public void UpdateStatus(string status)
        {
            if (!CheckAccess())
            {
                Dispatcher.Invoke(() => UpdateStatus(status));
                return;
            }
            this.Status = status;
        }

        public static async Task<bool> OpenAsync(Action<QrWindow> init, Func<QrWindow, Task<bool>> checker)
        {
            var window = new QrWindow();
            init(window);
            window.Show();
            var result = false;
            while (!window._isClosed)
            {
                result = await checker.Invoke(window);
                if (result)
                {
                    window.Close();
                    break;
                }
            }
            return result;
        }
    }
}
