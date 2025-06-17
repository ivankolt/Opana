using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Npgsql;

namespace UchPR
{
    public partial class InventoryReportsWindow : Window
    {
        private DataBase database;
        private ObservableCollection<StockRemainItem> remainItems;
        private ObservableCollection<StockMovementItem> movementItems;

        public InventoryReportsWindow()
        {
            InitializeComponent();
            database = new DataBase();
            dpStart.SelectedDate = DateTime.Today.AddMonths(-1);
            dpEnd.SelectedDate = DateTime.Today;
            cbTypeFilter.ItemsSource = new[] { "Все", "Ткань", "Фурнитура", "Изделие" };
            cbTypeFilter.SelectedIndex = 0;
            LoadRemains();
        }

        // Загрузка остатков
        private void LoadRemains()
        {
            remainItems = new ObservableCollection<StockRemainItem>();

            string sql = @"
        SELECT 'Ткань' AS type, 
               CAST(f.article AS varchar) AS article, 
               fn.name AS name, 
               u.name AS unit, 
               SUM(fw.length * fw.width) AS quantity, 
               SUM(fw.total_cost) AS total_cost
        FROM fabricwarehouse fw
        JOIN fabric f ON fw.fabric_article = f.article
        JOIN fabricname fn ON f.name_id = fn.code
        JOIN unitofmeasurement u ON f.unit_of_measurement_id = u.code
        GROUP BY f.article, fn.name, u.name

        UNION ALL

        SELECT 'Фурнитура', 
               a.article, 
               fan.name, 
               u.name, 
               SUM(aw.quantity), 
               SUM(aw.total_cost)
        FROM accessorywarehouse aw
        JOIN accessory a ON aw.accessory_article = a.article
        JOIN furnitureaccessoryname fan ON a.name_id = fan.id
        JOIN unitofmeasurement u ON a.unit_of_measurement_id = u.code
        GROUP BY a.article, fan.name, u.name

        UNION ALL

        SELECT 'Изделие', 
               p.article, 
               pn.name, 
               u.name, 
               SUM(pw.quantity), 
               SUM(pw.total_cost)
        FROM productwarehouse pw
        JOIN product p ON pw.product_article = p.article
        JOIN productname pn ON p.name_id = pn.id
        JOIN unitofmeasurement u ON p.unit_of_measurement_id = u.code
        GROUP BY p.article, pn.name, u.name
        ORDER BY type, name";

            var dt = database.GetData(sql);
            foreach (DataRow row in dt.Rows)
            {
                remainItems.Add(new StockRemainItem
                {
                    Type = row["type"].ToString(),
                    Article = row["article"].ToString(),
                    Name = row["name"].ToString(),
                    Unit = row["unit"].ToString(),
                    Quantity = Convert.ToDecimal(row["quantity"]),
                    TotalCost = Convert.ToDecimal(row["total_cost"])
                });
            }
            dgRemains.ItemsSource = remainItems;
        }
        // Загрузка движения за период
        private void LoadMovement()
        {
            if (dpStart.SelectedDate == null || dpEnd.SelectedDate == null) return;

            DateTime dateStart = dpStart.SelectedDate.Value.Date;
            DateTime dateEnd = dpEnd.SelectedDate.Value.Date;

            movementItems = new ObservableCollection<StockMovementItem>();

            string sql = @"
        SELECT 'Ткань' AS type, 
               CAST(f.article AS varchar) AS article, 
               fn.name AS name, 
               u.name AS unit,
               0 AS start_qty,
               0 AS incoming,
               0 AS outgoing,
               0 AS end_qty
        FROM fabric f
        JOIN fabricname fn ON f.name_id = fn.code
        JOIN unitofmeasurement u ON f.unit_of_measurement_id = u.code

        UNION ALL

        SELECT 'Фурнитура', 
               a.article, 
               fan.name, 
               u.name,
               0 AS start_qty,
               0 AS incoming,
               0 AS outgoing,
               0 AS end_qty
        FROM accessory a
        JOIN furnitureaccessoryname fan ON a.name_id = fan.id
        JOIN unitofmeasurement u ON a.unit_of_measurement_id = u.code

        UNION ALL

        SELECT 'Изделие', 
               p.article, 
               pn.name, 
               u.name,
               0 AS start_qty,
               0 AS incoming,
               0 AS outgoing,
               0 AS end_qty
        FROM product p
        JOIN productname pn ON p.name_id = pn.id
        JOIN unitofmeasurement u ON p.unit_of_measurement_id = u.code
        ORDER BY type, name";

            var dt = database.GetData(sql, new[] {
        new NpgsqlParameter("@dateStart", dateStart),
        new NpgsqlParameter("@dateEnd", dateEnd)
    });

            foreach (DataRow row in dt.Rows)
            {
                movementItems.Add(new StockMovementItem
                {
                    Type = row["type"].ToString(),
                    Article = row["article"].ToString(),
                    Name = row["name"].ToString(),
                    Unit = row["unit"].ToString(),
                    StartQty = Convert.ToDecimal(row["start_qty"]),
                    Incoming = Convert.ToDecimal(row["incoming"]),
                    Outgoing = Convert.ToDecimal(row["outgoing"]),
                    EndQty = Convert.ToDecimal(row["end_qty"])
                });
            }
            dgMovement.ItemsSource = movementItems;
        }

        private void BtnShow_Click(object sender, RoutedEventArgs e)
        {
            if (((TabItem)tabMain.SelectedItem).Header.ToString() == "Остатки")
            {
                LoadRemains();
            }
            else
            {
                LoadMovement();
            }
        }

        private void CbTypeFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Можно реализовать фильтрацию по типу (Ткань/Фурнитура/Изделие)
            // Например, фильтрация по remainItems или movementItems
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            // Для печати используйте FlowDocument или FastReport
            MessageBox.Show("Печать реализуется через FlowDocument или FastReport.");
        }
    }

    // Модели для привязки к DataGrid
    public class StockRemainItem
    {
        public string Type { get; set; }
        public string Article { get; set; }
        public string Name { get; set; }
        public string Unit { get; set; }
        public decimal Quantity { get; set; }
        public decimal TotalCost { get; set; }
    }

    public class StockMovementItem
    {
        public string Type { get; set; }
        public string Article { get; set; }
        public string Name { get; set; }
        public string Unit { get; set; }
        public decimal StartQty { get; set; }
        public decimal Incoming { get; set; }
        public decimal Outgoing { get; set; }
        public decimal EndQty { get; set; }
    }
}
