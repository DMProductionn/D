using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Media.Imaging;
using ShoeStoreApp.Models;
using ShoeStoreApp.Services;

namespace ShoeStoreApp.Views
{
    public partial class MainWindow : Window
    {
        private const string AllSuppliers = "Все поставщики";

        private static readonly string[] SearchColumns =
        {
            "Article",
            "Name",
            "Category",
            "Description",
            "Brand",
            "Supplier",
            "UnitOfMeasure"
        };

        private DataView _productsView;
        private bool _isRefreshingFilters;
        private AddProductWindow _activeProductWindow;

        public MainWindow()
        {
            InitializeComponent();
            UpdateUserInfo();
            ConfigureAccess();
            LoadProducts();
        }

        public Visibility AdminActionsVisibility
        {
            get { return UserSession.IsAdmin ? Visibility.Visible : Visibility.Collapsed; }
        }

        private void LoadProducts()
        {
            try
            {
                DataTable products = DatabaseHelper.GetProducts();
                products.Columns.Add("PhotoImage", typeof(BitmapImage));

                foreach (DataRow row in products.Rows)
                    row["PhotoImage"] = GetImage(row["PhotoPath"].ToString());

                _productsView = products.DefaultView;
                itemsProducts.ItemsSource = _productsView;

                LoadSupplierFilter(products);
                ApplyProductView();
            }
            catch (Exception exception)
            {
                itemsProducts.ItemsSource = null;
                lblProductCount.Text = "Не удалось загрузить товары.";
                ShowError(
                    "Не удалось загрузить каталог товаров. Проверьте подключение к базе данных и повторите вход.\n\n" +
                    "Подробности: " + exception.Message);
            }
        }

        private void LoadSupplierFilter(DataTable products)
        {
            string selectedSupplier = cmbSupplierFilter.SelectedItem as string;
            SortedSet<string> suppliers = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);

            foreach (DataRow row in products.Rows)
            {
                string supplier = row["Supplier"].ToString().Trim();

                if (!string.IsNullOrWhiteSpace(supplier))
                    suppliers.Add(supplier);
            }

            _isRefreshingFilters = true;
            cmbSupplierFilter.Items.Clear();
            cmbSupplierFilter.Items.Add(AllSuppliers);

            foreach (string supplier in suppliers)
                cmbSupplierFilter.Items.Add(supplier);

            cmbSupplierFilter.SelectedItem =
                !string.IsNullOrWhiteSpace(selectedSupplier) && cmbSupplierFilter.Items.Contains(selectedSupplier)
                    ? selectedSupplier
                    : AllSuppliers;

            _isRefreshingFilters = false;
        }

        private void ApplyProductView()
        {
            if (_productsView == null || _isRefreshingFilters)
                return;

            List<string> conditions = new List<string>();
            string selectedSupplier = cmbSupplierFilter.SelectedItem as string;

            if (!string.IsNullOrWhiteSpace(selectedSupplier) && selectedSupplier != AllSuppliers)
            {
                conditions.Add(
                    string.Format("Convert([Supplier], 'System.String') = '{0}'", EscapeFilterValue(selectedSupplier)));
            }

            string[] searchTerms = txtSearch.Text
                .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string searchTerm in searchTerms)
            {
                string escapedTerm = EscapeLikeValue(searchTerm);
                string termCondition = string.Join(
                    " OR ",
                    SearchColumns.Select(column =>
                        string.Format("Convert([{0}], 'System.String') LIKE '%{1}%'", column, escapedTerm)));

                conditions.Add("(" + termCondition + ")");
            }

            _productsView.RowFilter = string.Join(" AND ", conditions);

            ComboBoxItem selectedSort = cmbQuantitySort.SelectedItem as ComboBoxItem;
            _productsView.Sort = selectedSort == null ? "" : selectedSort.Tag.ToString();

