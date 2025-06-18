using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Npgsql;

namespace UchPR
{
    public partial class OrderWindow : Window
    {
        private DataBase database;
        private ObservableCollection<OrderItem> orderItems;
        private ObservableCollection<CatalogProductItem> availableProducts;
        private string currentUserLogin;
        private string currentUserRole;
        private int? currentOrderNumber;
        private DateTime currentOrderDate;

        private readonly IUserSessionService _userSessionService;

        public OrderWindow(string userLogin, string userRole, IUserSessionService userSessionService = null)
        {
            InitializeComponent();
            database = new DataBase();
            currentUserLogin = userLogin;
            currentUserRole = userRole;
            currentOrderDate = DateTime.Today;
            _userSessionService = userSessionService ?? new UserSessionService();

            orderItems = new ObservableCollection<OrderItem>();
            availableProducts = new ObservableCollection<CatalogProductItem>();

            dgOrderItems.ItemsSource = orderItems;
            cbProducts.ItemsSource = availableProducts;

            txtCurrentUser.Text = $"{userLogin} ({userRole})";

            LoadAvailableProducts();
            UpdateTotals();

            // Подписка на изменения в заказе
            orderItems.CollectionChanged += (s, e) => UpdateTotals();
        }

        private void LoadAvailableProducts()
        {
            try
            {
                string query = @"
                    SELECT 
                        p.article,
                        pn.name,
                        p.price,
                        COALESCE(SUM(pw.quantity), 0) AS available_quantity
                    FROM product p
                    LEFT JOIN productname pn ON p.name_id = pn.id
                    LEFT JOIN productwarehouse pw ON p.article = pw.product_article
                    GROUP BY p.article, pn.name, p.price
                    HAVING COALESCE(SUM(pw.quantity), 0) > 0
                    ORDER BY pn.name";

                var data = database.GetData(query);
                availableProducts.Clear();

                foreach (DataRow row in data.Rows)
                {
                    availableProducts.Add(new CatalogProductItem
                    {
                        Article = row["article"].ToString(),
                        Name = $"Арт. {row["article"]} - {row["name"]}",
                        Price = SafeDataReader.GetSafeDecimal(row, "price"),
                        AvailableQuantity = SafeDataReader.GetSafeInt32(row, "available_quantity")
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки товаров: {ex.Message}");
            }
        }

        private void CbProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbProducts.SelectedItem is CatalogProductItem product)
            {
                // Автоматически устанавливаем количество 1
                txtQuantity.Text = "1";
            }
        }

        private void BtnAddProduct_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (cbProducts.SelectedItem is CatalogProductItem selectedProduct)
                {
                    if (!int.TryParse(txtQuantity.Text, out int quantity) || quantity <= 0)
                    {
                        MessageBox.Show("Введите корректное количество", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (quantity > selectedProduct.AvailableQuantity)
                    {
                        MessageBox.Show($"На складе доступно только {selectedProduct.AvailableQuantity} шт.",
                            "Недостаточно товара", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Проверяем, есть ли уже такой товар в заказе
                    var existingItem = orderItems.FirstOrDefault(x => x.ProductArticle == selectedProduct.Article);
                    if (existingItem != null)
                    {
                        existingItem.Quantity += quantity;
                    }
                    else
                    {
                        orderItems.Add(new OrderItem
                        {
                            ProductArticle = selectedProduct.Article,
                            ProductName = selectedProduct.Name,
                            Quantity = quantity,
                            UnitPrice = selectedProduct.Price,
                            AvailableQuantity = selectedProduct.AvailableQuantity
                        });
                    }

                    // Сбрасываем выбор
                    cbProducts.SelectedIndex = -1;
                    txtQuantity.Text = "1";
                }
                else
                {
                    MessageBox.Show("Выберите изделие для добавления", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка добавления товара: {ex.Message}");
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
                string userLogin = GetCurrentUserLogin(); // Реализуйте этот метод
                string userPassword = GetCurrentUserPassword(); //
                var constructorWindow = new ProductDesignerWindow(userLogin, userPassword);
                if (constructorWindow.ShowDialog() == true)
                {
                    // После создания изделия в конструкторе, обновляем список доступных товаров
                    LoadAvailableProducts();
                    MessageBox.Show("Изделие создано! Теперь вы можете добавить его в заказ.",
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка открытия конструктора: {ex.Message}");
            }
        }

        private string GetCurrentUserLogin()
        {
            return _userSessionService.GetCurrentUserLogin();
        }

        private string GetCurrentUserPassword()
        {
            return _userSessionService.GetCurrentUserPassword();
        }


        private void UpdateTotals()
        {
            int itemCount = orderItems.Count;
            int totalQuantity = orderItems.Sum(x => x.Quantity);
            decimal totalCost = orderItems.Sum(x => x.TotalPrice);

            txtItemCount.Text = itemCount.ToString();
            txtTotalQuantity.Text = totalQuantity.ToString();
            txtTotalCost.Text = $"{totalCost:N2} руб.";
        }

        private void BtnSaveOrder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ValidateOrder())
                {
                    SaveOrder("Новый");
                    MessageBox.Show("Заказ сохранен", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
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
                        MessageBox.Show("Заказ отправлен на проверку менеджеру", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        this.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка отправки заказа: {ex.Message}");
            }
        }

        private bool ValidateOrder()
        {
            if (orderItems.Count == 0)
            {
                MessageBox.Show("Добавьте хотя бы одно изделие в заказ", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Проверяем наличие товаров на складе
            foreach (var item in orderItems)
            {
                var product = availableProducts.FirstOrDefault(p => p.Article == item.ProductArticle);
                if (product == null || item.Quantity > product.AvailableQuantity)
                {
                    MessageBox.Show($"Недостаточно товара на складе: {item.ProductName}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
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

                        // Получаем пароль пользователя
                        string userPassword = GetUserPassword(currentUserLogin, connection, transaction);

                        if (currentOrderNumber == null)
                        {
                            // Создаем новый заказ
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
                            // Обновляем существующий заказ
                            string updateOrderQuery = @"
                                UPDATE orders 
                                SET execution_stage = @status::order_status, cost = @cost
                                WHERE number = @number AND date = @date";

                            using (var cmd = new NpgsqlCommand(updateOrderQuery, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@status", status);
                                cmd.Parameters.AddWithValue("@cost", totalCost);
                                cmd.Parameters.AddWithValue("@number", currentOrderNumber);
                                cmd.Parameters.AddWithValue("@date", currentOrderDate);
                                cmd.ExecuteNonQuery();
                            }

                            // Удаляем старые позиции заказа
                            string deleteItemsQuery = @"
                                DELETE FROM orderedproducts 
                                WHERE order_number = @number AND order_date = @date";

                            using (var cmd = new NpgsqlCommand(deleteItemsQuery, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@number", currentOrderNumber);
                                cmd.Parameters.AddWithValue("@date", currentOrderDate);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // Сохраняем позиции заказа
                        foreach (var item in orderItems)
                        {
                            string insertItemQuery = @"
                                INSERT INTO orderedproducts (order_number, order_date, product_article, quantity)
                                VALUES (@order_number, @order_date, @product_article, @quantity)";

                            using (var cmd = new NpgsqlCommand(insertItemQuery, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@order_number", currentOrderNumber);
                                cmd.Parameters.AddWithValue("@order_date", currentOrderDate);
                                cmd.Parameters.AddWithValue("@product_article", item.ProductArticle);
                                cmd.Parameters.AddWithValue("@quantity", item.Quantity);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();

                        // Обновляем интерфейс
                        txtOrderNumber.Text = currentOrderNumber.ToString();
                        txtOrderStatus.Text = status;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private string GetUserPassword(string login, NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            string query = "SELECT password FROM users WHERE login = @login";
            using (var cmd = new NpgsqlCommand(query, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@login", login);
                return cmd.ExecuteScalar()?.ToString() ?? "";
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    #region Модели данных
    public class CatalogProductItem
    {
        public string Article { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int AvailableQuantity { get; set; }
    }
    public class OrderItem : INotifyPropertyChanged
    {
        private int _quantity;

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

    #endregion
}
