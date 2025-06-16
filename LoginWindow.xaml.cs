using System;
using System.Threading.Tasks;
using System.Windows;

namespace UchPR
{
    public partial class LoginWindow : Window
    {
        private readonly IAuthenticationService _authenticationService;
        private readonly INavigationService _navigationService;
        private readonly IMessageService _messageService;
        private readonly IUserSessionService _userSessionService;

        public LoginWindow()
        {
            InitializeComponent();

            // Создаем зависимости
            _userSessionService = new UserSessionService();
            _authenticationService = new AuthenticationService(new DataBase(), _userSessionService);
            _navigationService = new NavigationService(this, _userSessionService);

            // ИСПРАВЛЕНО: передаем lblError как TextBlock
            _messageService = new MessageService(lblError);

            // Загружаем сохраненные данные при открытии окна
            LoadSavedCredentials();
        }

        private void LoadSavedCredentials()
        {
            try
            {
                if (_userSessionService.HasSavedCredentials())
                {
                    txtLogin.Text = _userSessionService.GetCurrentUserLogin();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки сохраненных данных: {ex.Message}");
            }
        }

        private async void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await HandleLoginAsync();
            }
            catch (Exception ex)
            {
                _messageService.ShowError($"Произошла ошибка: {ex.Message}");
            }
        }

        private async Task HandleLoginAsync()
        {
            string login = txtLogin.Text?.Trim();
            string password = txtPassword.Password;

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                _messageService.ShowError("Пожалуйста, введите логин и пароль.");
                return;
            }

            SetLoadingState(true);

            try
            {
                var result = await _authenticationService.AuthenticateAsync(login, password);

                if (result.IsSuccess)
                {
                    _messageService.HideError();
                    SaveCredentialsForSession(login, password, result.UserInfo);
                    _navigationService.NavigateToMainWindow(result.UserInfo);
                }
                else
                {
                    _messageService.ShowError(result.ErrorMessage);
                }
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        private void SaveCredentialsForSession(string login, string password, UserInfo userInfo)
        {
            try
            {
                userInfo.Password = password;
                _userSessionService.SaveCredentials(login, password);
                _userSessionService.SetCurrentUser(userInfo);

                System.Diagnostics.Debug.WriteLine($"Сохранены учетные данные для пользователя: {login}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения учетных данных: {ex.Message}");
            }
        }

        private void SetLoadingState(bool isLoading)
        {
            btnLogin.IsEnabled = !isLoading;
            btnLogin.Content = isLoading ? "Вход..." : "Войти";
            txtLogin.IsEnabled = !isLoading;
            txtPassword.IsEnabled = !isLoading;
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _navigationService.NavigateToRegistration();
            }
            catch (Exception ex)
            {
                _messageService.ShowError($"Ошибка при переходе к регистрации: {ex.Message}");
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            _navigationService.CloseApplication();
        }
    }
}
