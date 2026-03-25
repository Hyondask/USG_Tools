using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using USG_Tools.Core.Extensions;

namespace USG_Tools.Core.Models
{
    /// <summary>
    /// Хранит профиль пользователя, включая учетные данные для SSH и прокси, 
    /// а также список целевых хостов для сбора данных.
    /// Этот класс сериализуется и сохраняется в файл secrets.json.
    /// </summary>
    public class UserCredentials
    {
        /// <summary>Логин для подключения к оборудованию Huawei.</summary>
        public string Login { get; set; } = string.Empty;

        /// <summary>Зашифрованный пароль для подключения к оборудованию.</summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>Указывает, требуется ли использование прокси-сервера (JumpHost) для подключения.</summary>
        public bool JumpHost { get; set; } = false;

        /// <summary>Список IP-адресов целевых устройств (Huawei USG) для опроса.</summary>
        public List<string> Hosts { get; set; } = new List<string>();

        /// <summary>IP-адрес или доменное имя прокси-сервера (заполняется, если <see cref="JumpHost"/> = <see langword="true"/>).</summary>
        public string? ProxyHost { get; set; }

        /// <summary>Логин для авторизации на прокси-сервере.</summary>
        public string? ProxyLogin { get; set; }

        /// <summary>Зашифрованный пароль для авторизации на прокси-сервере.</summary>
        public string? ProxyPassword { get; set; }

        private ILogger _logger;

        /// <summary>
        /// Инициализирует пустой экземпляр <see cref="UserCredentials"/> (необходимо для десериализации JSON).
        /// </summary>
        public UserCredentials()
        {
        }

        /// <summary>
        /// Проверяет заполненность всех необходимых полей для успешного старта приложения.
        /// </summary>
        /// <param name="errorMessage">Возвращает строку с описанием ошибки, если валидация не пройдена, иначе пустую строку.</param>
        /// <returns>Возвращает <see langword="true"/>, если данные валидны и готовы к использованию, иначе <see langword="false"/>.</returns>
        public bool IsValid(out string errorMessage)
        {
            // Проверка наличия основных данных (Логин и пароль)
            if (string.IsNullOrEmpty(Login) || string.IsNullOrEmpty(Password))
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
                    string.IsNullOrWhiteSpace(ProxyLogin) ||
                    string.IsNullOrWhiteSpace(ProxyPassword))
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
        /// Формирует безопасное строковое представление профиля пользователя, 
        /// скрывая чувствительные данные (пароли) с помощью маскирования.
        /// </summary>
        /// <returns>Многострочная <see cref="string"/> с настройками профиля.</returns>
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