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

            Database = new DatabaseService();

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