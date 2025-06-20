using Npgsql;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;

namespace UchPR
{
    public partial class ManagerOrderDetails : Window
    {
        private DataBase database;
        private int currentOrderNumber;
        private DateTime currentOrderDate;
        private string currentStatus;
        private string managerLogin;
        private ObservableCollection<OrderItem> orderItems;
        
        public ManagerOrderDetails(int orderNumber, DateTime orderDate, string managerLogin, string managerPassword)
        {
            InitializeComponent();
            database = new DataBase();
            currentOrderNumber = orderNumber;
            currentOrderDate = orderDate;
            this.managerLogin = managerLogin;

            orderItems = new ObservableCollection<OrderItem>();
            dgOrderItems.ItemsSource = orderItems;

            LoadOrderDetails();
            SetupManagerControls();
        }

        private void LoadOrderDetails()
        {
            try
            {
                // Загрузка основной информации о заказе (включая статус)
                string headerQuery = "SELECT execution_stage FROM orders WHERE number = @number AND date = @date";
                var headerParams = new[] {
    new NpgsqlParameter("@number", currentOrderNumber),
    new NpgsqlParameter("@date", currentOrderDate)
};
                var headerData = database.GetData(headerQuery, headerParams);

                if (headerData.Rows.Count > 0)
                {
                    currentStatus = headerData.Rows[0]["execution_stage"].ToString();
                }
                else
                {
                    currentStatus = "Новый"; // или выбросьте исключение, если это критично
                }

                string itemsQuery = @"
            SELECT 
                op.product_article, 
                pn.name as product_name, 
                op.quantity, 
                -- Расчет средней себестоимости со склада как цены за единицу
                COALESCE((SELECT AVG(pw.production_cost) 
                          FROM productwarehouse pw 
                          WHERE pw.product_article = op.product_article), 0) as unit_price,
            p.width,
            p.length
            FROM orderedproducts op
            JOIN product p ON op.product_article = p.article
            JOIN productname pn ON p.name_id = pn.id
            WHERE op.order_number = @number AND op.order_date = @date";

                var itemsParams = new[] {
            new NpgsqlParameter("@number", currentOrderNumber),
            new NpgsqlParameter("@date", currentOrderDate)
        };
                var itemsData = database.GetData(itemsQuery, itemsParams);
                orderItems.Clear();

                foreach (DataRow row in itemsData.Rows)
                {
                    orderItems.Add(new OrderItem
                    {
                        ProductArticle = row["product_article"].ToString(),
                        ProductName = row["product_name"].ToString(),
                        Quantity = (int)row["quantity"],
                        // Теперь цена подставляется корректно
                        UnitPrice = SafeDataReader.GetSafeDecimal(row, "unit_price"),
                         Width = SafeDataReader.GetSafeDecimal(row, "width"),
        Length = SafeDataReader.GetSafeDecimal(row, "length")
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки деталей заказа: {ex.Message}");
            }
        }

        private void SetupManagerControls()
        {
            // Кнопка "Далее" активна только если статус НЕ "Готов" и НЕ "К оплате"
            btnNextStep.IsEnabled = currentStatus != "Готов" && currentStatus != "К оплате";
            btnRejectOrder.IsEnabled = currentStatus != "Готов";

            switch (currentStatus)
            {
                case "На проверке":
                    btnNextStep.Content = "✅ Подтвердить (к оплате)";
                    break;
                case "К оплате":
                    btnNextStep.Content = "⏳ Ожидание оплаты";
                    break;
                case "Оплачен":
                    btnNextStep.Content = "▶️ Начать производство";
                    break;
                case "Обработка":
                    btnNextStep.Content = "✂️ В раскрой";
                    break;
                case "Раскрой":
                    btnNextStep.Content = "⚙️ В производство";
                    break;
                case "Производство":
                    btnNextStep.Content = "🏁 Завершить (Готов)";
                    break;
                case "Готов":
                    btnNextStep.Content = "✔️ Завершено";
                    break;
                default:
                    btnNextStep.Content = "Далее";
                    break;
            }
        }

        private void BtnNextStep_Click(object sender, RoutedEventArgs e)
        {
            // Определяем следующий статус
            string nextStatus = null;

            Console.WriteLine(currentStatus);
            switch (currentStatus)
            {
                case "На проверке": nextStatus = "К оплате"; break;
                case "Оплачен": nextStatus = "Обработка"; break;
                case "Обработка": nextStatus = "Раскрой"; break;
                case "Раскрой": nextStatus = "Производство"; break;
                case "Производство": nextStatus = "Готов"; break;
            }

            if (string.IsNullOrEmpty(nextStatus))
            {
                MessageBox.Show("Дальнейший переход невозможен. Заказ уже завершен или ожидает оплаты.");
                return;
            }

            if (nextStatus == "Раскрой")
            {
                // Передаём номер заказа, дату и список изделий
                var cuttingWindow = new CuttingWindow(currentOrderNumber, currentOrderDate, orderItems.ToList());
                cuttingWindow.ShowDialog();

                // После закрытия окна раскроя можно обновить статус, если нужно
                // UpdateOrderStatus("Раскрой", ...);
                // return; // или продолжить, если статус должен меняться после раскроя
            }

            using (var connection = new NpgsqlConnection(database.connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Если переход с "Оплачен" на "Обработка", списываем товары со склада
                        if (currentStatus == "Оплачен")
                        {
                            if (!DeductProductsFromWarehouse(connection, transaction))
                                throw new Exception("Не удалось списать товары со склада из-за нехватки остатков.");
                        }

                        UpdateOrderStatus(nextStatus, connection, transaction);
                        transaction.Commit();
                        MessageBox.Show($"Статус заказа обновлен на '{nextStatus}'.");
                        this.Close();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        MessageBox.Show($"Ошибка: {ex.Message}", "Операция отменена", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        private string GetNextStatus(string currentStatus)
        {
            string[] statusFlow = { "На проверке", "К оплате", "Оплачен", "Обработка", "Раскрой", "Производство", "Готов" };
            int idx = Array.IndexOf(statusFlow, currentStatus);
            if (idx >= 0 && idx + 1 < statusFlow.Length)
                return statusFlow[idx + 1];
            return null;
        }

        private void BtnRejectOrder_Click(object sender, RoutedEventArgs e)
        {
            using (var connection = new NpgsqlConnection(database.connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    UpdateOrderStatus("Отклонен", connection, transaction);
                    transaction.Commit();
                    MessageBox.Show("Заказ был отклонен.");
                    this.Close();
                }
            }
        }

        private bool DeductProductsFromWarehouse(NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            // 1. Проверяем, достаточно ли товаров на складе для всего заказа
            foreach (var item in orderItems)
            {
                string checkStockQuery = "SELECT COALESCE(SUM(quantity), 0) FROM productwarehouse WHERE product_article = @article";
                int availableQuantity = Convert.ToInt32(database.ExecuteScalarWithTransaction(checkStockQuery, connection, transaction, new[] { new NpgsqlParameter("@article", item.ProductArticle) }));

                if (availableQuantity < item.Quantity)
                {
                    MessageBox.Show($"Недостаточно товара на складе для '{item.ProductName}' (Арт: {item.ProductArticle}).\nТребуется: {item.Quantity}, в наличии: {availableQuantity}.", "Нехватка товара", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            // 2. Если товаров достаточно, списываем их по принципу FIFO
            foreach (var item in orderItems)
            {
                int quantityToDeduct = item.Quantity;

                string selectBatchesQuery = "SELECT batch_id, quantity FROM productwarehouse WHERE product_article = @article ORDER BY production_date ASC";
                var batches = database.GetDataWithTransaction(selectBatchesQuery, connection, transaction, new[] { new NpgsqlParameter("@article", item.ProductArticle) });

                foreach (DataRow batch in batches.Rows)
                {
                    if (quantityToDeduct <= 0) break;

                    int batchId = (int)batch["batch_id"];
                    int batchQuantity = (int)batch["quantity"];
                    int deductFromThisBatch = Math.Min(quantityToDeduct, batchQuantity);

                    if (deductFromThisBatch == batchQuantity)
                    {
                        // Списываем всю партию
                        string deleteQuery = "DELETE FROM productwarehouse WHERE batch_id = @batch_id";
                        database.ExecuteNonQueryWithTransaction(deleteQuery, connection, transaction, new[] { new NpgsqlParameter("@batch_id", batchId) });
                    }
                    else
                    {
                        // Уменьшаем количество в партии
                        string updateQuery = "UPDATE productwarehouse SET quantity = quantity - @deduct WHERE batch_id = @batch_id";
                        database.ExecuteNonQueryWithTransaction(updateQuery, connection, transaction, new[] {
                            new NpgsqlParameter("@deduct", deductFromThisBatch),
                            new NpgsqlParameter("@batch_id", batchId)
                        });
                    }

                    quantityToDeduct -= deductFromThisBatch;
                }
            }
            return true;
        }

        private void UpdateOrderStatus(string newStatus, NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            string query = "UPDATE orders SET execution_stage = @status::order_status WHERE number = @number AND date = @date";
            database.ExecuteNonQueryWithTransaction(query, connection, transaction, new[] {
                new NpgsqlParameter("@status", newStatus),
                new NpgsqlParameter("@number", currentOrderNumber),
                new NpgsqlParameter("@date", currentOrderDate)
            });
            currentStatus = newStatus;
        }
    }
    public class OrderItem : INotifyPropertyChanged
    {
        private int _quantity;
        public decimal Width { get; set; }
        public decimal Length { get; set; }

        public string ProductArticle { get; set; }
        public string ProductName { get; set; }
        public decimal UnitPrice { get; set; }
        public int AvailableQuantity { get; set; }

        public int Quantity
        {
            get => _quantity;
            set
            {
                _quantity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalPrice));
            }
        }

        public decimal TotalPrice => Quantity * UnitPrice;

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
