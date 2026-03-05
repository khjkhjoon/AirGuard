using AirGuard.WPF.Services;
using AirGuard.WPF.Views;
using System.Windows;

namespace AirGuard.WPF
{
    public partial class App : Application
    {
        public static DatabaseService Database { get; private set; } = null!;
        public static UserRecord? CurrentUser { get; set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
    MessageBox.Show($"오류: {ex.ExceptionObject}", "Fatal Error");
            DispatcherUnhandledException += (s, ex) =>
            {
                MessageBox.Show($"UI 오류: {ex.Exception.Message}", "Error");
                ex.Handled = true;
            };

            Database = new DatabaseService(
                host: "localhost",
                port: 3306,
                database: "airguard",
                user: "root",
                password: "khjoon"
            );

            var login = new LoginWindow(Database);
            bool? result = login.ShowDialog();

            if (result == true && login.LoggedInUser != null)
            {
                CurrentUser = login.LoggedInUser;
                var main = new MainWindow();
                main.Show();
            }
            else
            {
                Shutdown();
            }
        }
    }
}