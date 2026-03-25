using Hyondask.SSH;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using USG_Tools.Core.Models;
using USG_Tools.Core.Constants;

namespace USG_Tools.Core.Managers
{
    /// <summary>
    /// Предоставляет методы для подключения и взаимодействия с межсетевым экраном Huawei USG по SSH.
    /// Отвечает за отправку команд, смену контекста (vsys) и получение сырого текста.
    /// </summary>
    public class USGManager
    {
        private readonly UserCredentials _userCredentials;
        private readonly ILogger<USGManager> _logger;
        private SshConnection ssh;

        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="USGManager"/>.
        /// </summary>
        /// <param name="userCredentials">Учетные данные для авторизации на устройстве и прокси-сервере.</param>
        /// <param name="logger">Интерфейс для логирования. Если <see langword="null"/>, используется <see cref="NullLogger"/>.</param>
        public USGManager(UserCredentials userCredentials, ILogger<USGManager> logger = null)
        {
            _userCredentials = userCredentials;
            _logger = logger ?? NullLogger<USGManager>.Instance;

            _logger.LogDebug("USGManager инициализирован (используется {LoggerType})", _logger.GetType().Name);
        }

        /// <summary>
        /// Устанавливает SSH-соединение с указанным хостом.
        /// Если в настройках включен JumpHost, маршрутизирует трафик через прокси-сервер.
        /// </summary>
        /// <param name="host">IP-адрес или доменное имя межсетевого экрана.</param>
        /// <returns>Асинхронная задача.</returns>
        public async Task Connect(string host)
        {
            ssh = new SshConnection(host, _userCredentials.Login, _userCredentials.Password);

            if (_userCredentials.JumpHost)
            {
                ssh.SetProxy(_userCredentials.ProxyHost, _userCredentials.ProxyLogin, _userCredentials.ProxyPassword);
            }
            ssh.Prompts.Add(PromptPatterns.UserView); // User-view Prompt (">")
            ssh.Prompts.Add(PromptPatterns.SystemView); // System-view Prompt ("]")

            await ssh.ConnectAsync();
        }

        /// <summary>
        /// Запрашивает и возвращает таблицу маршрутизации для контекста INSIDE.
        /// </summary>
        /// <returns>Возвращает <see cref="string"/> с сырым выводом маршрутов или <see langword="null"/> в случае ошибки.</returns>
        /// <remarks>
        /// Команда на устройстве включает фильтрацию (exclude) хостовых маршрутов (/32), 
        /// специфичных интерфейсов и маршрута по умолчанию (0.0.0.0/0) для уменьшения объема данных.
        /// </remarks>
        public async Task<string> GetInsideRoutes()
        {
            return await ExecuteCommandInVsysAsync("display ip routing-table | exclude ([0-9\\.]\\/32|[0-9\\.]\\/30|Eth.*\\.400|NULL0|0\\.0\\.0\\.0\\/0|Virtual\\-if)", TimeSpan.FromSeconds(20));
        }

        /// <summary>
        /// Запрашивает и возвращает список конфигураций зон безопасности для контекста INSIDE.
        /// </summary>
        /// <returns>Возвращает <see cref="string"/> с сырым выводом конфигурации зон или <see langword="null"/> в случае ошибки.</returns>
        public async Task<string> GetInsideZones()
        {
            return await ExecuteCommandInVsysAsync("display zone", TimeSpan.FromSeconds(30));
        }

        /// <summary>
        /// Вспомогательный метод для выполнения команд строго внутри контекста INSIDE.
        /// </summary>
        /// <param name="command">Команда для выполнения на устройстве.</param>
        /// <param name="timeout">Максимальное время ожидания ответа.</param>
        /// <returns>Текст ответа устройства или <see langword="null"/>, если устройство не в нужном контексте.</returns>
        private async Task<string> ExecuteCommandInVsysAsync(string command, TimeSpan timeout)
        {
            if (!ssh.IsConnected) return null;

            // Проверяем, что мы именно в INSIDE (чтобы не выполнить команду в чужом контексте)
            if (await CheckPromptAsync(PromptPatterns.InsideVsys))
            {
                // Добавляем перенос строки, иначе команда не уйдет!
                await ssh.SendDataAsync(command + Environment.NewLine);
                return await ssh.ReadDataAsync(timeout);
            }

            _logger.LogError($"Ошибка выполнения '{command}': устройство не в режиме INSIDE.");
            return null;
        }

