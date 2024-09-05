using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace AliHelper
{
    public interface IViewItem
    {
        string Name { get; set; }
        string Driver { get; }
        string Parent { get; }
        string Current { get; }
    }

    public class BaseItem : DependencyObject, IViewItem
    {
        public string Name
        {
            get { return (string)GetValue(NameProperty); }
            set { SetValue(NameProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Name.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty NameProperty =
            DependencyProperty.Register("Name", typeof(string), typeof(BaseItem), new PropertyMetadata(null));



        public string? Url
        {
            get { return (string?)GetValue(UrlProperty); }
            set { SetValue(UrlProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Url.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty UrlProperty =
            DependencyProperty.Register("Url", typeof(string), typeof(BaseItem), new PropertyMetadata(null));



        public string? DownloadUrl
        {
            get { return (string?)GetValue(DownloadUrlProperty); }
            set { SetValue(DownloadUrlProperty, value); }
        }

        // Using a DependencyProperty as the backing store for DownloadUrl.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DownloadUrlProperty =
            DependencyProperty.Register("DownloadUrl", typeof(string), typeof(BaseItem), new PropertyMetadata(null));


        public string Driver { get; }

        public string Parent { get; }
        public string Current { get; }

        public BaseItem(string name, string driver, string parent,string current)
        {
            Name = name;
            Driver = driver;
            Parent = parent;
            Current = current;
        }
    }

    public class FileItem(string name, string driver, string parent, string current) : BaseItem(name, driver, parent,current) { }

    public class FolderItem(string name, string driver, string parent, string current) : BaseItem(name, driver, parent,current)
    {
        public ObservableCollection<IViewItem>? Items
        {
            get { return (ObservableCollection<IViewItem>?)GetValue(ItemsProperty); }
            set { SetValue(ItemsProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Items.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ItemsProperty =
            DependencyProperty.Register("Items", typeof(ObservableCollection<IViewItem>), typeof(FolderItem), new PropertyMetadata(null));
        public string? Marker;
    }

    public record DriverItem(string Name, string Id);
}
