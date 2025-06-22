using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace UchPR
{
    public partial class ThresholdSettingsWindow : Window
    {
        private readonly DataBase db = new DataBase();
        private List<ProductThresholdSettingsItem> thresholds;

        public ThresholdSettingsWindow()
        {
            InitializeComponent();
            LoadThresholds();
        }

        private void LoadThresholds()
        {
            thresholds = db.GetProductsForThresholdSettings();
            dgThresholds.ItemsSource = thresholds;
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            bool hasErrors = false;
            var errorMessages = new List<string>();

            foreach (var item in thresholds)
            {
                try
                {
                    if (item.ScrapThreshold < 0)
                    {
                        errorMessages.Add($"Изделие {item.ProductName}: порог не может быть отрицательным");
                        hasErrors = true;
                        continue;
                    }

                    if (!db.UpdateProductScrapThreshold(item.Article, item.ScrapThreshold))
                    {
                        errorMessages.Add($"Не удалось сохранить настройки для изделия {item.ProductName}");
                        hasErrors = true;
                    }
                }
                catch (Exception ex)
                {
                    errorMessages.Add($"Ошибка при сохранении {item.ProductName}: {ex.Message}");
                    hasErrors = true;
                }
            }

            if (!hasErrors)
            {
                MessageBox.Show("Настройки успешно сохранены!", "Успех",
                               MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
            else
            {
                string allErrors = string.Join("\n", errorMessages);
                MessageBox.Show($"При сохранении произошли ошибки:\n\n{allErrors}",
                               "Ошибки сохранения", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    // Пример модели для DataGrid
    public class ProductThresholdSettingsItem
    {
        public string ProductName { get; set; }
        public string Article { get; set; }
        public decimal ScrapThreshold { get; set; }
        public string UnitName { get; set; }
    }
}
