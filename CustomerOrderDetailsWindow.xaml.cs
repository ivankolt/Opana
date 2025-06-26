using Npgsql;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows;


namespace UchPR
{
    /// <summary>
    /// Логика взаимодействия для CustomerOrderDetailsWindow.xaml
    /// </summary>
    public partial class CustomerOrderDetailsWindow : Window
    {
        private readonly DataBase database = new DataBase();

        public int OrderNumber { get; }
        public DateTime OrderDate { get; }
        public string ManagerLogin { get; set; }
        public string Status { get; set; }

        public CustomerOrderDetailsWindow(int orderNumber, DateTime orderDate)
        {
            InitializeComponent();
            OrderNumber = orderNumber;
            OrderDate = orderDate;

            LoadOrderInfo();
            LoadProducts();
            LoadCuttings();
        }

        private void LoadOrderInfo()
        {
            string sql = @"SELECT manager_login, execution_stage FROM orders WHERE number=@number AND date=@date";
            var dt = database.GetData(sql, new[] {
            new NpgsqlParameter("@number", OrderNumber),
            new NpgsqlParameter("@date", OrderDate)
        });
            if (dt.Rows.Count > 0)
            {
                ManagerLogin = dt.Rows[0]["manager_login"]?.ToString() ?? "";
                Status = dt.Rows[0]["execution_stage"]?.ToString() ?? "";
            }
            DataContext = this;
        }
      
        private void BtnShowCutting_Click(object sender, RoutedEventArgs e)
        {
            var products = dgProducts.ItemsSource as IEnumerable<OrderProductItem>;
            if (products == null) return;

            var orderItems = products.Select(p => new OrderItem
            {
                ProductArticle = p.ProductArticle,
                ProductName = p.ProductName,
                Quantity = p.Quantity,
                Length = (decimal)GetProductLength(p.ProductArticle),
                Width = (decimal)GetProductWidth(p.ProductArticle)
            }).ToList();

            // Открываем окно визуализации
            var cuttingWindow = new CuttingWindow(OrderNumber, OrderDate, orderItems, true);
            cuttingWindow.Owner = this;
            cuttingWindow.ShowDialog();
        }

        private double GetProductLength(string article)
        {
            var dt = database.GetData("SELECT length FROM product WHERE article=@a", new[] { new NpgsqlParameter("@a", article) });
            return dt.Rows.Count > 0 ? Convert.ToDouble(dt.Rows[0]["length"]) : 0;
        }
        private double GetProductWidth(string article)
        {
            var dt = database.GetData("SELECT width FROM product WHERE article=@a", new[] { new NpgsqlParameter("@a", article) });
            return dt.Rows.Count > 0 ? Convert.ToDouble(dt.Rows[0]["width"]) : 0;
        }

        private void LoadProducts()
        {
            string sql = @"
            SELECT op.product_article, pn.name AS product_name, op.quantity
            FROM orderedproducts op
            JOIN product p ON p.article = op.product_article
            JOIN productname pn ON p.name_id = pn.id
            WHERE op.order_number = @number AND op.order_date = @date";
            var dt = database.GetData(sql, new[] {
            new NpgsqlParameter("@number", OrderNumber),
            new NpgsqlParameter("@date", OrderDate)
        });

            var products = new ObservableCollection<OrderProductItem>();
            foreach (DataRow row in dt.Rows)
            {
                products.Add(new OrderProductItem
                {
                    ProductArticle = row["product_article"].ToString(),
                    ProductName = row["product_name"].ToString(),
                    Quantity = Convert.ToInt32(row["quantity"])
                });
            }
            dgProducts.ItemsSource = products;
        }

        private void LoadCuttings()
        {
            string sql = @"
            SELECT product_article, length, width
            FROM productcuts
            WHERE order_number = @number AND order_date = @date
            ORDER BY product_article, cut_index";
            var dt = database.GetData(sql, new[] {
            new NpgsqlParameter("@number", OrderNumber),
            new NpgsqlParameter("@date", OrderDate)
        });

            var cuttings = new ObservableCollection<ProductCutItem>();
            foreach (DataRow row in dt.Rows)
            {
                cuttings.Add(new ProductCutItem
                {
                    ProductArticle = row["product_article"].ToString(),
                    Length = Convert.ToDecimal(row["length"]),
                    Width = Convert.ToDecimal(row["width"])
                });
            }
            dgCuttings.ItemsSource = cuttings;
        }
    }

    public class OrderProductItem
    {
        public string ProductArticle { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
    }

    public class ProductCutItem
    {
        public string ProductArticle { get; set; }
        public decimal Length { get; set; }
        public decimal Width { get; set; }
    }

}
