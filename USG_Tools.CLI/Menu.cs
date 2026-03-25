using Microsoft.Extensions.Logging;
using USG_Tools.CLI.Utils;
using USG_Tools.Core.Managers;
using USG_Tools.Core.Models;

namespace USG_Tools.CLI
{
    public class Menu
    {
        private readonly ConfigManager _configManager;
        private readonly ILogger _logger;
        private readonly ILoggerFactory _loggerFactory;
        private string _lastErrorMessage;
        public Menu(ConfigManager configManager, ILoggerFactory loggerFactory)
        {
            // Проверка на null
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));

            _logger = _loggerFactory.CreateLogger<Menu>();
        }

        // --- ТОЧКА ВХОДА ---
        public async Task RunAsync()
        {
            try
            {
                _configManager.init();
            }
            catch (Exception ex)
            {
                _lastErrorMessage = ex.Message;
                _logger.LogWarning($"Конфиг поврежден: ex.Message. Запуск мастера...");
                await RunSetup();
            } 
            while (true)
            {
                Console.Clear();
                ShowError();
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

            var discovery = new DiscoveryManager(_configManager, _loggerFactory);
            await discovery.UpdateDatabase();

            //Выполняем запросы
            //usg.Connect("10.7.219.11");

            //USGManager usg = new USGManager(_configManager.Credentials, _loggerFactory.CreateLogger<USGManager>());
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
                    ShowError();
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

        private async Task ShowError()
        {
            if (!string.IsNullOrEmpty(_lastErrorMessage))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($" >>> [ВНИМАНИЕ]: {_lastErrorMessage}");
                Console.ResetColor();
                Console.WriteLine("---------------------------------------");
                _lastErrorMessage = string.Empty;
            }
        }

    }
}
