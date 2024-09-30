using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace AliHelper
{
    public class StandardDataTemplateSelector : DataTemplateSelector
    {
        public static IEnumerable<Type> GetAllTypes(Type type)
        {
            //if(type.IsInterface)
            //{
            yield return type;
            //}
            foreach (Type iface in type.GetInterfaces())
            {
                foreach (var sub in GetAllTypes(iface))
                {
                    yield return sub;
                }
            }
            if (type.BaseType is Type baseType)
            {
                foreach (var sub in GetAllTypes(baseType))
                {
                    yield return sub;
                }
            }
        }
        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (base.SelectTemplate(item ?? throw new ArgumentNullException(nameof(item)), container ?? throw new ArgumentNullException(nameof(container))) is DataTemplate dataTemplate)
            {
                return dataTemplate;
            }

            if (container is not FrameworkElement itemContainer)
            {
                return null;
            }

            foreach (Type itemInterface in GetAllTypes(item.GetType()))
            {
                if (itemInterface == typeof(object))
                {
                    continue;
                }
                if (itemContainer.TryFindResource(new DataTemplateKey(itemInterface)) is DataTemplate template)
                {
                    return template;
                }
            }
            return null;
        }
    }


    public interface IViewItem
    {
        string? Name { get; }
    }

    public interface IFileViewItem : IViewItem
    {
        string? Url { get; }
        long? Size { get; }
    }
    public interface IFolderViewItem : IViewItem
    {
        IEnumerable<IViewItem>? Items { get; }
        string? RefreshTag { get; }
    }
    public interface IFolderViewItem<T> : IFolderViewItem where T : IViewItem
    {
        new IEnumerable<T>? Items { get; }
    }

    public abstract class ViewItem : INotifyPropertyChanged, IViewItem
    {
        private string? _name;
        public string? Name
        {
            get { return _name; }
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ViewItem(string name)
        {
            Name = name;
        }
    }

    public abstract class AliViewItem(string name, string driver, string parent, string current) : ViewItem(name)
    {
        public string Driver => driver;
        public string Parent => parent;
        public string Current => current;
    }

    public class AliFileItem(string name, long size, string driver, string parent, string current) : AliViewItem(name, driver, parent, current), IFileViewItem
    {
        public long? Size => size;

        private string? _url;
        public string? Url
        {
            get { return _url; }
            set
            {
                _url = value;
                OnPropertyChanged(nameof(Url));
            }
        }
    }

    public class AliFolderItem(string name, string driver, string parent, string current) : AliViewItem(name, driver, parent, current), IFolderViewItem<AliViewItem>
    {

        IEnumerable<AliViewItem>? IFolderViewItem<AliViewItem>.Items => _items;
        IEnumerable<IViewItem>? IFolderViewItem.Items => _items;
        private ObservableCollection<AliViewItem>? _items;
        public ObservableCollection<AliViewItem>? Items
        {
            get { return _items; }
            set
            {
                _items = value;
                OnPropertyChanged(nameof(Items));
            }
        }

        private string? _refreshTag;
        public string? RefreshTag
        {
            get { return _refreshTag; }
            set
            {
                _refreshTag = value;
                OnPropertyChanged(nameof(RefreshTag));
            }
        }

        public string? Marker { get; set; }
    }

    public record AliDriverItem(string Name, string Id);
    public abstract class TianYiViewItem(string name) : ViewItem(name)
    {
    }
    public class TianYiFileItem(string name, long size) : TianYiViewItem(name), IFileViewItem
    {
        public long? Size => size;


        private string? _url;
        public string? Url
        {
            get { return _url; }
            set
            {
                _url = value;
                OnPropertyChanged(nameof(Url));
            }
        }
    }

    public class TianYiFolderItem(string name) : TianYiViewItem(name), IFolderViewItem<TianYiViewItem>
    {
        public string? RefreshTag => null;
        IEnumerable<TianYiViewItem>? IFolderViewItem<TianYiViewItem>.Items => _items;
        IEnumerable<IViewItem>? IFolderViewItem.Items => _items;
        private TianYiViewItem[]? _items;
        public TianYiViewItem[]? Items
        {
            get { return _items; }
            set
            {
                _items = value;
                OnPropertyChanged(nameof(Items));
            }
        }
    }
}
