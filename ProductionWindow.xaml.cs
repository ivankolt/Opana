using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Npgsql;

namespace UchPR
{
    public partial class ProductionWindow : Window
    {
        private DataBase database;
        private ObservableCollection<ProductionMaterial> materials;
        private string currentUserRole;
        private int selectedProductId;
        private decimal totalCost;

        public ProductionWindow()
        {
            InitializeComponent();
            InitializeWindow();
        }

        public ProductionWindow(string userRole) : this()
        {
            currentUserRole = userRole;
            ConfigureByUserRole();
        }

        private void InitializeWindow()
        {
            database = new DataBase();
            materials = new ObservableCollection<ProductionMaterial>();
            dgMaterials.ItemsSource = materials;

            // Генерация номера документа
            GenerateDocumentNumber();

            // Установка текущей даты
            dpProductionDate.SelectedDate = DateTime.Now;

            // Установка ответственного (можно получить из сессии пользователя)
            txtResponsible.Text = currentUserRole ?? "Система";

            // Загрузка списка изделий
            LoadProducts();
        }

        private void ConfigureByUserRole()
        {
            // Настройка доступа в зависимости от роли
            if (currentUserRole == "Менеджер" || currentUserRole == "Руководитель")
            {
                btnStartProduction.IsEnabled = true;
            }
            else
            {
                btnStartProduction.IsEnabled = false;
                btnStartProduction.ToolTip = "Только менеджер или руководитель может запускать производство";
            }
        }

        private void GenerateDocumentNumber()
        {
            try
            {
                string query = @"SELECT COALESCE(MAX(CAST(SUBSTRING(docnumber, 3) AS INTEGER)), 0) + 1 
                                FROM production_documents 
                                WHERE docnumber LIKE 'ПР-%'";

                var result = database.GetScalarValue(query);
                int nextNumber = Convert.ToInt32(result);

                txtDocNumber.Text = $"ПР-{nextNumber:D6}";
            }
            catch (Exception ex)
            {
                txtDocNumber.Text = $"ПР-{DateTime.Now:yyyyMMdd}-001";
            }
        }

        private void LoadProducts()
        {
            try
            {
                string query = @"SELECT productid, productarticlenum, productname, productcategory 
                                FROM product 
                                ORDER BY productarticlenum";

                var data = database.GetData(query);
                var products = new List<ProductItem>();

                foreach (DataRow row in data.Rows)
                {
                    products.Add(new ProductItem
                    {
                        Id = Convert.ToInt32(row["productid"]),
                        Article = row["productarticlenum"].ToString(),
                        Name = $"{row["productarticlenum"]} - {row["productname"]}",
                        Category = row["productcategory"].ToString()
                    });
                }

                cmbProducts.ItemsSource = products;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки списка изделий: {ex.Message}");
            }
        }

