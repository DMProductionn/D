using System.Windows;
using ShoeStoreApp.Services;
using ShoeStoreApp.Models;

namespace ShoeStoreApp.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            string login = txtLogin.Text.Trim();
            string password = txtPassword.Password;

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show(
                    "Введите логин и пароль. Заполните оба поля и повторите вход.",
                    "Проверьте данные",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                bool success = DatabaseHelper.CheckLogin(login, password, out string fullName, out string role);

                if (success)
                {
                    UserSession.FullName = fullName;
                    UserSession.Role = role;
                    UserSession.IsAuthenticated = true;

                    MessageBox.Show(
                        $"Добро пожаловать, {fullName}.",
                        "Вход выполнен",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    MainWindow mainWindow = new MainWindow();
                    mainWindow.Show();
                    Close();
                }
                else
                {
                    MessageBox.Show(
                        "Логин или пароль указаны неверно. Проверьте раскладку клавиатуры, регистр символов и повторите вход.",
                        "Ошибка авторизации",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (System.Exception exception)
            {
                MessageBox.Show(
                    "Не удалось выполнить вход. Проверьте подключение к базе данных и повторите попытку.\n\n" +
                    "Подробности: " + exception.Message,
                    "Ошибка подключения",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
