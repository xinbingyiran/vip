using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Text;
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
        string Name { get; set; }
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

    public abstract class ViewItem : DependencyObject, IViewItem
    {
        public string Name
        {
            get { return (string)GetValue(NameProperty); }
            set { SetValue(NameProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Name.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty NameProperty =
            DependencyProperty.Register("Name", typeof(string), typeof(ViewItem), new PropertyMetadata(null));

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

        public string? Url
        {
            get { return (string?)GetValue(UrlProperty); }
            set { SetValue(UrlProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Url.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty UrlProperty =
            DependencyProperty.Register("Url", typeof(string), typeof(ViewItem), new PropertyMetadata(null));
    }

    public class AliFolderItem(string name, string driver, string parent, string current) : AliViewItem(name, driver, parent, current), IFolderViewItem<AliViewItem>
    {

        IEnumerable<AliViewItem>? IFolderViewItem<AliViewItem>.Items => Items;
        IEnumerable<IViewItem>? IFolderViewItem.Items => Items;
        public ObservableCollection<AliViewItem>? Items
        {
            get { return (ObservableCollection<AliViewItem>?)GetValue(ItemsProperty); }
            set { SetValue(ItemsProperty, value); }
        }



        public string? RefreshTag
        {
            get { return (string?)GetValue(RefreshTagProperty); }
            set { SetValue(RefreshTagProperty, value); }
        }

        // Using a DependencyProperty as the backing store for RefreshTag.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty RefreshTagProperty =
            DependencyProperty.Register("RefreshTag", typeof(string), typeof(AliFolderItem), new PropertyMetadata(null));


        // Using a DependencyProperty as the backing store for Items.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ItemsProperty =
            DependencyProperty.Register("Items", typeof(ObservableCollection<AliViewItem>), typeof(AliFolderItem), new PropertyMetadata(null));
        public string? Marker;
    }

    public record AliDriverItem(string Name, string Id);

    public abstract class TianYiViewItem(string name, string code, string id) : ViewItem(name)
    {
        public string Code => code;
        public string Id => id;
    }
    public class TianYiFileItem(string name,long size, string code, string id) : TianYiViewItem(name, code, id), IFileViewItem
    {
        public long? Size => size;

        public string? Url
        {
            get { return (string?)GetValue(UrlProperty); }
            set { SetValue(UrlProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Url.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty UrlProperty =
            DependencyProperty.Register("Url", typeof(string), typeof(TianYiFileItem), new PropertyMetadata(null));
    }

    public class TianYiFolderItem(string name, string code, string id) : TianYiViewItem(name, code, id), IFolderViewItem<TianYiViewItem>
    {

        IEnumerable<TianYiViewItem>? IFolderViewItem<TianYiViewItem>.Items => Items;
        IEnumerable<IViewItem>? IFolderViewItem.Items => Items;
        public TianYiViewItem[]? Items
        {
            get { return (TianYiViewItem[]?)GetValue(ItemsProperty); }
            set { SetValue(ItemsProperty, value); }
        }

        public string? RefreshTag => null;

        // Using a DependencyProperty as the backing store for Items.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ItemsProperty =
            DependencyProperty.Register("Items", typeof(TianYiViewItem[]), typeof(TianYiFolderItem), new PropertyMetadata(null));
        public string? Marker;
    }
}
