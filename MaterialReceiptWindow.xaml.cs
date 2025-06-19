using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Npgsql;

namespace UchPR
{
    public partial class MaterialReceiptWindow : Window
    {
        private DataBase database;
        private string currentUserRole;
        private int? receiptId;
        private bool isAccepted = false;
        private ObservableCollection<MaterialReceiptLine> receiptLines;

        public MaterialReceiptWindow(string userRole, int? receiptId = null)
        {
            InitializeComponent();
            database = new DataBase();
            currentUserRole = userRole;
            this.receiptId = receiptId;

            // Проверка прав доступа
            if (currentUserRole != "Кладовщик")
            {
                MessageBox.Show("У вас нет прав для работы с документами поступления", "Ошибка доступа",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                this.Close();
                return;
            }

            InitializeDocument();
            SetupDataGrid();
            LoadDocument();
        }

        private void InitializeDocument()
        {
            receiptLines = new ObservableCollection<MaterialReceiptLine>();
            receiptLines.CollectionChanged += ReceiptLines_CollectionChanged;

            dgMaterials.ItemsSource = receiptLines;
            dpDate.SelectedDate = DateTime.Now;

            if (receiptId == null)
            {
                // Новый документ
                txtDocNumber.Text = GenerateDocumentNumber();
                AddEmptyLine();
            }
        }

        private void ReceiptLines_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (MaterialReceiptLine item in e.NewItems)
                {
                    item.PropertyChanged += MaterialLine_PropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (MaterialReceiptLine item in e.OldItems)
                {
                    item.PropertyChanged -= MaterialLine_PropertyChanged;
                }
            }

            CalculateTotalAmount();
        }

        private void MaterialLine_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Quantity" || e.PropertyName == "UnitPrice")
            {
                var line = sender as MaterialReceiptLine;
                if (line != null)
                {
                    line.CalculateTotalSum();
                    CalculateTotalAmount();
                }
            }
        }

