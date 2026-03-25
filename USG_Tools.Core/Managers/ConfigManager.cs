using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
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
        private readonly string _zonemappingFilePath;
        private ILogger _logger;


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
            _configFolderPath = Path.Combine(_basePath, "configs");
            _zonemappingFilePath = Path.Combine(_configFolderPath, "zone_mappings.json");

            //2. Создаем папки с конфигами, если их еще нет

            if (!Directory.Exists(_secretFolder))
            {
                Directory.CreateDirectory(_secretFolder);
            }

            if (!Directory.Exists(_configFolderPath))
            {
                Directory.CreateDirectory(_configFolderPath);
            }

        }

        /// <summary>
        /// Чтение УЗ из json файла 
        /// </summary>
        public void init()
        {
            // Загружаем учетные данные 
            try
            {
                Credentials = LoadJson<UserCredentials>(_secretFilePath);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        /// <summary>
        /// Обновляет данные объекта UserCredentials и сохраняет в JSON-файл
        /// </summary>
        /// <param name="newCredentials">Обновленные данные UserCredentials</param>
        public void UpdateCredentials(UserCredentials newCredentials)
        {
            Credentials = newCredentials;
            try
            {
                SaveJson<UserCredentials>(_secretFilePath, Credentials);
            }
            catch (Exception ex) { throw; }
        }

        /// <summary>
        /// Чтение маппинга зон из Json файла
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, ZoneMapping> LoadZoneMappings()
        {
            string path = Path.Combine(_zonemappingFilePath);
            if (!File.Exists(path))
            {
                _logger.LogError("не найден файл {file}", _zonemappingFilePath);
                return new Dictionary<string, ZoneMapping>();
            }


            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Dictionary<string, ZoneMapping>>(json)
                   ?? new Dictionary<string, ZoneMapping>();
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
        private UserCredentials LoadJson<T>(string path) where T : class
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

                // Дессириализуем json в объект 1
                var creds = JsonSerializer.Deserialize<UserCredentials>(json);
                return creds != null ? UnprotectCredentials(creds) : null;
            }

            catch (Exception ex)
            {
                // Если файл битый или возникли проблемы пишем ошибку и возвращаем null 
                _logger.LogError($"Возникла ошибка при чтении {path}. Ошибка: {ex.Message}");
                Thread.Sleep(1000);
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

                // Перед сериализацией создаем копию объекта и шифруем в нем даннные 
                var copyCreds = ProtectCredentials();

                // Сериализуем и сохраняем. (Всегда перезаписываем актуальными данными)
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(copyCreds, options);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                // Если что то пошло не так, выводим ошибку 
                _logger.LogError($"Ошибка при сохранении конфига {path}: {ex.Message}");
            }
        }

        /// <summary>
        /// Метод отвечающий за шифрование Учетных данных
        /// </summary>
        /// <exception cref="Exception">Пароль пустой </exception>
        private UserCredentials ProtectCredentials()
        {
            var copyCreds = CopyCredentialsInNewObject();
            if (string.IsNullOrWhiteSpace(copyCreds.Password))
            {
                throw new Exception("поле Password пустое");
            }
            else
            {
                copyCreds.Password = ProtectPasswordWindows(copyCreds.Password);
            }
            if (!string.IsNullOrWhiteSpace(copyCreds.ProxyPassword))
            {
                copyCreds.ProxyPassword = ProtectPasswordWindows(copyCreds.ProxyPassword);
            }

            return copyCreds;

        }

        /// <summary>
        /// Метод дешифровки привязывающийся к профилю пользователя Windows
        /// </summary>
        /// <param name="creds">Заполненный класс с зашифрованными паролями</param>
        /// <returns></returns>
        private UserCredentials UnprotectCredentials(UserCredentials creds)
        {
            if (!string.IsNullOrWhiteSpace(creds.Password))
            {
                try
                {
                    creds.Password = UnprotectPasswordWindows(creds.Password);
                }

                catch (Exception ex)
                {
                    _logger.LogError($"Ошибка при расшифровке Password. Текст ошибки {ex.ToString()}");
                }
            }
            else
            {
                _logger.LogError("Поле Password пустое");
            }

            if (!string.IsNullOrWhiteSpace(creds.ProxyPassword))
            {
                try
                {
                    creds.ProxyPassword = UnprotectPasswordWindows(creds.ProxyPassword);
                }

                catch (Exception ex)
                {
                    _logger.LogError($"Ошибка при расшифровке ProxyPassword. Текст ошибки {ex.ToString()}");
                }
            }
            return creds;
        }

        /// <summary>
        /// Метод шифрования, привязывающийся к профилю пользователя Windows
        /// </summary>
        /// <param name="password"></param>
        /// <returns>Возвращает зашифрованную строку с паролем</returns>
        private string ProtectPasswordWindows(string password)
        {
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            return Convert.ToBase64String(ProtectedData.Protect(passwordBytes, null, DataProtectionScope.CurrentUser));
        }

        private string UnprotectPasswordWindows(string password)
        {
            byte[] passwordBytes = Convert.FromBase64String(password);
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(passwordBytes, null, DataProtectionScope.CurrentUser));
        }

        /// <summary>
        /// Создание копии класса
        /// </summary>
        /// <returns>Возвращает класс дублер </returns>
        private UserCredentials CopyCredentialsInNewObject()
        {
            return new UserCredentials()
            {
                Hosts = Credentials.Hosts,
                Login = Credentials.Login,
                Password = Credentials.Password,
                JumpHost = Credentials.JumpHost,
                ProxyHost = Credentials.ProxyHost,
                ProxyLogin = Credentials.ProxyLogin,
                ProxyPassword = Credentials.ProxyPassword
            };

        }
    }
}
