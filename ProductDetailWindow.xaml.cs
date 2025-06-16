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
                string currentDir = AppDomain.CurrentDomain.BaseDirectory;
                DirectoryInfo dir = new DirectoryInfo(currentDir);
                while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Images")))
                {
                    dir = dir.Parent;
                }

                if (dir != null)
                {
                    string imagesDir = Path.Combine(dir.FullName, "Images", "Изделия");
                    string[] extensions = { ".jpg", ".jpeg", ".png", ".bmp" };

                    foreach (string ext in extensions)
                    {
                        string imagePath = Path.Combine(imagesDir, $"{productArticle}{ext}");

                        if (File.Exists(imagePath))
                        {
                            BitmapImage bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                            bitmap.EndInit();
                            imgProduct.Source = bitmap;

                            // Загружаем дополнительные изображения (если есть)
                            LoadAdditionalImages(imagesDir, productArticle);
                            return;
                        }
                    }
                }

                // Если изображение не найдено, показываем заглушку
                imgProduct.Source = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки изображения: {ex.Message}");
            }
        }

        private void LoadAdditionalImages(string imagesDir, string productArticle)
        {
            try
            {
                wpThumbnails.Children.Clear();

                // Ищем дополнительные изображения с суффиксами _1, _2, _3
                for (int i = 1; i <= 5; i++)
                {
                    string[] extensions = { ".jpg", ".jpeg", ".png", ".bmp" };

                    foreach (string ext in extensions)
                    {
                        string imagePath = Path.Combine(imagesDir, $"{productArticle}_{i}{ext}");

                        if (File.Exists(imagePath))
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
                            bitmap.EndInit();
                            thumbnail.Source = bitmap;

                            thumbnailBorder.Child = thumbnail;

                            // Добавляем обработчик клика для смены основного изображения
                            string currentImagePath = imagePath;
                            thumbnailBorder.MouseLeftButtonDown += (s, e) => {
                                BitmapImage mainBitmap = new BitmapImage();
                                mainBitmap.BeginInit();
                                mainBitmap.UriSource = new Uri(currentImagePath, UriKind.Absolute);
                                mainBitmap.EndInit();
                                imgProduct.Source = mainBitmap;
                            };

                            wpThumbnails.Children.Add(thumbnailBorder);
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
                // Пример запроса для получения состава тканей
                // Нужно адаптировать под вашу структуру БД
                string query = @"
                    SELECT 
                        f.article AS Article,
                        fn.name AS Name,
                        fp.quantity AS Quantity,
                        uom.name AS Unit
                    FROM fabricproducts fp
                    JOIN fabric f ON fp.fabric_id = f.article
                    JOIN fabricname fn ON f.name_id = fn.code
                    JOIN unitofmeasurement uom ON f.unit_of_measurement_id = uom.code
                    WHERE fp.product_id = @productId";

                var parameters = new NpgsqlParameter[] {
                    new NpgsqlParameter("@productId", productId)
                };

                var fabricData = database.GetData(query, parameters);

                var fabricList = new List<MaterialComposition>();
                foreach (DataRow row in fabricData.Rows)
                {
                    fabricList.Add(new MaterialComposition
                    {
                        Article = row["Article"].ToString(),
                        Name = row["Name"].ToString(),
                        Quantity = Convert.ToDecimal(row["Quantity"]),
                        Unit = row["Unit"].ToString()
                    });
                }

                dgFabrics.ItemsSource = fabricList;
            }
            catch (Exception ex)
            {
                // Если таблицы связи не существуют, показываем заглушку
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
                // Аналогично для фурнитуры
                var accessoryList = new List<MaterialComposition>
                {
                    new MaterialComposition { Article = "Н/Д", Name = "Данные о составе фурнитуры недоступны", Quantity = 0, Unit = "-" }
                };

                dgAccessories.ItemsSource = accessoryList;
            }
            catch (Exception ex)
            {
                dgAccessories.ItemsSource = new List<MaterialComposition>();
            }
        }

        private void LoadFinancialInfo()
        {
            try
            {
                // Здесь можно добавить расчет себестоимости и цены
                txtCostPrice.Text = "0,00 руб.";
                txtSalePrice.Text = "0,00 руб.";
                txtMargin.Text = "0,00%";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки финансовой информации: {ex.Message}");
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
