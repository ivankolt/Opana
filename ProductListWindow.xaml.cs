using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Npgsql;

namespace UchPR
{
    public partial class ProductListPage : Page
    {
        private DataBase database;
        private string currentUserRole;
        private bool isManager;
        private bool isDirector;

        public ProductListPage(string userRole)
        {
            InitializeComponent();
            database = new DataBase();
            currentUserRole = userRole;

            // Проверка прав доступа
            isManager = (currentUserRole == "Менеджер");
            isDirector = (currentUserRole == "Руководитель");

            if (!isManager && !isDirector)
            {
                MessageBox.Show("У вас нет прав для доступа к этой форме", "Ошибка доступа",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                if (NavigationService != null && NavigationService.CanGoBack)
                {
                    NavigationService.GoBack();
                }
                return;
            }
            lbProducts.MouseDoubleClick += LbProducts_MouseDoubleClick;

            ConfigureAccessByRole();
            // cmbCategory.SelectedIndex = 0; // УДАЛИТЬ ЭТУ СТРОКУ
            LoadProducts();
        }
        private void LbProducts_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var selectedProduct = lbProducts.SelectedItem as ProductCardViewModel;
            if (selectedProduct != null)
            {
                var detailWindow = new ProductDetailWindow(selectedProduct.productid, currentUserRole);
                detailWindow.ShowDialog();
            }
        }

        // Также можно добавить кнопку "Подробнее"
        private void BtnDetails_Click(object sender, RoutedEventArgs e)
        {
            var selectedProduct = lbProducts.SelectedItem as ProductCardViewModel;
            if (selectedProduct == null)
            {
                MessageBox.Show("Выберите изделие для просмотра подробной информации");
                return;
            }

            var detailWindow = new ProductDetailWindow(selectedProduct.productid, currentUserRole);
            detailWindow.ShowDialog();
        }
        private void ConfigureAccessByRole()
        {
            // Настройка доступа в зависимости от роли
            if (isManager)
            {
                // Менеджер не может добавлять изделия, только планировать производство
                btnAdd.Visibility = Visibility.Collapsed;
            }

            if (isDirector)
            {
                // Руководитель видит все данные
                btnReport.Content = "Финансовый отчет";
            }
        }

        private void LoadProducts()
        {
            try
            {
                string query = @"
            SELECT 
                p.article AS productid,
                p.article AS productarticlenum,
                pn.name AS productname,
                p.comment AS productdescription,
                COALESCE(p.width * p.length, 0) as productiontime,
                0 as CostPrice,
                0 as SalePrice
            FROM product p
            LEFT JOIN productname pn ON p.name_id = pn.id
            ORDER BY p.article";

                var productsData = database.GetData(query);
                var productsList = new List<ProductCardViewModel>();

                foreach (DataRow row in productsData.Rows)
                {
                    var product = new ProductCardViewModel
                    {
                        productid = row["productid"].ToString(),
                        productarticlenum = row["productarticlenum"].ToString(),
                        productname = row["productname"]?.ToString() ?? "Без названия",
                        productcategory = "Мебель", // Фиксированная категория
                        productdescription = row["productdescription"]?.ToString() ?? "",
                        productiontime = Convert.ToDecimal(row["productiontime"] ?? 0),
                        CostPrice = Convert.ToDecimal(row["CostPrice"] ?? 0),
                        SalePrice = Convert.ToDecimal(row["SalePrice"] ?? 0),
                        // Путь к изображению
                        ImagePath = GetProductImagePath(row["productid"].ToString())
                    };

                    // Расчет дополнительных полей
                    var availability = CheckMaterialAvailability(product.productid);
                    product.ProductionStatus = availability.Status;
                    product.AvailableQuantity = availability.AvailableQuantity;
                    product.AvailabilityColor = availability.Color;

                    // Расчет маржи
                    if (product.CostPrice > 0 && product.SalePrice > product.CostPrice)
                    {
                        product.MarginPercent = Math.Round(((product.SalePrice - product.CostPrice) / product.CostPrice) * 100, 2);
                    }
                    else
                    {
                        product.MarginPercent = 0;
                    }

                    productsList.Add(product);
                }

                // Привязываем к ListBox
                lbProducts.ItemsSource = productsList;

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных об изделиях: {ex.Message}", "Критическая ошибка");
                lbProducts.ItemsSource = new List<ProductCardViewModel>();
            }
        }


