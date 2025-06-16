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
            // По умолчанию выбираем "Ткани"
            cmbMaterialType.SelectedIndex = 0;
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

            if (cmbMaterialType.SelectedIndex == 0) // Ткани
            {
                ShowFabricWarehouse();
            }
            else // Фурнитура
            {
                ShowAccessoryWarehouse();
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
            string materialType = cmbMaterialType.SelectedIndex == 0 ? "Fabric" : "Accessory";
            var thresholdWindow = new ThresholdSettingsWindow(materialType);
            thresholdWindow.ShowDialog();

            // Обновляем данные после закрытия окна
            if (cmbMaterialType.SelectedIndex == 0)
            {
                LoadFabricWarehouseData();
            }
            else
            {
                LoadAccessoryWarehouseData();
            }
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
