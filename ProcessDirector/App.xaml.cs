using System;
using System.Windows;

namespace ProcessDirector
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            AppDomain.CurrentDomain.UnhandledException += (s, args) => { };
            DispatcherUnhandledException += (s, args) => args.Handled = true;
        }
    }
}