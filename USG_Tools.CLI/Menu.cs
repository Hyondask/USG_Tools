using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using USG_Tools.CLI.Utils;
using USG_Tools.Core.Managers;
using USG_Tools.Core.Models;

namespace USG_Tools.CLI
{
    public class Menu
    {
        private readonly ConfigManager _configManager;
        private readonly ILogger _logger;
        public Menu(ConfigManager configManager, ILogger menulogger)
        {
            _configManager = configManager;
            _logger = menulogger;
        }

        // --- ТОЧКА ВХОДА ---
        public async Task RunAsync()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("========================================");
                Console.WriteLine("        HUAWEI USG HELPER TOOL          ");
                Console.WriteLine("========================================");
                Console.WriteLine("2. Поиск зоны по IP адресу");
                Console.WriteLine("8. Настройки (Credentials/Proxy)");
                Console.WriteLine("9. Обновление БД");
                Console.WriteLine("0. Выход");
                Console.WriteLine("========================================");
                Console.Write("\nВыберите действие: ");

                switch (Console.ReadLine())
                {
                    case "8": await RunSetup(); break;
                    case "9": await ShowDiscoveryMenu(); break;
                    case "0": return;
                    default:
                        Console.WriteLine("❌ Неверный ввод...");
                        Thread.Sleep(1000);
                        break;
                }
            }
        }

        // --- ПОДМЕНЮ ---

        private async Task ShowDiscoveryMenu()
        {
            Console.Clear();
            Console.WriteLine(">>> ЗАПУСК СБОРА ДАННЫХ");
            // Вызываем логику из Core через DeviceDiscoveryService
            // После сбора вызываем DatabaseService.SaveToSqliteAsync
            Console.WriteLine("\nНажмите любую клавишу для возврата...");
            Console.ReadKey();
        }


        // --- Технические функции ----

        /// <summary>
        /// Меню для обновления учетных данных для доступа на оборудование.
        /// Запрашивает данные, после сохраняет в json 
        /// </summary>
        /// <returns></returns>
        private async Task RunSetup()
        {
            Console.Clear();
            Console.WriteLine("=== НАСТРОЙКА УЧЕТНЫХ ДАННЫХ ===");

            // Если конфиг уже был, можем показать текущите значения (изменения)
            if (_configManager.Credentials != null)
            {

                while (true)
                {
                    ShowCredentials();
                    Console.WriteLine("1. Вызвать мастер настройки");
                    Console.WriteLine("2. Изменить УЗ для подключения");
                    Console.WriteLine("3. Изменить УЗ для Прокси");
                    Console.WriteLine("0. Выход");
                    Console.Write("Выберите действие");
                    switch (Console.ReadLine())
                    {
                        case "1": { SetupMaster(); break; }
                        case "2": { MainCredentialsSetup(); _configManager.UpdateCredentials(_configManager.Credentials); break;  }
                        case "3": { ProxyCredentialsSetup(); _configManager.UpdateCredentials(_configManager.Credentials); break; }
                        case "0": { return; }
                    }
                }
            }
            // Если нет, создаем новый объект и сохраняем его в Json-файл 
            else
            {
                await SetupMaster();
            }


        }

        private async Task ShowCredentials()
        {
            Console.WriteLine(_configManager.Credentials.ToString());
        }

        //--- Мастер настройки учетных данных --- 

        private async Task SetupMaster()
        {
            _configManager.Credentials = new UserCredentials();
            MainCredentialsSetup();
            _configManager.UpdateCredentials(_configManager.Credentials);
        }

        private async Task MainCredentialsSetup()
        {
            Console.WriteLine("===Настройка учетных данных===");
            Console.Write("Логин: ");
            _configManager.Credentials.Login = ConsoleUtils.GetNotEmptyString();
            Console.Write("Пароль: ");
            _configManager.Credentials.Password = ConsoleUtils.ReadSecret();
            Console.Write("Укажите список IP USG (одна строка, 1 ip)");
            _configManager.Credentials.Hosts = ConsoleUtils.GetIpList();
            Console.Write("Включить SSH_JumpHost (Bastion) ");
            _configManager.Credentials.JumpHost = ConsoleUtils.GetYesOrNo();
            if (_configManager.Credentials.JumpHost)
            {
                ProxyCredentialsSetup();
            }

        }

        private async Task ProxyCredentialsSetup()
        {
            Console.WriteLine("===Настройка данных прокси===");
            Console.Write("Логин: ");
            _configManager.Credentials.ProxyLogin = ConsoleUtils.GetNotEmptyString();
            Console.Write("Пароль: ");
            _configManager.Credentials.ProxyPassword = ConsoleUtils.ReadSecret();
            Console.Write("Адрес прокси сервера: ");
            _configManager.Credentials.ProxyHost = ConsoleUtils.GetIp();
        }
        
    }
}
