﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Winfred
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private void Application_Deactivated(object sender, EventArgs e)
        {
            this.MainWindow.Visibility = Visibility.Hidden;
        }
    }
}
