using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace EpubReaderWithAnnotations
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void ApplicationStartup(object sender, StartupEventArgs e)
        {
            MainWindow mw = new MainWindow();
            mw.Show();
            Application.Current.MainWindow = mw;
            Application.Current.MainWindow.Closed += (s, a) =>
             {
                 Shutdown();
             };
        }
    }
}
