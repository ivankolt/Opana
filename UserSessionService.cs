using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UchPR
{
    public class UserSessionService : IUserSessionService
    {
        private static UserInfo _currentUser;
        private static readonly object _lock = new object();

        // Статические поля для хранения данных авторизации
        private static string _savedLogin;
        private static string _savedPassword;

        public void SetCurrentUser(UserInfo userInfo)
        {
            lock (_lock)
            {
                _currentUser = userInfo ?? throw new ArgumentNullException(nameof(userInfo));

                // Сохраняем логин и пароль для дальнейшего использования
                _savedLogin = userInfo.Login;
                _savedPassword = userInfo.Password;
            }
        }

        public UserInfo GetCurrentUser()
        {
            lock (_lock)
            {
                return _currentUser;
            }
        }

        public string GetCurrentUserLogin()
        {
            lock (_lock)
            {
                return _savedLogin ?? _currentUser?.Login ?? string.Empty;
            }
        }

        public string GetCurrentUserPassword()
        {
            lock (_lock)
            {
                return _savedPassword ?? _currentUser?.Password ?? string.Empty;
            }
        }

        public void ClearSession()
        {
            lock (_lock)
            {
                _currentUser = null;
                _savedLogin = null;
                _savedPassword = null;
            }
        }

        // Дополнительные методы для работы с сохраненными данными
        public void SaveCredentials(string login, string password)
        {
            lock (_lock)
            {
                _savedLogin = login;
                _savedPassword = password;
            }
        }

        public bool HasSavedCredentials()
        {
            lock (_lock)
            {
                return !string.IsNullOrEmpty(_savedLogin) && !string.IsNullOrEmpty(_savedPassword);
            }
        }
    }
}
