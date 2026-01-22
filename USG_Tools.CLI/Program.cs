

using Microsoft.Extensions.Logging;
using USG_Tools.Core.Managers;

namespace USG_Tools.CLI
{
    class Program
    {
        static async Task Main(string[] args)
        {

            // Создаем фабрику логов 
            using var loggerFactory = LoggerFactory.Create(builder => {
                builder.AddConsole();
            });
            ILogger logger = loggerFactory.CreateLogger<Program>();

            //Инициализируем конфиг
            var configManager = new ConfigManager(loggerFactory.CreateLogger<ConfigManager>());

            // Запуск меню
            var menu = new Menu(configManager, loggerFactory.CreateLogger<Menu>());
            await menu.RunAsync();
        }
    }
}