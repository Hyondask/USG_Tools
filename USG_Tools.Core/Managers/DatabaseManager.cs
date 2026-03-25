using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using USG_Tools.Core.Models;

namespace USG_Tools.Core.Managers
{
    /// <summary>
    /// Управляет подключением и взаимодействием с локальной базой данных SQLite.
    /// Отвечает за создание таблиц и массовую запись данных.
    /// </summary>
    public class DatabaseManager
    {
        private readonly string _connectionString;
        private readonly ILogger<DatabaseManager> _logger;

        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="DatabaseManager"/>.
        /// При инициализации автоматически проверяет и создает необходимую структуру БД.
        /// </summary>
        /// <param name="dbPath">Путь к файлу базы данных SQLite.</param>
        /// <param name="logger">Интерфейс для логирования операций с базой данных.</param>
        public DatabaseManager(string dbPath, ILogger<DatabaseManager> logger)
        {
            // dbPath получим из ConfigManager
            _connectionString = $"Data Source={dbPath}";
            _logger = logger;
            EnsureTableCreated();
        }

        /// <summary>
        /// Проверяет наличие таблицы <c>Routes</c> в базе данных и создает её, если она отсутствует.
        /// </summary>
        /// <remarks>
        /// Таблица содержит обогащенные данные по маршрутам и зонам.
        /// Вызов выполняется синхронно через Dapper при создании менеджера.
        /// </remarks>
        public void EnsureTableCreated()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            connection.Execute(@"
        CREATE TABLE IF NOT EXISTS Routes (
            ip_min INTEGER,
            ip_max INTEGER,
            cidr TEXT,
            route TEXT,
            zone TEXT,
            interface_name TEXT,
            zone_in TEXT,
            zone_out TEXT
        );");
        }


        /// <summary>
        /// Выполняет массовое сохранение списка маршрутов в базу данных.
        /// </summary>
        /// <param name="routes">Список объектов <see cref="FinalRoute"/>, подготовленных для записи.</param>
        /// <returns>Представляет асинхронную операцию.</returns>
        /// <exception cref="SqliteException">Выбрасывается при ошибках выполнения SQL-запросов (например, при блокировке файла).</exception>
        /// <remarks>
        /// <para>Перед вставкой новых данных таблица <c>Routes</c> полностью очищается (выполняется <c>DELETE</c>).</para>
        /// <para>Вся операция завернута в транзакцию. В случае ошибки транзакция откатывается (<c>Rollback</c>), и старые данные сохраняются.</para>
        /// </remarks>
        public async Task BulkSaveRoutesAsync(List<FinalRoute> routes)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Начинаем транзакцию
            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                // 1. Очищаем таблицу перед вставкой новых данных
                // В SQLite TRUNCATE нет, используется DELETE без WHERE (это работает быстро)
                await connection.ExecuteAsync("DELETE FROM Routes", transaction: transaction);

                // 2. SQL запрос для вставки
                const string sql = @"
            INSERT INTO Routes (
                ip_min, ip_max, cidr, route, zone, 
                interface_name, zone_in, zone_out
            ) VALUES (
                @ip_min, @ip_max, @cidr, @route, @zone, 
                @interface_name, @zone_in, @zone_out
            );";

                // 3. Массовая вставка данных
                await connection.ExecuteAsync(sql, routes, transaction: transaction);

                // Если всё прошло успешно — фиксируем изменения
                await transaction.CommitAsync();

                _logger.LogInformation($"БД очищена и заполнена заново: {routes.Count} записей.");
            }
            catch (Exception ex)
            {
                // Если что-то пошло не так (например, выключился свет или ошибка в SQL)
                // Rollback вернет БД в состояние ДО начала очистки.
                await transaction.RollbackAsync();
                _logger.LogError($"Ошибка транзакции. БД не была изменена. Детали: {ex.Message}");
                throw;
            }
        }
    }
}