using System;

namespace UchPR
{
    public class UserInfo
    {
        public string Login { get; set; }
        public string Password { get; set; } // В реальном приложении лучше использовать токены
        public string FullName { get; set; }
        public string Role { get; set; }
        public DateTime LastLoginDate { get; set; }
    }

    public class AuthenticationResult
    {
        public bool IsSuccess { get; set; }
        public UserInfo UserInfo { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class UserData
    {
        public string FullName { get; set; }
        public string Role { get; set; }
    }
}