        // Обработчик изменения выбранного изделия
        private void cmbProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbProducts.SelectedItem is ProductItem selectedProduct)
            {
                selectedProductId = selectedProduct.Id;
                LoadProductMaterials();
                UpdateProductInfo(selectedProduct);
                CalculateTotalCost();
            }
        }

        // Обработчик изменения количества
        private void txtQuantity_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (decimal.TryParse(txtQuantity.Text, out decimal quantity) && quantity > 0)
            {
                UpdateMaterialQuantities(quantity);
                CalculateTotalCost();
            }
        }

        // Обработчик проверки материалов
        private void btnCheckMaterials_Click(object sender, RoutedEventArgs e)
        {
            CheckMaterialAvailability();
        }

        // Обработчик начала производства
        private void btnStartProduction_Click(object sender, RoutedEventArgs e)
        {
            StartProduction();
        }

        // Обработчик отмены
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void LoadProductMaterials()
        {
            try
            {
                materials.Clear();

                // Загрузка тканей для изделия
                string fabricQuery = @"
                    SELECT f.fabricname, f.fabricarticlenum, fp.requiredquantity, 
                           u.unitname, COALESCE(fw.quantity, 0) as available,
                           COALESCE(fw.totalcost, 0) as cost
                    FROM fabricproducts fp
                    JOIN fabric f ON fp.fabricid = f.fabricid
                    LEFT JOIN unitofmeasurement u ON f.accountingunitid = u.unitid
                    LEFT JOIN fabricwarehouse fw ON f.fabricid = fw.fabricid
                    WHERE fp.productid = @productId";

                var fabricParams = new NpgsqlParameter[] { new NpgsqlParameter("@productId", selectedProductId) };
                var fabricData = database.GetData(fabricQuery, fabricParams);

                foreach (DataRow row in fabricData.Rows)
                {
                    materials.Add(new ProductionMaterial
                    {
                        MaterialType = "Ткань",
                        MaterialName = $"{row["fabricarticlenum"]} - {row["fabricname"]}",
                        RequiredQuantity = Convert.ToDecimal(row["requiredquantity"]),
                        UnitName = row["unitname"].ToString(),
                        AvailableQuantity = Convert.ToDecimal(row["available"]),
                        TotalCost = Convert.ToDecimal(row["cost"])
                    });
                }

                // Загрузка фурнитуры для изделия
                string accessoryQuery = @"
                    SELECT a.accessoryname, a.accessoryarticlenum, ap.requiredquantity,
                           u.unitname, COALESCE(aw.quantity, 0) as available,
                           COALESCE(aw.totalcost, 0) as cost
                    FROM accessoryproducts ap
                    JOIN accessory a ON ap.accessoryid = a.accessoryid
                    LEFT JOIN unitofmeasurement u ON a.accountingunitid = u.unitid
                    LEFT JOIN accessorywarehouse aw ON a.accessoryid = aw.accessoryid
                    WHERE ap.productid = @productId";

                var accessoryParams = new NpgsqlParameter[] { new NpgsqlParameter("@productId", selectedProductId) };
                var accessoryData = database.GetData(accessoryQuery, accessoryParams);

                foreach (DataRow row in accessoryData.Rows)
                {
                    materials.Add(new ProductionMaterial
                    {
                        MaterialType = "Фурнитура",
                        MaterialName = $"{row["accessoryarticlenum"]} - {row["accessoryname"]}",
                        RequiredQuantity = Convert.ToDecimal(row["requiredquantity"]),
                        UnitName = row["unitname"].ToString(),
                        AvailableQuantity = Convert.ToDecimal(row["available"]),
                        TotalCost = Convert.ToDecimal(row["cost"])
                    });
                }

                // Обновление статусов доступности
                UpdateMaterialStatuses();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки материалов: {ex.Message}");
            }
        }

        private void UpdateProductInfo(ProductItem product)
        {
            lblProductInfo.Text = $"{product.Article}\n{product.Category}";

            // Здесь можно загрузить изображение изделия
            try
            {
                // imgProduct.Source = LoadProductImage(product.Id);
            }
            catch
            {
                // Если изображение не найдено, оставляем пустым
            }
        }

        private void UpdateMaterialQuantities(decimal quantity)
        {
            foreach (var material in materials)
            {
                material.RequiredQuantity = material.RequiredQuantity * quantity;
            }

            UpdateMaterialStatuses();
        }

        private void UpdateMaterialStatuses()
        {
            foreach (var material in materials)
            {
                if (material.AvailableQuantity >= material.RequiredQuantity)
                {
                    material.Status = "Достаточно";
                }
                else
                {
                    material.Status = $"Недостаток: {material.RequiredQuantity - material.AvailableQuantity:F3}";
                }
            }
        }

        private void CalculateTotalCost()
        {
            totalCost = 0;

            if (decimal.TryParse(txtQuantity.Text, out decimal quantity))
            {
                foreach (var material in materials)
                {
                    if (material.AvailableQuantity > 0)
                    {
                        decimal unitCost = material.TotalCost / material.AvailableQuantity;
                        totalCost += unitCost * material.RequiredQuantity;
                    }
                }
            }

            lblTotalCost.Text = $"{totalCost:C}";
        }

        private void CheckMaterialAvailability()
        {
            var unavailableMaterials = materials.Where(m => m.AvailableQuantity < m.RequiredQuantity).ToList();

            if (unavailableMaterials.Count == 0)
            {
                MessageBox.Show("Все материалы в наличии. Можно начинать производство!",
                    "Проверка материалов", MessageBoxButton.OK, MessageBoxImage.Information);
                btnStartProduction.IsEnabled = true;
            }
            else
            {
                string message = "Недостаточно материалов:\n\n";
                foreach (var material in unavailableMaterials)
                {
                    message += $"• {material.MaterialName}: недостаток {material.RequiredQuantity - material.AvailableQuantity:F3} {material.UnitName}\n";
                }

                MessageBox.Show(message, "Недостаток материалов",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                btnStartProduction.IsEnabled = false;
            }
        }

        private void StartProduction()
        {
            try
            {
                if (!ValidateProduction())
                    return;

                if (MessageBox.Show("Начать производство? Материалы будут списаны со склада.",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    // Создание документа производства
                    int documentId = CreateProductionDocument();

                    // Списание материалов
                    WriteOffMaterials(documentId);

                    // Обновление складских остатков
                    UpdateWarehouseStocks();

                    MessageBox.Show("Производство успешно запущено!", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска производства: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ValidateProduction()
        {
            if (selectedProductId == 0)
            {
                MessageBox.Show("Выберите изделие для производства");
                return false;
            }

            if (!decimal.TryParse(txtQuantity.Text, out decimal quantity) || quantity <= 0)
            {
                MessageBox.Show("Введите корректное количество для производства");
                return false;
            }

            var unavailableMaterials = materials.Where(m => m.AvailableQuantity < m.RequiredQuantity);
            if (unavailableMaterials.Any())
            {
                MessageBox.Show("Недостаточно материалов для производства. Проверьте наличие на складе.");
                return false;
            }

            return true;
        }

        private int CreateProductionDocument()
        {
            string query = @"
                INSERT INTO production_documents (docnumber, productiondate, productid, quantity, totalcost, responsible, status)
                VALUES (@docNumber, @productionDate, @productId, @quantity, @totalCost, @responsible, 'В производстве')
                RETURNING documentid";

            var parameters = new NpgsqlParameter[]
            {
                new NpgsqlParameter("@docNumber", txtDocNumber.Text),
                new NpgsqlParameter("@productionDate", dpProductionDate.SelectedDate ?? DateTime.Now),
                new NpgsqlParameter("@productId", selectedProductId),
                new NpgsqlParameter("@quantity", decimal.Parse(txtQuantity.Text)),
                new NpgsqlParameter("@totalCost", totalCost),
                new NpgsqlParameter("@responsible", txtResponsible.Text)
            };

            return Convert.ToInt32(database.GetScalarValue(query, parameters));
        }

        private void WriteOffMaterials(int documentId)
        {
            foreach (var material in materials)
            {
                string query = @"
                    INSERT INTO material_writeoffs (documentid, materialtype, materialname, quantity, cost, writeoffdate)
                    VALUES (@documentId, @materialType, @materialName, @quantity, @cost, @writeoffDate)";

                var unitCost = material.AvailableQuantity > 0 ? material.TotalCost / material.AvailableQuantity : 0;
                var totalMaterialCost = unitCost * material.RequiredQuantity;

                var parameters = new NpgsqlParameter[]
                {
                    new NpgsqlParameter("@documentId", documentId),
                    new NpgsqlParameter("@materialType", material.MaterialType),
                    new NpgsqlParameter("@materialName", material.MaterialName),
                    new NpgsqlParameter("@quantity", material.RequiredQuantity),
                    new NpgsqlParameter("@cost", totalMaterialCost),
                    new NpgsqlParameter("@writeoffDate", DateTime.Now)
                };

                database.ExecuteQuery(query, parameters);
            }
        }

        private void UpdateWarehouseStocks()
        {
            // Обновление остатков тканей и фурнитуры будет реализовано
            // в зависимости от структуры базы данных
        }
    }

    // Вспомогательные классы
    public class ProductItem
    {
        public int Id { get; set; }
        public string Article { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
    }

    public class ProductionMaterial
    {
        public string MaterialType { get; set; }
        public string MaterialName { get; set; }
        public decimal RequiredQuantity { get; set; }
        public string UnitName { get; set; }
        public decimal AvailableQuantity { get; set; }
        public string Status { get; set; }
        public decimal TotalCost { get; set; }
    }
}
