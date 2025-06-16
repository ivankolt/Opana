using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace UchPR
{
    // Сервис аутентификации
    public class AuthenticationService : IAuthenticationService
    {
        private readonly DataBase _database;
        private readonly IUserSessionService _userSessionService;

        public AuthenticationService(DataBase database, IUserSessionService userSessionService)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));
        }

        public async Task<AuthenticationResult> AuthenticateAsync(string login, string password)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== Начало аутентификации ===");
                System.Diagnostics.Debug.WriteLine($"Логин: {login}");

                if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
                {
                    return new AuthenticationResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "Логин и пароль не могут быть пустыми"
                    };
                }

                string userRole = await Task.Run(() => _database.AuthenticateUser(login, password));
                System.Diagnostics.Debug.WriteLine($"Роль из БД: {userRole}");

                if (userRole != null)
                {
                    var userInfo = await GetUserInfoAsync(login);
                    System.Diagnostics.Debug.WriteLine($"Роль из UserInfo: {userInfo.Role}");

                    userInfo.Password = password;

                    // Сохраняем учетные данные в сессии
                    _userSessionService.SaveCredentials(login, password);

                    return new AuthenticationResult
                    {
                        IsSuccess = true,
                        UserInfo = userInfo
                    };
                }

                return new AuthenticationResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Неверный логин или пароль"
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка аутентификации: {ex.Message}");
                return new AuthenticationResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Ошибка подключения к базе данных: {ex.Message}"
                };
            }
        }

        public async Task<UserInfo> GetUserInfoAsync(string login)
        {
            return await Task.Run(() =>
            {
                var userData = _database.GetUserData(login);
                return new UserInfo
                {
                    Login = login,
                    FullName = userData.FullName,
                    Role = userData.Role,
                    LastLoginDate = DateTime.Now
                };
            });
        }
    }

    // Сервис навигации
    public class NavigationService : INavigationService
    {
        private readonly Window _currentWindow;
        private readonly IUserSessionService _userSessionService;

        public NavigationService(Window currentWindow, IUserSessionService userSessionService)
        {
            _currentWindow = currentWindow ?? throw new ArgumentNullException(nameof(currentWindow));
            _userSessionService = userSessionService ?? throw new ArgumentNullException(nameof(userSessionService));
        }

        public void NavigateToMainWindow(UserInfo userInfo)
        {
            try
            {
                _userSessionService.SetCurrentUser(userInfo);

                var mainWindow = new MainWindow(userInfo.Role, userInfo.FullName, _userSessionService);
                mainWindow.Show();

                _currentWindow.Close();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Ошибка при переходе к главному окну: {ex.Message}", ex);
            }
        }

        public void NavigateToRegistration()
        {
            try
            {
                var registrationWindow = new RegistrationWindow();
                registrationWindow.Show();
                _currentWindow.Close();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Ошибка при переходе к регистрации: {ex.Message}", ex);
            }
        }

        public void CloseApplication()
        {
            Application.Current.Shutdown();
        }
    }

    // ИСПРАВЛЕНО: Сервис сообщений работает с TextBlock
    public class MessageService : IMessageService
    {
        private readonly TextBlock _errorLabel;

        public MessageService(TextBlock errorLabel)
        {
            _errorLabel = errorLabel ?? throw new ArgumentNullException(nameof(errorLabel));
        }

        public void ShowError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Сообщение не может быть пустым", nameof(message));

            _errorLabel.Text = message; // ИСПРАВЛЕНО: используем Text вместо Content
            _errorLabel.Visibility = Visibility.Visible;
        }

        public void HideError()
        {
            _errorLabel.Visibility = Visibility.Collapsed;
        }
    }

    // Альтернативная версия для работы с Label (если нужна)
    public class LabelMessageService : IMessageService
    {
        private readonly Label _errorLabel;

        public LabelMessageService(Label errorLabel)
        {
            _errorLabel = errorLabel ?? throw new ArgumentNullException(nameof(errorLabel));
        }

        public void ShowError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Сообщение не может быть пустым", nameof(message));

            _errorLabel.Content = message;
            _errorLabel.Visibility = Visibility.Visible;
        }

        public void HideError()
        {
            _errorLabel.Visibility = Visibility.Collapsed;
        }
    }
}
