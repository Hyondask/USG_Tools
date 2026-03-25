using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using USG_Tools.Core.Models;

namespace USG_Tools.Core.Managers
{
    public class DatabaseManager
    {
        private readonly string _connectionString;
        private readonly ILogger<DatabaseManager> _logger;


        public DatabaseManager(string dbPath, ILogger<DatabaseManager> logger)
        {
            // dbPath получим из ConfigManager
            _connectionString = $"Data Source={dbPath}";
            _logger = logger;
            EnsureTableCreated();
        }


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

        public void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
            CREATE TABLE IF NOT EXISTS routes (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                network TEXT,
                start_ip INTEGER,
                end_ip INTEGER,
                interface TEXT,
                host TEXT
            );

            CREATE TABLE IF NOT EXISTS zones (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                interface TEXT,
                zone_name TEXT,
                host TEXT
            );
            
            CREATE INDEX IF NOT EXISTS idx_routes_ips ON routes(start_ip, end_ip);
        ";
            command.ExecuteNonQuery();
        }
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
