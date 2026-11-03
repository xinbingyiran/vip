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

namespace ChouJiang
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        public static ICommand Start = new RoutedUICommand();
        public static ICommand Select = new RoutedUICommand();


        public ChouJiangType[] AllTypes
        {
            get { return (ChouJiangType[])GetValue(AllTypesProperty); }
            set { SetValue(AllTypesProperty, value); }
        }

        // Using a DependencyProperty as the backing store for AllTypes.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty AllTypesProperty =
            DependencyProperty.Register("AllTypes", typeof(ChouJiangType[]), typeof(MainWindow), new PropertyMetadata(null));



        public ChouJiangType CurrentType
        {
            get { return (ChouJiangType)GetValue(CurrentTypeProperty); }
            set { SetValue(CurrentTypeProperty, value); }
        }

        // Using a DependencyProperty as the backing store for CurrentType.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CurrentTypeProperty =
            DependencyProperty.Register("CurrentType", typeof(ChouJiangType), typeof(MainWindow), new PropertyMetadata(null));


        private ChouJiangItem[] _allItems;
        private Random _random;


        public ChouJiangItem[] ViewItems
        {
            get { return (ChouJiangItem[])GetValue(ViewItemsProperty); }
            set { SetValue(ViewItemsProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ViewItems.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ViewItemsProperty =
            DependencyProperty.Register("ViewItems", typeof(ChouJiangItem[]), typeof(MainWindow), new PropertyMetadata(null));


        public MainWindow()
        {
            _random = new Random();
            _allItems = new[]
            {
                new ChouJiangItem{  Name = "谢谢参与", Desc="", Value = 0.0, Status = 0 },
                new ChouJiangItem{  Name = "0.01金币", Desc="", Value = 1.0, Status = 0 },
                new ChouJiangItem{  Name = "0.02金币", Desc="", Value = 2.0, Status = 0 },
                new ChouJiangItem{  Name = "0.05金币", Desc="", Value = 3.0, Status = 0 },
                new ChouJiangItem{  Name = "0.1金币", Desc="", Value = 4.0, Status = 0 },
                new ChouJiangItem{  Name = "0.2金币", Desc="", Value = 5.0, Status = 0 },
                new ChouJiangItem{  Name = "0.5金币", Desc="", Value = 6.0, Status = 0 },
                new ChouJiangItem{  Name = "1金币", Desc="", Value = 7.0, Status = 0 },
                new ChouJiangItem{  Name = "10金币", Desc="", Value = 8.0, Status = 0 },
            };
            AllTypes = new[]
            {
                new ChouJiangType("随机抽取", (s, i) =>{
                    var rst = s.OrderBy(s=>_random.Next()).ToArray();
                    Array.ForEach(rst,r=>r.Status = 1);
                    rst[i].Status = 2;
                    return rst;
                }),
                new ChouJiangType("总是不中", (s, i) =>{
                    var oth = s.Skip(1).OrderBy(s=>_random.Next()).ToArray();
                    var rst = oth.Take(i).Append(s[0]).Concat(oth.Skip(i)).ToArray();
                    Array.ForEach(rst,r=>r.Status = 1);
                    rst[i].Status = 2;
                    return rst;
                }),
                new ChouJiangType("总是最大", (s, i) =>{
                    var oth = s.Take(s.Length-1).OrderBy(s=>_random.Next()).ToArray();
                    var rst = oth.Take(i).Append(s[s.Length-1]).Concat(oth.Skip(i)).ToArray();
                    Array.ForEach(rst,r=>r.Status = 1);
                    rst[i].Status = 2;
                    return rst;
                }),
                new ChouJiangType("不中居多", (s, i) =>{
                    var v = 100000;
                    var limit = new int[s.Length];
                    for(var idx = 0;idx < s.Length;idx ++)
                    {
                        limit[idx] = v;
                        if(idx > 0)
                        {
                            limit[idx] += limit[idx-1];
                        }
                        if(v >= 10)
                        {
                            v /= 10;
                        }
                    }
                    var si = _random.Next(0,limit[limit.Length - 1]);
                    var di = 0;
                    while(si >= limit[di])
                    {
                        di ++;
                    }
                    var oth = s.Take(di).Concat(s.Skip(di+1)).OrderBy(s=>_random.Next()).ToArray();
                    var rst = oth.Take(i).Append(s[di]).Concat(oth.Skip(i)).ToArray();
                    Array.ForEach(rst,r=>r.Status = 1);
                    rst[i].Status = 2;
                    return rst;
                }),
                new ChouJiangType("小奖居多", (s, i) =>{
                    var v = 100000;
                    var limit = new int[s.Length];
                    for(var idx = 1;idx < s.Length;idx ++)
                    {
                        limit[idx] = v;
                        if(idx > 0)
                        {
                            limit[idx] += limit[idx-1];
                        }
                        if(v >= 10)
                        {
                            v /= 10;
                        }
                    }
                    var si = _random.Next(0,limit[limit.Length - 1]);
                    var di = 0;
                    while(si >= limit[di])
                    {
                        di ++;
                    }
                    var oth = s.Take(di).Concat(s.Skip(di+1)).OrderBy(s=>_random.Next()).ToArray();
                    var rst = oth.Take(i).Append(s[di]).Concat(oth.Skip(i)).ToArray();
                    Array.ForEach(rst,r=>r.Status = 1);
                    rst[i].Status = 2;
                    return rst;
                })
            };
            CurrentType = AllTypes[0];
            ViewItems = _allItems;
            InitializeComponent();
            CommandBindings.Add(new CommandBinding(Start, OnStart, CanStart));
            CommandBindings.Add(new CommandBinding(Select, OnSelect, CanSelect));
        }

        private void CanSelect(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CurrentType is not null && _allItems[0].Status is 0;
        }

        private void CanStart(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CurrentType is not null && _allItems[0].Status is not 0;
        }

        private void OnSelect(object sender, ExecutedRoutedEventArgs e)
        {
            ViewItems = CurrentType.SelectItem(_allItems, Convert.ToInt32(e.Parameter));
        }

        private void OnStart(object sender, ExecutedRoutedEventArgs e)
        {
            Array.ForEach(_allItems, s => s.Status = 0);
            ViewItems = _allItems;
        }
    }

    public class ChouJiangItem : DependencyObject
    {
        public String? Name { get; init; }
        public String? Desc { get; init; }
        public double Value { get; init; }

        public int Status
        {
            get { return (int)GetValue(StatusProperty); }
            set { SetValue(StatusProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Status.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register("Status", typeof(int), typeof(ChouJiangItem), new PropertyMetadata(0));

    }

    public delegate ChouJiangItem[] SelectItem(ChouJiangItem[] items, int index);
    public record ChouJiangType(String Title, SelectItem SelectItem);
}
