using System;
using System.Windows;
using System.Windows.Controls;

namespace UchPR
{
    public partial class MainWindow : Window
    {

        private readonly IUserSessionService _userSessionService;
        private string currentUserRole;
        private string currentUserName;

        public MainWindow(string userRole, string userName, IUserSessionService userSessionService = null)
        {
            InitializeComponent();
            _userSessionService = userSessionService ?? new UserSessionService();

            currentUserRole = userRole;
            currentUserName = userName;

            UserInfo.Text = $"Пользователь: {userName}, Роль: {userRole}";
            ConfigureNavigationByRole();
            SetInitialPage();
        }

        private void ConfigureNavigationByRole()
        {
            // Скрываем все кнопки по умолчанию
            HideAllNavigationButtons();
            
            switch (currentUserRole)
            {
                case "Кладовщик":
                    ConfigureWarehouseNavigation();
                    break;
                    
                case "Менеджер":
                    ConfigureManagerNavigation();
                    break;
                    
                case "Руководитель":
                    ConfigureDirectorNavigation();
                    break;
                    
                case "Заказчик":
                    ConfigureCustomerNavigation();
                    break;
            }
        }

        private void HideAllNavigationButtons()
        {
            btnWarehouse.Visibility = Visibility.Collapsed;
            btnFabricList.Visibility = Visibility.Collapsed;
            btnAccessoryList.Visibility = Visibility.Collapsed;
            btnProductList.Visibility = Visibility.Collapsed;
            btnOrders.Visibility = Visibility.Collapsed;
            btnReports.Visibility = Visibility.Collapsed;
            btnCatalog.Visibility = Visibility.Collapsed;
            btnMyOrders.Visibility = Visibility.Collapsed;
            btnMaterialReceipt.Visibility = Visibility.Collapsed;
            btnInventar2.Visibility = Visibility.Collapsed;
            btnInventar.Visibility = Visibility.Collapsed;
            btnReports.Visibility = Visibility.Collapsed;
            btnCreateProduction.Visibility = Visibility.Collapsed;
           

        }

        private void ConfigureWarehouseNavigation()
        {
            btnWarehouse.Visibility = Visibility.Visible;
            btnFabricList.Visibility = Visibility.Visible;
            btnAccessoryList.Visibility = Visibility.Visible;
            btnMaterialReceipt.Visibility = Visibility.Visible; // Показываем только кладовщику
            btnInventar.Visibility = Visibility.Visible;
            btnInventar2.Visibility = Visibility.Collapsed;
            btnReports.Visibility = Visibility.Collapsed;
            btnCreateProduction.Visibility = Visibility.Collapsed;
            btnOrders2.Visibility = Visibility.Collapsed;
        }

        private void ConfigureManagerNavigation()
        {
            // Менеджер видит изделия, конструктор и заказы
            btnProductList.Visibility = Visibility.Visible;
            btnProductDesigner.Visibility = Visibility.Visible; // ДОБАВЛЕНО
            btnOrders.Visibility = Visibility.Visible;
            btnWarehouse.Visibility = Visibility.Visible; // Доступ к складу только для просмотра
            btnMaterialReceipt.Visibility = Visibility.Collapsed;
            btnInventar2.Visibility = Visibility.Collapsed;
            btnInventar.Visibility = Visibility.Collapsed;
            btnReports.Visibility = Visibility.Collapsed;
            btnCreateProduction.Visibility = Visibility.Visible;
            btnOrders2.Visibility = Visibility.Collapsed;
        }

        private void ConfigureDirectorNavigation()
        {
            // Руководитель видит все, включая отчеты
            btnWarehouse.Visibility = Visibility.Visible;
            btnProductList.Visibility = Visibility.Visible;
            btnProductDesigner.Visibility = Visibility.Visible; // ДОБАВЛЕНО
            btnOrders.Visibility = Visibility.Visible;
            btnReports.Visibility = Visibility.Visible;
            btnMaterialReceipt.Visibility = Visibility.Collapsed;
            btnInventar2.Visibility = Visibility.Visible;
            btnInventar.Visibility = Visibility.Collapsed;
            btnOrders2.Visibility = Visibility.Collapsed;
            btnCreateProduction.Visibility = Visibility.Collapsed;
            btnReports.Visibility = Visibility.Visible;
        }


        private void BtnOrdersClient_Click(object sender, RoutedEventArgs e)
        {
            string userLogin = GetCurrentUserLogin(); // Реализуйте этот метод
            string userPassword = GetCurrentUserPassword();

            var designerWindow = new OrderWindow(userLogin, "Заказчик");
            designerWindow.Owner = this;
            designerWindow.ShowDialog();
        }
        private void BtnProductDesigner_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Получаем данные пользователя для передачи в конструктор
                string userLogin = GetCurrentUserLogin(); // Реализуйте этот метод
                string userPassword = GetCurrentUserPassword(); // Реализуйте этот метод

                var designerWindow = new ProductDesignerWindow(userLogin, userPassword);
                designerWindow.Owner = this;
                designerWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка открытия конструктора изделий: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetCurrentUserLogin()
        {
            return _userSessionService.GetCurrentUserLogin();
        }

        private string GetCurrentUserPassword()
        {
            return _userSessionService.GetCurrentUserPassword();
        }

