using Npgsql;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace UchPR
{
    public partial class OrderWindow : Window
    {
        #region Поля и свойства
        private DataBase database;
        private ObservableCollection<OrderItem> orderItems;
        private string currentUserLogin;
        private string currentUserRole;
        private int? currentOrderNumber;
        private DateTime currentOrderDate;
        private string currentStatus;

        #endregion

        #region Конструктор и инициализация
        public OrderWindow(string userLogin, string userRole)
        {
            InitializeComponent();
            database = new DataBase();
            currentUserLogin = userLogin;
            currentUserRole = userRole;
            currentOrderDate = DateTime.Today;

            orderItems = new ObservableCollection<OrderItem>();
            dgOrderItems.ItemsSource = orderItems;

            txtCurrentUser.Text = $"{userLogin} ({userRole})";
            txtOrderDate.Text = DateTime.Today.ToString("dd.MM.yyyy");

            // Если открывается существующий заказ, здесь будет логика его загрузки,
            // которая установит currentOrderNumber и currentStatus.
            // Например: LoadExistingOrder(orderId);

            UpdateTotals();
            SetupCustomerControls(); // Управляем видимостью кнопок
            orderItems.CollectionChanged += (s, e) => UpdateTotals();
        }

        // Метод для управления видимостью и доступностью кнопок в зависимости от статуса заказа
        private void SetupCustomerControls()
        {
            // Редактировать заказ можно только если он "Новый" или еще не сохранен
            bool isEditable = (currentStatus == "Новый" || string.IsNullOrEmpty(currentStatus));

            dgOrderItems.IsReadOnly = !isEditable;
            btnSelectProducts.IsEnabled = isEditable;
            btnOpenConstructor.IsEnabled = isEditable;
            btnSaveOrder.Visibility = isEditable ? Visibility.Visible : Visibility.Collapsed;
            btnSubmitOrder.Visibility = isEditable ? Visibility.Visible : Visibility.Collapsed;

            // Кнопка оплаты видна, только когда заказ имеет статус "К оплате"
            btnPayForOrder.Visibility = (currentStatus == "К оплате") ? Visibility.Visible : Visibility.Collapsed;
        }
        #endregion

        #region Обработчики событий UI
        // НОВЫЙ МЕТОД: Открывает окно выбора изделий
        private void BtnSelectProducts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var productSelectionWindow = new ProductSelectionWindow();
                if (productSelectionWindow.ShowDialog() == true)
                {
                    foreach (var selectedItem in productSelectionWindow.SelectedOrderItems)
                    {
                        var existingItem = orderItems.FirstOrDefault(oi => oi.ProductArticle == selectedItem.ProductArticle);
                        if (existingItem != null)
                        {
                            existingItem.Quantity += selectedItem.Quantity;
                        }
                        else
                        {
                            if (selectedItem.Quantity > selectedItem.AvailableQuantity)
                            {
                                MessageBox.Show("Нельзя добавить больше, чем есть на складе.");
                            }
                            else
                            {
                                // --- ДОБАВЛЯЕМ ЗДЕСЬ ---
                                // Получаем размеры изделия из БД
                                string query = "SELECT width, length FROM product WHERE article = @article";
                                var dt = database.GetData(query, new[] { new NpgsqlParameter("@article", selectedItem.ProductArticle) });
                                if (dt.Rows.Count > 0)
                                {
                                    selectedItem.Width = SafeDataReader.GetSafeDecimal(dt.Rows[0], "width");
                                    selectedItem.Length = SafeDataReader.GetSafeDecimal(dt.Rows[0], "length");
                                }
                                orderItems.Add(selectedItem);
                            }
                        }
                    }
                    UpdateTotals();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка выбора товаров: {ex.Message}");
            }
        }

        private void BtnRemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is OrderItem item)
            {
                orderItems.Remove(item);
            }
        }

        private void BtnOpenConstructor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Для конструктора нужно передать данные текущего пользователя
                var constructorWindow = new ProductDesignerWindow(currentUserLogin, GetUserPassword(currentUserLogin));
                if (constructorWindow.ShowDialog() == true)
                {
                    MessageBox.Show("Изделие создано! Теперь вы можете добавить его в заказ через каталог.",
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка открытия конструктора: {ex.Message}");
            }
        }

        private void BtnSaveOrder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ValidateOrder())
                {
                    SaveOrder("Новый");
                    MessageBox.Show("Заказ сохранен как черновик.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения заказа: {ex.Message}");
            }
        }

        private void BtnSubmitOrder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ValidateOrder())
                {
                    var result = MessageBox.Show("Отправить заказ на проверку менеджеру?\nПосле отправки изменения будут ограничены.",
                        "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        SaveOrder("На проверке");
                        MessageBox.Show("Заказ отправлен на проверку менеджеру.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                        int orderNumber = currentOrderNumber ?? 0;
                        var cuttingWindow = new CuttingWindow(orderNumber, currentOrderDate, orderItems.ToList());
                        cuttingWindow.ShowDialog();
                        this.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка отправки заказа: {ex.Message}");
            }
        }

        private void BtnPayForOrder_Click(object sender, RoutedEventArgs e)
        {
            UpdateOrderStatus("Оплачен");
            MessageBox.Show("Заказ успешно оплачен и скоро будет передан в производство.", "Оплата прошла", MessageBoxButton.OK, MessageBoxImage.Information);
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        #endregion

        #region Вспомогательные методы
        private void UpdateTotals()
        {
            int itemCount = orderItems.Count;
            int totalQuantity = orderItems.Sum(x => x.Quantity);
            decimal totalCost = orderItems.Sum(x => x.TotalPrice);

            txtItemCount.Text = itemCount.ToString();
            txtTotalQuantity.Text = totalQuantity.ToString();
            txtTotalCost.Text = $"{totalCost:N2}"; // Формат без символа валюты
        }

        private void UpdateOrderStatus(string newStatus)
        {
            if (currentOrderNumber.HasValue)
            {
                string query = "UPDATE orders SET execution_stage = @status::order_status WHERE number = @number AND date = @date";
                database.ExecuteNonQuery(query, new[] {
                    new NpgsqlParameter("@status", newStatus),
                    new NpgsqlParameter("@number", currentOrderNumber.Value),
                    new NpgsqlParameter("@date", currentOrderDate)
                });
                currentStatus = newStatus;
                SetupCustomerControls(); // Обновляем интерфейс
            }
        }

        private bool ValidateOrder()
        {
            if (orderItems.Count == 0)
            {
                MessageBox.Show("Добавьте хотя бы одно изделие в заказ.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            foreach (var item in orderItems)
            {
                if (item.Quantity > item.AvailableQuantity)
                {
                    MessageBox.Show($"Недостаточно товара на складе для '{item.ProductName}'.\nТребуется: {item.Quantity}, в наличии: {item.AvailableQuantity}.", "Нехватка товара", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }
            return true;
        }

        private void SaveOrder(string status)
        {
            using (var connection = new NpgsqlConnection(database.connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        decimal totalCost = orderItems.Sum(x => x.TotalPrice);
                        string userPassword = GetUserPassword(currentUserLogin, connection, transaction);

                        if (currentOrderNumber == null)
                        {
                            string insertOrderQuery = @"
                                INSERT INTO orders (date, execution_stage, customer_login, customer_password, cost)
                                VALUES (@date, @status::order_status, @customer_login, @customer_password, @cost)
                                RETURNING number";

                            using (var cmd = new NpgsqlCommand(insertOrderQuery, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@date", currentOrderDate);
                                cmd.Parameters.AddWithValue("@status", status);
                                cmd.Parameters.AddWithValue("@customer_login", currentUserLogin);
                                cmd.Parameters.AddWithValue("@customer_password", userPassword);
                                cmd.Parameters.AddWithValue("@cost", totalCost);
                                currentOrderNumber = (int)cmd.ExecuteScalar();
                            }
                        }
                        else
                        {
                            string updateOrderQuery = @"
                                UPDATE orders SET execution_stage = @status::order_status, cost = @cost
                                WHERE number = @number AND date = @date";
                            database.ExecuteNonQueryWithTransaction(updateOrderQuery, connection, transaction, new[] {
                                new NpgsqlParameter("@status", status),
                                new NpgsqlParameter("@cost", totalCost),
                                new NpgsqlParameter("@number", currentOrderNumber.Value),
                                new NpgsqlParameter("@date", currentOrderDate)
                            });

                            string deleteItemsQuery = "DELETE FROM orderedproducts WHERE order_number = @number AND order_date = @date";
                            database.ExecuteNonQueryWithTransaction(deleteItemsQuery, connection, transaction, new[] {
                                new NpgsqlParameter("@number", currentOrderNumber.Value),
                                new NpgsqlParameter("@date", currentOrderDate)
                            });
                        }

                        foreach (var item in orderItems)
                        {
                            string insertItemQuery = @"
                                INSERT INTO orderedproducts (order_number, order_date, product_article, quantity)
                                VALUES (@order_number, @order_date, @product_article, @quantity)";
                            database.ExecuteNonQueryWithTransaction(insertItemQuery, connection, transaction, new[] {
                                new NpgsqlParameter("@order_number", currentOrderNumber.Value),
                                new NpgsqlParameter("@order_date", currentOrderDate),
                                new NpgsqlParameter("@product_article", item.ProductArticle),
                                new NpgsqlParameter("@quantity", item.Quantity)
                            });
                        }

                        transaction.Commit();

                        txtOrderNumber.Text = currentOrderNumber.ToString();
                        txtOrderStatus.Text = status;
                        currentStatus = status;
                        SetupCustomerControls();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private void LoadOrderItems()
        {
            if (currentOrderNumber == null)
                return;

            string itemsQuery = @"
        SELECT 
            op.product_article, 
            pn.name as product_name, 
            op.quantity, 
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
        new NpgsqlParameter("@number", currentOrderNumber.Value),
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
                    UnitPrice = SafeDataReader.GetSafeDecimal(row, "unit_price"),
                    Width = SafeDataReader.GetSafeDecimal(row, "width"),
                    Length = SafeDataReader.GetSafeDecimal(row, "length")
                });
            }
        }

        private string GetUserPassword(string login, NpgsqlConnection connection = null, NpgsqlTransaction transaction = null)
        {
            string query = "SELECT password FROM users WHERE login = @login";
            if (connection != null && transaction != null)
            {
                return database.ExecuteScalarWithTransaction(query, connection, transaction, new[] { new NpgsqlParameter("@login", login) })?.ToString() ?? "";
            }
            else
            {
                return database.ExecuteScalar(query, new[] { new NpgsqlParameter("@login", login) })?.ToString() ?? "";
            }
        }
        #endregion

    }
     
}
