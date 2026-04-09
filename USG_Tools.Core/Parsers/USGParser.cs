using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using USG_Tools.Core.Extensions;
using USG_Tools.Core.Models;

namespace USG_Tools.Core.Parsers
{
    public static class USGParser
    {

        private static readonly Regex RouteRegex = new Regex(
        @"^[ ]+(?<dest>[\d\.\/]+)\s+(?<proto>\w+)\s+(?<pre>\d+)\s+(?<cost>\d+)\s+(?<flags>\w+)?\s+(?<next>[\d\.]+)\s+(?<intf>[\w\.\-\/]+)",
        RegexOptions.Compiled | RegexOptions.Multiline);

        // Паттерн для зон (учитывает description и другие поля между именем и интерфейсами)
        private static readonly Regex ZoneRegex = new Regex(
            @"vpn-instance\s+\S+\s+(?<zoneName>\S+)[\s\S]*?interface\s+of\s+the\s+zone\s+is\s+\(\d+\):\s*(?<interfaces>[^#]*?)\s*(?=#)",
            RegexOptions.Compiled);

        // Адрес сеты
        // 1. Вырезает блок адрес-сета целиком (от "ip address-set" до "#")
        private static readonly Regex AddressSetBlockRegex = new Regex(
            @"ip address-set\s+(?<name>\S+)[^\r\n]*\r?\n(?<body>.*?)(?=\r?\n\s*#|\r?\n\s*ip address-set|\z)",
            RegexOptions.Compiled | RegexOptions.Singleline);

        // 2. Ищет общее описание (description) на отдельной строке
        private static readonly Regex AddressSetDescRegex = new Regex(
            @"^\s*description\s+(?<desc>.+)$",
            RegexOptions.Compiled | RegexOptions.Multiline);

        private static readonly Regex AddressSetItemRegex = new Regex(
            @"^\s*address\s+\d+\s+.*$",
            RegexOptions.Compiled | RegexOptions.Multiline);

        // Security-policy rule
        // Регулярка для вырезания блока политик 
        // RegexOptions.Singleline позволяет точке (.) захватывать переносы строк
        private static readonly Regex SecurityPolicyBlockRegex = new Regex(
            @"^security-policy\r?\n(.+?)(?:^#|^auth-policy)",
            RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);


        /// <summary>
        /// Парсит сырой лог полного конфига и возвращает список объектов AddressSet
        /// </summary>
        /// <param name="fullConfig">Сырой лог конфига</param>
        /// <returns></returns>
        /// <summary>
        /// Парсит сырой лог полного конфига и возвращает список объектов AddressSet
        /// </summary>
        /// <summary>
        /// Парсит сырой лог полного конфига и возвращает список объектов AddressSet
        /// </summary>
        /// <param name="fullConfig">Сырой лог конфига</param>
        /// <returns>Список AddressSet</returns>
        public static List<AddressSet> ParseAddressSets(string fullConfig)
        {
            var addressSets = new List<AddressSet>();
            var errors = new List<string>(); // Копилка для ошибок
            var blockMatches = AddressSetBlockRegex.Matches(fullConfig);

            foreach (Match blockMatch in blockMatches)
            {
                var addressSet = new AddressSet()
                {
                    rawstring = blockMatch.Value,
                    Name = blockMatch.Groups["name"].Value.Trim()
                };
                string body = blockMatch.Groups["body"].Value;

                // 1. Ищем общее описание
                var descMatch = AddressSetDescRegex.Match(body);
                if (descMatch.Success)
                {
                    addressSet.Description = descMatch.Groups["desc"].Value.Trim();
                }

                // 2. Ищем все строки адресов внутри блока 
                var itemMatches = AddressSetItemRegex.Matches(body);

                foreach (Match itemMatch in itemMatches)
                {
                    string rawLine = itemMatch.Value.Trim();
                    // Бьем строку на массив слов
                    string[] parts = rawLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                    // Минимальная длина: "address 0 10.1.1.1" или "address 0 address-set Name"
                    if (parts.Length < 3) continue;

                    if (!int.TryParse(parts[1], out int id)) continue;

                    // --- СЦЕНАРИЙ 1: ДИАПАЗОН (range) ---
                    if (parts[2] == "range" && parts.Length >= 5)
                    {
                        if (System.Net.IPAddress.TryParse(parts[3], out var startIp) &&
                            System.Net.IPAddress.TryParse(parts[4], out var endIp))
                        {
                            addressSet.Address[id] = new IpRange(startIp, endIp);
                        }
                        else
                        {
                            errors.Add($"[Сет: {addressSet.Name}] Ошибка в range: {parts[3]} - {parts[4]}");
                        }
                    }
                    // --- СЦЕНАРИЙ 2: ВЛОЖЕННЫЙ СЕТ (address-set) ---
                    else if (parts[2] == "address-set" && parts.Length >= 4)
                    {
                        string nestedSetName = parts[3];
                        addressSet.NestedGroups[id] = nestedSetName; // Просто сохраняем имя, например "R00PassportA"
                    }
                    // --- СЦЕНАРИЙ 3: ОБЫЧНЫЙ IP (с 0 или mask) ---
                    else if (System.Net.IPAddress.TryParse(parts[2], out var parsedIp))
                    {
                        if (parts.Length >= 4)
                        {
                            string maskString = parts[3];

                            if (maskString == "mask" && parts.Length >= 5)
                            {
                                if (System.Net.IPAddress.TryParse(parts[4], out var parsedMask))
                                {
                                    addressSet.Address[id] = IpRange.FromCidrMask(parsedIp, parsedMask);
                                }
                                else
                                {
                                    errors.Add($"[Сет: {addressSet.Name}] Не удалось распарсить маску: {parts[4]}");
                                }
                            }
                            else if (maskString == "0")
                            {
                                addressSet.Address[id] = new IpRange(parsedIp);
                            }
                            else
                            {
                                errors.Add($"[Сет: {addressSet.Name}] Неподдерживаемый формат маски: {maskString} для IP {parts[2]}");
                            }
                        }
                    }
                    else
                    {
                        errors.Add($"[Сет: {addressSet.Name}] Неизвестный формат адреса: '{rawLine}'");
                    }

                    // --- ДОПОЛНИТЕЛЬНО: Поиск description внутри строки ---
                    int descIndex = Array.IndexOf(parts, "description");
                    if (descIndex != -1 && descIndex + 1 < parts.Length && string.IsNullOrEmpty(addressSet.Description))
                    {
                        addressSet.Description = string.Join(" ", parts.Skip(descIndex + 1));
                    }
                }

                // Добавляем сет всегда, даже если в нем 0 IP-адресов
                addressSets.Add(addressSet);
            }

            // Выводим все ошибки одним списком, если они были
            if (errors.Count > 0)
            {
                string errorMessage = "КРИТИЧЕСКАЯ ОШИБКА ПАРСИНГА ADDRESS-SETS. Ошибки парсинга в конфиге:\n- "
                                      + string.Join("\n- ", errors);
                throw new Exception(errorMessage);
            }

            return addressSets;
        }

