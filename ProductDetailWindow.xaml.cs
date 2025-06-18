using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Npgsql;

namespace UchPR
{
    public partial class ProductDetailWindow : Window
    {
        private DataBase database;
        private string productId;
        private string currentUserRole;
        private ProductCardViewModel productInfo;

        public ProductDetailWindow(string productId, string userRole)
        {
            InitializeComponent();
            database = new DataBase();
            this.productId = productId;
            this.currentUserRole = userRole;

            ConfigureAccessByRole();
            LoadProductDetails();
        }

        private void ConfigureAccessByRole()
        {
            // Настройка видимости элементов в зависимости от роли
            if (currentUserRole == "Менеджер")
            {
                // Менеджер не видит финансовую информацию
                borderFinancial.Visibility = Visibility.Collapsed;
                btnEdit.Visibility = Visibility.Collapsed;
            }
            else if (currentUserRole == "Руководитель")
            {
                // Руководитель видит все
                borderFinancial.Visibility = Visibility.Visible;
                btnEdit.Visibility = Visibility.Visible;
            }
        }

        private void LoadProductDetails()
        {
            try
            {
                // Загружаем основную информацию об изделии
                string query = @"
                    SELECT 
                        p.article,
                        pn.name AS productname,
                        p.width,
                        p.length,
                        p.comment AS description,
                        uom.name AS unit_name,
                        p.image
                    FROM product p
                    LEFT JOIN productname pn ON p.name_id = pn.id
                    LEFT JOIN unitofmeasurement uom ON p.unit_of_measurement_id = uom.code
                    WHERE p.article = @productId";

                var parameters = new NpgsqlParameter[] {
                    new NpgsqlParameter("@productId", productId)
                };

                var data = database.GetData(query, parameters);

                if (data.Rows.Count > 0)
                {
                    var row = data.Rows[0];

                    // Заполняем основную информацию
                    txtProductTitle.Text = row["productname"].ToString();
                    txtProductArticle.Text = $"Артикул: {row["article"]}";
                    txtProductName.Text = row["productname"].ToString();
                    txtArticle.Text = row["article"].ToString();
                    txtDimensions.Text = $"{row["width"]} × {row["length"]} см";
                    txtUnit.Text = row["unit_name"].ToString();
                    txtDescription.Text = row["description"]?.ToString() ?? "Описание не указано";

                    // Загружаем изображение
                    LoadProductImage(row["article"].ToString());

                    // Загружаем состав материалов
                    LoadProductComposition();

                    // Загружаем финансовую информацию (если доступно)
                    if (currentUserRole == "Руководитель")
                    {
                        LoadFinancialInfo();
                    }
                }
                else
                {
                    MessageBox.Show("Изделие не найдено", "Ошибка");
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки информации об изделии: {ex.Message}", "Ошибка");
            }
        }

        private void LoadProductImage(string productArticle)
        {
            try
            {
                string[] extensions = { ".jpg", ".jpeg", ".png", ".bmp" };

                // Сначала пробуем загрузить как ресурс (pack URI)
                foreach (string ext in extensions)
                {
                    string resourcePath = $"pack://application:,,,/Images/Products/{productArticle}{ext}";

                    try
                    {
                        var uri = new Uri(resourcePath);
                        var resourceInfo = Application.GetResourceStream(uri);
                        if (resourceInfo != null)
                        {
                            BitmapImage bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.StreamSource = resourceInfo.Stream;
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            bitmap.Freeze();

                            imgProduct.Source = bitmap;
                            resourceInfo.Stream.Close();

                            // Загружаем дополнительные изображения
                            LoadAdditionalImages(productArticle);
                            return;
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                // Если ресурс не найден, пробуем загрузить из файловой системы
                LoadImageFromFileSystem(productArticle);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки изображения: {ex.Message}");
                // Устанавливаем изображение по умолчанию
                SetDefaultImage();
            }
        }

        private void LoadImageFromFileSystem(string productArticle)
        {
            try
            {
                string currentDir = AppDomain.CurrentDomain.BaseDirectory;
                DirectoryInfo dir = new DirectoryInfo(currentDir);

                while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Images")))
                {
                    dir = dir.Parent;
                }

                if (dir != null)
                {
                    // Проверяем обе папки: Products и Изделия
                    string[] imageDirs = {
                Path.Combine(dir.FullName, "Images", "Products"),
                Path.Combine(dir.FullName, "Images", "Изделия")
            };

                    string[] extensions = { ".jpg", ".jpeg", ".png", ".bmp" };

                    foreach (string imagesDir in imageDirs)
                    {
                        if (!Directory.Exists(imagesDir)) continue;

                        foreach (string ext in extensions)
                        {
                            string imagePath = Path.Combine(imagesDir, $"{productArticle}{ext}");

                            if (File.Exists(imagePath))
                            {
                                BitmapImage bitmap = new BitmapImage();
                                bitmap.BeginInit();
                                bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                bitmap.EndInit();
                                bitmap.Freeze();

                                imgProduct.Source = bitmap;

                                // Загружаем дополнительные изображения
                                LoadAdditionalImages(imagesDir, productArticle);
                                return;
                            }
                        }
                    }
                }

                // Если изображение не найдено
                SetDefaultImage();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки из файловой системы: {ex.Message}");
                SetDefaultImage();
            }
        }

        private void SetDefaultImage()
        {
            try
            {
                // Пытаемся загрузить изображение по умолчанию
                string defaultImagePath = "pack://application:,,,/Images/Products/default.jpg";
                var uri = new Uri(defaultImagePath);
                var resourceInfo = Application.GetResourceStream(uri);

                if (resourceInfo != null)
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = resourceInfo.Stream;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    imgProduct.Source = bitmap;
                    resourceInfo.Stream.Close();
                }
                else
                {
                    imgProduct.Source = null;
                }
            }
            catch
            {
                imgProduct.Source = null;
            }
        }

        private void LoadAdditionalImages(string productArticle)
        {
            try
            {
                wpThumbnails.Children.Clear();
                string[] extensions = { ".jpg", ".jpeg", ".png", ".bmp" };

                // Ищем дополнительные изображения как ресурсы
                for (int i = 1; i <= 5; i++)
                {
                    foreach (string ext in extensions)
                    {
                        string resourcePath = $"pack://application:,,,/Images/Products/{productArticle}_{i}{ext}";

                        try
                        {
                            var uri = new Uri(resourcePath);
                            var resourceInfo = Application.GetResourceStream(uri);
                            if (resourceInfo != null)
                            {
                                CreateThumbnail(resourceInfo.Stream, resourcePath);
                                resourceInfo.Stream.Close();
                                break;
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки дополнительных изображений: {ex.Message}");
            }
        }

        private void LoadAdditionalImages(string imagesDir, string productArticle)
        {
            try
            {
                wpThumbnails.Children.Clear();
                string[] extensions = { ".jpg", ".jpeg", ".png", ".bmp" };

                for (int i = 1; i <= 5; i++)
                {
                    foreach (string ext in extensions)
                    {
                        string imagePath = Path.Combine(imagesDir, $"{productArticle}_{i}{ext}");

                        if (File.Exists(imagePath))
                        {
                            CreateThumbnailFromFile(imagePath);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки дополнительных изображений: {ex.Message}");
            }
        }

        private void CreateThumbnail(Stream imageStream, string imagePath)
        {
            Border thumbnailBorder = new Border
            {
                Width = 60,
                Height = 60,
                Margin = new Thickness(5),
                BorderBrush = System.Windows.Media.Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new System.Windows.CornerRadius(5)
            };

            Image thumbnail = new Image
            {
                Stretch = System.Windows.Media.Stretch.UniformToFill
            };

            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = imageStream;
            bitmap.DecodePixelWidth = 60;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            thumbnail.Source = bitmap;

            thumbnailBorder.Child = thumbnail;

            // Обработчик клика
            thumbnailBorder.MouseLeftButtonDown += (s, e) => {
                var uri = new Uri(imagePath);
                var resourceInfo = Application.GetResourceStream(uri);
                if (resourceInfo != null)
                {
                    BitmapImage mainBitmap = new BitmapImage();
                    mainBitmap.BeginInit();
                    mainBitmap.StreamSource = resourceInfo.Stream;
                    mainBitmap.CacheOption = BitmapCacheOption.OnLoad;
                    mainBitmap.EndInit();
                    mainBitmap.Freeze();
                    imgProduct.Source = mainBitmap;
                    resourceInfo.Stream.Close();
                }
            };

            wpThumbnails.Children.Add(thumbnailBorder);
        }

        private void CreateThumbnailFromFile(string imagePath)
        {
            Border thumbnailBorder = new Border
            {
                Width = 60,
                Height = 60,
                Margin = new Thickness(5),
                BorderBrush = System.Windows.Media.Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new System.Windows.CornerRadius(5)
            };

            Image thumbnail = new Image
            {
                Stretch = System.Windows.Media.Stretch.UniformToFill
            };

            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
            bitmap.DecodePixelWidth = 60;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            thumbnail.Source = bitmap;

            thumbnailBorder.Child = thumbnail;

            // Обработчик клика
            string currentImagePath = imagePath;
            thumbnailBorder.MouseLeftButtonDown += (s, e) => {
                BitmapImage mainBitmap = new BitmapImage();
                mainBitmap.BeginInit();
                mainBitmap.UriSource = new Uri(currentImagePath, UriKind.Absolute);
                mainBitmap.CacheOption = BitmapCacheOption.OnLoad;
                mainBitmap.EndInit();
                mainBitmap.Freeze();
                imgProduct.Source = mainBitmap;
            };

            wpThumbnails.Children.Add(thumbnailBorder);
        }


        private void LoadProductComposition()
        {
            try
            {
                // Загружаем состав тканей
                LoadFabricComposition();

                // Загружаем состав фурнитуры
                LoadAccessoryComposition();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки состава материалов: {ex.Message}");
            }
        }

        private void LoadFabricComposition()
        {
            try
            {
                string query = @"
            SELECT 
                CAST(f.article AS varchar) AS Article,
                fn.name AS Name,
                1 AS Quantity,  -- Фиксированное значение, так как quantity нет в fabricproducts
                uom.name AS Unit
            FROM fabricproducts fp
            JOIN fabric f ON fp.fabric_article = f.article
            JOIN fabricname fn ON f.name_id = fn.code
            JOIN unitofmeasurement uom ON f.unit_of_measurement_id = uom.code
            WHERE fp.product_article = @productId";

                var parameters = new NpgsqlParameter[] {
            new NpgsqlParameter("@productId", productId)
        };

                var fabricData = database.GetData(query, parameters);

                var fabricList = new List<MaterialComposition>();
                foreach (DataRow row in fabricData.Rows)
                {
                    fabricList.Add(new MaterialComposition
                    {
                        Article = SafeDataReader.GetSafeString(row, "Article"),
                        Name = SafeDataReader.GetSafeString(row, "Name"),
                        Quantity = SafeDataReader.GetSafeDecimal(row, "Quantity"),
                        Unit = SafeDataReader.GetSafeString(row, "Unit")
                    });
                }

                dgFabrics.ItemsSource = fabricList;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки состава тканей: {ex.Message}");
                dgFabrics.ItemsSource = new List<MaterialComposition>
        {
            new MaterialComposition { Article = "Н/Д", Name = "Данные о составе тканей недоступны", Quantity = 0, Unit = "-" }
        };
            }
        }

        private void LoadAccessoryComposition()
        {
            try
            {
                string query = @"
            SELECT 
                a.article AS Article,
                fan.name AS Name,
                ap.quantity AS Quantity,
                uom.name AS Unit
            FROM accessoryproducts ap
            JOIN accessory a ON ap.accessory_article = a.article
            JOIN furnitureaccessoryname fan ON a.name_id = fan.id
            JOIN unitofmeasurement uom ON ap.unit_of_measurement_id = uom.code
            WHERE ap.product_article = @productId";

                var parameters = new NpgsqlParameter[] {
            new NpgsqlParameter("@productId", productId)
        };

                var accessoryData = database.GetData(query, parameters);

                var accessoryList = new List<MaterialComposition>();
                foreach (DataRow row in accessoryData.Rows)
                {
                    accessoryList.Add(new MaterialComposition
                    {
                        Article = row["Article"].ToString(),
                        Name = row["Name"].ToString(),
                        Quantity = Convert.ToDecimal(row["Quantity"]),
                        Unit = row["Unit"].ToString()
                    });
                }

                dgAccessories.ItemsSource = accessoryList;
            }
            catch (Exception ex)
            {
                dgAccessories.ItemsSource = new List<MaterialComposition>
        {
            new MaterialComposition { Article = "Н/Д", Name = "Данные о составе фурнитуры недоступны", Quantity = 0, Unit = "-" }
        };
            }
        }


        private decimal CalculateProductCost()
        {
            decimal totalCost = 0;

            try
            {
                // Расчет стоимости тканей по базовой цене из справочника
                totalCost += CalculateFabricCost();
                Console.WriteLine($"Стоимость тканей: {totalCost}");

                // Расчет стоимости фурнитуры по базовой цене из справочника
                totalCost += CalculateAccessoryCost();
                Console.WriteLine($"Общая стоимость материалов: {totalCost}");

                // Применяем надбавку 90% (умножаем на 1.9)
                totalCost *= 1.9m;
                Console.WriteLine($"Итоговая стоимость с надбавкой 90%: {totalCost}");

                return totalCost;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка расчета себестоимости: {ex.Message}");
                return 0;
            }
        }



        private decimal CalculateFabricCost()
        {
            decimal fabricCost = 0;

            try
            {
                // Получаем базовую цену ткани из справочника fabric, а не со склада
                string query = @"
            SELECT 
                f.price AS fabric_price,
                p.width,
                p.length
            FROM fabricproducts fp
            JOIN fabric f ON fp.fabric_article = f.article
            JOIN product p ON fp.product_article = p.article
            WHERE fp.product_article = @productId";

                var parameters = new NpgsqlParameter[] {
            new NpgsqlParameter("@productId", productId)
        };

                var data = database.GetData(query, parameters);

                foreach (DataRow row in data.Rows)
                {
                    decimal fabricPrice = SafeDataReader.GetSafeDecimal(row, "fabric_price");
                    decimal width = SafeDataReader.GetSafeDecimal(row, "width") / 1000; // переводим в метры
                    decimal length = SafeDataReader.GetSafeDecimal(row, "length") / 1000; // переводим в метры

                    // Площадь ткани в кв.м
                    decimal fabricArea = width * length;

                    // Стоимость = Цена за кв.м * Площадь
                    fabricCost += fabricPrice * fabricArea;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка расчета стоимости тканей: {ex.Message}");
            }

            return fabricCost;
        }


        private decimal CalculateAccessoryCost()
        {
            decimal accessoryCost = 0;

            try
            {
                // Получаем базовую цену фурнитуры из справочника accessory, а не со склада
                string query = @"
            SELECT 
                a.price AS accessory_price,
                ap.quantity AS used_quantity
            FROM accessoryproducts ap
            JOIN accessory a ON ap.accessory_article = a.article
            WHERE ap.product_article = @productId";

                var parameters = new NpgsqlParameter[] {
            new NpgsqlParameter("@productId", productId)
        };

                var data = database.GetData(query, parameters);

                foreach (DataRow row in data.Rows)
                {
                    decimal accessoryPrice = SafeDataReader.GetSafeDecimal(row, "accessory_price");
                    decimal usedQuantity = SafeDataReader.GetSafeDecimal(row, "used_quantity");

                    // Стоимость = Цена за единицу * Количество
                    accessoryCost += accessoryPrice * usedQuantity;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка расчета стоимости фурнитуры: {ex.Message}");
            }

            return accessoryCost;
        }


        private void LoadFinancialInfo()
        {
            try
            {
                // Расчет себестоимости по базовым ценам материалов (с учетом 90%)
                decimal costPrice = CalculateProductCost();

                // Получение цены продажи из таблицы product
                decimal salePrice = 0;

                // Расчет маржи в процентах
                decimal margin = 0;
                if (salePrice > 0)
                {
                    margin = ((salePrice - costPrice) / salePrice) * 100;
                }

                // Отображение результатов
                txtCostPrice.Text = $"{costPrice:N2} руб.";
                txtSalePrice.Text = $"{salePrice:N2} руб.";
                txtMargin.Text = $"{margin:N1}%";

                // Отладочная информация
                System.Diagnostics.Debug.WriteLine($"Себестоимость: {costPrice:N2}, Цена продажи: {salePrice:N2}, Маржа: {margin:N1}%");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки финансовой информации: {ex.Message}");
                txtCostPrice.Text = "0,00 руб.";
                txtSalePrice.Text = "0,00 руб.";
                txtMargin.Text = "0,0%";
            }
        }


        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Редактирование изделия");
            // Здесь можно открыть окно редактирования
        }

        private void BtnComposition_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Редактирование состава материалов");
            // Здесь можно открыть окно редактирования состава
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Печать информации об изделии");
            // Здесь можно добавить функцию печати
        }
    }

    public class MaterialComposition
    {
        public string Article { get; set; }
        public string Name { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
    }
}
