using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Npgsql;

namespace UchPR
{
    public partial class DocumentHistoryWindow : Window
    {
        private DataBase database;
        private ObservableCollection<DocumentHistoryItem> _documents;
        private ObservableCollection<DocumentHistoryItem> _allDocuments;

        public DocumentHistoryWindow()
        {
            InitializeComponent();
            database = new DataBase();
            InitializeData();
        }

        private void InitializeData()
        {
            try
            {
                LoadDocuments();
                SetupFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetupFilters()
        {
            // Добавляем варианты фильтрации по статусу
            StatusFilterComboBox.Items.Clear();
            StatusFilterComboBox.Items.Add(new ComboBoxItem { Content = "Все документы" });
            StatusFilterComboBox.Items.Add(new ComboBoxItem { Content = "Сохранен как черновик" });
            StatusFilterComboBox.Items.Add(new ComboBoxItem { Content = "Принят к учету" });
            StatusFilterComboBox.SelectedIndex = 0;
        }

        private void LoadDocuments()
        {
            try
            {
                string query = @"
                    SELECT id, document_name, document_type, status, created_date, 
                           modified_date, file_path, file_size, description, 
                           created_by, modified_by
                    FROM document_history 
                    WHERE document_type = 'Поступление материалов'
                    ORDER BY created_date DESC";

                var data = database.GetData(query);
                var documents = new List<DocumentHistoryItem>();

                foreach (DataRow row in data.Rows)
                {
                    var doc = new DocumentHistoryItem
                    {
                        Id = Convert.ToInt32(row["id"]),
                        DocumentName = row["document_name"].ToString(),
                        DocumentType = row["document_type"].ToString(),
                        Status = row["status"].ToString(),
                        CreatedDate = Convert.ToDateTime(row["created_date"]),
                        ModifiedDate = Convert.ToDateTime(row["modified_date"]),
                        FilePath = row["file_path"]?.ToString(),
                        FileSize = row["file_size"] != DBNull.Value ? Convert.ToInt64(row["file_size"]) : 0,
                        Description = row["description"]?.ToString(),
                        CreatedBy = row["created_by"]?.ToString(),
                        ModifiedBy = row["modified_by"]?.ToString()
                    };

                    documents.Add(doc);
                }

                _allDocuments = new ObservableCollection<DocumentHistoryItem>(documents);
                _documents = new ObservableCollection<DocumentHistoryItem>(documents);
                DocumentsDataGrid.ItemsSource = _documents;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки документов: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilters()
        {
            if (_allDocuments == null) return;

            try
            {
                var selectedStatus = ((ComboBoxItem)StatusFilterComboBox.SelectedItem)?.Content?.ToString();
                var searchText = SearchTextBox?.Text?.ToLower() ?? "";

                var filteredDocuments = _allDocuments.Where(doc =>
                {
                    // Фильтр по статусу
                    bool statusMatch = selectedStatus == "Все документы" ||
                                     doc.Status == selectedStatus;

                    // Фильтр по тексту поиска
                    bool textMatch = string.IsNullOrEmpty(searchText) ||
                                   doc.DocumentName.ToLower().Contains(searchText) ||
                                   doc.Description?.ToLower().Contains(searchText) == true;

                    return statusMatch && textMatch;
                });

                _documents.Clear();
                foreach (var doc in filteredDocuments)
                {
                    _documents.Add(doc);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка фильтрации: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ViewButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedDocument = DocumentsDataGrid.SelectedItem as DocumentHistoryItem;
            if (selectedDocument == null)
            {
                MessageBox.Show("Выберите документ для просмотра.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Открываем окно с деталями документа
     
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии документа: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AcceptDraftButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedDocument = DocumentsDataGrid.SelectedItem as DocumentHistoryItem;
            if (selectedDocument == null)
            {
                MessageBox.Show("Выберите документ.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (selectedDocument.Status != "Сохранен как черновик")
            {
                MessageBox.Show("Можно принять к учету только черновики.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Принять документ '{selectedDocument.DocumentName}' к учету?",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    AcceptDocumentToAccounting(selectedDocument);
                    LoadDocuments(); // Перезагружаем список
                    MessageBox.Show("Документ принят к учету.", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void AcceptDocumentToAccounting(DocumentHistoryItem document)
        {
            // Здесь нужно реализовать логику принятия к учету:
            // 1. Обновить статус документа в истории
            // 2. Применить изменения к складским остаткам
            // 3. Обновить соответствующий документ поступления

            using (var connection = new NpgsqlConnection(database.connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Обновляем статус в истории
                        string updateHistoryQuery = @"
                            UPDATE document_history 
                            SET status = 'Принят к учету', modified_date = @modified_date
                            WHERE id = @id";

                        using (var cmd = new NpgsqlCommand(updateHistoryQuery, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@modified_date", DateTime.Now);
                            cmd.Parameters.AddWithValue("@id", document.Id);
                            cmd.ExecuteNonQuery();
                        }

                        // Здесь добавить логику обновления складских остатков
                        // если это необходимо

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedDocument = DocumentsDataGrid.SelectedItem as DocumentHistoryItem;
            if (selectedDocument == null)
            {
                MessageBox.Show("Выберите документ для удаления.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Удалить документ '{selectedDocument.DocumentName}' из истории?",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    string deleteQuery = "DELETE FROM document_history WHERE id = @id";

                    using (var connection = new NpgsqlConnection(database.connectionString))
                    {
                        connection.Open();
                        using (var cmd = new NpgsqlCommand(deleteQuery, connection))
                        {
                            cmd.Parameters.AddWithValue("@id", selectedDocument.Id);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    _documents.Remove(selectedDocument);
                    _allDocuments.Remove(selectedDocument);

                    MessageBox.Show("Документ удален из истории.", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
