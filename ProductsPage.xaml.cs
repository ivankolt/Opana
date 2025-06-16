// File: ProductsPage.xaml.cs
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging; // Для картинок
using System;

namespace UchPR
{
    public partial class ProductsPage : Page
    {
        private readonly DataBase db = new DataBase();
        private readonly string _userRole;

        public ProductsPage(string userRole)
        {
            InitializeComponent();
            _userRole = userRole;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadProducts();
            SetPermissions();
        }

        private void LoadProducts()
        {
            dgProducts.ItemsSource = db.GetProducts();
        }

        private void SetPermissions()
        {
            // Пример: директор не может добавлять/редактировать изделия
            if (_userRole == "Руководитель")
            {
                btnAddNewProduct.Visibility = Visibility.Collapsed;
                btnEditProduct.Visibility = Visibility.Collapsed;
            }
        }

        private void dgProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgProducts.SelectedItem is ProductDisplayItem selectedProduct)
            {
                // Показываем панель с деталями
                DetailsPanel.Visibility = Visibility.Visible;

                // Заполняем данными
                tbProductName.Text = selectedProduct.Name;
                tbProductArticle.Text = $"Артикул: {selectedProduct.Article}";
                tbProductComment.Text = selectedProduct.Comment ?? "Описание отсутствует.";

                // Загружаем картинку (убедитесь, что Build Action у картинок = Resource)
                try
                {
                    if (!string.IsNullOrEmpty(selectedProduct.ImagePath))
                    {
                        // Путь должен быть относительным, например /Images/Products/image.jpg
                        imgProduct.Source = new BitmapImage(new Uri(selectedProduct.ImagePath, UriKind.RelativeOrAbsolute));
                    }
                }
                catch { /* Ошибка загрузки картинки, ничего страшного */ }

                // Загружаем спецификацию
                lvComposition.ItemsSource = db.GetProductComposition(selectedProduct.Article);
            }
            else
            {
                DetailsPanel.Visibility = Visibility.Collapsed;
            }
        }
    }
}
