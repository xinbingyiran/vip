using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GameBox;

public static class Commands
{
    public static readonly RoutedUICommand Search = new ();
    public static readonly RoutedUICommand GetFile = new ();
    public static readonly RoutedUICommand GetUrl = new ();
    public static readonly RoutedUICommand Download = new ();
}