        /// <summary>
        /// Отключает пагинацию вывода в консоли (постраничный просмотр).
        /// Позволяет скачивать длинные ответы (например, полную таблицу маршрутизации) единым блоком текста без необходимости отправлять пробелы.
        /// </summary>
        /// <returns>Асинхронная задача.</returns>
        /// <remarks>Команда "screen-len 0 temp" выполняется в режиме User View (">").</remarks>
        public async Task UndoScreenLength()
        {
            _logger.LogDebug($"{nameof(UndoScreenLength)}");

            bool isConnected = ssh.IsConnected;
            bool userView = await CheckPromptAsync(PromptPatterns.UserView);
            if (isConnected && userView)
            {
                await ssh.SendDataAsync("screen-len 0 temp" + Environment.NewLine);
                await ssh.ReadDataAsync(TimeSpan.FromSeconds(1));
            }
            else
            {
                _logger.LogError($"{nameof(UndoScreenLength)} | Возникла ошибка. isConnected = {isConnected} | userView = {userView}");
            }
        }

        /// <summary>
        /// Переводит терминал из режима User View (">") в режим System View ("]").
        /// </summary>
        /// <returns>Асинхронная задача.</returns>
        public async Task GoToSystemView()
        {
            _logger.LogDebug($"{nameof(GoToSystemView)}");
            bool isConnected = ssh.IsConnected;
            bool userView = await CheckPromptAsync(PromptPatterns.UserView);
            if (isConnected && userView)
            {
                await ssh.SendDataAsync("System-view" + Environment.NewLine);
                await ssh.ReadDataAsync(TimeSpan.FromSeconds(1));
            }
            else
            {
                _logger.LogError($"{nameof(GoToSystemView)} | Возникла ошибка. isConnected = {isConnected} | userView = {userView}");
            }
        }

        /// <summary>
        /// Переключает контекст виртуальной системы на INSIDE.
        /// Требует, чтобы терминал уже находился в режиме System View ("]").
        /// </summary>
        /// <returns>Асинхронная задача.</returns>
        public async Task SwitchVsysInside()
        {
            _logger.LogDebug($"{nameof(SwitchVsysInside)}");
            bool isConnected = ssh.IsConnected;
            bool systemView = await CheckPromptAsync(PromptPatterns.SystemView);
            if (isConnected && systemView)
            {
                await ssh.SendDataAsync("switch vsys INSIDE");
                await ssh.ReadDataAsync(TimeSpan.FromSeconds(1));
            }
            else
            {
                _logger.LogError($"{nameof(SwitchVsysInside)} | Возникла ошибка. isConnected = {isConnected} | systemView = {systemView}");
            }
        }

        /// <summary>
        /// Проверяет, соответствует ли текущее приглашение командной строки (Prompt) заданному регулярному выражению.
        /// </summary>
        /// <param name="pattern">Регулярное выражение для поиска (например, из <see cref="PromptPatterns"/>).</param>
        /// <returns>Возвращает <see langword="true"/>, если текущая строка совпадает с паттерном, иначе <see langword="false"/>.</returns>
        /// <remarks>Отправляет пустой перенос строки (Enter), чтобы устройство "отрисковало" текущий Prompt.</remarks>
        public async Task<bool> CheckPromptAsync(string pattern)
        {
            await ssh.SendDataAsync(Environment.NewLine);
            string reply = await ssh.ReadDataAsync(TimeSpan.FromSeconds(2));
            return Regex.IsMatch(reply.Trim(), pattern, RegexOptions.Multiline);
        }
    }
}