            lblProductCount.Text = string.Format(
                "Показано товаров: {0} из {1}",
                _productsView.Count,
                _productsView.Table.Rows.Count);
        }

        private void ConfigureAccess()
        {
            btnAddProduct.Visibility = UserSession.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
            filterPanel.Visibility = UserSession.CanManageCatalogView ? Visibility.Visible : Visibility.Collapsed;
        }

        private void btnAddProduct_Click(object sender, RoutedEventArgs e)
        {
            OpenProductEditor(null);
        }

        private void btnEditProduct_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            Button button = sender as Button;

            if (button != null)
                OpenProductEditor(Convert.ToInt32(button.Tag));
        }

        private void ProductBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!UserSession.IsAdmin || FindParent<Button>(e.OriginalSource as DependencyObject) != null)
                return;

            Border border = sender as Border;
            DataRowView product = border == null ? null : border.DataContext as DataRowView;

            if (product != null)
                OpenProductEditor(Convert.ToInt32(product["ProductID"]));
        }

        private void OpenProductEditor(int? productId)
        {
            if (!UserSession.IsAdmin)
            {
                ShowWarning("Добавлять и редактировать товары может только администратор.");
                return;
            }

            if (_activeProductWindow != null)
            {
                _activeProductWindow.Activate();
                ShowWarning("Сначала завершите работу с уже открытым окном товара.");
                return;
            }

            _activeProductWindow = new AddProductWindow(productId);
            _activeProductWindow.Owner = this;

            try
            {
                bool? saved = _activeProductWindow.ShowDialog();

                if (saved == true)
                    LoadProducts();
            }
            finally
            {
                _activeProductWindow = null;
            }
        }

        private void btnDeleteProduct_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (!UserSession.IsAdmin)
            {
                ShowWarning("Удалять товары может только администратор.");
                return;
            }

            Button button = sender as Button;

            if (button == null)
                return;

            int productId = Convert.ToInt32(button.Tag);

            try
            {
                ProductRecord product = DatabaseHelper.GetProduct(productId);
                MessageBoxResult answer = MessageBox.Show(
                    string.Format(
                        "Удалить товар «{0}»?\n\nОперацию нельзя отменить. Товар из заказа удалить невозможно.",
                        product.Name),
                    "Предупреждение об удалении",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (answer != MessageBoxResult.Yes)
                    return;

                DatabaseHelper.DeleteProduct(productId);

                try
                {
                    TryDeleteStoredPhoto(product.PhotoPath, productId);
                }
                catch (Exception exception)
                {
                    MessageBox.Show(
                        "Товар удалён, но его изображение удалить не удалось. " +
                        "При необходимости удалите файл вручную из папки Images.\n\n" +
                        "Подробности: " + exception.Message,
                        "Предупреждение",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                LoadProducts();

                MessageBox.Show(
                    "Товар удалён из каталога.",
                    "Удаление выполнено",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (InvalidOperationException exception)
            {
                ShowWarning(exception.Message);
            }
            catch (Exception exception)
            {
                ShowError("Не удалось удалить товар.\n\nПодробности: " + exception.Message);
            }
        }

        private BitmapImage GetImage(string photoPath)
        {
            string fullPath = ResolvePhotoPath(photoPath);

            if (!string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath))
                return LoadBitmap(new Uri(fullPath, UriKind.Absolute));

            return LoadBitmap(new Uri("pack://application:,,,/Resources/picture.png", UriKind.Absolute));
        }

        private static BitmapImage LoadBitmap(Uri uri)
        {
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = uri;
            image.EndInit();
            image.Freeze();
            return image;
        }

        private static string ResolvePhotoPath(string photoPath)
        {
            if (string.IsNullOrWhiteSpace(photoPath))
                return null;

            if (Path.IsPathRooted(photoPath))
                return photoPath;

            string imagesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", photoPath);

            if (File.Exists(imagesPath))
                return imagesPath;

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, photoPath);
        }

        private static void TryDeleteStoredPhoto(string photoPath, int productId)
        {
            if (string.IsNullOrWhiteSpace(photoPath) ||
                DatabaseHelper.IsPhotoUsedByAnotherProduct(photoPath, productId))
            {
                return;
            }

            string imagesDirectory = Path.GetFullPath(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images"));

            string fullPath = ResolvePhotoPath(photoPath);

            if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
                return;

            fullPath = Path.GetFullPath(fullPath);

            if (fullPath.StartsWith(imagesDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                File.Delete(fullPath);
        }

        private void UpdateUserInfo()
        {
            if (UserSession.IsAuthenticated)
            {
                lblUserFio.Text = UserSession.FullName;
                lblUserRole.Text = "(" + UserSession.Role + ")";
            }
            else
            {
                lblUserFio.Text = "Гость";
                lblUserRole.Text = "";
            }
        }

        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult answer = MessageBox.Show(
                "Вернуться к окну входа?",
                "Возврат к авторизации",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
                return;

            UserSession.Clear();
            new LoginWindow().Show();
            Close();
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyProductView();
        }

        private void cmbSupplierFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyProductView();
        }

        private void cmbQuantitySort_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyProductView();
        }

        private static string EscapeFilterValue(string value)
        {
            return value.Replace("'", "''");
        }

        private static string EscapeLikeValue(string value)
        {
            return EscapeFilterValue(value)
                .Replace("[", "[[]")
                .Replace("%", "[%]")
                .Replace("*", "[*]");
        }

        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                T parent = child as T;

                if (parent != null)
                    return parent;

                if (child is Visual || child is Visual3D)
                {
                    child = VisualTreeHelper.GetParent(child);
                }
                else
                {
                    ContentElement contentElement = child as ContentElement;
                    child = contentElement == null
                        ? LogicalTreeHelper.GetParent(child)
                        : ContentOperations.GetParent(contentElement);
                }
            }

            return null;
        }

        private static void ShowWarning(string message)
        {
            MessageBox.Show(message, "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private static void ShowError(string message)
        {
            MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
