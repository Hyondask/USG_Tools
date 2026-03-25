using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using USG_Tools.Core.Managers;

namespace USG_Tools.CLI
{
    class Program
    {
        /// <summary>
        /// Точка старта программы
        /// </summary>
        /// <param name="args">Аргументы, при вызове командной строки</param>
        /// <returns></returns>
        static async Task Main(string[] args)
        {
            SQLitePCL.Batteries.Init();
            // Берем конфигурацию логера из json 
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("LogSettings.json", optional: false, reloadOnChange: true)
                .Build();

            // Настройка Serilog
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();
            try
            {
                // 3. Настройка DI-контейнера (если используешь) или LoggerFactory
                using var loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddSerilog(); // Используем Serilog под капотом Microsoft Logging
                });

                // 4. Создание зависимостей
                var configLogger = loggerFactory.CreateLogger<ConfigManager>();
                var configManager = new ConfigManager(configLogger);

                var menuLogger = loggerFactory.CreateLogger<Menu>();
                var menu = new Menu(configManager, loggerFactory);

                // 5. Запуск
                await menu.RunAsync();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Приложение аварийно завершилось");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}