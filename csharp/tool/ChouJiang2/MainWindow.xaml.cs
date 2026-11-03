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

namespace ChouJiang2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        public static ICommand Start = new RoutedUICommand();


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
        private int _isrolling = 0;

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
                new ChouJiangItem{ Index = 0, Name = "谢谢参与", Desc="", Value = 0.0, Status = 0 },
                new ChouJiangItem{ Index = 1, Name = "0.01金币", Desc="", Value = 1.0, Status = 0 },
                new ChouJiangItem{ Index = 2, Name = "0.02金币", Desc="", Value = 2.0, Status = 0 },
                new ChouJiangItem{ Index = 3, Name = "0.05金币", Desc="", Value = 3.0, Status = 0 },
                new ChouJiangItem{ Index = 4, Name = "0.1金币", Desc="", Value = 4.0, Status = 0 },
                new ChouJiangItem{ Index = 5, Name = "0.2金币", Desc="", Value = 5.0, Status = 0 },
                new ChouJiangItem{ Index = 6, Name = "0.5金币", Desc="", Value = 6.0, Status = 0 },
                new ChouJiangItem{ Index = 7, Name = "1金币", Desc="", Value = 7.0, Status = 0 },
            }.OrderBy(s => _random.Next()).ToArray();
            AllTypes = new[]
            {
                new ChouJiangType("随机抽取", (s) =>{
                    return _random.Next(0,s.Length);
                }),
                new ChouJiangType("总是不中", (s) =>{
                    return 0;
                }),
                new ChouJiangType("总是最大", (s) =>{
                    return s.Length - 1;
                }),
                new ChouJiangType("不中居多", (s) =>{
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
                    return di;
                }),
                new ChouJiangType("小奖居多", (s) =>{
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
                    return di;
                })
            };
            CurrentType = AllTypes[0];
            ViewItems = _allItems;
            InitializeComponent();
            CommandBindings.Add(new CommandBinding(Start, OnStart, CanStart));
        }

        private async Task StartRolling(int target)
        {
            _isrolling = 1;
            try
            {
                var start = 0;
                var pre = start % _allItems.Length;
                _allItems[pre].Status = 1;
                var wait = 50;
                while (start < target)
                {
                    var dis = target - start;
                    if (dis < 10)
                    {
                        wait += 50;
                    }
                    await Task.Delay(wait);
                    _allItems[pre].Status = 0;
                    pre = ++start % _allItems.Length;
                    _allItems[pre].Status = 1;
                }
                await Task.Delay(wait);
                _allItems[pre].Status = 2;
            }
            finally
            {
                _isrolling = 0;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void CanStart(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CurrentType is not null && _isrolling == 0;
        }

        private async void OnStart(object sender, ExecutedRoutedEventArgs e)
        {
            Array.ForEach(_allItems, s => s.Status = 0);
            var index = CurrentType.SelectItem(_allItems);
            var target = Array.FindIndex(_allItems, s => s.Index == index);
            await StartRolling(target + _allItems.Length * 5);
        }
    }

    public class ChouJiangItem : DependencyObject
    {
        public int Index { get; init; }
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

    public delegate int SelectItem(ChouJiangItem[] items);
    public record ChouJiangType(String Title, SelectItem SelectItem);
}