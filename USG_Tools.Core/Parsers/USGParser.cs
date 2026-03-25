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
            // Изменения: 
            // 1. Убрали лишние \s перед и после приоритета.
            // 2. interfaces теперь захватывает всё до ПЕРВОГО встречного символа #
            @"vpn-instance\s+\S+\s+(?<zoneName>\S+)[\s\S]*?interface\s+of\s+the\s+zone\s+is\s+\(\d+\):\s*(?<interfaces>[^#]*?)\s*(?=#)",
            RegexOptions.Compiled);

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

                // ВРЕМЕННО для отладки: посмотри в консоль, что там внутри
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
