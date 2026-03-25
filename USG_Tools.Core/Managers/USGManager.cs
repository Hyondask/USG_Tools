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
    public class USGManager
    {
        private readonly UserCredentials _userCredentials;

        private readonly ILogger<USGManager> _logger;

        private SshConnection ssh;

        public USGManager (UserCredentials userCredentials, ILogger<USGManager> logger = null)
        {
            _userCredentials = userCredentials;
            _logger = logger ?? NullLogger<USGManager>.Instance;

            _logger.LogDebug("USGManager инициализирован (используется {LoggerType})", _logger.GetType().Name);
        }



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

        public async Task<string> GetInsideRoutes()
        {
            return await ExecuteCommandInVsysAsync("display ip routing-table | exclude ([0-9\\.]\\/32|[0-9\\.]\\/30|Eth.*\\.400|NULL0|0\\.0\\.0\\.0\\/0|Virtual\\-if)", TimeSpan.FromSeconds(20));
        }

        public async Task<string> GetInsideZones()
        {
            return await ExecuteCommandInVsysAsync("display zone", TimeSpan.FromSeconds(30));
        }

        // Вспомогательный метод, чтобы не дублировать проверки во всех Get... методах
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

        public async Task UndoScreenLength()
        {
            _logger.LogDebug ($"{nameof(UndoScreenLength)}");
            
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

        public async Task<bool> CheckPromptAsync(string pattern)
        {
            await ssh.SendDataAsync(Environment.NewLine);
            string reply = await ssh.ReadDataAsync(TimeSpan.FromSeconds(2));
            return Regex.IsMatch(reply.Trim(), pattern, RegexOptions.Multiline);
        }
    }
}
