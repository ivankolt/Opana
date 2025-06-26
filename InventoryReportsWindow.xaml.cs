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
            LoadWriteOffReport();
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
        private ObservableCollection<WriteOffReportItem> writeOffItems;

        private void LoadWriteOffReport()
        {
            writeOffItems = new ObservableCollection<WriteOffReportItem>();

            DateTime? fromDate = dpStart.SelectedDate;
            DateTime? toDate = dpEnd.SelectedDate;

            string sql = @"
        SELECT
            mw.materialtype AS type,
            mw.materialname AS name,
            mw.quantity AS quantity,
            uom.name AS unit,
            mw.cost AS cost,
            mw.writeoffdate AS date
       
        FROM material_writeoffs mw
        LEFT JOIN unitofmeasurement uom ON uom.code = (
            SELECT unit_id FROM material_units
            WHERE material_article = mw.materialname AND material_type = mw.materialtype AND is_primary = true LIMIT 1
        )
        WHERE (@fromDate IS NULL OR mw.writeoffdate >= @fromDate)
          AND (@toDate IS NULL OR mw.writeoffdate <= @toDate)

        UNION ALL

        SELECT
            CASE
                WHEN p.article IS NOT NULL THEN 'Изделие'
                WHEN f.article IS NOT NULL THEN 'Ткань'
                WHEN a.article IS NOT NULL THEN 'Фурнитура'
                ELSE 'Неизвестно'
            END AS type,
            COALESCE(pn.name, fn.name, fan.name, sl.material_article) AS name,
            sl.quantity_scrapped AS quantity,
            uom.name AS unit,
            sl.cost_scrapped AS cost,
            sl.log_date AS date
        FROM scraplog sl
        LEFT JOIN product p ON p.article = sl.material_article
        LEFT JOIN productname pn ON p.name_id = pn.id
        LEFT JOIN fabric f ON f.article::text = sl.material_article
        LEFT JOIN fabricname fn ON f.name_id = fn.code
        LEFT JOIN accessory a ON a.article::text = sl.material_article
        LEFT JOIN furnitureaccessoryname fan ON a.name_id = fan.id
        LEFT JOIN unitofmeasurement uom ON uom.code = sl.unit_of_measurement_id
        WHERE (@fromDate IS NULL OR sl.log_date >= @fromDate)
          AND (@toDate IS NULL OR sl.log_date <= @toDate)
        ORDER BY date DESC, type, name;
        ";

            var dt = database.GetData(sql, new[] {
        new NpgsqlParameter("@fromDate", (object)fromDate ?? DBNull.Value),
        new NpgsqlParameter("@toDate", (object)toDate ?? DBNull.Value)
    });

            foreach (DataRow row in dt.Rows)
            {
                writeOffItems.Add(new WriteOffReportItem
                {
                    Type = row["type"].ToString(),
                    Name = row["name"].ToString(),
                    Quantity = Convert.ToDecimal(row["quantity"]),
                    Unit = row["unit"].ToString(),
                    Cost = Convert.ToDecimal(row["cost"]),
                    Date = Convert.ToDateTime(row["date"])
                   
                });
            }
            dgWriteOffs.ItemsSource = writeOffItems;
        }

        private void LoadMovement()
        {
            movementItems = new ObservableCollection<StockMovementItem>();

            string sql = @"
SELECT
    t.type,
    t.article,
    t.name,
    t.unit,
    -- Начальный остаток (до периода)
    COALESCE((
        SELECT SUM(COALESCE(rl.quantity,0)) - SUM(COALESCE(mc.quantity,0))
        FROM receiptlines rl
        JOIN receipts r ON rl.receiptid = r.receiptid
        LEFT JOIN material_consumption mc ON mc.material_article = rl.materialid AND mc.material_type = rl.materialtype AND mc.consumption_date < '2025-01-01'
        WHERE rl.materialtype = t.type AND rl.materialid = t.article AND r.docdate < '2025-01-01'
    ), 0) AS start_qty,
    -- Приход за период
    COALESCE((
        SELECT SUM(rl.quantity)
        FROM receiptlines rl
        JOIN receipts r ON rl.receiptid = r.receiptid
        WHERE rl.materialtype = t.type AND rl.materialid = t.article AND r.docdate >= '2025-01-01' AND r.docdate <= '2025-07-01'
    ), 0) AS incoming,
    -- Расход за период
    COALESCE((
        SELECT SUM(mc.quantity)
        FROM material_consumption mc
        WHERE mc.material_type = t.type AND mc.material_article = t.article AND mc.consumption_date >= '2025-01-01' AND mc.consumption_date <= '2025-07-01'
    ), 0) AS outgoing,
    -- Конечный остаток (старт + приход - расход)
    (
        COALESCE((
            SELECT SUM(COALESCE(rl.quantity,0)) - SUM(COALESCE(mc.quantity,0))
            FROM receiptlines rl
            JOIN receipts r ON rl.receiptid = r.receiptid
            LEFT JOIN material_consumption mc ON mc.material_article = rl.materialid AND mc.material_type = rl.materialtype AND mc.consumption_date < '2025-01-01'
            WHERE rl.materialtype = t.type AND rl.materialid = t.article AND r.docdate < '2025-01-01'
        ), 0)
        +
        COALESCE((
            SELECT SUM(rl.quantity)
            FROM receiptlines rl
            JOIN receipts r ON rl.receiptid = r.receiptid
            WHERE rl.materialtype = t.type AND rl.materialid = t.article AND r.docdate >= '2025-01-01' AND r.docdate <= '2025-07-01'
        ), 0)
        -
        COALESCE((
            SELECT SUM(mc.quantity)
            FROM material_consumption mc
            WHERE mc.material_type = t.type AND mc.material_article = t.article AND mc.consumption_date >= '2025-01-01' AND mc.consumption_date <= '2025-07-01'
        ), 0)
    ) AS end_qty
FROM (
    -- Все ткани
    SELECT 'fabric' AS type, CAST(f.article AS varchar) AS article, fn.name AS name, u.name AS unit
    FROM fabric f
    JOIN fabricname fn ON f.name_id = fn.code
    JOIN unitofmeasurement u ON f.unit_of_measurement_id = u.code

    UNION ALL

    -- Все фурнитуры
    SELECT 'accessory' AS type, a.article, fan.name, u.name
    FROM accessory a
    JOIN furnitureaccessoryname fan ON a.name_id = fan.id
    JOIN unitofmeasurement u ON a.unit_of_measurement_id = u.code
) t
WHERE
    (
        COALESCE((
            SELECT SUM(rl.quantity)
            FROM receiptlines rl
            JOIN receipts r ON rl.receiptid = r.receiptid
            WHERE rl.materialtype = t.type AND rl.materialid = t.article
        ), 0) > 0
        OR
        COALESCE((
            SELECT SUM(mc.quantity)
            FROM material_consumption mc
            WHERE mc.material_type = t.type AND mc.material_article = t.article
        ), 0) > 0
        OR
        (
            COALESCE((
                SELECT SUM(rl.quantity)
                FROM receiptlines rl
                JOIN receipts r ON rl.receiptid = r.receiptid
                WHERE rl.materialtype = t.type AND rl.materialid = t.article
            ), 0)
            -
            COALESCE((
                SELECT SUM(mc.quantity)
                FROM material_consumption mc
                WHERE mc.material_type = t.type AND mc.material_article = t.article
            ), 0)
        ) != 0
    )
ORDER BY t.type, t.name;
    ";

            var dt = database.GetData(sql);

            foreach (DataRow row in dt.Rows)
            {
                string type = row["type"].ToString();
                string unit = row["unit"].ToString();

                // Если это аксессуар, всегда "шт."
                if (type == "accessory")
                    unit = "шт.";

                movementItems.Add(new StockMovementItem
                {
                    Type = type,
                    Article = row["article"].ToString(),
                    Name = row["name"].ToString(),
                    Unit = unit,
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
    public class WriteOffReportItem
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public decimal Cost { get; set; }
        public DateTime Date { get; set; }
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
