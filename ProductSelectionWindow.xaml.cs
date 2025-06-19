using Npgsql;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.IO; // For Path, DirectoryInfo
using System.Windows.Media.Imaging; // For BitmapImage
using System.Text.RegularExpressions; // For numeric input validation

namespace UchPR
{
    // Модель для отображения товара в окне выбора
    public class ProductDisplayItem1 : INotifyPropertyChanged
    {
        public string Article { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; } // Средняя себестоимость со склада
        public int AvailableQuantity { get; set; }
        
        // Путь к изображению (URI или абсолютный путь к файлу)
        public string ImagePath { get; set; } 

        private int _selectedQuantity = 1;
        public int SelectedQuantity
        {
            get => _selectedQuantity;
            set
            {
                if (value < 1) _selectedQuantity = 1;
                else if (value > AvailableQuantity) _selectedQuantity = AvailableQuantity;
                else _selectedQuantity = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class ProductSelectionWindow : Window
    {
        private DataBase database;
        private ObservableCollection<ProductDisplayItem1> availableProducts;
        
        // Эта коллекция будет содержать выбранные товары и будет возвращена в OrderWindow
        public ObservableCollection<OrderItem> SelectedOrderItems { get; private set; }

        public ProductSelectionWindow()
        {
            InitializeComponent();
            database = new DataBase();
            availableProducts = new ObservableCollection<ProductDisplayItem1>();
            lbProducts.ItemsSource = availableProducts;
            SelectedOrderItems = new ObservableCollection<OrderItem>(); // Инициализируем внутренний список

            LoadProducts();
        }

        private void LoadProducts()
        {
            try
            {
                // Запрос для получения товаров с их средней себестоимостью со склада и количеством
                string query = @"
                    SELECT 
                        p.article,
                        pn.name,
                        COALESCE(AVG(pw.production_cost), 0) AS price,
                        COALESCE(SUM(pw.quantity), 0) AS available_quantity,
                        p.image -- Имя файла изображения из таблицы product
                    FROM product p
                    JOIN productname pn ON p.name_id = pn.id
                    JOIN productwarehouse pw ON p.article = pw.product_article
                    GROUP BY p.article, pn.name, p.image
                    HAVING COALESCE(SUM(pw.quantity), 0) > 0
                    ORDER BY pn.name";

                var data = database.GetData(query);
                availableProducts.Clear();

                foreach (DataRow row in data.Rows)
                {
                    string article = row["article"].ToString();
                    string imageName = row["image"]?.ToString(); // Получаем имя файла изображения из БД

                    // Получаем полный путь к изображению
                    string imagePath = GetProductImagePath(article, imageName); 

                    availableProducts.Add(new ProductDisplayItem1
                    {
                        Article = article,
                        Name = row["name"].ToString(), // Имя продукта
                        Price = SafeDataReader.GetSafeDecimal(row, "price"),
                        AvailableQuantity = SafeDataReader.GetSafeInt32(row, "available_quantity"),
                        ImagePath = imagePath
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки товаров: {ex.Message}");
            }
        }

        // Вспомогательный метод для получения пути к изображению
        private string GetProductImagePath(string productArticle, string imageNameFromDb)
        {
            string[] extensions = { ".jpg", ".jpeg", ".png", ".bmp" };

            // Попробуем загрузить как ресурс (изображения в проекте Build Action = Resource)
            foreach (string ext in extensions)
            {
                // Попробуем с именем из article
                string resourcePath = $"pack://application:,,,/Images/Products/{productArticle}{ext}";
                try
                {
                    if (Application.GetResourceStream(new Uri(resourcePath)) != null)
                    {
                        return resourcePath;
                    }
                }
                catch { /* Игнорируем, пробуем дальше */ }

                // Если имя изображения из БД отличается, пробуем его
                if (!string.IsNullOrEmpty(imageNameFromDb) && imageNameFromDb != productArticle)
                {
                    resourcePath = $"pack://application:,,,/Images/Products/{imageNameFromDb}{ext}";
                    try
                    {
                        if (Application.GetResourceStream(new Uri(resourcePath)) != null)
                        {
                            return resourcePath;
                        }
                    }
                    catch { /* Игнорируем */ }
                }
            }

            // Если не найдено как ресурс, попробуем из файловой системы
            // Ищем папку Images/Products относительно корня проекта
            string currentDir = AppDomain.CurrentDomain.BaseDirectory;
            DirectoryInfo dir = new DirectoryInfo(currentDir);
            
            // Поднимаемся по папкам, пока не найдем папку Images
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Images")))
            {
                dir = dir.Parent;
            }

            if (dir != null)
            {
                string imagesFolder = Path.Combine(dir.FullName, "Images", "Products"); // Папка с изображениями
                if (Directory.Exists(imagesFolder))
                {
                    foreach (string ext in extensions)
                    {
                        string filePath = Path.Combine(imagesFolder, $"{productArticle}{ext}");
                        if (File.Exists(filePath)) return new Uri(filePath).AbsoluteUri;

                        if (!string.IsNullOrEmpty(imageNameFromDb) && imageNameFromDb != productArticle)
                        {
                            filePath = Path.Combine(imagesFolder, $"{imageNameFromDb}{ext}");
                            if (File.Exists(filePath)) return new Uri(filePath).AbsoluteUri;
                        }
                    }
                }
            }

            // Возврат изображения по умолчанию, если ничего не найдено
            return "pack://application:,,,/Images/Products/default.jpg"; // Убедитесь, что у вас есть default.jpg
        }

        private void BtnAddItemToSelection_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ProductDisplayItem1 product)
            {
                if (product.SelectedQuantity <= 0)
                {
                    MessageBox.Show("Введите количество больше нуля.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (product.SelectedQuantity > product.AvailableQuantity)
                {
                    MessageBox.Show($"Доступно только {product.AvailableQuantity} шт. для '{product.Name}'.", "Недостаточно на складе", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Проверяем, есть ли уже такой товар во временном списке SelectedOrderItems
                var existingItem = SelectedOrderItems.FirstOrDefault(oi => oi.ProductArticle == product.Article);
                if (existingItem != null)
                {
                    int totalDesired = existingItem.Quantity + product.SelectedQuantity;
                    if (totalDesired > product.AvailableQuantity)
                    {
                        MessageBox.Show($"Нельзя добавить больше. Общее количество для '{product.Name}' ({totalDesired} шт.) будет превышать доступное ({product.AvailableQuantity} шт.).", "Превышение лимита", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    existingItem.Quantity += product.SelectedQuantity;
                }
                else
                {
                    // Добавляем новый OrderItem во временный список
                    SelectedOrderItems.Add(new OrderItem // OrderItem - это модель, используемая в OrderWindow
                    {
                        ProductArticle = product.Article,
                        ProductName = product.Name,
                        Quantity = product.SelectedQuantity,
                        UnitPrice = product.Price, 
                        AvailableQuantity = product.AvailableQuantity 
                    });
                }
                MessageBox.Show($"'{product.Name}' ({product.SelectedQuantity} шт.) добавлено в список для заказа.", "Добавлено", MessageBoxButton.OK, MessageBoxImage.Information);
                product.SelectedQuantity = 1; // Сбросить количество для следующего добавления
            }
        }

        private void BtnConfirmSelection_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedOrderItems.Any())
            {
                DialogResult = true; // Указываем, что выбор сделан успешно
                this.Close();
            }
            else
            {
                MessageBox.Show("Пожалуйста, добавьте изделия в список заказа.", "Пустой выбор", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnCancelSelection_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false; // Указываем, что выбор отменен
            this.Close();
        }

        // Валидация ввода только чисел в TextBox
        private void TextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }
}