        private void SetupDataGrid()
        {
            // Настройка колонки типа материала
            var materialTypeColumn = dgMaterials.Columns[0] as DataGridComboBoxColumn;
            materialTypeColumn.ItemsSource = GetMaterialTypes();
            materialTypeColumn.DisplayMemberPath = "Name";
            materialTypeColumn.SelectedValuePath = "Type";
            materialTypeColumn.SelectedValueBinding = new System.Windows.Data.Binding("MaterialType")
            {
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };

            // Настройка колонки наименования - ИСПРАВЛЕНО
            var materialNameColumn = dgMaterials.Columns[1] as DataGridComboBoxColumn;
            materialNameColumn.DisplayMemberPath = "Name";
            materialNameColumn.SelectedValuePath = "Id"; // БЫЛО Article, СТАЛО Id
            materialNameColumn.SelectedValueBinding = new System.Windows.Data.Binding("MaterialArticle")
            {
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };

            // Настройка колонки единиц измерения
            var unitColumn = dgMaterials.Columns[3] as DataGridComboBoxColumn;
            unitColumn.ItemsSource = GetUnitsOfMeasurement();
            unitColumn.DisplayMemberPath = "Name";
            unitColumn.SelectedValuePath = "Code";
            unitColumn.SelectedValueBinding = new System.Windows.Data.Binding("UnitCode")
            {
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };

            dgMaterials.CellEditEnding += DgMaterials_CellEditEnding;
        }
        private void DgMaterials_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Cancel) return;

            var line = e.Row.Item as MaterialReceiptLine;
            if (line == null) return;

            // Обработка изменения типа материала
            if (e.Column.Header.ToString() == "Тип материала")
            {
                var comboBox = e.EditingElement as ComboBox;
                if (comboBox?.SelectedValue != null)
                {
                    string materialType = comboBox.SelectedValue.ToString();

                    // Сбрасываем выбранный материал при смене типа
                    line.MaterialArticle = null;

                    // Обновляем список материалов с задержкой
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        UpdateMaterialsList(materialType);
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }

            // Проверяем заполненность строки и добавляем новую при необходимости
            Dispatcher.BeginInvoke(new Action(() =>
            {
                CheckAndAddNewLine();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
        private void CheckAndAddNewLine()
        {
            // Проверяем, есть ли пустая строка в конце
            var lastLine = receiptLines.LastOrDefault();
            if (lastLine != null &&
                !string.IsNullOrEmpty(lastLine.MaterialType) &&
                !string.IsNullOrEmpty(lastLine.MaterialArticle))
            {
                // Если последняя строка заполнена, добавляем новую пустую
                AddEmptyLine();
            }

            // Удаляем лишние пустые строки (оставляем только одну в конце)
            var emptyLines = receiptLines.Where(line =>
                string.IsNullOrEmpty(line.MaterialType) &&
                string.IsNullOrEmpty(line.MaterialArticle) &&
                line.Quantity == 0).ToList();

            if (emptyLines.Count > 1)
            {
                for (int i = 0; i < emptyLines.Count - 1; i++)
                {
                    receiptLines.Remove(emptyLines[i]);
                }
            }
        }
        private void UpdateMaterialsList(string materialType)
        {
            var materialNameColumn = dgMaterials.Columns[1] as DataGridComboBoxColumn;
            materialNameColumn.ItemsSource = GetMaterialsByType(materialType);
        }

        private List<MaterialTypeItem> GetMaterialTypes()
        {
            return new List<MaterialTypeItem>
            {
                new MaterialTypeItem { Type = "fabric", Name = "Ткани" },
                new MaterialTypeItem { Type = "accessory", Name = "Фурнитура" }
            };
        }

        private List<MaterialItem> GetMaterialsByType(string materialType)
        {
            try
            {
                string query = "";
                if (materialType == "fabric")
                {
                    query = @"
                        SELECT f.article, fn.name 
                        FROM fabric f
                        LEFT JOIN fabricname fn ON f.name_id = fn.code
                        WHERE fn.name IS NOT NULL
                        ORDER BY fn.name";
                }
                else if (materialType == "accessory")
                {
                    query = @"
                        SELECT a.article, fan.name 
                        FROM accessory a
                        LEFT JOIN furnitureaccessoryname fan ON a.name_id = fan.id
                        WHERE fan.name IS NOT NULL
                        ORDER BY fan.name";
                }
                else
                {
                    return new List<MaterialItem>();
                }

                var data = database.GetData(query);
                var materials = new List<MaterialItem>();

                foreach (DataRow row in data.Rows)
                {
                    materials.Add(new MaterialItem
                    {
                        Id = row["article"].ToString(),
                        Name = row["name"].ToString()
                    });
                }

                return materials;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки материалов: {ex.Message}");
                return new List<MaterialItem>();
            }
        }

        private List<UnitItem> GetUnitsOfMeasurement()
        {
            try
            {
                string query = "SELECT code, name FROM unitofmeasurement ORDER BY name";
                var data = database.GetData(query);
                var units = new List<UnitItem>();

                foreach (DataRow row in data.Rows)
                {
                    units.Add(new UnitItem
                    {
                        Code = Convert.ToInt32(row["code"]),
                        Name = row["name"].ToString()
                    });
                }

                return units;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки единиц измерения: {ex.Message}");
                return new List<UnitItem>();
            }
        }

        private string GenerateDocumentNumber()
        {
            try
            {
                string query = "SELECT COALESCE(MAX(receiptid), 0) + 1 FROM receipts";
                var result = database.GetScalarValue(query);
                return $"ПМ-{result:D6}";
            }
            catch
            {
                return $"ПМ-{DateTime.Now:yyyyMMddHHmmss}";
            }
        }

        private void LoadDocument()
        {
            if (receiptId.HasValue)
            {
                try
                {
                    // Загрузка заголовка документа
                    string headerQuery = @"
                        SELECT docnumber, docdate, totalamount, isaccepted 
                        FROM receipts 
                        WHERE receiptid = @receiptId";

                    var parameters = new NpgsqlParameter[] {
                        new NpgsqlParameter("@receiptId", receiptId.Value)
                    };

                    var headerData = database.GetData(headerQuery, parameters);
                    if (headerData.Rows.Count > 0)
                    {
                        var row = headerData.Rows[0];
                        txtDocNumber.Text = row["docnumber"].ToString();
                        dpDate.SelectedDate = Convert.ToDateTime(row["docdate"]);
                        isAccepted = Convert.ToBoolean(row["isaccepted"]);

                        if (isAccepted)
                        {
                            SetDocumentReadOnly();
                        }
                    }

                    LoadDocumentLines();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка загрузки документа: {ex.Message}");
                }
            }
        }

        private void LoadDocumentLines()
        {
            try
            {
                string query = @"
                    SELECT lineid, materialtype, materialid, quantity, unitprice, totalsum
                    FROM receiptlines 
                    WHERE receiptid = @receiptId
                    ORDER BY lineid";

                var parameters = new NpgsqlParameter[] {
                    new NpgsqlParameter("@receiptId", receiptId.Value)
                };

                var data = database.GetData(query, parameters);
                receiptLines.Clear();

                foreach (DataRow row in data.Rows)
                {
                    var line = new MaterialReceiptLine
                    {
                        LineId = Convert.ToInt32(row["lineid"]),
                        MaterialType = row["materialtype"].ToString(),
                        MaterialArticle = row["materialid"].ToString(),
                        Quantity = Convert.ToDecimal(row["quantity"]),
                        UnitPrice = Convert.ToDecimal(row["unitprice"]),
                        TotalSum = Convert.ToDecimal(row["totalsum"])
                    };

                    receiptLines.Add(line);
                }

                if (receiptLines.Count == 0)
                {
                    AddEmptyLine();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки строк документа: {ex.Message}");
            }
        }

        private void SetDocumentReadOnly()
        {
            dgMaterials.IsReadOnly = true;
            dpDate.IsEnabled = false;
            btnAddLine.IsEnabled = false;
            btnRemoveLine.IsEnabled = false;
            btnSaveDocument.IsEnabled = false;
            btnAcceptDocument.IsEnabled = false;

            Title += " (ПРИНЯТ К УЧЕТУ)";
        }

        private void AddEmptyLine()
        {
            receiptLines.Add(new MaterialReceiptLine());
        }

        private void CalculateTotalAmount()
        {
            decimal total = receiptLines.Sum(line => line.TotalSum);
            txtTotalAmount.Text = $"{total:C}";
        }

        // ======================= ОБРАБОТЧИКИ КНОПОК =======================

        private void BtnAddLine_Click(object sender, RoutedEventArgs e)
        {
            AddEmptyLine();
        }

        private void BtnRemoveLine_Click(object sender, RoutedEventArgs e)
        {
            if (dgMaterials.SelectedItem is MaterialReceiptLine selectedLine)
            {
                receiptLines.Remove(selectedLine);

                if (receiptLines.Count == 0)
                {
                    AddEmptyLine();
                }
            }
            else
            {
                MessageBox.Show("Выберите строку для удаления");
            }
        }

        // СОХРАНЕНИЕ ДОКУМЕНТА (БЕЗ ВЛИЯНИЯ НА СКЛАД)
        private void BtnSaveDocument_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateDocument())
            {
                SaveDocument(false); // false = не принимать к учету
                SaveToHistory("save", false); // Сохраняем в историю
            }
        }
        // ПРИНЯТИЕ К УЧЕТУ (С ОБНОВЛЕНИЕМ СКЛАДА)
        private void BtnAcceptDocument_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateDocument())
            {
                var result = MessageBox.Show(
                    "После принятия к учету документ нельзя будет изменить и товары поступят на склад.\nПродолжить?",
                    "Подтверждение принятия к учету",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    SaveDocument(true); // true = принять к учету
                    SaveToHistory("accept", true); // Сохраняем в историю
                }
            }
        }

        private bool ValidateDocument()
        {
            // Отладочная информация
            System.Diagnostics.Debug.WriteLine($"Всего строк в receiptLines: {receiptLines.Count}");

            foreach (var line in receiptLines)
            {
                if (!string.IsNullOrEmpty(line.MaterialType) &&
                    !string.IsNullOrEmpty(line.MaterialArticle) &&
                    line.Quantity > 0 &&
                    line.UnitPrice > 0)
                {
                    decimal basePrice = GetBasePrice(line.MaterialType, line.MaterialArticle);

                    if (basePrice > 0 && line.UnitPrice > basePrice)
                    {
                        MessageBox.Show(
                            $"Цена за ед. ({line.UnitPrice:N2} руб.) по материалу '{line.MaterialArticle}' " +
                            $"превышает базовую цену ({basePrice:N2} руб.) в справочнике.\n" +
                            $"Проверьте корректность данных.",
                            "Ошибка в цене",
                            MessageBoxButton.OK, MessageBoxImage.Warning
                        );
                        return false;
                    }
                }
            }

            // Проверка заполненности строк
            var validLines = receiptLines.Where(line =>
                !string.IsNullOrEmpty(line.MaterialType) &&
                !string.IsNullOrEmpty(line.MaterialArticle) &&
                line.Quantity > 0 &&
                line.UnitPrice > 0).ToList();

            System.Diagnostics.Debug.WriteLine($"Валидных строк: {validLines.Count}");

            if (validLines.Count == 0)
            {
                MessageBox.Show("Добавьте хотя бы одну корректно заполненную строку");
                return false;
            }

            if (dpDate.SelectedDate == null)
            {
                MessageBox.Show("Укажите дату документа");
                return false;
            }

            return true;
        }

        private void SaveDocument(bool acceptToAccounting)
        {
            try
            {
                using (var connection = new NpgsqlConnection(database.connectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // 1. Сохранение заголовка документа
                            int documentId = SaveDocumentHeader(connection, transaction, acceptToAccounting);

                            // 2. Сохранение строк документа
                            SaveDocumentLines(connection, transaction, documentId);

                            // 3. КЛЮЧЕВОЕ ОТЛИЧИЕ: обновляем склад только при принятии к учету
                            if (acceptToAccounting)
                            {
                                UpdateWarehouseBalances(connection, transaction);
                            }

                            transaction.Commit();

                            string message = acceptToAccounting ?
                                "Документ принят к учету. Товары поступили на склад." :
                                "Документ сохранен как черновик. Склад не изменен.";

                            MessageBox.Show(message, "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);

                            if (acceptToAccounting)
                            {
                                isAccepted = true;
                                SetDocumentReadOnly();
                            }
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            throw ex;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения документа: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private int SaveDocumentHeader(NpgsqlConnection connection, NpgsqlTransaction transaction, bool isAccepted)
        {
            decimal totalAmount = receiptLines.Sum(line => line.TotalSum);

            if (receiptId.HasValue)
            {
                // Обновление существующего документа
                string updateQuery = @"
                    UPDATE receipts 
                    SET docdate = @docdate, totalamount = @totalamount, isaccepted = @isaccepted
                    WHERE receiptid = @receiptid";

                using (var cmd = new NpgsqlCommand(updateQuery, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@docdate", dpDate.SelectedDate.Value);
                    cmd.Parameters.AddWithValue("@totalamount", totalAmount);
                    cmd.Parameters.AddWithValue("@isaccepted", isAccepted);
                    cmd.Parameters.AddWithValue("@receiptid", receiptId.Value);
                    cmd.ExecuteNonQuery();
                }

                return receiptId.Value;
            }
            else
            {
                // Создание нового документа
                string insertQuery = @"
                    INSERT INTO receipts (docnumber, docdate, totalamount, isaccepted, created_by)
                    VALUES (@docnumber, @docdate, @totalamount, @isaccepted, @created_by)
                    RETURNING receiptid";

                using (var cmd = new NpgsqlCommand(insertQuery, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@docnumber", txtDocNumber.Text);
                    cmd.Parameters.AddWithValue("@docdate", dpDate.SelectedDate.Value);
                    cmd.Parameters.AddWithValue("@totalamount", totalAmount);
                    cmd.Parameters.AddWithValue("@isaccepted", isAccepted);
                    cmd.Parameters.AddWithValue("@created_by", currentUserRole);

                    receiptId = (int)cmd.ExecuteScalar();
                    return receiptId.Value;
                }
            }
        }

        private void SaveDocumentLines(NpgsqlConnection connection, NpgsqlTransaction transaction, int documentId)
        {
            // Удаление старых строк    
            string deleteQuery = "DELETE FROM receiptlines WHERE receiptid = @receiptid";
            using (var cmd = new NpgsqlCommand(deleteQuery, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@receiptid", documentId);
                cmd.ExecuteNonQuery();
            }

            // Добавление новых строк
            var validLines = receiptLines.Where(line =>
                !string.IsNullOrEmpty(line.MaterialType) &&
                !string.IsNullOrEmpty(line.MaterialArticle) &&
                line.Quantity > 0 &&
                line.UnitPrice > 0).ToList();

            string insertQuery = @"
        INSERT INTO receiptlines (receiptid, materialtype, materialid, quantity, unitprice, totalsum)
        VALUES (@receiptid, @materialtype, @materialid, @quantity, @unitprice, @totalsum)";

            foreach (var line in validLines)
            {
                using (var cmd = new NpgsqlCommand(insertQuery, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@receiptid", documentId);
                    cmd.Parameters.AddWithValue("@materialtype", line.MaterialType);

                    // ИСПРАВЛЕНО: Теперь materialid всегда строка
                    cmd.Parameters.AddWithValue("@materialid", line.MaterialArticle);

                    cmd.Parameters.AddWithValue("@quantity", line.Quantity);
                    cmd.Parameters.AddWithValue("@unitprice", line.UnitPrice);
                    cmd.Parameters.AddWithValue("@totalsum", line.TotalSum);
                    cmd.ExecuteNonQuery();
                }
            }
        }
        // КЛЮЧЕВОЙ МЕТОД: ОБНОВЛЕНИЕ СКЛАДСКИХ ОСТАТКОВ
        private void UpdateWarehouseBalances(NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            var validLines = receiptLines.Where(line =>
                !string.IsNullOrEmpty(line.MaterialType) &&
                !string.IsNullOrEmpty(line.MaterialArticle) &&
                line.Quantity > 0 &&
                line.UnitPrice > 0).ToList();

            foreach (var line in validLines)
            {
                if (line.MaterialType == "fabric")
                {
                    AddToFabricWarehouse(connection, transaction, line);
                }
                else if (line.MaterialType == "accessory")
                {
                    AddToAccessoryWarehouse(connection, transaction, line);
                }
            }
        }

        private void AddToFabricWarehouse(NpgsqlConnection connection, NpgsqlTransaction transaction, MaterialReceiptLine line)
        {
            string insertQuery = @"
        INSERT INTO fabricwarehouse (fabric_article, width, length, purchase_price, total_cost)
        VALUES (@fabric_article, @width, @length, @purchase_price, @total_cost)";

            using (var cmd = new NpgsqlCommand(insertQuery, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@fabric_article", Convert.ToInt32(line.MaterialArticle));
                cmd.Parameters.AddWithValue("@width", line.Quantity);
                cmd.Parameters.AddWithValue("@length", 1.0m);
                cmd.Parameters.AddWithValue("@purchase_price", line.UnitPrice);
                cmd.Parameters.AddWithValue("@total_cost", line.TotalSum);
                cmd.ExecuteNonQuery();
            }
        }
        private void SaveToHistory(string action, bool isAccepted)
        {
            try
            {
                using (var connection = new NpgsqlConnection(database.connectionString))
                {
                    connection.Open();

                    string query = @"
                INSERT INTO document_history 
                (document_name, document_type, status, created_date, modified_date, 
                 description, created_by, modified_by)
                VALUES (@document_name, @document_type, @status, @created_date, 
                        @modified_date, @description, @created_by, @modified_by)";

                    using (var cmd = new NpgsqlCommand(query, connection))
                    {
                        string status = isAccepted ? "Принят к учету" : "Сохранен как черновик";
                        string description = $"Документ поступления материалов {txtDocNumber.Text}. " +
                                           $"Дата: {dpDate.SelectedDate:dd.MM.yyyy}. " +
                                           $"Общая сумма: {txtTotalAmount.Text}";

                        cmd.Parameters.AddWithValue("@document_name", $"Поступление {txtDocNumber.Text}");
                        cmd.Parameters.AddWithValue("@document_type", "Поступление материалов");
                        cmd.Parameters.AddWithValue("@status", status);
                        cmd.Parameters.AddWithValue("@created_date", DateTime.Now);
                        cmd.Parameters.AddWithValue("@modified_date", DateTime.Now);
                        cmd.Parameters.AddWithValue("@description", description);
                        cmd.Parameters.AddWithValue("@created_by", currentUserRole);
                        cmd.Parameters.AddWithValue("@modified_by", currentUserRole);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения в историю: {ex.Message}");
            }
        }
        private decimal GetBasePrice(string materialType, string materialArticle)
        {
            try
            {
                string query = "";
                if (materialType == "fabric")
                {
                    query = "SELECT price FROM fabric WHERE article = @article";
                }
                else if (materialType == "accessory")
                {
                    query = "SELECT price FROM accessory WHERE article = @article";
                }
                else
                {
                    return 0;
                }

                NpgsqlParameter[] parameters;
                if (materialType == "fabric")
                    parameters = new NpgsqlParameter[] { new NpgsqlParameter("@article", Convert.ToInt32(materialArticle)) };
                else
                    parameters = new NpgsqlParameter[] { new NpgsqlParameter("@article", materialArticle) };
                var data = database.GetData(query, parameters);

                if (data.Rows.Count > 0)
                    return Convert.ToDecimal(data.Rows[0]["price"]);
                else
                    return 0;
            }
            catch
            {
                return 0;
            }
        }


        private void AddToAccessoryWarehouse(NpgsqlConnection connection, NpgsqlTransaction transaction, MaterialReceiptLine line)
        {
            // Проверяем, есть ли уже такая фурнитура на складе
            string checkQuery = @"
        SELECT batch_number, quantity, total_cost 
        FROM accessorywarehouse 
        WHERE accessory_article = @accessory_article 
        ORDER BY batch_number DESC 
        LIMIT 1";

            using (var checkCmd = new NpgsqlCommand(checkQuery, connection, transaction))
            {
                checkCmd.Parameters.AddWithValue("@accessory_article", line.MaterialArticle); // Строка
                var result = checkCmd.ExecuteScalar();

                if (result != null)
                {
                    // Фурнитура уже есть - обновляем количество
                    string updateQuery = @"
                UPDATE accessorywarehouse 
                SET quantity = quantity + @quantity,
                    total_cost = total_cost + @total_cost
                WHERE accessory_article = @accessory_article 
                AND batch_number = (
                    SELECT MAX(batch_number) 
                    FROM accessorywarehouse 
                    WHERE accessory_article = @accessory_article
                )";

                    using (var updateCmd = new NpgsqlCommand(updateQuery, connection, transaction))
                    {
                        updateCmd.Parameters.AddWithValue("@accessory_article", line.MaterialArticle);
                        updateCmd.Parameters.AddWithValue("@quantity", (int)line.Quantity);
                        updateCmd.Parameters.AddWithValue("@total_cost", line.TotalSum);
                        updateCmd.ExecuteNonQuery();
                    }
                }
                else
                {
                    // Новая фурнитура - создаем новую запись
                    int batchNumber = GenerateBatchNumber(connection, transaction);

                    string insertQuery = @"
                INSERT INTO accessorywarehouse (batch_number, accessory_article, quantity, purchase_price, total_cost)
                VALUES (@batch_number, @accessory_article, @quantity, @purchase_price, @total_cost)";

                    using (var insertCmd = new NpgsqlCommand(insertQuery, connection, transaction))
                    {
                        insertCmd.Parameters.AddWithValue("@batch_number", batchNumber);
                        insertCmd.Parameters.AddWithValue("@accessory_article", line.MaterialArticle); // Строка
                        insertCmd.Parameters.AddWithValue("@quantity", (int)line.Quantity);
                        insertCmd.Parameters.AddWithValue("@purchase_price", line.UnitPrice);
                        insertCmd.Parameters.AddWithValue("@total_cost", line.TotalSum);
                        insertCmd.ExecuteNonQuery();
                    }
                }
            }
        }

        private int GenerateBatchNumber(NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            string query = "SELECT COALESCE(MAX(batch_number), 0) + 1 FROM accessorywarehouse";
            using (var cmd = new NpgsqlCommand(query, connection, transaction))
            {
                return (int)cmd.ExecuteScalar();
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DocumentHistoryWindow documentHistoryItem = new DocumentHistoryWindow();
            documentHistoryItem.Show();
        }
    }

    // ======================= МОДЕЛИ ДАННЫХ =======================

    public class MaterialReceiptLine : INotifyPropertyChanged
    {
        private string _materialType;
        private string _materialArticle;
        private decimal _quantity;
        private decimal _unitPrice;
        private decimal _totalSum;
        private int _unitCode;

        public int LineId { get; set; }

        public string MaterialType
        {
            get => _materialType;
            set
            {
                if (_materialType != value)
                {
                    _materialType = value;
                    OnPropertyChanged(nameof(MaterialType));
                }
            }
        }

        public string MaterialArticle
        {
            get => _materialArticle;
            set
            {
                if (_materialArticle != value)
                {
                    _materialArticle = value;
                    OnPropertyChanged(nameof(MaterialArticle));
                }
            }
        }

        public decimal Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity != value)
                {
                    _quantity = value;
                    OnPropertyChanged(nameof(Quantity));
                    CalculateTotalSum();
                }
            }
        }

        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                if (_unitPrice != value)
                {
                    _unitPrice = value;
                    OnPropertyChanged(nameof(UnitPrice));
                    CalculateTotalSum();
                }
            }
        }

        public decimal TotalSum
        {
            get => _totalSum;
            set
            {
                if (_totalSum != value)
                {
                    _totalSum = value;
                    OnPropertyChanged(nameof(TotalSum));
                }
            }
        }

        public int UnitCode
        {
            get => _unitCode;
            set
            {
                if (_unitCode != value)
                {
                    _unitCode = value;
                    OnPropertyChanged(nameof(UnitCode));
                }
            }
        }

        public void CalculateTotalSum()
        {
            TotalSum = Quantity * UnitPrice;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }


    public class MaterialTypeItem
    {
        public string Type { get; set; }
        public string Name { get; set; }
    }


    public class UnitItem
    {
        public int Code { get; set; }
        public string Name { get; set; }
    }
}
