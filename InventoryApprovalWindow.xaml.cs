using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Npgsql;

namespace UchPR
{
    public partial class InventoryApprovalWindow : Window
    {
        private DataBase database;
        private ObservableCollection<InventoryDocument> documents;
        private ObservableCollection<DiscrepancySummary> summaryData;
        private ObservableCollection<LargeDiscrepancyItem> largeDiscrepancies;
        private InventoryDocument selectedDocument;

        public InventoryApprovalWindow()
        {
            InitializeComponent();
            database = new DataBase();
            LoadDocuments();
            SetupFilters();
        }

        private void LoadDocuments()
        {
            // Загружаем только документы, ожидающие утверждения руководителя
            string query = @"
                SELECT document_id, document_number, inventory_date, responsible_person, status,
                       total_discrepancy_amount, discrepancy_percentage
                FROM inventory_documents
                WHERE status = 'Черновик'
                ORDER BY inventory_date DESC";
            var data = database.GetData(query);
            documents = new ObservableCollection<InventoryDocument>();
            foreach (System.Data.DataRow row in data.Rows)
            {
                documents.Add(new InventoryDocument
                {
                    DocumentId = Convert.ToInt32(row["document_id"]),
                    DocumentNumber = row["document_number"].ToString(),
                    InventoryDate = Convert.ToDateTime(row["inventory_date"]),
                    ResponsiblePerson = row["responsible_person"].ToString(),
                    Status = row["status"].ToString(),
                    TotalDiscrepancyAmount = Convert.ToDecimal(row["total_discrepancy_amount"]),
                    DiscrepancyPercentage = Convert.ToDecimal(row["discrepancy_percentage"])
                });
            }
            dgDocuments.ItemsSource = documents;
        }

        private void SetupFilters()
        {
            cbStatusFilter.ItemsSource = new string[]
            {
                "Все", "Ожидает утверждения руководителя", "Утверждено директором", "Отклонено директором"
            };
            cbStatusFilter.SelectedIndex = 0;
            cbStatusFilter.SelectionChanged += (s, e) => ApplyFilters();
            tbSearch.TextChanged += (s, e) => ApplyFilters();
        }

        private void ApplyFilters()
        {
            var filtered = documents.AsEnumerable();
            if (cbStatusFilter.SelectedItem != null && cbStatusFilter.SelectedItem.ToString() != "Все")
                filtered = filtered.Where(d => d.Status == cbStatusFilter.SelectedItem.ToString());
            if (!string.IsNullOrWhiteSpace(tbSearch.Text))
                filtered = filtered.Where(d =>
                   d.DocumentNumber != null && d.DocumentNumber.ToLower().Contains(tbSearch.Text.ToLower()));
            dgDocuments.ItemsSource = new ObservableCollection<InventoryDocument>(filtered);
        }

        private void DgDocuments_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedDocument = dgDocuments.SelectedItem as InventoryDocument;
            if (selectedDocument != null)
            {
                LoadSummary();
                LoadLargeDiscrepancies();
                txtDirectorComment.Text = "";
            }
        }

        private void LoadSummary()
        {
            summaryData = new ObservableCollection<DiscrepancySummary>();
            // Пример: загрузить сводку по категориям
            summaryData.Add(GetCategorySummary(selectedDocument.DocumentId, "Ткани", "inventory_fabric_lines", "difference_amount"));
            summaryData.Add(GetCategorySummary(selectedDocument.DocumentId, "Фурнитура", "inventory_accessory_lines", "difference_amount"));
            summaryData.Add(GetCategorySummary(selectedDocument.DocumentId, "Изделия", "inventory_product_lines", "difference_amount"));
            dgSummary.ItemsSource = summaryData.Where(s => s.ItemCount > 0);
        }

