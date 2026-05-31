using ShoeStoreApp.Services;
using System;
using System.Windows;

namespace ShoeStoreApp.Views
{
    public partial class AddProductWindow : Window
    {
        public AddProductWindow()
        {
            InitializeComponent();
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            // Проверяем, что заполнены обязательные поля
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Введите наименование товара!");
                return;
            }

            if (string.IsNullOrWhiteSpace(txtPrice.Text))
            {
                MessageBox.Show("Введите цену!");
                return;
            }

            // Собираем данные для сохранения
            string article = txtArticle.Text;
            string name = txtName.Text;
            string category = txtCategory.Text;
            string description = txtDescription.Text;
            string brand = txtBrand.Text;
            string supplier = txtSupplier.Text;
            decimal price = Convert.ToDecimal(txtPrice.Text);
            int quantity = string.IsNullOrWhiteSpace(txtQuantity.Text) ? 0 : Convert.ToInt32(txtQuantity.Text);
            string photoPath = txtPhotoPath.Text;

            // Сохраняем в базу
            DatabaseHelper.AddProduct(article, name, category, description, brand, supplier, price, quantity, photoPath);

            MessageBox.Show("Товар добавлен!");
            this.Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}