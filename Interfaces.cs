using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UchPR
{
    public interface IAuthenticationService
    {
        Task<AuthenticationResult> AuthenticateAsync(string login, string password);
        Task<UserInfo> GetUserInfoAsync(string login);
    }

    // Интерфейс для навигации
    public interface INavigationService
    {
        void NavigateToMainWindow(UserInfo userInfo);
        void NavigateToRegistration();
        void CloseApplication();
    }

    // Интерфейс для отображения сообщений
    public interface IMessageService
    {
        void ShowError(string message);
        void HideError();
    }

    // Интерфейс для управления сессией пользователя
    public interface IUserSessionService
    {
        void SetCurrentUser(UserInfo userInfo);
        UserInfo GetCurrentUser();
        string GetCurrentUserLogin();
        string GetCurrentUserPassword();
        void ClearSession();
        void SaveCredentials(string login, string password);
        bool HasSavedCredentials();
    }
}
