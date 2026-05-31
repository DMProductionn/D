using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using ShoeStoreApp.Models;
using ShoeStoreApp.Services;

namespace ShoeStoreApp.Views
{
    public partial class AddProductWindow : Window
    {
        private readonly int? _productId;
        private string _originalPhotoPath;
        private string _selectedPhotoSourcePath;
        private bool _isLoaded;
        private bool _allowClose;

        public AddProductWindow()
            : this(null)
        {
        }

        public AddProductWindow(int? productId)
        {
            _productId = productId;
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isLoaded)
                return;

            _isLoaded = true;

            try
            {
                LoadLookupValues();

                if (_productId.HasValue)
                    LoadProduct(_productId.Value);
                else
                    PrepareNewProduct();
            }
            catch (Exception exception)
            {
                ShowError(
                    "Не удалось открыть форму товара. Вернитесь к списку и повторите попытку.\n\n" +
                    "Подробности: " + exception.Message);
                _allowClose = true;
                Close();
            }
        }

        private void LoadLookupValues()
        {
            FillComboBox(cmbCategory, DatabaseHelper.GetLookupValues("Category"));
            FillComboBox(cmbBrand, DatabaseHelper.GetLookupValues("Brand"));
            FillComboBox(cmbSupplier, DatabaseHelper.GetLookupValues("Supplier"));
            FillComboBox(cmbUnitOfMeasure, DatabaseHelper.GetLookupValues("UnitOfMeasure"));
        }

        private void LoadProduct(int productId)
        {
            ProductRecord product = DatabaseHelper.GetProduct(productId);

            Title = "Редактирование товара - Магазин обуви";
            lblHeading.Text = "Редактирование товара";
            panelProductId.Visibility = Visibility.Visible;

            txtProductId.Text = product.ProductId.ToString(CultureInfo.InvariantCulture);
            txtArticle.Text = product.Article;
            txtName.Text = product.Name;
            cmbCategory.Text = product.Category;
            txtDescription.Text = product.Description;
            cmbBrand.Text = product.Brand;
            cmbSupplier.Text = product.Supplier;
            txtPrice.Text = product.Price.ToString("0.00", CultureInfo.CurrentCulture);
            cmbUnitOfMeasure.Text = product.UnitOfMeasure;
            txtQuantity.Text = product.Quantity.ToString(CultureInfo.CurrentCulture);
            txtDiscount.Text = product.Discount.ToString("0.00", CultureInfo.CurrentCulture);

            _originalPhotoPath = product.PhotoPath;
            imgPhoto.Source = GetImage(product.PhotoPath);
        }

        private void PrepareNewProduct()
        {
            Title = "Добавление товара - Магазин обуви";
            lblHeading.Text = "Добавление товара";
            panelProductId.Visibility = Visibility.Collapsed;

            txtQuantity.Text = "0";
            txtDiscount.Text = "0";
            cmbUnitOfMeasure.Text = "шт.";
            imgPhoto.Source = GetImage(null);
        }

