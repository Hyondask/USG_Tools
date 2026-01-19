using System;
using System.Collections.Generic;
using System.Text;

namespace USG_Tools.Core.Models
{
    // credentials.json
    public class UserCredentials
    {
        public string Login = string.Empty;
        public string Password = string.Empty;
        public bool UseProxy = false;
        public List<string>? Hosts;
        public string? ProxyHost;
        public string? ProxyLogin;
        public string? ProxyPassword;

        public bool IsValid (out string errorMessage)
        {
            // Проверка наличия основных данных (Логин и пароль)
            if (string.IsNullOrEmpty (Login) || string.IsNullOrEmpty (Password) )
            {
                errorMessage = "Не заполнены основные учетные данные (Логин/Пароль).";
                return false;
            }
            
            // Проверка наличия хостов для подключения
            if (Hosts == null || Hosts.Count == 0)
            {
                errorMessage = "Список устройств (Hosts) пуст.";
                return false;
            }
            // Проверка заполненности данных для подключения к прокси 
            if (UseProxy)
            {
                if (string.IsNullOrWhiteSpace(ProxyHost) || 
                    string.IsNullOrWhiteSpace (ProxyLogin) ||
                    string.IsNullOrWhiteSpace(ProxyPassword) ) 
                    {
                    errorMessage = "Включен прокси, но данные прокси-сервера заполнены не полностью.";
                    return false;
                    }

            }
            //Все проверки выполнены, возвращаем true 
            errorMessage = string.Empty;
            return true;
        }

    }
}
