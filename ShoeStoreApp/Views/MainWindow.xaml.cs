using System;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ShoeStoreApp.Models;
using ShoeStoreApp.Services;

namespace ShoeStoreApp.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            LoadProducts();
            UpdateUserInfo();
            ShowAdminButtons(); // Показываем кнопки только для админа
        }

        private void LoadProducts()
        {
            DataTable products = DatabaseHelper.GetProducts();
            products.Columns.Add("PhotoImage", typeof(BitmapImage));

            foreach (DataRow row in products.Rows)
            {
                row["PhotoImage"] = GetImage(row["PhotoPath"]?.ToString());
            }

            products.Columns.Remove("PhotoPath");
            itemsProducts.ItemsSource = products.DefaultView;
        }

        // Показываем кнопки админа
        private void ShowAdminButtons()
        {
            if (UserSession.Role == "Admin")
            {
                btnAddProduct.Visibility = Visibility.Visible;

                // Показываем кнопки удаления у каждого товара
                // Это можно сделать через поиск элементов, но проще перезагрузить
            }
        }

        // Кнопка добавления товара
        private void btnAddProduct_Click(object sender, RoutedEventArgs e)
        {
            if (UserSession.Role != "Admin")
            {
                MessageBox.Show("Доступ запрещён. Только для администратора.");
                return;
            }

            AddProductWindow addWindow = new AddProductWindow();
            addWindow.ShowDialog();
            LoadProducts();
        }

        private void btnDeleteProduct_Click(object sender, RoutedEventArgs e)
        {
            if (UserSession.Role != "Администратор")
            {
                MessageBox.Show("Доступ запрещён. Только для администратора.");
                return;
            }

            Button btn = sender as Button;
            int productId = (int)btn.Tag;

            if (MessageBox.Show("Удалить товар?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                DatabaseHelper.DeleteProduct(productId);
                LoadProducts();
            }
        }

        private BitmapImage GetImage(string fileName)
        {
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", fileName ?? "");

            if (File.Exists(fullPath))
                return new BitmapImage(new Uri(fullPath));
            else
                return new BitmapImage(new Uri("/Resources/picture.png", UriKind.Relative));
        }

        private void UpdateUserInfo()
        {
            if (UserSession.IsAuthenticated)
            {
                lblUserFio.Text = UserSession.FullName;
                lblUserRole.Text = $"({UserSession.Role})";
            }
            else
            {
                lblUserFio.Text = "Гость";
                lblUserRole.Text = "";
            }
        }

        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            UserSession.Clear();
            new LoginWindow().Show();
            this.Close();
        }
    }
}