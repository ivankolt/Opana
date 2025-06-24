using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Npgsql;

namespace UchPR
{
    public partial class FabricListWindow : Window
    {
        private DataBase database;
        private DataTable fabricsTable;
        private string currentUserRole;
        private List<UnitOfMeasurement> units;
        private UnitOfMeasurement selectedUnit;
        private List<FabricCardViewModel> allFabricsList = new List<FabricCardViewModel>();
        public FabricListWindow(string userRole)
        {
            InitializeComponent();
            database = new DataBase();
            currentUserRole = userRole;

            if (currentUserRole != "Кладовщик")
            {
                MessageBox.Show("У вас нет прав для доступа к этой форме", "Ошибка доступа",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                this.Close();
                return;
            }

            LoadUnits();
            LoadCompositions();
            _ = LoadFabricsAsync(); // запуск без ожидания завершения
        }

        private void LoadUnits()
        {
            try
            {
                // ИСПРАВЛЕНО: Используем правильные имена столбцов: 'code', 'name', 'conversionfactor'
                var query = "SELECT code, name, conversionfactor FROM public.unitofmeasurement ORDER BY name";
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


        private void LoadCompositions()
        {
            try
            {
                // ИСПРАВЛЕНО: Используем правильное имя столбца 'name' из таблицы-справочника
                var query = "SELECT id, name FROM public.composition ORDER BY name";
                var compositionsData = database.GetData(query);

                cmbComposition.Items.Add("Все составы");

                foreach (DataRow row in compositionsData.Rows)
                {
                    cmbComposition.Items.Add(row["name"].ToString());
                }

                cmbComposition.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки составов: {ex.Message}");
            }
        }


        private async Task LoadFabricsAsync()
        {
            try
            {
                string query = @"
        SELECT 
            f.article AS ""fabric_article"",
            fn.name AS ""fabric_name"",
            c.name AS ""ColorName"",
            p.name AS ""PatternName"",
            comp.name AS ""CompositionName"",
            COALESCE(fw.quantity, 0) AS ""StockQuantity"",
            COALESCE(fw.total_cost, 0) AS ""TotalCost"",
            f.price AS ""Price"",
            uom.name AS ""AccountingUnitName"",
            COALESCE(f.scrap_threshold, 0) AS ""ScrapLimit"",
            0 AS ""MinStock""
        FROM 
            public.fabric f
        LEFT JOIN 
            public.fabricname fn ON f.name_id = fn.code
        LEFT JOIN 
            public.colors c ON f.color_id = c.id
        LEFT JOIN 
            public.pattern p ON f.pattern_id = p.id
        LEFT JOIN 
            public.composition comp ON f.composition_id = comp.id
        LEFT JOIN 
            public.unitofmeasurement uom ON f.unit_of_measurement_id = uom.code
        LEFT JOIN 
            (SELECT fabric_article, SUM(COALESCE(total_cost, 0)) as total_cost, 
                    SUM(COALESCE(length, 0) * COALESCE(width, 0)) as quantity
             FROM public.fabricwarehouse 
             WHERE fabric_article IS NOT NULL
             GROUP BY fabric_article) fw ON f.article = fw.fabric_article
        ORDER BY 
            f.article";

                // Асинхронно получаем таблицу данных
                var fabricsTable = await Task.Run(() => database.GetData(query));

                if (fabricsTable == null)
                {
                    fabricsTable = new DataTable();
                    lbFabrics.ItemsSource = new List<FabricCardViewModel>();
                    return;
                }

                // Отладочная информация
                System.Diagnostics.Debug.WriteLine("Столбцы в fabricsTable:");
                foreach (DataColumn column in fabricsTable.Columns)
                {
                    System.Diagnostics.Debug.WriteLine($"- {column.ColumnName}");
                }

                // Создаем список карточек для отображения
                var fabricsList = new List<FabricCardViewModel>();

                foreach (DataRow row in fabricsTable.Rows)
                {
                    var fabric = new FabricCardViewModel
                    {
                        fabric_article = Convert.ToInt32(row["fabric_article"]),
                        fabric_name = row["fabric_name"]?.ToString() ?? "Без названия",
                        ColorName = row["ColorName"]?.ToString() ?? "Не указан",
                        PatternName = row["PatternName"]?.ToString() ?? "Не указан",
                        CompositionName = row["CompositionName"]?.ToString() ?? "Не указан",
                        StockQuantity = Convert.ToDecimal(row["StockQuantity"] ?? 0),
                        AccountingUnitName = row["AccountingUnitName"]?.ToString() ?? "шт",
                        TotalCost = Convert.ToDecimal(row["TotalCost"] ?? 0),
                        Price = Convert.ToDecimal(row["Price"] ?? 0),
                        ScrapLimit = Convert.ToDecimal(row["ScrapLimit"] ?? 0),
                        MinStock = Convert.ToDecimal(row["MinStock"] ?? 0),
                        // Путь к изображению
                        ImagePath = GetFabricImagePath(Convert.ToInt32(row["fabric_article"]))
                    };

                    fabricsList.Add(fabric);
                }

                // Привязываем к ItemsControl вместо DataGrid
                allFabricsList = fabricsList;
                lbFabrics.ItemsSource = allFabricsList;


                // Сохраняем исходную таблицу для фильтрации
                this.fabricsTable = fabricsTable;
                AddCalculatedColumns();
                if (fabricsTable.Rows.Count > 0)
                {
                    ProcessFabricData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных о тканях: {ex.Message}", "Критическая ошибка");
                lbFabrics.ItemsSource = new List<FabricCardViewModel>();
            }
        }


        // Метод для получения пути к изображению ткани
        private string GetFabricImagePath(int fabricArticle)
        {
            try
            {
                string[] extensions = { ".jpg", ".jpeg", ".png", ".bmp" };

                foreach (string ext in extensions)
                {
                    string resourcePath = $"pack://application:,,,/Images/Fabric/{fabricArticle}{ext}";

                    // Проверяем существование ресурса
                    try
                    {
                        var uri = new Uri(resourcePath);
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

                // Возвращаем изображение по умолчанию
                return "pack://application:,,,/Images/Fabric/default.jpg";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка: {ex.Message}");
                return string.Empty;
            }
        }


        private void AddCalculatedColumns()
        {
            if (fabricsTable == null)
            {
                fabricsTable = new DataTable();
            }

            // Добавляем столбцы только если их нет
            if (!fabricsTable.Columns.Contains("ConvertedQuantity"))
                fabricsTable.Columns.Add("ConvertedQuantity", typeof(decimal));

            if (!fabricsTable.Columns.Contains("DisplayUnit"))  // Изменено с SelectedUnitName
                fabricsTable.Columns.Add("DisplayUnit", typeof(string));

            if (!fabricsTable.Columns.Contains("StatusColor"))
                fabricsTable.Columns.Add("StatusColor", typeof(string));

            // Инициализируем значения по умолчанию
            foreach (DataRow row in fabricsTable.Rows)
            {
                if (row["ConvertedQuantity"] == DBNull.Value)
                    row["ConvertedQuantity"] = row["StockQuantity"];
                if (row["DisplayUnit"] == DBNull.Value)
                    row["DisplayUnit"] = row["AccountingUnitName"];
                if (row["StatusColor"] == DBNull.Value)
                    row["StatusColor"] = "#00FF00";
            }
        }

        private void ProcessFabricData()
        {
            if (fabricsTable == null || fabricsTable.Rows == null)
            {
                return;
            }

            foreach (DataRow row in fabricsTable.Rows)
            {
                try
                {
                    // ИСПРАВЛЕНО: используем правильные имена столбцов
                    decimal stockQuantity = Convert.ToDecimal(row["StockQuantity"] ?? 0);
                    decimal minStock = Convert.ToDecimal(row["MinStock"] ?? 0);
                    decimal scrapLimit = Convert.ToDecimal(row["ScrapLimit"] ?? 0);

                    // Конвертация количества в выбранную единицу
                    if (selectedUnit != null)
                    {
                        row["ConvertedQuantity"] = stockQuantity * selectedUnit.ConversionFactor;
                        row["DisplayUnit"] = selectedUnit.Name;
                    }
                    else
                    {
                        row["ConvertedQuantity"] = stockQuantity;
                        row["DisplayUnit"] = row["AccountingUnitName"]?.ToString() ?? "";
                    }

                    // Определение статуса остатка
                    if (stockQuantity <= scrapLimit)
                        row["StatusColor"] = "#FF0000"; // Красный - обрезки
                    else if (stockQuantity <= minStock)
                        row["StatusColor"] = "#FFA500"; // Оранжевый - критический остаток
                    else
                        row["StatusColor"] = "#00FF00"; // Зеленый - нормальный остаток
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка обработки строки: {ex.Message}");
                }
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void CmbUnit_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbUnit.SelectedIndex > 0 && units != null)
            {
                selectedUnit = units[cmbUnit.SelectedIndex - 1];
                ConvertStockQuantities(); // Новый метод для пересчета
                UpdateDataGridDisplay();
            }
        }
        private void UpdateDataGridDisplay()
        {
            if (fabricsTable == null) return;

            // Обновляем отображение DataGrid
            lbFabrics.Items.Refresh();

            // Альтернативно, если используете привязку к DefaultView:
            // fabricsTable.DefaultView.RowFilter = fabricsTable.DefaultView.RowFilter;
        }
        private void ConvertStockQuantities()
        {
            if (fabricsTable == null || selectedUnit == null) return;

            foreach (DataRow row in fabricsTable.Rows)
            {
                try
                {
                    decimal baseQuantity = Convert.ToDecimal(row["StockQuantity"] ?? 0);
                    int fabricArticle = Convert.ToInt32(row["fabric_article"]);

                    // Получаем коэффициент конвертации для конкретной ткани
                    decimal conversionFactor = GetConversionFactor(fabricArticle, selectedUnit.Code);

                    // Пересчитываем количество
                    row["ConvertedQuantity"] = baseQuantity * conversionFactor;
                    row["DisplayUnit"] = selectedUnit.Name; // Используем DisplayUnit вместо SelectedUnitName
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка конвертации для строки: {ex.Message}");
                }
            }

            // Обновляем отображение
            UpdateDataGridDisplay();
        }
        private decimal GetConversionFactor(int materialArticle, int targetUnitId)
        {
            string query = @"
        SELECT conversion_factor 
        FROM unitconversionrules 
        WHERE material_article = @article 
        AND to_unit_id = @unitId";

            var parameters = new NpgsqlParameter[]
            {
        new NpgsqlParameter("@article", materialArticle.ToString()),
        new NpgsqlParameter("@unitId", targetUnitId)
            };

            var result = database.GetScalarValue(query, parameters);
            return result != null ? Convert.ToDecimal(result) : 1.0m;
        }

        private void CmbComposition_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

       
        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadFabricsAsync();
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            // Открываем форму добавления новой ткани
          //  var addWindow = new FabricEditWindow();
          //  if (addWindow.ShowDialog() == true)
            {
                LoadFabricsAsync();
            }
        }
        private void ApplyFilters()
        {
            if (allFabricsList == null) return;

            var filteredFabrics = allFabricsList.AsEnumerable();

            // Фильтр по поиску
            if (!string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                string searchText = txtSearch.Text.ToLower();
                filteredFabrics = filteredFabrics.Where(f =>
                    f.fabric_article.ToString().Contains(searchText) ||
                    f.fabric_name.ToLower().Contains(searchText) ||
                    f.ColorName.ToLower().Contains(searchText) ||
                    f.CompositionName.ToLower().Contains(searchText));
            }

            // Фильтр по составу
            if (cmbComposition.SelectedIndex > 0)
            {
                string selectedComposition = cmbComposition.SelectedItem as string;
                if (selectedComposition != "Все составы")
                {
                    filteredFabrics = filteredFabrics.Where(f => f.CompositionName == selectedComposition);
                }
            }

            lbFabrics.ItemsSource = filteredFabrics.ToList();
        }

        private bool ColumnExists(string columnName)
        {
            return fabricsTable != null && fabricsTable.Columns.Contains(columnName);
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            var selectedFabric = GetSelectedFabric();
            if (selectedFabric == null)
            {
                MessageBox.Show("Выберите ткань для редактирования");
                return;
            }

            int fabricId = selectedFabric.fabric_article;
            // var editWindow = new FabricEditWindow(fabricId);
            // if (editWindow.ShowDialog() == true)
            {
                LoadFabricsAsync();
            }
        }
        private FabricCardViewModel GetSelectedFabric()
        {
            // Поскольку ItemsControl не имеет SelectedItem, нужно реализовать выбор через события
            // Временно возвращаем первый элемент для тестирования
            var fabrics = lbFabrics.ItemsSource as List<FabricCardViewModel>;
            return fabrics?.FirstOrDefault();
        }


        private void BtnReceive_Click(object sender, RoutedEventArgs e)
        {
            var selectedFabric = GetSelectedFabric();
          
            if (selectedFabric == null)
            {
                MessageBox.Show("Выберите ткань для оформления поступления");
                return;
            }

            var selectedRow = ((DataRowView)lbFabrics.SelectedItem).Row;
            int fabricId = Convert.ToInt32(selectedRow["fabricid"]);

            var receiptWindow = new MaterialReceiptWindow("fabric", fabricId);
            if (receiptWindow.ShowDialog() == true)
            {
                LoadFabricsAsync();
            }
        }

        private void BtnScrap_Click(object sender, RoutedEventArgs e)
        {
            ProcessScrapFabrics();
        }

        private void ProcessScrapFabrics()
        {
            try
            {
                var scrapItems = new List<string>();
                decimal totalScrapCost = 0;

                foreach (DataRow row in fabricsTable.Rows)
                {
                    decimal stockQuantity = Convert.ToDecimal(row["StockQuantity"]);
                    decimal scrapLimit = Convert.ToDecimal(row["ScrapLimit"]);

                    if (stockQuantity > 0 && stockQuantity <= scrapLimit)
                    {
                        decimal itemCost = Convert.ToDecimal(row["TotalCost"]);
                        scrapItems.Add($"{row["fabricname"]}: {stockQuantity} {row["AccountingUnitName"]} на сумму {itemCost:F2} руб.");
                        totalScrapCost += itemCost;

                        // Списываем обрезки
                        ScrapFabric(Convert.ToInt32(row["fabricid"]), stockQuantity, itemCost);
                    }
                }

                if (scrapItems.Count > 0)
                {
                    string message = $"Списано обрезков на общую сумму {totalScrapCost:F2} руб.:\n\n";
                    message += string.Join("\n", scrapItems);
                    MessageBox.Show(message, "Списание обрезков");
                    LoadFabricsAsync();
                }
                else
                {
                    MessageBox.Show("Нет тканей для списания в обрезки");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при списании обрезков: {ex.Message}");
            }
        }

        private void ScrapFabric(int fabricId, decimal quantity, decimal cost)
        {
            string query = @"
                INSERT INTO scraplog (materialtype, materialid, quantity, cost, scrapdate, reason)
                VALUES ('fabric', @fabricId, @quantity, @cost, @scrapDate, 'Автоматическое списание обрезков');
                
                UPDATE fabricwarehouse 
                SET quantity = 0, totalcost = 0 
                WHERE fabricid = @fabricId";

            var parameters = new NpgsqlParameter[]
            {
                new NpgsqlParameter("@fabricId", fabricId),
                new NpgsqlParameter("@quantity", quantity),
                new NpgsqlParameter("@cost", cost),
                new NpgsqlParameter("@scrapDate", DateTime.Now)
            };

            database.ExecuteQuery(query, parameters);
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
         //   var settingsWindow = new FabricSettingsWindow();
           // if (settingsWindow.ShowDialog() == true)
            {
                LoadFabricsAsync();
            }
        }
        public class FabricCardViewModel
        {
            public int fabric_article { get; set; }
            public string fabric_name { get; set; }
            public string ColorName { get; set; }
            public string PatternName { get; set; }
            public string CompositionName { get; set; }
            public decimal StockQuantity { get; set; }
            public string AccountingUnitName { get; set; }
            public decimal TotalCost { get; set; }
            public decimal Price { get; set; }
            public string ImagePath { get; set; }

            // Добавляем недостающие свойства
            public decimal ScrapLimit { get; set; }
            public decimal MinStock { get; set; }

            // Дополнительные свойства для конвертации единиц
            public decimal ConvertedQuantity { get; set; }
            public string DisplayUnit { get; set; }
            public string StatusColor { get; set; } = "#00FF00";
        }

    }

}
