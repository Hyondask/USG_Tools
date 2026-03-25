using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using USG_Tools.Core.Extensions;


namespace USG_Tools.Core.Models
{
    public class UserCredentials
    {
        public string Login { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool JumpHost { get; set; } = false;
        public List<string> Hosts { get; set; } = new List<string>();
        public string? ProxyHost { get; set; }
        public string? ProxyLogin { get; set; }
        public string? ProxyPassword { get; set; }

        private ILogger _logger;

        public UserCredentials()
        {

        }


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
            if (JumpHost)
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

        /// <summary>
        /// Форматированный вывод свойств класса  
        /// </summary>
        /// <returns></returns>
        public string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("--- Профиль пользователя ---")
              .AppendLine($"Login: {Login}")
              .AppendLine($"Password: {Password.MaskSecretData()} ")
              .Append("Hosts: ").AppendJoin(", ", Hosts).AppendLine()
              .AppendLine($"Jump Host: {JumpHost}");
            if (JumpHost)
            {
                sb.AppendLine($"Proxy Host: {ProxyHost}")
                  .AppendLine($"Proxy Login: {ProxyLogin}")
                  .AppendLine($"Proxy Password: {ProxyPassword.MaskSecretData()}");
            }
            sb.AppendLine("--------------------");
            return sb.ToString();
        }
    }
}