        /// <summary>
        /// Парсит сырой лог полного конфига и возвращает список объектов FirewallRule.
        /// </summary>
        public static List<FirewallRule> ParseSecurityPolicies(string fullConfig)
        {
            var rulesList = new List<FirewallRule>();

            // 1. Вырезаем только блок политик
            var match = SecurityPolicyBlockRegex.Match(fullConfig);
            if (!match.Success)
            {
                // Если блока политик нет, возвращаем пустой список
                return rulesList;
            }

            string policyBlock = match.Groups[1].Value;

            // 2. Бьем текст блока на отдельные правила 
            // Исключаем пустые элементы
            var rawRules = Regex.Split(policyBlock, @"(?=rule name)")
                                .Where(r => !string.IsNullOrWhiteSpace(r))
                                .ToList();

            // 3. Парсим каждое правило
            var ruleParser = new FirewallRuleParser();
            foreach (var rawRule in rawRules)
            {
                // ruleParser сам найдет source, destination, action и т.д.
                rulesList.Add(ruleParser.ParseRule(rawRule));
            }

            return rulesList;
        }

        public static List<RouteEntry> ParseRoutes(string rawData)
        {
            var routes = new List<RouteEntry>();

            // Находим все совпадения
            var matches = RouteRegex.Matches(rawData);

            foreach (Match match in matches)
            {
                var (start, end) = NetworkExtensions.GetUintRange(match.Groups["dest"].Value);
                routes.Add(new RouteEntry(
                    minIp: start,
                    maxIp: end,
                    Destination: match.Groups["dest"].Value,
                    Protocol: match.Groups["proto"].Value,
                    Preference: int.Parse(match.Groups["pre"].Value),
                    Cost: int.Parse(match.Groups["cost"].Value),
                    Flags: match.Groups["flags"].Value, // Может быть пустым
                    NextHop: match.Groups["next"].Value,
                    Interface: match.Groups["intf"].Value
                ));
            }

            return routes;
        }

        public static List<ZoneEntry> ParseZones(string rawData)
        {
            var zones = new List<ZoneEntry>();

            // Ищем все блоки зон в тексте
            var matches = ZoneRegex.Matches(rawData);

            foreach (Match match in matches)
            {
                string name = match.Groups["zoneName"].Value.Trim();
                string rawInterfaces = match.Groups["interfaces"].Value;

                // ВРЕМЕННО для отладки: 
                //Console.WriteLine($"Zone: {name}, Raw content: [{rawInterfaces}]");

                var interfaceList = rawInterfaces
                    .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries) // Добавил явный \r\n
                    .Select(i => i.Trim())
                    .Where(i => !string.IsNullOrEmpty(i))
                    // Убираем возможные артефакты, если Regex захватил лишние пробелы перед #
                    .Where(i => i != "#")
                    .ToList();

                zones.Add(new ZoneEntry(name, interfaceList));
            }

            return zones;
        }
    }
}
