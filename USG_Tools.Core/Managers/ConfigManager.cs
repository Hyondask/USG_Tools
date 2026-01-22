using Microsoft.Extensions.Logging;
using System.Text.Json;
using USG_Tools.Core.Models;

namespace USG_Tools.Core.Managers
{
    public class ConfigManager
    {
        private readonly string _basePath;
        private readonly string _appData;
        private readonly string _configFolderPath;
        private readonly string _secretFolder;
        private readonly string _secretFilePath;
        private ILogger _logger;

        // Пути к конкретным файлам 
        private string CredentialsPath => Path.Combine(_configFolderPath, "credentials.json");

        // Конфигурации 
        public UserCredentials? Credentials { get; set; }

        public ConfigManager(ILogger logger)
        {
            // 1. Определяем пути
            _logger = logger;
            _appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _secretFolder = Path.Combine(_appData, "USG_Tools");
            _secretFilePath = Path.Combine(_secretFolder, "secrets.json");
            _basePath = AppContext.BaseDirectory;
            _configFolderPath = Path.Combine(_basePath, "config");

            //2. Создаем папки с конфигами, если их еще нет

            if (!Directory.Exists(_secretFolder))
            {
                Directory.CreateDirectory(_secretFolder);
            }

            if (!Directory.Exists(_configFolderPath))
            {
                Directory.CreateDirectory(_configFolderPath);
            }

            // 3. Загружаем данные (если файлов нет, передаем null)
            Credentials = LoadJson<UserCredentials>(CredentialsPath);

        }

        /// <summary>
        /// Обновляет данные объекта UserCredentials и сохраняет в JSON-файл
        /// </summary>
        /// <param name="newCredentials">Обновленные данные UserCredentials</param>
        public void UpdateCredentials(UserCredentials newCredentials)
        {
            Credentials = newCredentials;
            SaveJson<UserCredentials>(CredentialsPath, Credentials);

        }

        /// <summary>
        /// Загружает данные из JSON-файла и десериализует их в объект указанного типа 
        /// </summary>
        /// <typeparam name="T">Тип объекта в который десериализуются  данные(должен быть ссылочным типом)</typeparam>
        /// <param name="path">Относительный, или абсолютный путь к Json-файлу</param>
        /// <returns>
        /// Возвращает экземпляр типа <typeparamref name="T"/>, если файл успешно прочитан; 
        /// в противном случае (файл не найден, ошибка структуры JSON) возвращает <see langword="null"/>.
        /// </returns>
        private T? LoadJson<T>(string path) where T : class
        {
            // Проверяем существует ли файл 
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                // Читаем весь текст из файла 
                string json = File.ReadAllText(path);

                // Дессириализуем json в объект 
                return JsonSerializer.Deserialize<T>(json);
            }

            catch (Exception ex) 
            {
                // Если файл битый или возникли проблемы пишем ошибку и возвращаем null 
                _logger.LogError($"Возникла ошибка при чтении {path}. Ошибка: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Сериализует указанный объект в JSON-формат и сохраняет его в файл.
        /// </summary>
        /// <param name="path">Относительный или абсолютный путь к файлу</param>
        /// <param name="obj">Объект, который сериализуется в json </param>
        /// <remarks>
        /// Данные сохраняются с использованием форматирования (отступов) для удобства чтения человеком.
        /// Если файл по указанному пути уже существует, он будет перезаписан.
        /// </remarks>
        private void SaveJson<T>(string path, T obj) where T : class
        {
            try
            {
                // Получаем путь к папке {из пути к файлу 
                string? directory = Path.GetDirectoryName(path);

                // Если папка указана и ее нет, создаём ее 
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                // Сериализуем и сохраняем. (Всегда перезаписываем актуальными данными)
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(obj, options);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                // Если что то пошло не так, выводим ошибку 
                _logger.LogError($"Ошибка при сохранении конфига {path}: {ex.Message}");
            }
        }
    }
}
