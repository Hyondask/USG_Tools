using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using USG_Tools.Core.Models;
using USG_Tools.Core.Parsers;

namespace USG_Tools.Core.Managers
{
    public record HostDiscoveryResult(string Host, string Zones, string Routes);
    public class DiscoveryManager
    {
        private readonly ConfigManager _configManager;
        private readonly UserCredentials _userCredentials;

        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<DiscoveryManager> _logger;

        private List<RouteEntry> routes = new List<RouteEntry>();
        private List<ZoneEntry> zones = new List<ZoneEntry>();

        private USGManager usg;

        public DiscoveryManager(ConfigManager configManager, ILoggerFactory loggerFactory)
        {
            // Проверка на null
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _configManager = configManager;
            _userCredentials = configManager.Credentials ?? throw new ArgumentNullException(nameof(configManager.Credentials));
            _logger = _loggerFactory.CreateLogger<DiscoveryManager>();
        }

        public async Task UpdateDatabase()
        {
            // 1. Загружаем конфиг один раз
            var zoneMapping = _configManager.LoadZoneMappings();

            // 2. Получаем сырые данные от всех USG
            var usgAnswers = await GetRoutesAndZonesAsync();

            // 3. Создаем один общий список для всей сети
            var allFinalRoutes = new List<FinalRoute>();

            var _databaseManager = new DatabaseManager("zone_ip.sqlite", _loggerFactory.CreateLogger<DatabaseManager>());

            foreach (var answer in usgAnswers)
            {
                _logger.LogInformation($"Парсинг данных для хоста: {answer.Host}");

                // Парсим текущий хост
                var currentRoutes = USGParser.ParseRoutes(answer.Routes);
                var currentZones = USGParser.ParseZones(answer.Zones);

                // Превращаем в FinalRoute и добавляем в общий котел
                var finalEntries = PrepareDatabaseEntries(currentRoutes, currentZones, zoneMapping);
                allFinalRoutes.AddRange(finalEntries);
            }

            // 4. Когда цикл завершен и у нас есть данные ВСЕХ хостов — пишем в БД
            if (allFinalRoutes.Count > 0)
            {
                _logger.LogInformation($"Начинаю сохранение в БД. Всего записей: {allFinalRoutes.Count}");

                // Группируем по cidr и zone, оставляя только первую встреченную запись
                var cleanRoutes = allFinalRoutes
                    .DistinctBy(r => new { r.cidr, r.zone })
                    .ToList();

                // Теперь сохраняем уже чистый список
                await _databaseManager.BulkSaveRoutesAsync(cleanRoutes);
            }
            else
            {
                _logger.LogWarning("Нет данных для сохранения в БД.");
            }
        }
        private async Task<List<HostDiscoveryResult>> GetRoutesAndZonesAsync() 
        {
            var hostlist = _userCredentials.Hosts;

            var tasks = hostlist.Select(async host =>
            {
                try
                {
                    // СОЗДАЕМ ЛОКАЛЬНУЮ ПЕРЕМЕННУЮ, чтобы не было конфликтов между потоками
                    var usg = new USGManager(_userCredentials, _loggerFactory.CreateLogger<USGManager>());

                    await usg.Connect(host);

                    await usg.UndoScreenLength();
                    await usg.GoToSystemView();
                    await usg.SwitchVsysInside();

                    string zonesRaw = await usg.GetInsideZones();
                    string routesRaw = await usg.GetInsideRoutes();

                    _logger.LogInformation($"[SUCCESS] {host}: Данные получены.");

                    // ВЫЗОВ СОХРАНЕНИЯ
                    await SaveDataToFileAsync(host, zonesRaw, routesRaw);

                    _logger.LogInformation($"[DONE] {host}: Файл записан.");

                    return new HostDiscoveryResult(host,zonesRaw,routesRaw);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[FAILED] {host}: {ex.Message}");
                    return null;
                }
            });

            // Ожидаем получения зон и маршрутов от всех хостов. Формируем массив record
            HostDiscoveryResult?[] results = await Task.WhenAll(tasks);

            // 3. Фильтруем успешные (убираем null) и превращаем в итоговый список
            List<HostDiscoveryResult> finalData = results.Where(r => r != null).Cast<HostDiscoveryResult>().ToList();

            _logger.LogInformation($"Сбор завершен. В списке объектов: {finalData.Count}");

            return finalData;
        }

        private static List<FinalRoute> PrepareDatabaseEntries(
            List<RouteEntry> routes,
            List<ZoneEntry> zones,
            Dictionary<string, ZoneMapping> zoneConfigs)
        {
            var interfaceMap = zones
                .SelectMany(z => z.Interfaces.Select(i => new { i, z.Name }))
                .ToDictionary(x => x.i.Trim(), x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase);

            // Создаем копию конфига с игнорированием регистра для надежности
            var rules = new Dictionary<string, ZoneMapping>(zoneConfigs, StringComparer.OrdinalIgnoreCase);

            return routes.Select(r =>
            {
                string zoneName = interfaceMap.TryGetValue(r.Interface.Trim(), out var zName) ? zName : "Unknown";

                // Поиск правила в JSON
                var mapping = rules.GetValueOrDefault(zoneName, new ZoneMapping { In = "Unknown_In", Out = "Unknown_Out" });

                return new FinalRoute(
                    ip_min: r.minIp,
                    ip_max: r.maxIp,
                    cidr: r.Destination,
                    route: r.NextHop,
                    zone: zoneName,
                    interface_name: r.Interface,
                    zone_in: mapping.In,
                    zone_out: mapping.Out
                );
            }).ToList();
        }

        private async Task SaveDataToFileAsync(string host, string zones, string routes)
        {
            try
            {
                // Создаем имя папки с текущей датой
                string folderName = Path.Combine("Output", DateTime.Now.ToString("yyyy-MM-dd"));
                Directory.CreateDirectory(folderName); // Создаст папку, если её нет

                // Подготавливаем содержимое файла
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"=== HOST: {host} ===");
                sb.AppendLine($"=== DATE: {DateTime.Now} ===");
                sb.AppendLine("\n--- ZONES ---");
                sb.AppendLine(zones ?? "No data");
                sb.AppendLine("\n--- ROUTES ---");
                sb.AppendLine(routes ?? "No data");

                // Формируем имя файла (заменяем недопустимые символы в IP/имени хоста)
                string safeHostName = host.Replace(":", "_").Replace("/", "_");
                string filePath = Path.Combine(folderName, $"{safeHostName}.txt");

                // Асинхронная запись
                await File.WriteAllTextAsync(filePath, sb.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка при сохранении файла для {host}: {ex.Message}");
            }
        }
    }
}