        private void btnSelectPhoto_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Выберите изображение товара",
                Filter = "Изображения (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                imgPhoto.Source = LoadBitmap(new Uri(dialog.FileName, UriKind.Absolute));
                _selectedPhotoSourcePath = dialog.FileName;
            }
            catch (Exception exception)
            {
                ShowError(
                    "Выбранный файл не удалось открыть как изображение. Выберите корректный JPG, PNG или BMP файл.\n\n" +
                    "Подробности: " + exception.Message);
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!UserSession.IsAdmin)
            {
                ShowWarning("Сохранять изменения товара может только администратор.");
                return;
            }

            ProductRecord product;

            if (!TryReadProduct(out product))
                return;

            string savedPhotoPath = null;

            try
            {
                if (!string.IsNullOrWhiteSpace(_selectedPhotoSourcePath))
                {
                    savedPhotoPath = SaveSelectedImage(_selectedPhotoSourcePath);
                    product.PhotoPath = savedPhotoPath;
                }
                else
                {
                    product.PhotoPath = _originalPhotoPath ?? "";
                }

                int savedProductId;

                if (_productId.HasValue)
                {
                    DatabaseHelper.UpdateProduct(product);
                    savedProductId = product.ProductId;
                }
                else
                {
                    savedProductId = DatabaseHelper.AddProduct(product);
                }

                if (!string.IsNullOrWhiteSpace(savedPhotoPath) &&
                    !string.Equals(savedPhotoPath, _originalPhotoPath, StringComparison.OrdinalIgnoreCase))
                {
                    TryDeleteOldPhoto(_originalPhotoPath, savedProductId);
                }

                MessageBox.Show(
                    _productId.HasValue ? "Изменения товара сохранены." : "Товар добавлен в каталог.",
                    "Сохранение выполнено",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                _allowClose = true;
                DialogResult = true;
            }
            catch (Exception exception)
            {
                if (!string.IsNullOrWhiteSpace(savedPhotoPath))
                    TryDeleteGeneratedPhoto(savedPhotoPath);

                ShowError(
                    "Не удалось сохранить товар. Проверьте заполненные поля и повторите попытку.\n\n" +
                    "Подробности: " + exception.Message);
            }
        }

        private bool TryReadProduct(out ProductRecord product)
        {
            product = null;

            string article = txtArticle.Text.Trim();
            string name = txtName.Text.Trim();
            string category = cmbCategory.Text.Trim();
            string brand = cmbBrand.Text.Trim();
            string supplier = cmbSupplier.Text.Trim();
            string unitOfMeasure = cmbUnitOfMeasure.Text.Trim();

            if (string.IsNullOrWhiteSpace(article))
                return ShowValidation("Введите артикул товара.", txtArticle);

            if (string.IsNullOrWhiteSpace(name))
                return ShowValidation("Введите наименование товара.", txtName);

            if (string.IsNullOrWhiteSpace(category))
                return ShowValidation("Выберите категорию товара или введите новую.", cmbCategory);

            if (string.IsNullOrWhiteSpace(brand))
                return ShowValidation("Выберите производителя или введите нового.", cmbBrand);

            if (string.IsNullOrWhiteSpace(supplier))
                return ShowValidation("Выберите поставщика или введите нового.", cmbSupplier);

            decimal price;

            if (!TryParseDecimal(txtPrice.Text, out price) || price < 0)
            {
                return ShowValidation(
                    "Цена должна быть неотрицательным числом. Допускаются сотые части, например 2499,90.",
                    txtPrice);
            }

            if (string.IsNullOrWhiteSpace(unitOfMeasure))
                return ShowValidation("Введите единицу измерения, например «шт.».", cmbUnitOfMeasure);

            int quantity;

            if (!int.TryParse(txtQuantity.Text.Trim(), out quantity) || quantity < 0)
                return ShowValidation("Количество на складе должно быть целым неотрицательным числом.", txtQuantity);

            decimal discount;

            if (!TryParseDecimal(txtDiscount.Text, out discount) || discount < 0 || discount > 100)
                return ShowValidation("Скидка должна быть числом от 0 до 100.", txtDiscount);

            product = new ProductRecord
            {
                ProductId = _productId.GetValueOrDefault(),
                Article = article,
                Name = name,
                Category = category,
                Description = txtDescription.Text.Trim(),
                Brand = brand,
                Supplier = supplier,
                Price = price,
                UnitOfMeasure = unitOfMeasure,
                Quantity = quantity,
                Discount = discount
            };

            return true;
        }

        private static bool TryParseDecimal(string value, out decimal result)
        {
            string trimmedValue = value.Trim();

            return decimal.TryParse(trimmedValue, NumberStyles.Number, CultureInfo.CurrentCulture, out result) ||
                   decimal.TryParse(trimmedValue, NumberStyles.Number, CultureInfo.GetCultureInfo("ru-RU"), out result) ||
                   decimal.TryParse(trimmedValue, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
        }

        private static string SaveSelectedImage(string sourcePath)
        {
            BitmapDecoder decoder = BitmapDecoder.Create(
                new Uri(sourcePath, UriKind.Absolute),
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);

            BitmapSource source = decoder.Frames[0];
            double scale = Math.Min(
                1,
                Math.Min(300.0 / source.PixelWidth, 200.0 / source.PixelHeight));

            BitmapSource resizedImage = source;

            if (scale < 1)
                resizedImage = new TransformedBitmap(source, new ScaleTransform(scale, scale));

            string imagesDirectory = GetImagesDirectory();
            Directory.CreateDirectory(imagesDirectory);

            string fileName = "Product_" + Guid.NewGuid().ToString("N") + ".png";
            string destinationPath = Path.Combine(imagesDirectory, fileName);

            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(resizedImage));

            using (FileStream stream = File.Create(destinationPath))
            {
                encoder.Save(stream);
            }

            return fileName;
        }

        private static BitmapImage GetImage(string photoPath)
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

            string imagesPath = Path.Combine(GetImagesDirectory(), photoPath);

            if (File.Exists(imagesPath))
                return imagesPath;

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, photoPath);
        }

        private static string GetImagesDirectory()
        {
            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images"));
        }

        private static void TryDeleteOldPhoto(string photoPath, int productId)
        {
            if (string.IsNullOrWhiteSpace(photoPath))
                return;

            try
            {
                if (!DatabaseHelper.IsPhotoUsedByAnotherProduct(photoPath, productId))
                    DeletePhotoFromImagesDirectory(photoPath);
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    "Товар сохранён, но старое изображение удалить не удалось. " +
                    "При необходимости удалите файл вручную из папки Images.\n\n" +
                    "Подробности: " + exception.Message,
                    "Предупреждение",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private static void TryDeleteGeneratedPhoto(string photoPath)
        {
            try
            {
                DeletePhotoFromImagesDirectory(photoPath);
            }
            catch
            {
                // The database write already failed; a cleanup error must not hide the useful message.
            }
        }

        private static void DeletePhotoFromImagesDirectory(string photoPath)
        {
            string fullPath = ResolvePhotoPath(photoPath);

            if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
                return;

            string imagesDirectory = GetImagesDirectory();
            fullPath = Path.GetFullPath(fullPath);

            if (fullPath.StartsWith(imagesDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                File.Delete(fullPath);
        }

        private static void FillComboBox(ComboBox comboBox, System.Collections.Generic.IEnumerable<string> values)
        {
            comboBox.Items.Clear();

            foreach (string value in values)
                comboBox.Items.Add(value);
        }

        private static bool ShowValidation(string message, Control control)
        {
            MessageBox.Show(
                message + "\n\nИсправьте значение в выделенном поле и повторите сохранение.",
                "Проверьте данные",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            control.Focus();
            return false;
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (_allowClose)
                return;

            MessageBoxResult answer = MessageBox.Show(
                "Вернуться к списку товаров?\n\nНесохранённые изменения будут потеряны.",
                "Предупреждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (answer != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            _allowClose = true;
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