        private void ConfigureCustomerNavigation()
        {
            // Заказчик видит каталог, свои заказы и конструктор
            btnCatalog.Visibility = Visibility.Visible;
            btnMyOrders.Visibility = Visibility.Visible;
            btnProductDesigner.Visibility = Visibility.Visible; // ДОБАВЛЕНО для заказчиков
            btnMaterialReceipt.Visibility = Visibility.Collapsed;
        }

        private void BtnMaterialReceipt_Click(object sender, RoutedEventArgs e)
        {
            // Только для кладовщика!
            if (currentUserRole != "Кладовщик")
            {
                MessageBox.Show("Доступ разрешен только кладовщику.");
                return;
            }
            var receiptWindow = new MaterialReceiptWindow(currentUserRole);
            receiptWindow.Owner = this;
            receiptWindow.ShowDialog();
        }
        private void BtnInventar_Click(object sender, RoutedEventArgs e)
        {
            // Только для кладовщика!
            if (currentUserRole != "Кладовщик")
            {
                MessageBox.Show("Доступ разрешен только кладовщику.");
                return;
            }
            var receiptWindow = new InventoryWindow(currentUserRole);
            receiptWindow.Owner = this;
            receiptWindow.ShowDialog();
        }
        private void btnCreateProduction_Click(object sender, RoutedEventArgs e)
        {
            var receiptWindow = new CreateProduction();
            receiptWindow.Owner = this;
            receiptWindow.ShowDialog();
        }
        private void BtnInventar_Click2(object sender, RoutedEventArgs e)
        {
            // Только для кладовщика!
            if (currentUserRole != "Руководитель")
            {
                MessageBox.Show("Доступ разрешен только кладовщику.");
                return;
            }
            var receiptWindow = new InventoryApprovalWindow();
            receiptWindow.Owner = this;
            receiptWindow.ShowDialog();
        }

        private void SetInitialPage()
        {
            Page initialPage = null;

            switch (currentUserRole)
            {
                case "Кладовщик":
                    initialPage = new WarehousePage(currentUserRole);
                    break;

                case "Менеджер":
                case "Руководитель":
                    initialPage = new ProductListPage(currentUserRole);
                    break;

                case "Заказчик":
                    // initialPage = new CustomerCatalogPage(currentUserRole);
                    break;
            }

            if (initialPage != null)
            {
                MainFrame.Navigate(initialPage);
            }
        }

        // Обработчики событий навигации
        private void BtnWarehouse_Click(object sender, RoutedEventArgs e)
        {
            var warehousePage = new WarehousePage(currentUserRole);
            MainFrame.Navigate(warehousePage);
            HighlightActiveButton(btnWarehouse);
        }

        private void BtnFabricList_Click(object sender, RoutedEventArgs e)
        {
            var fabricWindow = new FabricListWindow(currentUserRole);
            fabricWindow.Owner = this;
            fabricWindow.ShowDialog();
        }

        private void BtnAccessoryList_Click(object sender, RoutedEventArgs e)
        {
            var accessoryWindow = new AccessoryListWindow(currentUserRole);
            accessoryWindow.Owner = this;
            accessoryWindow.ShowDialog();
        }

        private void BtnProductList_Click(object sender, RoutedEventArgs e)
        {
            var productPage = new ProductListPage(currentUserRole);
            MainFrame.Navigate(productPage);
            HighlightActiveButton(btnProductList);
        }

        private void BtnOrders_Click(object sender, RoutedEventArgs e)
        {
            // var ordersPage = new OrdersPage(currentUserRole);
            // MainFrame.Navigate(ordersPage);
            // HighlightActiveButton(btnOrders);
            MessageBox.Show("Страница заказов в разработке");
        }

        private void BtnReports_Click(object sender, RoutedEventArgs e)
        {
           var statistics = new InventoryReportsWindow();
            statistics.Owner = this;
            statistics.ShowDialog();
            // HighlightActiveButton(btnReports);
            // MessageBox.Show("Страница отчетов в разработке");    
        }

        private void BtnCatalog_Click(object sender, RoutedEventArgs e)
        {
            // var catalogPage = new CustomerCatalogPage(currentUserRole);
            // MainFrame.Navigate(catalogPage);
            // HighlightActiveButton(btnCatalog);
            MessageBox.Show("Каталог для заказчиков в разработке");
        }

        private void BtnMyOrders_Click(object sender, RoutedEventArgs e)
        {
            // var myOrdersPage = new CustomerOrdersPage(currentUserRole);
            // MainFrame.Navigate(myOrdersPage);
            // HighlightActiveButton(btnMyOrders);
            MessageBox.Show("Страница заказов заказчика в разработке");
        }

        private void HighlightActiveButton(Button activeButton)
        {
            // Сброс стилей всех кнопок
            ResetButtonStyles();
            
            // Выделение активной кнопки
            activeButton.Background = System.Windows.Media.Brushes.LightBlue;
            activeButton.FontWeight = FontWeights.Bold;
        }

        private void ResetButtonStyles()
        {
            var buttons = new[] { btnWarehouse, btnFabricList, btnAccessoryList, 
                                 btnProductList, btnOrders, btnReports, 
                                 btnCatalog, btnMyOrders };
            
            foreach (var button in buttons)
            {
                if (button.Visibility == Visibility.Visible)
                {
                    button.Background = System.Windows.Media.Brushes.White;
                    button.FontWeight = FontWeights.SemiBold;
                }
            }
        }

        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }
    }
}
