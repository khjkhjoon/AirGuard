using AirGuard.WPF.Services;
using System.Windows;
using System.Windows.Input;

namespace AirGuard.WPF.Views
{
    public partial class LoginWindow : Window
    {
        private readonly DatabaseService _db;
        public UserRecord? LoggedInUser { get; private set; }

        public LoginWindow(DatabaseService db)
        {
            InitializeComponent();
            _db = db;
            UsernameBox.Focus();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e) => TryLogin();

        private void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) TryLogin();
        }

        private void TryLogin()
        {
            string username = UsernameBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowError("아이디와 비밀번호를 입력하세요");
                return;
            }

            LoginButton.IsEnabled = false;
            LoginButton.Content = "확인 중...";

            var user = _db.Login(username, password);

            if (user != null)
            {
                LoggedInUser = user;
                DialogResult = true;
                Close();
            }
            else
            {
                ShowError("아이디 또는 비밀번호가 올바르지 않습니다");
                LoginButton.IsEnabled = true;
                LoginButton.Content = "LOGIN";
                PasswordBox.Clear();
                PasswordBox.Focus();
            }
        }

        private void ShowError(string msg)
        {
            ErrorText.Text = msg;
            ErrorText.Visibility = Visibility.Visible;
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}