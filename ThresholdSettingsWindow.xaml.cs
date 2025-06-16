// File: ThresholdSettingsWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace UchPR
{
    public partial class ThresholdSettingsWindow : Window
    {
        private readonly DataBase db = new DataBase();
        private readonly string materialType;
        private List<ThresholdSettingsItem> thresholds;

        public ThresholdSettingsWindow(string materialType)
        {
            InitializeComponent();
            this.materialType = materialType;
            LoadThresholds();
        }

        private void LoadThresholds()
        {
            thresholds = db.GetMaterialsForThresholdSettings(materialType);
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
                    // Проверяем корректность введенного значения
                    if (item.ScrapThreshold < 0)
                    {
                        errorMessages.Add($"Материал {item.MaterialName}: порог не может быть отрицательным");
                        hasErrors = true;
                        continue;
                    }

                    if (!db.UpdateScrapThreshold(item.Article, item.ScrapThreshold, materialType))
                    {
                        errorMessages.Add($"Не удалось сохранить настройки для материала {item.MaterialName}");
                        hasErrors = true;
                    }
                }
                catch (Exception ex)
                {
                    errorMessages.Add($"Ошибка при сохранении {item.MaterialName}: {ex.Message}");
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
}