        private DiscrepancySummary GetCategorySummary(int docId, string category, string table, string amountField)
        {
            string sql = $@"SELECT COUNT(*) as cnt, COALESCE(SUM(ABS({amountField})),0) as sum
                            FROM {table} WHERE document_id = @docId AND difference_quantity != 0";
            using (var conn = new NpgsqlConnection(database.connectionString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@docId", docId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int cnt = Convert.ToInt32(reader["cnt"]);
                            decimal sum = Convert.ToDecimal(reader["sum"]);
                            decimal total = selectedDocument.TotalDiscrepancyAmount == 0 ? 0 : (sum / selectedDocument.TotalDiscrepancyAmount) * 100;
                            return new DiscrepancySummary
                            {
                                Category = category,
                                ItemCount = cnt,
                                DiscrepancyAmount = sum,
                                PercentageOfTotal = total
                            };
                        }
                    }
                }
            }
            return new DiscrepancySummary { Category = category };
        }

        private void LoadLargeDiscrepancies()
        {
            largeDiscrepancies = new ObservableCollection<LargeDiscrepancyItem>();
            // Пример: крупные расхождения по всем категориям (сумма > 1000)
            LoadLargeDiscrepancyCategory(selectedDocument.DocumentId, "Ткани", "inventory_fabric_lines", "fabric_article", "difference_quantity", "difference_amount");
            LoadLargeDiscrepancyCategory(selectedDocument.DocumentId, "Фурнитура", "inventory_accessory_lines", "accessory_article", "difference_quantity", "difference_amount");
            LoadLargeDiscrepancyCategory(selectedDocument.DocumentId, "Изделия", "inventory_product_lines", "product_article", "difference_quantity", "difference_amount");
            dgLargeDiscrepancies.ItemsSource = largeDiscrepancies;
        }

        private void LoadLargeDiscrepancyCategory(int docId, string category, string table, string articleField, string diffQtyField, string diffAmountField)
        {
            string sql = $@"SELECT {articleField} as article, {diffQtyField} as diff, {diffAmountField} as amount
                            FROM {table}
                            WHERE document_id = @docId AND ABS({diffAmountField}) > 1";
            using (var conn = new NpgsqlConnection(database.connectionString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@docId", docId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            largeDiscrepancies.Add(new LargeDiscrepancyItem
                            {
                                Article = reader["article"].ToString(),
                                Name = "", // Можно дополнительно подгрузить название
                                Category = category,
                                Difference = Convert.ToDecimal(reader["diff"]),
                                Amount = Convert.ToDecimal(reader["amount"])
                            });
                        }
                    }
                }
            }
        }

        private void BtnApprove_Click(object sender, RoutedEventArgs e)
        {
            if (selectedDocument == null)
                return;
            if (string.IsNullOrWhiteSpace(txtDirectorComment.Text))
            {
                MessageBox.Show("Комментарий обязателен для утверждения!");
                return;
            }
            UpdateDocumentStatus(selectedDocument.DocumentId, true, txtDirectorComment.Text);
            selectedDocument.Status = "Утверждено директором";
            ApplyFilters();
            MessageBox.Show("Документ утвержден.");
        }

        private void BtnReject_Click(object sender, RoutedEventArgs e)
        {
            if (selectedDocument == null)
                return;
            UpdateDocumentStatus(selectedDocument.DocumentId, false, txtDirectorComment.Text);
            selectedDocument.Status = "Отклонено директором";
            ApplyFilters();
            MessageBox.Show("Документ отклонен и возвращен на доработку.");
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadDocuments();
            ApplyFilters();
        }

        private void UpdateDocumentStatus(int docId, bool approved, string comment)
        {
            string sql = @"UPDATE inventory_documents
                           SET director_approved = @approved,
                               director_approval_date = @date,
                               status = @status,
                               director_comment = @comment
                           WHERE document_id = @docId";
            using (var conn = new NpgsqlConnection(database.connectionString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@approved", approved);
                    cmd.Parameters.AddWithValue("@date", DateTime.Now);
                    cmd.Parameters.AddWithValue("@status", approved ? "Завершен" : "Отклонено");
                    cmd.Parameters.AddWithValue("@comment", comment ?? "");
                    cmd.Parameters.AddWithValue("@docId", docId);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }


    public class DiscrepancySummary
    {
        public string Category { get; set; }
        public int ItemCount { get; set; }
        public decimal DiscrepancyAmount { get; set; }
        public decimal PercentageOfTotal { get; set; }
    }

    public class LargeDiscrepancyItem
    {
        public string Article { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public decimal Difference { get; set; }
        public decimal Amount { get; set; }
    }
}
