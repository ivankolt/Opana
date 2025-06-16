using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace UchPR
{
    public partial class DocumentHistoryWindow : Window
    {
      //  private DocumentHistoryRepository _repository;
        private ObservableCollection<DocumentHistoryItem> _documents;

        public DocumentHistoryWindow()
        {
            InitializeComponent();
            InitializeRepository();
        }

        private void InitializeRepository()
        {
            try
            {
                // Используйте вашу строку подключения к базе данных
            //    string connectionString = "Data Source=UchPR.db;Version=3;";
             //   _repository = new DocumentHistoryRepository(connectionString);
                LoadDocuments();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации базы данных: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadDocuments()
        {
            try
            {
                var documents = _repository.GetAllDocuments();
                _documents = new ObservableCollection<DocumentHistoryItem>(documents);
                DocumentsDataGrid.ItemsSource = _documents;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки документов: {ex.Message}", "Ошибка",
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

        private void ApplyFilters()
        {
            if (_repository == null) return;

            try
            {
                var selectedStatus = ((ComboBoxItem)StatusFilterComboBox.SelectedItem)?.Content?.ToString();
                var searchText = SearchTextBox?.Text;

                string statusFilter = (selectedStatus == "Все документы") ? null : selectedStatus;

                var filteredDocuments = _repository.SearchDocuments(searchText, statusFilter);
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
                if (!string.IsNullOrEmpty(selectedDocument.FilePath) && File.Exists(selectedDocument.FilePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = selectedDocument.FilePath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    // Если файл не найден, попробуем открыть из базы данных
                    var documentData = _repository.GetDocumentData(selectedDocument.Id);
                    if (documentData != null && documentData.Length > 0)
                    {
                        // Создаем временный файл и открываем его
                        string tempPath = Path.GetTempFileName();
                        File.WriteAllBytes(tempPath, documentData);

                        Process.Start(new ProcessStartInfo
                        {
                            FileName = tempPath,
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        MessageBox.Show("Документ не найден.", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии документа: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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
                $"Вы уверены, что хотите удалить документ '{selectedDocument.DocumentName}'?",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _repository.DeleteDocument(selectedDocument.Id);
                    _documents.Remove(selectedDocument);

                    MessageBox.Show("Документ успешно удалён.", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении документа: {ex.Message}", "Ошибка",
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
