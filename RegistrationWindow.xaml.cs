// File: RegistrationWindow.xaml.cs
using System.Windows;

namespace UchPR
{
    public partial class RegistrationWindow : Window
    {
        // Создаем один экземпляр класса для работы с БД
        private readonly DataBase db = new DataBase();

        public RegistrationWindow()
        {
            InitializeComponent();
        }

        private void btnRegister_Click(object sender, RoutedEventArgs e)
        {
            string name = txtName.Text.Trim();
            string login = txtLogin.Text.Trim();
            string password = txtPassword.Password;
            string repeatPassword = txtRepeatPassword.Password;

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                ShowError("Все поля должны быть заполнены.");
                return;
            }

            if (password != repeatPassword)
            {
                ShowError("Пароли не совпадают.");
                return;
            }

            // СТАЛО: Простой вызов метода
            if (db.LoginExists(login))
            {
                ShowError($"Пользователь с логином '{login}' уже существует.");
                return;
            }

            // СТАЛО: Еще один простой вызов
            if (db.RegisterUser(name, login, password))
            {
                MessageBox.Show("Регистрация прошла успешно! Теперь вы можете войти.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                BackToLogin_Click(null, null);
            }
            else
            {
                ShowError("Произошла ошибка при регистрации. Пожалуйста, попробуйте позже.");
            }
        }

        // Остальные методы (BackToLogin_Click, ShowError) остаются без изменений
        private void BackToLogin_Click(object sender, RoutedEventArgs e)
        {
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }

        private void ShowError(string message)
        {
            lblError.Text = message;
            lblError.Visibility = Visibility.Visible;
        }
    }
}
