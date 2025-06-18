using Npgsql;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace UchPR
{
    public partial class CreateProduction : Window
    {
        private DataBase database;
        private ObservableCollection<ProductionFabricItem> fabricItems;
        private ObservableCollection<ProductionAccessoryItem> accessoryItems;
        private string selectedProductArticle;

        public CreateProduction()
        {
            InitializeComponent();
            database = new DataBase();

            fabricItems = new ObservableCollection<ProductionFabricItem>();
            accessoryItems = new ObservableCollection<ProductionAccessoryItem>();

            dgFabrics.ItemsSource = fabricItems;
            dgAccessories.ItemsSource = accessoryItems;

            dpProductionDate.SelectedDate = DateTime.Today;

            LoadProducts();
        }

        private void LoadProducts()
        {
            try
            {
                string query = @"
                    SELECT p.article, pn.name 
                    FROM product p
                    LEFT JOIN productname pn ON p.name_id = pn.id
                    ORDER BY pn.name";

                var data = database.GetData(query);
                var products = new List<ProductItem>();

                foreach (DataRow row in data.Rows)
                {
                    products.Add(new ProductItem
                    {
                        Article = row["article"].ToString(),
                        Name = $"Арт. {row["article"]} - {row["name"]}"
                    });
                }

                cbProduct.ItemsSource = products;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки изделий: {ex.Message}");
            }
        }

        private void CbProduct_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbProduct.SelectedItem is ProductItem product)
            {
                selectedProductArticle = product.Article;
                LoadProductComposition();
            }
        }

        private void LoadProductComposition()
        {
            if (string.IsNullOrEmpty(selectedProductArticle)) return;

            try
            {
                LoadFabricComposition();
                LoadAccessoryComposition();
                CalculateTotals();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки состава изделия: {ex.Message}");
            }
        }

        private void LoadFabricComposition()
        {
            fabricItems.Clear();

            string query = @"
                SELECT 
                    CAST(f.article AS varchar) AS fabric_article,
                    fn.name AS fabric_name,
                    p.width * p.length / 10000.0 AS planned_quantity,
                    f.price AS fabric_price
                FROM fabricproducts fp
                JOIN fabric f ON fp.fabric_article = f.article
                JOIN fabricname fn ON f.name_id = fn.code
                JOIN product p ON fp.product_article = p.article
                WHERE fp.product_article = @productId";

            var parameters = new NpgsqlParameter[] {
                new NpgsqlParameter("@productId", selectedProductArticle)
            };

            var data = database.GetData(query, parameters);

            foreach (DataRow row in data.Rows)
            {
                var item = new ProductionFabricItem
                {
                    FabricArticle = row["fabric_article"].ToString(),
                    FabricName = row["fabric_name"].ToString(),
                    PlannedQuantity = SafeDataReader.GetSafeDecimal(row, "planned_quantity"),
                    ActualQuantity = SafeDataReader.GetSafeDecimal(row, "planned_quantity"),
                    AveragePrice = SafeDataReader.GetSafeDecimal(row, "fabric_price") // ИСПРАВЛЕНО: берем цену из справочника
                };

                fabricItems.Add(item);
            }
        }

        private void LoadAccessoryComposition()
        {
            accessoryItems.Clear();

            string query = @"
                SELECT 
                    a.article AS accessory_article,
                    fan.name AS accessory_name,
                    ap.quantity AS planned_quantity,
                    a.price AS accessory_price
                FROM accessoryproducts ap
                JOIN accessory a ON ap.accessory_article = a.article
                JOIN furnitureaccessoryname fan ON a.name_id = fan.id
                WHERE ap.product_article = @productId";

            var parameters = new NpgsqlParameter[] {
                new NpgsqlParameter("@productId", selectedProductArticle)
            };

            var data = database.GetData(query, parameters);

            foreach (DataRow row in data.Rows)
            {
                var item = new ProductionAccessoryItem
                {
                    AccessoryArticle = row["accessory_article"].ToString(),
                    AccessoryName = row["accessory_name"].ToString(),
                    PlannedQuantity = SafeDataReader.GetSafeInt32(row, "planned_quantity"),
                    ActualQuantity = SafeDataReader.GetSafeInt32(row, "planned_quantity"),
                    AveragePrice = SafeDataReader.GetSafeDecimal(row, "accessory_price") // ИСПРАВЛЕНО: берем цену из справочника
                };

                accessoryItems.Add(item);
            }
        }

        private void TxtQuantity_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (fabricItems == null || accessoryItems == null)
                return;

            if (int.TryParse(txtQuantity.Text, out int quantity) && quantity > 0)
            {
                foreach (var item in fabricItems)
                {
                    item.RecalculateForQuantity(quantity);
                }

                foreach (var item in accessoryItems)
                {
                    item.RecalculateForQuantity(quantity);
                }

                CalculateTotals();
            }
        }

        private void CalculateTotals()
        {
            if (fabricItems == null || accessoryItems == null)
                return;

            decimal totalCost = 0;
            decimal totalScraps = 0;
            bool hasOverLimit = false;

            foreach (var item in fabricItems)
            {
                totalCost += item.TotalCost;
                totalScraps += Math.Max(0, item.ActualQuantity - item.PlannedQuantity);

                if (item.IsOverLimit)
                    hasOverLimit = true;
            }

            foreach (var item in accessoryItems)
            {
                totalCost += item.TotalCost;

                if (item.IsOverLimit)
                    hasOverLimit = true;
            }

            if (txtTotalCost != null)
                txtTotalCost.Text = $"{totalCost:N2} руб.";
            if (txtScraps != null)
                txtScraps.Text = $"{totalScraps:N2} кв.м";
            if (txtOverLimitWarning != null)
                txtOverLimitWarning.Visibility = hasOverLimit ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnSaveDraft_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveProduction("Черновик");
                MessageBox.Show("Черновик сохранен");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}");
            }
        }

        private void BtnComplete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ValidateProduction())
                {
                    ProcessProductionDirect();
                    MessageBox.Show("Производство завершено успешно!");
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка завершения производства: {ex.Message}");
            }
        }

        private bool ValidateProduction()
        {
            if (string.IsNullOrEmpty(selectedProductArticle))
            {
                MessageBox.Show("Выберите изделие", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!int.TryParse(txtQuantity.Text, out int quantity) || quantity <= 0)
            {
                MessageBox.Show("Введите корректное количество", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (dpProductionDate.SelectedDate == null)
            {
                MessageBox.Show("Выберите дату производства", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // НОВАЯ ПРОВЕРКА: Наличие материалов на складе
            if (!ValidateMaterialAvailability(out List<string> missingMaterials))
            {
                string message = "❌ НЕДОСТАТОЧНО МАТЕРИАЛОВ НА СКЛАДЕ:\n\n" +
                                string.Join("\n", missingMaterials) +
                                "\n\nПроизводство невозможно!";

                MessageBox.Show(message, "Недостаток материалов",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        // УПРОЩЕННАЯ ЛОГИКА: Сразу работаем со складами без промежуточных таблиц
        private void ProcessProductionDirect()
        {
            using (var connection = new NpgsqlConnection(database.connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Списание материалов со складов
                        WriteOffMaterialsFromWarehouse(connection, transaction);

                        // 2. Оприходование готовых изделий
                        AddFinishedProductsToWarehouse(connection, transaction);

                        // 3. Учет обрезков по scrap_threshold
                        ProcessScrapsWithThreshold(connection, transaction);

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private void WriteOffMaterialsFromWarehouse(NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            // Списание тканей
            foreach (var fabric in fabricItems)
            {
                WriteOffFabricFromWarehouse(connection, transaction, int.Parse(fabric.FabricArticle), fabric.ActualQuantity);
            }

            // Списание фурнитуры
            foreach (var accessory in accessoryItems)
            {
                WriteOffAccessoryFromWarehouse(connection, transaction, accessory.AccessoryArticle, accessory.ActualQuantity);
            }
        }

        private void WriteOffFabricFromWarehouse(NpgsqlConnection connection, NpgsqlTransaction transaction, int fabricArticle, decimal usedArea)
        {
            decimal scrapThreshold = GetScrapThreshold(fabricArticle, connection, transaction);

            string selectQuery = @"
                SELECT roll, width, length, purchase_price, total_cost
                FROM fabricwarehouse
                WHERE fabric_article = @fabric_article
                ORDER BY roll";

            var warehouseRecords = new List<dynamic>();
            using (var cmd = new NpgsqlCommand(selectQuery, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@fabric_article", fabricArticle);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        warehouseRecords.Add(new
                        {
                            Roll = SafeDataReader.GetSafeInt32(reader, "roll"),
                            Width = SafeDataReader.GetSafeDecimal(reader, "width"),
                            Length = SafeDataReader.GetSafeDecimal(reader, "length"),
                            PurchasePrice = SafeDataReader.GetSafeDecimal(reader, "purchase_price"),
                            TotalCost = SafeDataReader.GetSafeDecimal(reader, "total_cost")
                        });
                    }
                }
            }

            decimal areaToWriteOff = usedArea;
            foreach (var record in warehouseRecords)
            {
                if (areaToWriteOff <= 0) break;

                decimal recordArea = record.Width * record.Length;
                decimal writeOffArea = Math.Min(areaToWriteOff, recordArea);
                decimal newArea = recordArea - writeOffArea;

                if (newArea < scrapThreshold && newArea > 0)
                {
                    writeOffArea += newArea;
                    newArea = 0;
                }

                if (newArea == 0)
                {
                    string deleteQuery = @"DELETE FROM fabricwarehouse WHERE roll = @roll";
                    using (var cmd = new NpgsqlCommand(deleteQuery, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@roll", record.Roll);
                        cmd.ExecuteNonQuery();
                    }
                }
                else
                {
                    string updateQuery = @"
                        UPDATE fabricwarehouse
                        SET length = @new_length,
                            total_cost = @new_cost
                        WHERE roll = @roll";
                    using (var cmd = new NpgsqlCommand(updateQuery, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@new_length", newArea / record.Width);
                        cmd.Parameters.AddWithValue("@new_cost", newArea * record.PurchasePrice);
                        cmd.Parameters.AddWithValue("@roll", record.Roll);
                        cmd.ExecuteNonQuery();
                    }
                }

                areaToWriteOff -= writeOffArea;
            }
        }
        private void SaveProduction(string status)
        {
            using (var connection = new NpgsqlConnection(database.connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        string docNumber = GenerateProductionNumber();
                        int quantity = int.Parse(txtQuantity.Text);
                        decimal totalCost = 0;

                        // Рассчитываем общую стоимость
                        if (fabricItems != null)
                            totalCost += fabricItems.Sum(f => f.TotalCost);
                        if (accessoryItems != null)
                            totalCost += accessoryItems.Sum(a => a.TotalCost);

                        // Сохраняем документ производства
                        string insertDocQuery = @"
                    INSERT INTO production_documents 
                    (docnumber, productid, quantity, productiondate, status, totalcost, responsible)
                    VALUES (@doc_number, @product_id, @quantity, @production_date, @status, @total_cost, @responsible)
                    RETURNING documentid";

                        int documentId;
                        using (var cmd = new NpgsqlCommand(insertDocQuery, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@doc_number", docNumber);
                            cmd.Parameters.AddWithValue("@product_id", selectedProductArticle);
                            cmd.Parameters.AddWithValue("@quantity", quantity);
                            cmd.Parameters.AddWithValue("@production_date", dpProductionDate.SelectedDate.Value);
                            cmd.Parameters.AddWithValue("@status", status);
                            cmd.Parameters.AddWithValue("@total_cost", totalCost);
                            cmd.Parameters.AddWithValue("@responsible", Environment.UserName);
                            documentId = (int)cmd.ExecuteScalar();
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }
        private void WriteOffAccessoryFromWarehouse(NpgsqlConnection connection, NpgsqlTransaction transaction, string accessoryArticle, int quantity)
        {
            string selectQuery = @"
                SELECT batch_number, quantity, purchase_price, total_cost
                FROM accessorywarehouse 
                WHERE accessory_article = @accessory_article
                ORDER BY batch_number";

            var warehouseRecords = new List<dynamic>();
            using (var cmd = new NpgsqlCommand(selectQuery, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@accessory_article", accessoryArticle);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        warehouseRecords.Add(new
                        {
                            BatchNumber = SafeDataReader.GetSafeInt32(reader, "batch_number"),
                            Quantity = SafeDataReader.GetSafeInt32(reader, "quantity"),
                            PurchasePrice = SafeDataReader.GetSafeDecimal(reader, "purchase_price"),
                            TotalCost = SafeDataReader.GetSafeDecimal(reader, "total_cost")
                        });
                    }
                }
            }

            int quantityToWrite = quantity;
            foreach (var record in warehouseRecords)
            {
                if (quantityToWrite <= 0) break;

                int writeOffQuantity = Math.Min(quantityToWrite, record.Quantity);
                int newQuantity = record.Quantity - writeOffQuantity;

                if (newQuantity <= 0)
                {
                    string deleteQuery = @"
                        DELETE FROM accessorywarehouse 
                        WHERE batch_number = @batch_number";

                    using (var cmd = new NpgsqlCommand(deleteQuery, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@batch_number", record.BatchNumber);
                        cmd.ExecuteNonQuery();
                    }
                }
                else
                {
                    string updateQuery = @"
                        UPDATE accessorywarehouse 
                        SET quantity = @new_quantity, total_cost = @new_cost
                        WHERE batch_number = @batch_number";

                    using (var cmd = new NpgsqlCommand(updateQuery, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@new_quantity", newQuantity);
                        cmd.Parameters.AddWithValue("@new_cost", newQuantity * record.PurchasePrice);
                        cmd.Parameters.AddWithValue("@batch_number", record.BatchNumber);
                        cmd.ExecuteNonQuery();
                    }
                }

                quantityToWrite -= writeOffQuantity;
            }
        }
        private bool CheckFabricAvailability(NpgsqlConnection connection, NpgsqlTransaction transaction,
 int fabricArticle, decimal requiredArea, out decimal availableArea)
        {
            availableArea = 0;

            string query = @"
        SELECT SUM(width * length) as total_area
        FROM fabricwarehouse 
        WHERE fabric_article = @fabric_article";

            using (var cmd = new NpgsqlCommand(query, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@fabric_article", fabricArticle);
                var result = cmd.ExecuteScalar();

                if (result != null && result != DBNull.Value)
                {
                    availableArea = Convert.ToDecimal(result);
                    return availableArea >= requiredArea;
                }

                return false;
            }
        }
        private bool CheckAccessoryAvailability(NpgsqlConnection connection, NpgsqlTransaction transaction,
            string accessoryArticle, int requiredQuantity, out int availableQuantity)
        {
            availableQuantity = 0;

            string query = @"
        SELECT SUM(quantity) as total_quantity
        FROM accessorywarehouse 
        WHERE accessory_article = @accessory_article";

            using (var cmd = new NpgsqlCommand(query, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@accessory_article", accessoryArticle);
                var result = cmd.ExecuteScalar();

                if (result != null && result != DBNull.Value)
                {
                    availableQuantity = Convert.ToInt32(result);
                    return availableQuantity >= requiredQuantity;
                }

                return false;
            }
        }
        private bool ValidateMaterialAvailability(out List<string> missingMaterials)
        {
            missingMaterials = new List<string>();

            using (var connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=12345;Database=UchPR"))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Проверка тканей
                        foreach (var fabric in fabricItems)
                        {
                            if (!CheckFabricAvailability(connection, transaction,
                                int.Parse(fabric.FabricArticle), fabric.ActualQuantity, out decimal available))
                            {
                                missingMaterials.Add($"Ткань {fabric.FabricName} (арт. {fabric.FabricArticle}): " +
                                    $"требуется {fabric.ActualQuantity:F2} кв.м, доступно {available:F2} кв.м");
                            }
                        }

                        // Проверка фурнитуры
                        foreach (var accessory in accessoryItems)
                        {
                            if (!CheckAccessoryAvailability(connection, transaction,
                                accessory.AccessoryArticle, accessory.ActualQuantity, out int available))
                            {
                                missingMaterials.Add($"Фурнитура {accessory.AccessoryName} (арт. {accessory.AccessoryArticle}): " +
                                    $"требуется {accessory.ActualQuantity} шт., доступно {available} шт.");
                            }
                        }

                        return missingMaterials.Count == 0;
                    }
                    catch (Exception ex)
                    {
                        missingMaterials.Add($"Ошибка проверки остатков: {ex.Message}");
                        return false;
                    }
                }
            }
        }


        private void AddFinishedProductsToWarehouse(NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            int quantity = int.Parse(txtQuantity.Text);
            decimal totalCost = fabricItems.Sum(f => f.TotalCost) + accessoryItems.Sum(a => a.TotalCost);
            decimal unitCost = totalCost / quantity;

            string insertQuery = @"
                INSERT INTO productwarehouse (product_article, quantity, production_cost, total_cost)
                VALUES (@product_article, @quantity, @production_cost, @total_cost)";

            using (var cmd = new NpgsqlCommand(insertQuery, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@product_article", selectedProductArticle);
                cmd.Parameters.AddWithValue("@quantity", quantity);
                cmd.Parameters.AddWithValue("@production_cost", unitCost);
                cmd.Parameters.AddWithValue("@total_cost", totalCost);
                cmd.ExecuteNonQuery();
            }
        }

        private void ProcessScrapsWithThreshold(NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            // Обрезки уже учтены в WriteOffFabricFromWarehouse через scrap_threshold
            // Дополнительная логика обрезков не требуется
        }

        private decimal GetScrapThreshold(int fabricArticle, NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            string sql = "SELECT COALESCE(scrap_threshold, 0) FROM fabric WHERE article = @article";
            using (var cmd = new NpgsqlCommand(sql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@article", fabricArticle);
                var result = cmd.ExecuteScalar();
                return result != null ? Convert.ToDecimal(result) : 0m;
            }
        }

        private string GenerateProductionNumber()
        {
            return $"ПР-{DateTime.Now:yyyyMMdd}-{DateTime.Now:HHmmss}";
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    #region Модели данных


    public class ProductionFabricItem : INotifyPropertyChanged
    {
        private decimal _actualQuantity;
        private decimal _basePlannedQuantity;

        public string FabricArticle { get; set; }
        public string FabricName { get; set; }
        public decimal PlannedQuantity { get; set; }
        public decimal AveragePrice { get; set; }

        public decimal ActualQuantity
        {
            get => _actualQuantity;
            set
            {
                _actualQuantity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DeviationPercent));
                OnPropertyChanged(nameof(IsOverLimit));
                OnPropertyChanged(nameof(TotalCost));
            }
        }

        public decimal DeviationPercent
        {
            get
            {
                if (PlannedQuantity == 0) return 0;
                return ((ActualQuantity - PlannedQuantity) / PlannedQuantity) * 100;
            }
        }

        public bool IsOverLimit => DeviationPercent > 15;

        public decimal TotalCost => ActualQuantity * AveragePrice;

        public void RecalculateForQuantity(int quantity)
        {
            if (_basePlannedQuantity == 0)
                _basePlannedQuantity = PlannedQuantity;

            PlannedQuantity = _basePlannedQuantity * quantity;
            ActualQuantity = PlannedQuantity;
            OnPropertyChanged(nameof(PlannedQuantity));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ProductionAccessoryItem : INotifyPropertyChanged
    {
        private int _actualQuantity;
        private int _basePlannedQuantity;

        public string AccessoryArticle { get; set; }
        public string AccessoryName { get; set; }
        public int PlannedQuantity { get; set; }
        public decimal AveragePrice { get; set; }

        public int ActualQuantity
        {
            get => _actualQuantity;
            set
            {
                _actualQuantity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DeviationPercent));
                OnPropertyChanged(nameof(IsOverLimit));
                OnPropertyChanged(nameof(TotalCost));
            }
        }

        public decimal DeviationPercent
        {
            get
            {
                if (PlannedQuantity == 0) return 0;
                return ((ActualQuantity - PlannedQuantity) / (decimal)PlannedQuantity) * 100;
            }
        }

     

        public bool IsOverLimit => DeviationPercent > 15;

        public decimal TotalCost => ActualQuantity * AveragePrice;

        public void RecalculateForQuantity(int quantity)
        {
            if (_basePlannedQuantity == 0)
                _basePlannedQuantity = PlannedQuantity;

            PlannedQuantity = _basePlannedQuantity * quantity;
            ActualQuantity = PlannedQuantity;
            OnPropertyChanged(nameof(PlannedQuantity));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    #endregion
}
