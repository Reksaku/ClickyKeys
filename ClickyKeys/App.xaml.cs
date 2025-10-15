using System.Configuration;
using System.Data;
using System;
using System.Windows;


namespace ClickyKeys
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            GlobalInputHook.Instance.Start();   // uruchamia globalne hooki

            var main = new MainWindow();
            MainWindow = main;
            main.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            GlobalInputHook.Instance.Stop();

            base.OnExit(e);
            // np. sprzątanie zasobów
        }
    }


}
