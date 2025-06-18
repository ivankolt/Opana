using Npgsql;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows;
using System.Windows.Input;

namespace UchPR
{
    public partial class ManagerOrdersList : Window
    {
        private DataBase database;
        private string managerLogin;
        private string managerPassword;
        private ObservableCollection<OrderSummaryItem> newOrders;

        public ManagerOrdersList(string login, string password)
        {
            InitializeComponent();
            database = new DataBase();
            managerLogin = login;
            managerPassword = password;

            newOrders = new ObservableCollection<OrderSummaryItem>();
            dgNewOrders.ItemsSource = newOrders;

            LoadNewOrders();
        }

        private void LoadNewOrders()
        {
            try
            {
                // Загружаем:
                // 1. Все новые заказы, ожидающие проверки (manager_login IS NULL).
                // 2. Все заказы, назначенные на ТЕКУЩЕГО менеджера, которые еще НЕ завершены.
                string query = @"
            SELECT number, date, customer_login, cost, execution_stage, manager_login 
            FROM orders 
            WHERE execution_stage <> 'Готов'::order_status 
              AND (manager_login IS NULL OR manager_login = @managerLogin)";

                var parameters = new[] { new NpgsqlParameter("@managerLogin", managerLogin) };
                var data = database.GetData(query, parameters);
                newOrders.Clear();

                foreach (DataRow row in data.Rows)
                {
                    newOrders.Add(new OrderSummaryItem
                    {
                        OrderNumber = (int)row["number"],
                        OrderDate = (DateTime)row["date"],
                        CustomerLogin = row["customer_login"].ToString(),
                        TotalCost = SafeDataReader.GetSafeDecimal(row, "cost"),
                        Status = row["execution_stage"].ToString(),
                        // Добавляем информацию о менеджере для отображения
                        ManagerLogin = row["manager_login"]?.ToString() ?? "Нет"
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки заказов: {ex.Message}");
            }
        }
        private void DgNewOrders_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgNewOrders.SelectedItem is OrderSummaryItem selectedOrder)
            {
                bool isOrderClaimed = false;

                // Если у заказа еще нет менеджера, пытаемся его "захватить"
                if (string.IsNullOrEmpty(selectedOrder.ManagerLogin) || selectedOrder.ManagerLogin == "Нет")
                {
                    isOrderClaimed = ClaimOrder(selectedOrder.OrderNumber, selectedOrder.OrderDate);
                    if (!isOrderClaimed)
                    {
                        MessageBox.Show("Этот заказ только что был взят другим менеджером.", "Заказ занят");
                        LoadNewOrders();
                        return;
                    }
                }
                // Если заказ уже назначен на ТЕКУЩЕГО менеджера, просто открываем его
                else if (selectedOrder.ManagerLogin == this.managerLogin)
                {
                    isOrderClaimed = true;
                }

                if (isOrderClaimed)
                {
                    var orderDetailsWindow = new ManagerOrderDetails(selectedOrder.OrderNumber, selectedOrder.OrderDate, managerLogin, managerPassword);
                    orderDetailsWindow.ShowDialog();
                    LoadNewOrders(); // Обновляем список после закрытия окна деталей
                }
            }
        }
        private bool ClaimOrder(int orderNumber, DateTime orderDate)
        {
            try
            {
                // Пытаемся назначить себя менеджером только если заказ еще не занят
                string query = @"UPDATE orders 
                                 SET manager_login = @manager_login, manager_password = @manager_password
                                 WHERE number = @number AND date = @date AND manager_login IS NULL";

                int rowsAffected = database.ExecuteNonQuery(query, new[] {
                    new NpgsqlParameter("@manager_login", managerLogin),
                    new NpgsqlParameter("@manager_password", managerPassword),
                    new NpgsqlParameter("@number", orderNumber),
                    new NpgsqlParameter("@date", orderDate)
                });

                // Если была обновлена 1 строка, значит, мы успешно "заняли" заказ
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка назначения менеджера: {ex.Message}");
                return false;
            }
        }
    }

    // Модель для отображения в DataGrid
    public class OrderSummaryItem
    {
        public int OrderNumber { get; set; }
        public DateTime OrderDate { get; set; }
        public string CustomerLogin { get; set; }
        public decimal TotalCost { get; set; }
        public string Status { get; set; }

        // Добавьте это свойство
        public string ManagerLogin { get; set; }
    }
}