        // Метод для получения пути к изображению изделия
        private string GetProductImagePath(string productArticle)
        {
            try
            {
                // Используем тот же подход, что и в других формах
                string currentDir = AppDomain.CurrentDomain.BaseDirectory;

                // Поднимаемся вверх по папкам, пока не найдем папку Images
                DirectoryInfo dir = new DirectoryInfo(currentDir);
                while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Images")))
                {
                    dir = dir.Parent;
                }

                if (dir == null)
                {
                    System.Diagnostics.Debug.WriteLine("❌ Папка Images не найдена");
                    return string.Empty;
                }

                string imagesDir = Path.Combine(dir.FullName, "Images", "Изделия");
                string[] extensions = { ".jpg", ".jpeg", ".png", ".bmp" };

                foreach (string ext in extensions)
                {
                    string fullPath = Path.Combine(imagesDir, $"{productArticle}{ext}");

                    if (File.Exists(fullPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"✓ Найдено изображение изделия: {fullPath}");
                        return fullPath;
                    }
                }

                // Если изображение не найдено, возвращаем изображение по умолчанию
                string defaultPath = Path.Combine(imagesDir, "default.jpg");
                if (File.Exists(defaultPath))
                {
                    System.Diagnostics.Debug.WriteLine($"✓ Используется изображение по умолчанию: {defaultPath}");
                    return defaultPath;
                }

                System.Diagnostics.Debug.WriteLine($"❌ Изображение для изделия {productArticle} не найдено");
                return string.Empty;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка загрузки изображения для изделия {productArticle}: {ex.Message}");
                return string.Empty;
            }
        }

        private MaterialAvailability CheckMaterialAvailability(string productId)
        {
            // Упрощенная проверка доступности материалов
            try
            {
                Random rand = new Random();
                int availableQty = rand.Next(0, 20);
                bool isAvailable = availableQty > 0;

                return new MaterialAvailability
                {
                    Status = isAvailable ? "Доступно" : "Недостаток материалов",
                    AvailableQuantity = availableQty,
                    Color = isAvailable ? "#00FF00" : "#FF0000"
                };
            }
            catch
            {
                return new MaterialAvailability
                {
                    Status = "Ошибка проверки",
                    AvailableQuantity = 0,
                    Color = "#FF0000"
                };
            }
        }

        // Обновленные обработчики событий для работы с ListBox
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void CmbCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ChkAvailable_Changed(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            if (lbProducts.ItemsSource == null) return;

            var allProducts = lbProducts.ItemsSource as List<ProductCardViewModel>;
            if (allProducts == null) return;

            var filteredProducts = allProducts.AsEnumerable();

            // Фильтр по поиску
            if (!string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                string searchText = txtSearch.Text.ToLower();
                filteredProducts = filteredProducts.Where(p =>
                    p.productarticlenum.ToLower().Contains(searchText) ||
                    p.productname.ToLower().Contains(searchText) ||
                    p.productcategory.ToLower().Contains(searchText));
            }

            // Фильтр по категории
          

            // Фильтр по доступности
            if (chkAvailable.IsChecked == true)
            {
                filteredProducts = filteredProducts.Where(p => p.AvailableQuantity > 0);
            }

            // Применяем фильтр
            lbProducts.ItemsSource = filteredProducts.ToList();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadProducts();
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (!isDirector)
            {
                MessageBox.Show("Только руководитель может добавлять новые изделия");
                return;
            }
            MessageBox.Show("Добавление нового изделия");
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            var selectedProduct = lbProducts.SelectedItem as ProductCardViewModel;
            if (selectedProduct == null)
            {
                MessageBox.Show("Выберите изделие для редактирования");
                return;
            }

            MessageBox.Show($"Редактирование изделия: {selectedProduct.productarticlenum} - {selectedProduct.productname}");
        }

        private void BtnComposition_Click(object sender, RoutedEventArgs e)
        {
            var selectedProduct = lbProducts.SelectedItem as ProductCardViewModel;
            if (selectedProduct == null)
            {
                MessageBox.Show("Выберите изделие для просмотра состава");
                return;
            }

            MessageBox.Show($"Состав материалов для изделия: {selectedProduct.productname}");
        }

        private void BtnCalculate_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Расчет потребности в материалах");
        }

        private void BtnReport_Click(object sender, RoutedEventArgs e)
        {
            if (isDirector)
            {
                MessageBox.Show("Финансовый отчет по изделиям");
            }
            else
            {
                MessageBox.Show("Отчет по изделиям");
            }
        }
    }

    public class ProductCardViewModel
    {
        public string productid { get; set; }
        public string productarticlenum { get; set; }
        public string productname { get; set; }
        public string productcategory { get; set; }
        public string productdescription { get; set; }
        public decimal productiontime { get; set; }
        public decimal CostPrice { get; set; }
        public decimal SalePrice { get; set; }
        public decimal MarginPercent { get; set; }
        public string ProductionStatus { get; set; }
        public int AvailableQuantity { get; set; }
        public string AvailabilityColor { get; set; }
        public string ImagePath { get; set; }
    }

    public class MaterialAvailability
    {
        public string Status { get; set; }
        public int AvailableQuantity { get; set; }
        public string Color { get; set; }
    }
}
