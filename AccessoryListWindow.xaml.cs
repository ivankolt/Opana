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
    public partial class AccessoryListWindow : Window
    {
        private DataBase database;
        private string currentUserRole;
        private List<UnitOfMeasurement> units;
        private UnitOfMeasurement selectedUnit;
        private List<AccessoryCardViewModel> allAccessoriesList = new List<AccessoryCardViewModel>();

        public AccessoryListWindow(string userRole)
        {
            InitializeComponent();
            database = new DataBase();
            currentUserRole = userRole;

            // Проверка прав доступа
            if (currentUserRole != "Кладовщик")
            {
                MessageBox.Show("У вас нет прав для доступа к этой форме", "Ошибка доступа",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                this.Close();
                return;
            }

            LoadUnits();
            cmbType.SelectedIndex = 0;
            LoadAccessories();
        }

        private void LoadUnits()
        {
            try
            {
                var query = "SELECT code, name, conversionfactor FROM unitofmeasurement ORDER BY name";
                var unitsData = database.GetData(query);

                units = new List<UnitOfMeasurement>();
                cmbUnit.Items.Add("Все единицы");

                foreach (DataRow row in unitsData.Rows)
                {
                    var unit = new UnitOfMeasurement
                    {
                        Code = Convert.ToInt32(row["code"]),
                        Name = row["name"].ToString(),
                        ConversionFactor = Convert.ToDecimal(row["conversionfactor"])
                    };
                    units.Add(unit);
                    cmbUnit.Items.Add(unit.Name);
                }

                cmbUnit.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки единиц измерения: {ex.Message}");
            }
        }

        private void LoadAccessories()
        {
            try
            {
                string query = @"
                    SELECT 
         a.article,
         fan.name AS accessory_name,
         ft.name AS accessory_type,
         a.width,
         a.length,
         a.weight,
         a.price,
         uom.name AS unit_name,
		 a.image
     FROM 
         public.accessory a
     LEFT JOIN 
         public.furnitureaccessoryname fan ON a.name_id = fan.id
     LEFT JOIN 
         public.furnituretype ft ON a.type_id = ft.id
     LEFT JOIN 
         public.unitofmeasurement uom ON a.unit_of_measurement_id = uom.code
     ORDER BY 
         a.article";

                var accessoriesData = database.GetData(query);
                var accessoriesList = new List<AccessoryCardViewModel>();

                foreach (DataRow row in accessoriesData.Rows)
                {
                    var accessory = new AccessoryCardViewModel
                    {
                        article = row["article"].ToString(),
                        accessory_name = row["accessory_name"]?.ToString() ?? "Без названия",
                        accessory_type = row["accessory_type"]?.ToString() ?? "Не указан",
                        width = Convert.ToDecimal(row["width"] ?? 0),
                        length = row["length"] != DBNull.Value ? Convert.ToDecimal(row["length"]) : 0,
                        weight = row["weight"] != DBNull.Value ? Convert.ToDecimal(row["weight"]) : 0,
                        price = Convert.ToDecimal(row["price"] ?? 0),
                        unit_name = row["unit_name"]?.ToString() ?? "шт",
                        // Убираем пробелы в начале и конце имени файла
                        ImagePath = $"pack://application:,,,/Images/Accessories/{(row["image"]?.ToString().Trim() ?? "default.jpg")}"
                    };

                    accessoriesList.Add(accessory);
                }


                // ИСПРАВЛЕНО: Привязываем к ListBox вместо DataGrid
                allAccessoriesList = accessoriesList;
                lbAccessories.ItemsSource = allAccessoriesList;

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных о фурнитуре: {ex.Message}", "Критическая ошибка");
                lbAccessories.ItemsSource = new List<AccessoryCardViewModel>();
            }
        }

        // Метод для получения пути к изображению фурнитуры
        private string GetAccessoryImagePath(string accessoryArticle)
        {
            string accessoryArticleTrim = accessoryArticle.TrimStart();
            string[] extensions = { ".jpg", ".jpeg", ".png", ".bmp" };

            foreach (string ext in extensions)
            {
                string resourcePath = $"pack://application:,,,/Images/Accessories/{accessoryArticleTrim}{ext}";
                try
                {
                    var uri = new Uri(resourcePath, UriKind.Absolute);
                    var resourceInfo = Application.GetResourceStream(uri);
                    if (resourceInfo != null)
                    {
                        resourceInfo.Stream.Close();
                        return resourcePath;
                    }
                }
                catch
                {
                    continue;
                }
            }

            // Если ничего не найдено, обновляем поле image на "default.jpg" в accessory
            try
            {
                using (var conn = new Npgsql.NpgsqlConnection(database.connectionString))
                {
                    conn.Open();
                    string sql = "UPDATE accessory SET image = @img WHERE article = @article";
                    using (var cmd = new Npgsql.NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@img", "default.jpg");
                        cmd.Parameters.AddWithValue("@article", accessoryArticleTrim);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка обновления image для {accessoryArticleTrim}: {ex.Message}");
            }

            // Возвращаем изображение по умолчанию
            return "pack://application:,,,/Images/Accessories/default.jpg";
        }



        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void CmbUnit_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbUnit.SelectedIndex > 0)
            {
                selectedUnit = units[cmbUnit.SelectedIndex - 1];
            }
            else
            {
                selectedUnit = null;
            }

            ApplyFilters();
        }

        private void CmbType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        // ИСПРАВЛЕНО: Фильтрация для ListBox вместо DataTable

        private void ApplyFilters()
        {
            if (allAccessoriesList == null) return;

            var filteredAccessories = allAccessoriesList.AsEnumerable();

            // Фильтр по поиску
            if (!string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                string searchText = txtSearch.Text.ToLower();
                filteredAccessories = filteredAccessories.Where(a =>
                    a.article.ToLower().Contains(searchText) ||
                    a.accessory_name.ToLower().Contains(searchText) ||
                    a.accessory_type.ToLower().Contains(searchText));
            }

            // Фильтр по типу
            if (cmbType.SelectedIndex > 0)
            {
                string selectedType = ((ComboBoxItem)cmbType.SelectedItem).Content.ToString();
                if (selectedType != "Все типы")
                {
                    filteredAccessories = filteredAccessories.Where(a => a.accessory_type == selectedType);
                }
            }

            // Фильтр по единице измерения (если используется)
            if (cmbUnit.SelectedIndex > 0)
            {
                string selectedUnit = cmbUnit.SelectedItem.ToString();
                filteredAccessories = filteredAccessories.Where(a => a.unit_name == selectedUnit);
            }

            lbAccessories.ItemsSource = filteredAccessories.ToList();
        }


        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadAccessories();
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Добавление новой фурнитуры");
            // var addWindow = new AccessoryEditWindow();
            // if (addWindow.ShowDialog() == true)
            // {
            //     LoadAccessories();
            // }
        }

        // ИСПРАВЛЕНО: Работа с выбранным элементом из ListBox
        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            var selectedAccessory = lbAccessories.SelectedItem as AccessoryCardViewModel;
            if (selectedAccessory == null)
            {
                MessageBox.Show("Выберите фурнитуру для редактирования");
                return;
            }

            string accessoryArticle = selectedAccessory.article;
            MessageBox.Show($"Редактирование фурнитуры: {accessoryArticle} - {selectedAccessory.accessory_name}");

            // var editWindow = new AccessoryEditWindow(accessoryArticle);
            // if (editWindow.ShowDialog() == true)
            // {
            //     LoadAccessories();
            // }
        }

        // ИСПРАВЛЕНО: Работа с выбранным элементом из ListBox
        private void BtnReceive_Click(object sender, RoutedEventArgs e)
        {
            var selectedAccessory = lbAccessories.SelectedItem as AccessoryCardViewModel;
            if (selectedAccessory == null)
            {
                MessageBox.Show("Выберите фурнитуру для оформления поступления");
                return;
            }

            string accessoryArticle = selectedAccessory.article;
         //   var receiptWindow = new MaterialReceiptWindow("accessory", accessoryArticle);
          //  if (receiptWindow.ShowDialog() == true)
            {
                LoadAccessories();
            }
        }

        private void BtnScrap_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Списание фурнитуры");
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Настройки фурнитуры");
        }
    }

    public class AccessoryCardViewModel
    {
        public string article { get; set; }
        public string accessory_name { get; set; }
        public string accessory_type { get; set; }
        public decimal width { get; set; }
        public decimal length { get; set; }
        public decimal weight { get; set; }
        public decimal price { get; set; }
        public string unit_name { get; set; }
        public string ImagePath { get; set; }
    }
}
