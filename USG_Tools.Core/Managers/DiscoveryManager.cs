using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using USG_Tools.Core.Models;
using USG_Tools.Core.Parsers;

namespace USG_Tools.Core.Managers
{
    /// <summary>
    /// Представляет сырые текстовые данные, полученные от конкретного узла (межсетевого экрана).
    /// </summary>
    /// <param name="Host">IP-адрес или имя хоста.</param>
    /// <param name="Zones">Сырой текст вывода команды просмотра зон безопасности.</param>
    /// <param name="Routes">Сырой текст вывода таблицы маршрутизации.</param>
    public record HostDiscoveryResult(string Host, string Zones, string Routes);

    /// <summary>
    /// Главный оркестратор процесса сбора данных (Discovery).
    /// Отвечает за координацию подключения к устройствам, парсинг сырых данных, 
    /// обогащение их правилами из конфигурации и передачу на сохранение в базу данных.
    /// </summary>
    public class DiscoveryManager
    {
        private readonly ConfigManager _configManager;
        private readonly UserCredentials _userCredentials;

        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<DiscoveryManager> _logger;

        private List<RouteEntry> routes = new List<RouteEntry>();
        private List<ZoneEntry> zones = new List<ZoneEntry>();


        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="DiscoveryManager"/>.
        /// </summary>
        /// <param name="configManager">Менеджер конфигурации для получения учетных данных и правил маппинга.</param>
        /// <param name="loggerFactory">Фабрика логгеров для создания логгеров внутренних компонентов.</param>
        /// <exception cref="ArgumentNullException">Выбрасывается, если фабрика логгеров или учетные данные не инициализированы.</exception>
        public DiscoveryManager(ConfigManager configManager, ILoggerFactory loggerFactory)
        {
            // Проверка на null
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _configManager = configManager;
            _userCredentials = configManager.Credentials ?? throw new ArgumentNullException(nameof(configManager.Credentials));
            _logger = _loggerFactory.CreateLogger<DiscoveryManager>();
        }

        /// <summary>
        /// Запускает полный цикл обновления базы данных.
        /// </summary>
        /// <remarks>
        /// Процесс включает:
        /// 1. Параллельный сбор сырых данных со всех устройств из конфигурации.
        /// 2. Парсинг текста в объекты C#.
        /// 3. Склейку маршрутов с зонами и маппинг групп In/Out из JSON.
        /// 4. Удаление дубликатов (схлопывание по сети и зоне).
        /// 5. Транзакционную перезапись таблицы в базе данных SQLite.
        /// </remarks>
        /// <returns>Асинхронная задача.</returns>
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

        /// <summary>
        /// Асинхронно и параллельно подключается ко всем устройствам из конфигурации
        /// для сбора таблиц маршрутизации и зон безопасности.
        /// </summary>
        /// <returns>Возвращает <see cref="List{T}"/> объектов <see cref="HostDiscoveryResult"/> с сырыми данными опрошенных узлов.</returns>
        private async Task<List<HostDiscoveryResult>> GetRoutesAndZonesAsync()
        {
            var hostlist = _userCredentials.Hosts;

            var tasks = hostlist.Select(async host =>
            {
                try
                {
                    var usg = new USGManager(_userCredentials, _loggerFactory.CreateLogger<USGManager>());

                    await usg.Connect(host);

                    await usg.UndoScreenLength();
                    await usg.GoToSystemView();
                    await usg.SwitchVsysInside();

                    string zonesRaw = await usg.GetInsideZones();
                    string routesRaw = await usg.GetInsideRoutes();

                    // =====================================================================
                    // ВАЛИДАЦИЯ ПОЛУЧЕННЫХ ДАННЫХ
                    // =====================================================================

                    // 1. Проверяем зоны
                    if (string.IsNullOrWhiteSpace(zonesRaw) || !zonesRaw.Contains("vpn-instance"))
                    {
                        throw new Exception("Получены некорректные или пустые данные зон (отсутствует 'vpn-instance').");
                    }

                    // 2. Проверяем маршруты
                    if (string.IsNullOrWhiteSpace(routesRaw) || !routesRaw.Contains("Destination/Mask"))
                    {
                        throw new Exception("Таблица маршрутов не получена или оборвалась (отсутствует заголовок 'Destination/Mask').");
                    }

                    // Опционально: можно проверять минимальную длину строки
                    if (routesRaw.Length < 500)
                    {
                        throw new Exception($"Подозрительно короткий ответ с маршрутами ({routesRaw.Length} символов). Возможен обрыв.");
                    }
                    // =====================================================================

                    _logger.LogInformation($"[SUCCESS] {host}: Данные получены и прошли проверку.");

                    // ВЫЗОВ СОХРАНЕНИЯ
                    await SaveDataToFileAsync(host, zonesRaw, routesRaw);

                    _logger.LogInformation($"[DONE] {host}: Файл записан.");

                    return new HostDiscoveryResult(host, zonesRaw, routesRaw);
                }
                catch (Exception ex)
                {
                    // Если сработал наш throw new Exception, он прилетит прямо сюда
                    _logger.LogError($"[FAILED] {host}: {ex.Message}");
                    return null;
                }
            });

            // Ожидаем получения зон и маршрутов от всех хостов.
            HostDiscoveryResult?[] results = await Task.WhenAll(tasks);

            // Фильтруем успешные (убираем null) и превращаем в итоговый список
            List<HostDiscoveryResult> finalData = results.Where(r => r != null).Cast<HostDiscoveryResult>().ToList();

            _logger.LogInformation($"Сбор завершен. Успешных хостов: {finalData.Count} из {hostlist.Count}");

            return finalData;
        }

        /// <summary>
        /// Сопоставляет спарсенные маршруты и зоны по имени интерфейса, 
        /// а также обогащает их родительскими группами (In/Out) из конфигурации JSON.
        /// </summary>
        /// <param name="routes">Список маршрутов, полученных от парсера.</param>
        /// <param name="zones">Список зон безопасности, полученных от парсера.</param>
        /// <param name="zoneConfigs">Словарь правил маппинга зон, загруженный из конфигурационного файла.</param>
        /// <returns>Готовый к записи в БД список объектов <see cref="FinalRoute"/>.</returns>
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

        /// <summary>
        /// Сохраняет сырые ответы от устройства в текстовый файл для аудита или отладки.
        /// </summary>
        /// <param name="host">Имя или IP-адрес узла.</param>
        /// <param name="zones">Сырой текст ответа для зон.</param>
        /// <param name="routes">Сырой текст ответа для маршрутов.</param>
        /// <returns>Асинхронная задача сохранения файла.</returns>
        /// <remarks>Файлы сохраняются в директорию <c>Output/YYYY-MM-DD/IP_Адрес.txt</c>.</remarks>
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