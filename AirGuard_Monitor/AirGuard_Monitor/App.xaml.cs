using AirGuard.WPF.Views;
using System.Windows;

namespace AirGuard.WPF
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += (s, ex) =>
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                        "airguard_error.txt"),
                    $"[{System.DateTime.Now}] UI 예외: {ex.Exception.Message}\n{ex.Exception.StackTrace}\n\n");
                ex.Handled = true;
            };

            new MainWindow().Show();
        }
    }
}