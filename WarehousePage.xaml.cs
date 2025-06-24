using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace UchPR
{
    public partial class WarehousePage : Page
    {
        private readonly DataBase db = new DataBase();
        private readonly string _userRole;

        public WarehousePage(string userRole)
        {
            InitializeComponent();
            _userRole = userRole;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            SetPermissions();
            FillMaterialTypeCombo();
            cmbMaterialType.SelectedIndex = 0;
        }

        private void FillMaterialTypeCombo()
        {
            cmbMaterialType.Items.Clear();
            cmbMaterialType.Items.Add(new ComboBoxItem { Content = "Ткани" });
            cmbMaterialType.Items.Add(new ComboBoxItem { Content = "Фурнитура" });
            if (_userRole == "Руководитель" || _userRole == "Менеджер")
                cmbMaterialType.Items.Add(new ComboBoxItem { Content = "Продукция" });
        }
        private void SetPermissions()
        {
            if (_userRole == "Кладовщик")
            {
                btnSetThreshold.Visibility = Visibility.Visible;
            }
            else
            {
                btnSetThreshold.Visibility = Visibility.Collapsed;
            }
        }

        private void cmbMaterialType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;

            string selectedType = (cmbMaterialType.SelectedItem as ComboBoxItem)?.Content.ToString();
            if (selectedType == "Ткани")
            {
                dgFabrics.Visibility = Visibility.Visible;
                dgAccessories.Visibility = Visibility.Collapsed;
                dgProducts.Visibility = Visibility.Collapsed;
                LoadFabricWarehouseData();
            }
            else if (selectedType == "Фурнитура")
            {
                dgFabrics.Visibility = Visibility.Collapsed;
                dgAccessories.Visibility = Visibility.Visible;
                dgProducts.Visibility = Visibility.Collapsed;
                LoadAccessoryWarehouseData();
            }
            else if (selectedType == "Продукция")
            {
                dgFabrics.Visibility = Visibility.Collapsed;
                dgAccessories.Visibility = Visibility.Collapsed;
                dgProducts.Visibility = Visibility.Visible;
                LoadProductWarehouseData();
            }
        }

        private void LoadProductWarehouseData()
        {
            try
            {
                var productData = db.GetProductWarehouseData();
                dgProducts.ItemsSource = productData;
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных склада продукции: {ex.Message}");
            }
        }
        private void ShowFabricWarehouse()
        {
            dgFabrics.Visibility = Visibility.Visible;
            dgAccessories.Visibility = Visibility.Collapsed;
            LoadFabricWarehouseData();
        }

        private void ShowAccessoryWarehouse()
        {
            dgFabrics.Visibility = Visibility.Collapsed;
            dgAccessories.Visibility = Visibility.Visible;
            LoadAccessoryWarehouseData();
        }

        private void LoadFabricWarehouseData()
        {
            try
            {
                var fabricData = db.GetFabricWarehouseData();
                dgFabrics.ItemsSource = fabricData;
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных склада тканей: {ex.Message}");
            }
        }

        private void LoadAccessoryWarehouseData()
        {
            try
            {
                var accessoryData = db.GetAccessoryWarehouseData();
                dgAccessories.ItemsSource = accessoryData;
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных склада фурнитуры: {ex.Message}");
            }
        }

        private void btnSetThreshold_Click(object sender, RoutedEventArgs e)
        {
            // Открываем окно настройки порогов для изделий
            var thresholdWindow = new ThresholdSettingsWindow();
            thresholdWindow.ShowDialog();

            
        }

        private void btnScrapLog_Click(object sender, RoutedEventArgs e)
        {
            var scrapLogWindow = new ScrapLogWindow();
            scrapLogWindow.ShowDialog();
        }
    }

    // Модель данных для склада тканей
    public class FabricWarehouseItem : INotifyPropertyChanged
    {
        public int Roll { get; set; }
        public int FabricArticle { get; set; }
        public string FabricName { get; set; }
        public decimal Width { get; set; }
        public decimal Length { get; set; }
        public decimal Area => Width * Length;
        public decimal PurchasePrice { get; set; }
        public decimal TotalCost { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ProductWarehouseItem : INotifyPropertyChanged
    {
        public int BatchId { get; set; }
        public string ProductArticle { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal ProductionCost { get; set; }
        public decimal TotalCost { get; set; }
        public DateTime ProductionDate { get; set; }
        public decimal Length { get; set; }
        public decimal Width { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Модель данных для склада фурнитуры
    public class AccessoryWarehouseItem : INotifyPropertyChanged
    {
        public int BatchNumber { get; set; }
        public string AccessoryArticle { get; set; }
        public string AccessoryName { get; set; }
        public int Quantity { get; set; }   
        public string UnitName { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal TotalCost { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
