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
                MessageBox.Show("Введите логин или пароль", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool success = DatabaseHelper.CheckLogin(login, password, out string fullName, out string role);

            if (success)
            {
                UserSession.FullName = fullName;
                UserSession.Role = role;
                UserSession.IsAuthenticated = true;

                MessageBox.Show($"Добро пожаловать {fullName}", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);

                MainWindow mainWindow = new MainWindow();
                mainWindow.Show();
                this.Close();
            }
            else
            {
                MessageBox.Show("Неверный логин или пароль", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}