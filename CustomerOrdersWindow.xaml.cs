using Npgsql;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows;
using System.Windows.Input;

namespace UchPR
{
    public partial class CustomerOrdersWindow : Window
    {
        private DataBase database;
        private string customerLogin;
        private ObservableCollection<CustomerOrder> customerOrders;

        public CustomerOrdersWindow(string login)
        {
            InitializeComponent();
            database = new DataBase();
            customerLogin = login;

            customerOrders = new ObservableCollection<CustomerOrder>();
            dgOrders.ItemsSource = customerOrders;

            LoadCustomerOrders();
        }
        private void dgOrders_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgOrders.SelectedItem is CustomerOrder selectedOrder)
            {
                var detailsWindow = new CustomerOrderDetailsWindow(selectedOrder.OrderNumber, selectedOrder.OrderDate);
                detailsWindow.Owner = this;
                detailsWindow.ShowDialog();
            }
        }
        private void LoadCustomerOrders()
        {
            try
            {
                string query = @"SELECT number, date, cost, execution_stage 
                                 FROM orders 
                                 WHERE customer_login = @login 
                                 ORDER BY date DESC";

                var parameters = new[] { new NpgsqlParameter("@login", customerLogin) };
                var data = database.GetData(query, parameters);

                customerOrders.Clear();
                foreach (DataRow row in data.Rows)
                {
                    customerOrders.Add(new CustomerOrder
                    {
                        OrderNumber = (int)row["number"],
                        OrderDate = (DateTime)row["date"],
                        TotalCost = SafeDataReader.GetSafeDecimal(row, "cost"),
                        Status = row["execution_stage"].ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки заказов: {ex.Message}");
            }
        }
        private void BtnPay_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is CustomerOrder orderToPay)
            {
                var result = MessageBox.Show($"Вы уверены, что хотите оплатить заказ №{orderToPay.OrderNumber} на сумму {orderToPay.TotalCost:C}?",
                                             "Подтверждение оплаты", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        string updateQuery = @"UPDATE orders 
                                               SET execution_stage = 'Оплачен'::order_status 
                                               WHERE number = @number AND date = @date";

                        var parameters = new[] {
                            new NpgsqlParameter("@number", orderToPay.OrderNumber),
                            new NpgsqlParameter("@date", orderToPay.OrderDate)
                        };

                        database.ExecuteNonQuery(updateQuery, parameters);

                        MessageBox.Show("Заказ успешно оплачен!", "Оплата прошла", MessageBoxButton.OK, MessageBoxImage.Information);

                        // Обновляем список, чтобы скрыть кнопку и показать новый статус
                        LoadCustomerOrders();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при оплате заказа: {ex.Message}");
                    }
                }
            }
        }
    }

    public class CustomerOrder
    {
        public int OrderNumber { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal TotalCost { get; set; }
        public string Status { get; set; }
    }
}
