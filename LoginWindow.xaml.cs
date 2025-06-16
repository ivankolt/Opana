using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace UchPR
{
    /// <summary>
    /// Логика взаимодействия для LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        private readonly DataBase db = new DataBase();
        public LoginWindow()
        {
            InitializeComponent();
        }
        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            string login = txtLogin.Text;
            string password = txtPassword.Password;

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                ShowError("Пожалуйста, введите логин и пароль.");
                return;
            }

            // СТАЛО: Простой и понятный вызов метода
            string userRole = db.AuthenticateUser(login, password);

            if (userRole != null)
            {
                lblError.Visibility = Visibility.Collapsed;
                RedirectToRoleWindow(userRole);
            }
            else
            {
                ShowError("Неверный логин или пароль, либо ошибка подключения к БД.");
            }
        }
        private void RedirectToRoleWindow(string userRole)
        {
            // Здесь нам нужно получить и имя пользователя, не только роль
            // Давайте немного изменим метод AuthenticateUser в классе DataBase

            // Получаем полное имя пользователя
            string userName = db.GetUserName(txtLogin.Text); // Добавим этот метод в DataBase

            // Создаем экземпляр главного окна и передаем ему роль и имя
            var mainWindow = new MainWindow(userRole, userName);
            mainWindow.Show();

            // Закрываем окно авторизации
            this.Close();
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            RegistrationWindow registrationWindow = new RegistrationWindow();
            registrationWindow.Show();
            this.Close();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void ShowError(string message)
        {
            lblError.Text = message;
            lblError.Visibility = Visibility.Visible;
        }

    }
}
