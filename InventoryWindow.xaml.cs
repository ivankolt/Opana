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
    public partial class InventoryWindow : Window
    {
        private DataBase database;
        private ObservableCollection<InventoryFabricLine> fabricLines;
        private ObservableCollection<InventoryAccessoryLine> accessoryLines;
        private ObservableCollection<InventoryProductLine> productLines;
        private InventoryDocument currentDocument;
        private string currentUserRole;

        public InventoryWindow(string userRole)
        {
            InitializeComponent();
            database = new DataBase();
            currentUserRole = userRole;

            fabricLines = new ObservableCollection<InventoryFabricLine>();
            accessoryLines = new ObservableCollection<InventoryAccessoryLine>();
            productLines = new ObservableCollection<InventoryProductLine>();

            dgFabrics.ItemsSource = fabricLines;
            dgAccessories.ItemsSource = accessoryLines;
            dgProducts.ItemsSource = productLines;

            InitializeDocument();
            LoadMaterials();

            // Показать кнопку директора только для директора
            if (userRole == "Руководитель")
            {
                btnDirectorApprove.Visibility = Visibility.Visible;
            }
        }

        private void InitializeDocument()
        {
            currentDocument = new InventoryDocument
            {
                DocumentNumber = GenerateDocumentNumber(),
                InventoryDate = DateTime.Now,
                ResponsiblePerson = Environment.UserName,
                Status = "В процессе"
            };

            txtDocNumber.Text = currentDocument.DocumentNumber;
            txtResponsible.Text = currentDocument.ResponsiblePerson;
            dpInventoryDate.SelectedDate = currentDocument.InventoryDate;
        }

        private string GenerateDocumentNumber()
        {
            return $"ИНВ-{DateTime.Now:yyyyMMdd}-{DateTime.Now:HHmmss}";
        }

        private void LoadMaterials()
        {
            try
            {
                // Загрузка тканей
                string fabricQuery = @"
                SELECT f.article, fn.name 
                FROM fabric f
                LEFT JOIN fabricname fn ON f.name_id = fn.code
                ORDER BY fn.name";

                var fabricData = database.GetData(fabricQuery);
                var fabrics = new List<MaterialItem>();

                foreach (DataRow row in fabricData.Rows)
                {
                    fabrics.Add(new MaterialItem
                    {
                        Id = row["article"].ToString(),
                        Name = row["name"].ToString(),
                        Type = "fabric"
                    });
                }

                cbFabricSelect.ItemsSource = fabrics;

                // Загрузка фурнитуры
                string accessoryQuery = @"
                SELECT a.article, fan.name 
                FROM accessory a
                LEFT JOIN furnitureaccessoryname fan ON a.name_id = fan.id
                ORDER BY fan.name";

                var accessoryData = database.GetData(accessoryQuery);
                var accessories = new List<MaterialItem>();

                foreach (DataRow row in accessoryData.Rows)
                {
                    accessories.Add(new MaterialItem
                    {
                        Id = row["article"].ToString(),
                        Name = row["name"].ToString(),
                        Type = "accessory"
                    });
                }

                cbAccessorySelect.ItemsSource = accessories;

                // Загрузка изделий
                string productQuery = @"
                SELECT p.article, pn.name 
                FROM product p
                LEFT JOIN productname pn ON p.name_id = pn.id
                ORDER BY pn.name";

                var productData = database.GetData(productQuery);
                var products = new List<MaterialItem>();

                foreach (DataRow row in productData.Rows)
                {
                    products.Add(new MaterialItem
                    {
                        Id = row["article"].ToString(),
                        Name = row["name"].ToString(),
                        Type = "product"
                    });
                }

                cbProductSelect.ItemsSource = products;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки материалов: {ex.Message}");
            }
        }

        #region Обработчики событий для placeholder текста

        private void TxtFabricQuantity_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtFabricQuantity.Text != "")
            {
                lblFabricPlaceholder.Visibility = Visibility.Hidden;
            }
            else
            {
                lblFabricPlaceholder.Visibility = Visibility.Visible;
            }
        }

        private void TxtAccessoryQuantity_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtAccessoryQuantity.Text != "")
            {
                lblAccessoryPlaceholder.Visibility = Visibility.Hidden;
            }
            else
            {
                lblAccessoryPlaceholder.Visibility = Visibility.Visible;
            }
        }

        private void TxtProductQuantity_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtProductQuantity.Text != "")
            {
                lblProductPlaceholder.Visibility = Visibility.Hidden;
            }
            else
            {
                lblProductPlaceholder.Visibility = Visibility.Visible;
            }
        }

        #endregion

        #region Добавление материалов

        private void BtnAddFabric_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedFabric = cbFabricSelect.SelectedItem as MaterialItem;
                if (selectedFabric == null)
                {
                    MessageBox.Show("Выберите ткань");
                    return;
                }

                if (!decimal.TryParse(txtFabricQuantity.Text, out decimal quantity) || quantity < 0)
                {
                    MessageBox.Show("Введите корректное количество");
                    return;
                }

                // Проверяем, не добавлена ли уже эта ткань
                var existingLine = fabricLines.FirstOrDefault(f => f.FabricArticle == int.Parse(selectedFabric.Id));
                if (existingLine != null)
                {
                    existingLine.ActualQuantity = quantity;
                }
                else
                {
                    var newLine = new InventoryFabricLine
                    {
                        FabricArticle = int.Parse(selectedFabric.Id),
                        FabricName = selectedFabric.Name,
                        ActualQuantity = quantity
                    };

                    fabricLines.Add(newLine);
                }

                txtFabricQuantity.Clear();
                cbFabricSelect.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка добавления ткани: {ex.Message}");
            }
        }

        private void BtnAddAccessory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedAccessory = cbAccessorySelect.SelectedItem as MaterialItem;
                if (selectedAccessory == null)
                {
                    MessageBox.Show("Выберите фурнитуру");
                    return;
                }

                if (!int.TryParse(txtAccessoryQuantity.Text, out int quantity) || quantity < 0)
                {
                    MessageBox.Show("Введите корректное количество");
                    return;
                }

                // Проверяем, не добавлена ли уже эта фурнитура
                var existingLine = accessoryLines.FirstOrDefault(a => a.AccessoryArticle == selectedAccessory.Id);
                if (existingLine != null)
                {
                    existingLine.ActualQuantity = quantity;
                }
                else
                {
                    var newLine = new InventoryAccessoryLine
                    {
                        AccessoryArticle = selectedAccessory.Id,
                        AccessoryName = selectedAccessory.Name,
                        ActualQuantity = quantity
                    };

                    accessoryLines.Add(newLine);
                }

                txtAccessoryQuantity.Clear();
                cbAccessorySelect.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка добавления фурнитуры: {ex.Message}");
            }
        }

        private void BtnAddProduct_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedProduct = cbProductSelect.SelectedItem as MaterialItem;
                if (selectedProduct == null)
                {
                    MessageBox.Show("Выберите изделие");
                    return;
                }

                if (!int.TryParse(txtProductQuantity.Text, out int quantity) || quantity < 0)
                {
                    MessageBox.Show("Введите корректное количество");
                    return;
                }

                // Проверяем, не добавлено ли уже это изделие
                var existingLine = productLines.FirstOrDefault(p => p.ProductArticle == selectedProduct.Id);
                if (existingLine != null)
                {
                    existingLine.ActualQuantity = quantity;
                }
                else
                {
                    var newLine = new InventoryProductLine
                    {
                        ProductArticle = selectedProduct.Id,
                        ProductName = selectedProduct.Name,
                        ActualQuantity = quantity
                    };

                    productLines.Add(newLine);
                }

                txtProductQuantity.Clear();
                cbProductSelect.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка добавления изделия: {ex.Message}");
            }
        }

        #endregion

        #region Удаление материалов

        private void BtnRemoveFabric_Click(object sender, RoutedEventArgs e)
        {
            var selectedLine = dgFabrics.SelectedItem as InventoryFabricLine;
            if (selectedLine != null)
            {
                fabricLines.Remove(selectedLine);
            }
            else
            {
                MessageBox.Show("Выберите строку для удаления");
            }
        }

        private void BtnRemoveAccessory_Click(object sender, RoutedEventArgs e)
        {
            var selectedLine = dgAccessories.SelectedItem as InventoryAccessoryLine;
            if (selectedLine != null)
            {
                accessoryLines.Remove(selectedLine);
            }
            else
            {
                MessageBox.Show("Выберите строку для удаления");
            }
        }

        private void BtnRemoveProduct_Click(object sender, RoutedEventArgs e)
        {
            var selectedLine = dgProducts.SelectedItem as InventoryProductLine;
            if (selectedLine != null)
            {
                productLines.Remove(selectedLine);
            }
            else
            {
                MessageBox.Show("Выберите строку для удаления");
            }
        }

        #endregion

        private void BtnRecalculate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CalculateSystemQuantities();
                CalculateTotalDiscrepancy();
                UpdateUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка пересчета: {ex.Message}");
            }
        }

        private void CalculateSystemQuantities()
        {
            // Расчет для тканей
            foreach (var fabricLine in fabricLines)
            {
                string fabricQuery = @"
                SELECT 
                    COALESCE(SUM(width * length), 0) as total_quantity,
                    COALESCE(AVG(purchase_price), 0) as avg_price
                FROM fabricwarehouse 
                WHERE fabric_article = @fabric_article";

                using (var connection = new NpgsqlConnection(database.connectionString))
                {
                    connection.Open();
                    using (var cmd = new NpgsqlCommand(fabricQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@fabric_article", fabricLine.FabricArticle);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                fabricLine.SystemQuantity = reader.GetDecimal("total_quantity");
                                fabricLine.AveragePurchasePrice = reader.GetDecimal("avg_price");
                            }
                        }
                    }
                }
            }

            // Расчет для фурнитуры
            foreach (var accessoryLine in accessoryLines)
            {
                string accessoryQuery = @"
                SELECT 
                    COALESCE(SUM(quantity), 0) as total_quantity,
                    COALESCE(AVG(purchase_price), 0) as avg_price
                FROM accessorywarehouse 
                WHERE accessory_article = @accessory_article";

                using (var connection = new NpgsqlConnection(database.connectionString))
                {
                    connection.Open();
                    using (var cmd = new NpgsqlCommand(accessoryQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@accessory_article", accessoryLine.AccessoryArticle);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                accessoryLine.SystemQuantity = reader.GetInt32("total_quantity");
                                accessoryLine.AveragePurchasePrice = reader.GetDecimal("avg_price");
                            }
                        }
                    }
                }
            }

            // Расчет для изделий
            foreach (var productLine in productLines)
            {
                string productQuery = @"
                SELECT 
                    COALESCE(SUM(quantity), 0) as total_quantity,
                    COALESCE(AVG(production_cost), 0) as avg_cost
                FROM productwarehouse 
                WHERE product_article = @product_article";

                using (var connection = new NpgsqlConnection(database.connectionString))
                {
                    connection.Open();
                    using (var cmd = new NpgsqlCommand(productQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@product_article", productLine.ProductArticle);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                productLine.SystemQuantity = reader.GetInt32("total_quantity");
                                productLine.AverageProductionCost = reader.GetDecimal("avg_cost");
                            }
                        }
                    }
                }
            }
        }

        private void CalculateTotalDiscrepancy()
        {
            decimal totalDiscrepancy = 0;
            decimal totalSystemValue = 0;

            // Суммируем расхождения по тканям
            foreach (var fabricLine in fabricLines)
            {
                totalDiscrepancy += Math.Abs(fabricLine.DifferenceAmount);
                totalSystemValue += fabricLine.SystemQuantity * fabricLine.AveragePurchasePrice;
            }

            // Суммируем расхождения по фурнитуре
            foreach (var accessoryLine in accessoryLines)
            {
                totalDiscrepancy += Math.Abs(accessoryLine.DifferenceAmount);
                totalSystemValue += accessoryLine.SystemQuantity * accessoryLine.AveragePurchasePrice;
            }

            // Суммируем расхождения по изделиям
            foreach (var productLine in productLines)
            {
                totalDiscrepancy += Math.Abs(productLine.DifferenceAmount);
                totalSystemValue += productLine.SystemQuantity * productLine.AverageProductionCost;
            }

            currentDocument.TotalDiscrepancyAmount = totalDiscrepancy;

            if (totalSystemValue > 0)
            {
                currentDocument.DiscrepancyPercentage = (totalDiscrepancy / totalSystemValue) * 100;
            }

            // Проверяем, нужно ли утверждение директора
            currentDocument.RequiresDirectorApproval = currentDocument.DiscrepancyPercentage > 20;
        }

        private void UpdateUI()
        {
            txtTotalDiscrepancy.Text = $"{currentDocument.TotalDiscrepancyAmount:C}";
            txtDiscrepancyPercentage.Text = $"({currentDocument.DiscrepancyPercentage:F1}%)";

            if (currentDocument.RequiresDirectorApproval)
            {
                txtApprovalRequired.Visibility = Visibility.Visible;
                txtApprovalGranted.Visibility = Visibility.Collapsed;
                btnComplete.IsEnabled = false;
            }
            else
            {
                txtApprovalRequired.Visibility = Visibility.Collapsed;
                btnComplete.IsEnabled = true;
            }

            if (currentDocument.DirectorApproved)
            {
                txtApprovalGranted.Visibility = Visibility.Visible;
                txtApprovalRequired.Visibility = Visibility.Collapsed;
                btnComplete.IsEnabled = true;
            }
        }

        private void BtnSaveDraft_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveInventoryDocument("Черновик");
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
                if (currentDocument.RequiresDirectorApproval && !currentDocument.DirectorApproved)
                {
                    MessageBox.Show("Документ требует утверждения директора");
                    return;
                }

                SaveInventoryDocument("Завершен");
                ProcessInventoryAdjustments();
                MessageBox.Show("Инвентаризация завершена. Корректировки применены.");
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка завершения: {ex.Message}");
            }
        }

        private void BtnDirectorApprove_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (currentUserRole != "Руководитель")
                {
                    MessageBox.Show("Только директор может утверждать документы");
                    return;
                }

                currentDocument.DirectorApproved = true;
                currentDocument.DirectorApprovalDate = DateTime.Now;
                UpdateUI();

                MessageBox.Show("Документ утвержден директором");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка утверждения: {ex.Message}");
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Вы уверены, что хотите закрыть окно? Несохраненные данные будут потеряны.",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                this.Close();
            }
        }

        private void SaveInventoryDocument(string status)
        {
            using (var connection = new NpgsqlConnection(database.connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Сохраняем документ
                        string insertDocQuery = @"
                        INSERT INTO inventory_documents 
                        (document_number, inventory_date, responsible_person, status, 
                         total_discrepancy_amount, discrepancy_percentage, 
                         requires_director_approval, director_approved, director_approval_date)
                        VALUES (@doc_number, @inventory_date, @responsible, @status,
                                @total_discrepancy, @discrepancy_percentage,
                                @requires_approval, @director_approved, @approval_date)
                        RETURNING document_id";

                        using (var cmd = new NpgsqlCommand(insertDocQuery, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@doc_number", currentDocument.DocumentNumber);
                            cmd.Parameters.AddWithValue("@inventory_date", currentDocument.InventoryDate);
                            cmd.Parameters.AddWithValue("@responsible", currentDocument.ResponsiblePerson);
                            cmd.Parameters.AddWithValue("@status", status);
                            cmd.Parameters.AddWithValue("@total_discrepancy", currentDocument.TotalDiscrepancyAmount);
                            cmd.Parameters.AddWithValue("@discrepancy_percentage", currentDocument.DiscrepancyPercentage);
                            cmd.Parameters.AddWithValue("@requires_approval", currentDocument.RequiresDirectorApproval);
                            cmd.Parameters.AddWithValue("@director_approved", currentDocument.DirectorApproved);
                            cmd.Parameters.AddWithValue("@approval_date",
                                currentDocument.DirectorApprovalDate?.ToString() ?? (object)DBNull.Value);

                            currentDocument.DocumentId = (int)cmd.ExecuteScalar();
                        }

                        // Сохраняем строки тканей
                        SaveFabricLines(connection, transaction);

                        // Сохраняем строки фурнитуры
                        SaveAccessoryLines(connection, transaction);

                        // Сохраняем строки изделий
                        SaveProductLines(connection, transaction);

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

        private void SaveFabricLines(NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            string insertFabricQuery = @"
            INSERT INTO inventory_fabric_lines 
            (document_id, fabric_article, actual_quantity, system_quantity, 
             difference_quantity, average_purchase_price, difference_amount)
            VALUES (@document_id, @fabric_article, @actual_quantity, @system_quantity,
                    @difference_quantity, @average_price, @difference_amount)";

            foreach (var fabricLine in fabricLines)
            {
                using (var cmd = new NpgsqlCommand(insertFabricQuery, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@document_id", currentDocument.DocumentId);
                    cmd.Parameters.AddWithValue("@fabric_article", fabricLine.FabricArticle);
                    cmd.Parameters.AddWithValue("@actual_quantity", fabricLine.ActualQuantity);
                    cmd.Parameters.AddWithValue("@system_quantity", fabricLine.SystemQuantity);
                    cmd.Parameters.AddWithValue("@difference_quantity", fabricLine.DifferenceQuantity);
                    cmd.Parameters.AddWithValue("@average_price", fabricLine.AveragePurchasePrice);
                    cmd.Parameters.AddWithValue("@difference_amount", fabricLine.DifferenceAmount);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void SaveAccessoryLines(NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            string insertAccessoryQuery = @"
            INSERT INTO inventory_accessory_lines 
            (document_id, accessory_article, actual_quantity, system_quantity, 
             difference_quantity, average_purchase_price, difference_amount)
            VALUES (@document_id, @accessory_article, @actual_quantity, @system_quantity,
                    @difference_quantity, @average_price, @difference_amount)";

            foreach (var accessoryLine in accessoryLines)
            {
                using (var cmd = new NpgsqlCommand(insertAccessoryQuery, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@document_id", currentDocument.DocumentId);
                    cmd.Parameters.AddWithValue("@accessory_article", accessoryLine.AccessoryArticle);
                    cmd.Parameters.AddWithValue("@actual_quantity", accessoryLine.ActualQuantity);
                    cmd.Parameters.AddWithValue("@system_quantity", accessoryLine.SystemQuantity);
                    cmd.Parameters.AddWithValue("@difference_quantity", accessoryLine.DifferenceQuantity);
                    cmd.Parameters.AddWithValue("@average_price", accessoryLine.AveragePurchasePrice);
                    cmd.Parameters.AddWithValue("@difference_amount", accessoryLine.DifferenceAmount);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void SaveProductLines(NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            string insertProductQuery = @"
            INSERT INTO inventory_product_lines 
            (document_id, product_article, actual_quantity, system_quantity, 
             difference_quantity, average_production_cost, difference_amount)
            VALUES (@document_id, @product_article, @actual_quantity, @system_quantity,
                    @difference_quantity, @average_cost, @difference_amount)";

            foreach (var productLine in productLines)
            {
                using (var cmd = new NpgsqlCommand(insertProductQuery, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@document_id", currentDocument.DocumentId);
                    cmd.Parameters.AddWithValue("@product_article", productLine.ProductArticle);
                    cmd.Parameters.AddWithValue("@actual_quantity", productLine.ActualQuantity);
                    cmd.Parameters.AddWithValue("@system_quantity", productLine.SystemQuantity);
                    cmd.Parameters.AddWithValue("@difference_quantity", productLine.DifferenceQuantity);
                    cmd.Parameters.AddWithValue("@average_cost", productLine.AverageProductionCost);
                    cmd.Parameters.AddWithValue("@difference_amount", productLine.DifferenceAmount);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void ProcessInventoryAdjustments()
        {
            using (var connection = new NpgsqlConnection(database.connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Обрабатываем корректировки по тканям
                        ProcessFabricAdjustments(connection, transaction);

                        // Обрабатываем корректировки по фурнитуре
                        ProcessAccessoryAdjustments(connection, transaction);

                        // Обрабатываем корректировки по изделиям
                        ProcessProductAdjustments(connection, transaction);

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

        private void ProcessFabricAdjustments(NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            foreach (var fabricLine in fabricLines.Where(f => f.DifferenceQuantity != 0))
            {
                if (fabricLine.DifferenceQuantity > 0)
                {
                    // Излишки - оприходование
                    string insertQuery = @"
                    INSERT INTO fabricwarehouse (fabric_article, width, length, purchase_price, total_cost)
                    VALUES (@fabric_article, @width, @length, @purchase_price, @total_cost)";

                    using (var cmd = new NpgsqlCommand(insertQuery, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@fabric_article", fabricLine.FabricArticle);
                        cmd.Parameters.AddWithValue("@width", 1.0m);
                        cmd.Parameters.AddWithValue("@length", fabricLine.DifferenceQuantity);
                        cmd.Parameters.AddWithValue("@purchase_price", fabricLine.AveragePurchasePrice);
                        cmd.Parameters.AddWithValue("@total_cost", Math.Abs(fabricLine.DifferenceAmount));
                        cmd.ExecuteNonQuery();
                    }
                }
                else
                {
                    // Недостача - списание
                    decimal quantityToWrite = Math.Abs(fabricLine.DifferenceQuantity);

                    string selectQuery = @"
                    SELECT roll, fabric_article, width, length, purchase_price, total_cost
                    FROM fabricwarehouse 
                    WHERE fabric_article = @fabric_article
                    ORDER BY roll";

                    var warehouseRecords = new List<dynamic>();
                    using (var cmd = new NpgsqlCommand(selectQuery, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@fabric_article", fabricLine.FabricArticle);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                warehouseRecords.Add(new
                                {
                                    Roll = reader.GetInt32("roll"),
                                    FabricArticle = reader.GetInt32("fabric_article"),
                                    Width = reader.GetDecimal("width"),
                                    Length = reader.GetDecimal("length"),
                                    PurchasePrice = reader.GetDecimal("purchase_price"),
                                    TotalCost = reader.GetDecimal("total_cost")
                                });
                            }
                        }
                    }

                    // Пропорциональное списание
                    foreach (var record in warehouseRecords)
                    {
                        decimal recordQuantity = record.Width * record.Length;
                        if (quantityToWrite <= 0) break;

                        decimal writeOffQuantity = Math.Min(quantityToWrite, recordQuantity);
                        decimal newQuantity = recordQuantity - writeOffQuantity;

                        if (newQuantity <= 0)
                        {
                            string deleteQuery = @"
                            DELETE FROM fabricwarehouse 
                            WHERE roll = @roll AND fabric_article = @fabric_article";

                            using (var cmd = new NpgsqlCommand(deleteQuery, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@roll", record.Roll);
                                cmd.Parameters.AddWithValue("@fabric_article", record.FabricArticle);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            string updateQuery = @"
                            UPDATE fabricwarehouse 
                            SET length = @new_length, total_cost = @new_cost
                            WHERE roll = @roll AND fabric_article = @fabric_article";

                            using (var cmd = new NpgsqlCommand(updateQuery, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@new_length", newQuantity / record.Width);
                                cmd.Parameters.AddWithValue("@new_cost", newQuantity * record.PurchasePrice);
                                cmd.Parameters.AddWithValue("@roll", record.Roll);
                                cmd.Parameters.AddWithValue("@fabric_article", record.FabricArticle);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        quantityToWrite -= writeOffQuantity;
                    }
                }
            }
        }

        private void ProcessAccessoryAdjustments(NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            foreach (var accessoryLine in accessoryLines.Where(a => a.DifferenceQuantity != 0))
            {
                if (accessoryLine.DifferenceQuantity > 0)
                {
                    // Излишки - оприходование
                    string insertQuery = @"
                    INSERT INTO accessorywarehouse (batch_number, accessory_article, quantity, purchase_price, total_cost)
                    VALUES ((SELECT COALESCE(MAX(batch_number), 0) + 1 FROM accessorywarehouse), 
                            @accessory_article, @quantity, @purchase_price, @total_cost)";

                    using (var cmd = new NpgsqlCommand(insertQuery, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@accessory_article", accessoryLine.AccessoryArticle);
                        cmd.Parameters.AddWithValue("@quantity", accessoryLine.DifferenceQuantity);
                        cmd.Parameters.AddWithValue("@purchase_price", accessoryLine.AveragePurchasePrice);
                        cmd.Parameters.AddWithValue("@total_cost", Math.Abs(accessoryLine.DifferenceAmount));
                        cmd.ExecuteNonQuery();
                    }
                }
                else
                {
                    // Недостача - списание
                    int quantityToWrite = Math.Abs(accessoryLine.DifferenceQuantity);

                    string selectQuery = @"
                    SELECT batch_number, accessory_article, quantity, purchase_price, total_cost
                    FROM accessorywarehouse 
                    WHERE accessory_article = @accessory_article
                    ORDER BY batch_number";

                    var warehouseRecords = new List<dynamic>();
                    using (var cmd = new NpgsqlCommand(selectQuery, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@accessory_article", accessoryLine.AccessoryArticle);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                warehouseRecords.Add(new
                                {
                                    BatchNumber = reader.GetInt32("batch_number"),
                                    AccessoryArticle = reader.GetString("accessory_article"),
                                    Quantity = reader.GetInt32("quantity"),
                                    PurchasePrice = reader.GetDecimal("purchase_price"),
                                    TotalCost = reader.GetDecimal("total_cost")
                                });
                            }
                        }
                    }

                    // Списание по партиям
                    foreach (var record in warehouseRecords)
                    {
                        if (quantityToWrite <= 0) break;

                        int writeOffQuantity = Math.Min(quantityToWrite, record.Quantity);
                        int newQuantity = record.Quantity - writeOffQuantity;

                        if (newQuantity <= 0)
                        {
                            string deleteQuery = @"
                            DELETE FROM accessorywarehouse 
                            WHERE batch_number = @batch_number AND accessory_article = @accessory_article";

                            using (var cmd = new NpgsqlCommand(deleteQuery, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@batch_number", record.BatchNumber);
                                cmd.Parameters.AddWithValue("@accessory_article", record.AccessoryArticle);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            string updateQuery = @"
                            UPDATE accessorywarehouse 
                            SET quantity = @new_quantity, total_cost = @new_cost
                            WHERE batch_number = @batch_number AND accessory_article = @accessory_article";

                            using (var cmd = new NpgsqlCommand(updateQuery, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@new_quantity", newQuantity);
                                cmd.Parameters.AddWithValue("@new_cost", newQuantity * record.PurchasePrice);
                                cmd.Parameters.AddWithValue("@batch_number", record.BatchNumber);
                                cmd.Parameters.AddWithValue("@accessory_article", record.AccessoryArticle);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        quantityToWrite -= writeOffQuantity;
                    }
                }
            }
        }
        private void ProcessProductAdjustments(NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            foreach (var productLine in productLines.Where(p => p.DifferenceQuantity != 0))
            {
                if (productLine.DifferenceQuantity > 0)
                {
                    // Излишки - оприходование
                    string insertQuery = @"
            INSERT INTO productwarehouse (product_article, quantity, production_cost, total_cost)
            VALUES (@product_article, @quantity, @production_cost, @total_cost)";

                    using (var cmd = new NpgsqlCommand(insertQuery, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@product_article", productLine.ProductArticle);
                        cmd.Parameters.AddWithValue("@quantity", productLine.DifferenceQuantity);
                        cmd.Parameters.AddWithValue("@production_cost", productLine.AverageProductionCost);
                        cmd.Parameters.AddWithValue("@total_cost", Math.Abs(productLine.DifferenceAmount));
                        cmd.ExecuteNonQuery();
                    }
                }
                else
                {
                    // Недостача - списание
                    int quantityToWrite = Math.Abs(productLine.DifferenceQuantity);

                    string selectQuery = @"
            SELECT batch_id, product_article, quantity, production_cost, total_cost
            FROM productwarehouse 
            WHERE product_article = @product_article
            ORDER BY batch_id";

                    var warehouseRecords = new List<dynamic>();
                    using (var cmd = new NpgsqlCommand(selectQuery, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@product_article", productLine.ProductArticle);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                warehouseRecords.Add(new
                                {
                                    // ИСПРАВЛЕНО: Проверяем тип поля перед чтением
                                    BatchId = reader.IsDBNull("batch_id") ? 0 : Convert.ToInt32(reader["batch_id"]),
                                    ProductArticle = reader.IsDBNull("product_article") ? "" : reader["product_article"].ToString(),
                                    Quantity = reader.IsDBNull("quantity") ? 0 : Convert.ToInt32(reader["quantity"]),
                                    ProductionCost = reader.IsDBNull("production_cost") ? 0m : Convert.ToDecimal(reader["production_cost"]),
                                    TotalCost = reader.IsDBNull("total_cost") ? 0m : Convert.ToDecimal(reader["total_cost"])
                                });
                            }
                        }
                    }

                    // Списание по партиям
                    foreach (var record in warehouseRecords)
                    {
                        if (quantityToWrite <= 0) break;

                        int writeOffQuantity = Math.Min(quantityToWrite, record.Quantity);
                        int newQuantity = record.Quantity - writeOffQuantity;

                        if (newQuantity <= 0)
                        {
                            string deleteQuery = @"
                    DELETE FROM productwarehouse 
                    WHERE batch_id = @batch_id";

                            using (var cmd = new NpgsqlCommand(deleteQuery, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@batch_id", record.BatchId);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            string updateQuery = @"
                    UPDATE productwarehouse 
                    SET quantity = @new_quantity, total_cost = @new_cost
                    WHERE batch_id = @batch_id";

                            using (var cmd = new NpgsqlCommand(updateQuery, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@new_quantity", newQuantity);
                                cmd.Parameters.AddWithValue("@new_cost", newQuantity * record.ProductionCost);
                                cmd.Parameters.AddWithValue("@batch_id", record.BatchId);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        quantityToWrite -= writeOffQuantity;
                    }
                }
            }
        }

    }

    #region Модели данных

    public class InventoryDocument
    {
        public int DocumentId { get; set; }
        public string DocumentNumber { get; set; }
        public DateTime InventoryDate { get; set; }
        public string ResponsiblePerson { get; set; }
        public string Status { get; set; }
        public decimal TotalDiscrepancyAmount { get; set; }
        public decimal DiscrepancyPercentage { get; set; }
        public bool RequiresDirectorApproval { get; set; }
        public bool DirectorApproved { get; set; }
        public DateTime? DirectorApprovalDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class InventoryFabricLine : INotifyPropertyChanged
    {
        private int _lineId;
        private int _documentId;
        private int _fabricArticle;
        private string _fabricName;
        private decimal _actualQuantity;
        private decimal _systemQuantity;
        private decimal _differenceQuantity;
        private decimal _averagePurchasePrice;
        private decimal _differenceAmount;

        public int LineId
        {
            get => _lineId;
            set { _lineId = value; OnPropertyChanged(); }
        }

        public int DocumentId
        {
            get => _documentId;
            set { _documentId = value; OnPropertyChanged(); }
        }

        public int FabricArticle
        {
            get => _fabricArticle;
            set { _fabricArticle = value; OnPropertyChanged(); }
        }

        public string FabricName
        {
            get => _fabricName;
            set { _fabricName = value; OnPropertyChanged(); }
        }

        public decimal ActualQuantity
        {
            get => _actualQuantity;
            set
            {
                _actualQuantity = value;
                OnPropertyChanged();
                CalculateDifference();
            }
        }

        public decimal SystemQuantity
        {
            get => _systemQuantity;
            set
            {
                _systemQuantity = value;
                OnPropertyChanged();
                CalculateDifference();
            }
        }

        public decimal DifferenceQuantity
        {
            get => _differenceQuantity;
            set { _differenceQuantity = value; OnPropertyChanged(); }
        }

        public decimal AveragePurchasePrice
        {
            get => _averagePurchasePrice;
            set
            {
                _averagePurchasePrice = value;
                OnPropertyChanged();
                CalculateDifference();
            }
        }

        public decimal DifferenceAmount
        {
            get => _differenceAmount;
            set { _differenceAmount = value; OnPropertyChanged(); }
        }

        private void CalculateDifference()
        {
            DifferenceQuantity = ActualQuantity - SystemQuantity;
            DifferenceAmount = DifferenceQuantity * AveragePurchasePrice;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class InventoryAccessoryLine : INotifyPropertyChanged
    {
        private int _lineId;
        private int _documentId;
        private string _accessoryArticle;
        private string _accessoryName;
        private int _actualQuantity;
        private int _systemQuantity;
        private int _differenceQuantity;
        private decimal _averagePurchasePrice;
        private decimal _differenceAmount;

        public int LineId
        {
            get => _lineId;
            set { _lineId = value; OnPropertyChanged(); }
        }

        public int DocumentId
        {
            get => _documentId;
            set { _documentId = value; OnPropertyChanged(); }
        }

        public string AccessoryArticle
        {
            get => _accessoryArticle;
            set { _accessoryArticle = value; OnPropertyChanged(); }
        }

        public string AccessoryName
        {
            get => _accessoryName;
            set { _accessoryName = value; OnPropertyChanged(); }
        }

        public int ActualQuantity
        {
            get => _actualQuantity;
            set
            {
                _actualQuantity = value;
                OnPropertyChanged();
                CalculateDifference();
            }
        }

        public int SystemQuantity
        {
            get => _systemQuantity;
            set
            {
                _systemQuantity = value;
                OnPropertyChanged();
                CalculateDifference();
            }
        }

        public int DifferenceQuantity
        {
            get => _differenceQuantity;
            set { _differenceQuantity = value; OnPropertyChanged(); }
        }

        public decimal AveragePurchasePrice
        {
            get => _averagePurchasePrice;
            set
            {
                _averagePurchasePrice = value;
                OnPropertyChanged();
                CalculateDifference();
            }
        }

        public decimal DifferenceAmount
        {
            get => _differenceAmount;
            set { _differenceAmount = value; OnPropertyChanged(); }
        }

        private void CalculateDifference()
        {
            DifferenceQuantity = ActualQuantity - SystemQuantity;
            DifferenceAmount = DifferenceQuantity * AveragePurchasePrice;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class InventoryProductLine : INotifyPropertyChanged
    {
        private int _lineId;
        private int _documentId;
        private string _productArticle;
        private string _productName;
        private int _actualQuantity;
        private int _systemQuantity;
        private int _differenceQuantity;
        private decimal _averageProductionCost;
        private decimal _differenceAmount;

        public int LineId
        {
            get => _lineId;
            set { _lineId = value; OnPropertyChanged(); }
        }

        public int DocumentId
        {
            get => _documentId;
            set { _documentId = value; OnPropertyChanged(); }
        }

        public string ProductArticle
        {
            get => _productArticle;
            set { _productArticle = value; OnPropertyChanged(); }
        }

        public string ProductName
        {
            get => _productName;
            set { _productName = value; OnPropertyChanged(); }
        }

        public int ActualQuantity
        {
            get => _actualQuantity;
            set
            {
                _actualQuantity = value;
                OnPropertyChanged();
                CalculateDifference();
            }
        }

        public int SystemQuantity
        {
            get => _systemQuantity;
            set
            {
                _systemQuantity = value;
                OnPropertyChanged();
                CalculateDifference();
            }
        }

        public int DifferenceQuantity
        {
            get => _differenceQuantity;
            set { _differenceQuantity = value; OnPropertyChanged(); }
        }

        public decimal AverageProductionCost
        {
            get => _averageProductionCost;
            set
            {
                _averageProductionCost = value;
                OnPropertyChanged();
                CalculateDifference();
            }
        }

        public decimal DifferenceAmount
        {
            get => _differenceAmount;
            set { _differenceAmount = value; OnPropertyChanged(); }
        }

        private void CalculateDifference()
        {
            DifferenceQuantity = ActualQuantity - SystemQuantity;
            DifferenceAmount = DifferenceQuantity * AverageProductionCost;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    #endregion
}
