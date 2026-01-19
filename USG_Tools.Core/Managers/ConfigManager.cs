using System.Text.Json;
using USG_Tools.Core.Models;

namespace USG_Tools.Core.Managers
{
    public class ConfigManager
    {
        private readonly string _basePath;
        private readonly string _configFolderPath;

        // Пути к конкретным файлам 
        private string CredentialsPath => Path.Combine(_configFolderPath, "credentials.json");

        // Конфигурации 
        public UserCredentials? Credentials { get; private set; }

        public ConfigManager()
        {
            // 1. Определяем пути

            _basePath = AppDomain.CurrentDomain.BaseDirectory;
            _configFolderPath = Path.Combine(_basePath, "config");

            //2. Создаем папку config, если ее еще нет

            if (!Directory.Exists(_configFolderPath))
            {
                Directory.CreateDirectory(_configFolderPath);
            }

            // 3. Загружаем данные (если файлов нет, создаем конструктор конфигурации)
            Credentials = LoadJson<UserCredentials>(CredentialsPath);

        }

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
                Console.WriteLine($"Возникла ошибка при чтении {path}. Ошибка: {ex.Message}");
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
                Console.WriteLine($"Ошибка при сохранении конфига {path}: {ex.Message}");
            }
        }
    }
}
