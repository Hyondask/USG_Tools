using ClosedXML;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using USG_Tools.Core.Models;

namespace USG_Tools.Core.Parsers
{
    public class ExcelParser
    {

        // Регулярное выражение для поиска IPv4-адресов.
        private static readonly string IpV4RegexPattern = @"\b(?:[0-9]{1,3}\.){3}[0-9]{1,3}\b";
        private static readonly Regex IpV4Regex = new Regex(IpV4RegexPattern, RegexOptions.Compiled);

        /// <summary>
        /// Считывает список IP-адресов для миграции из файла Excel,
        /// создавая перекрестные пары OldIp/NewIp.
        /// </summary>
        /// <param name="filepath">Путь к файлу Excel.</param>
        /// <returns>Список объектов IpMigration.</returns>
        public static List<IpMigration> ReadMigrationIp(string filepath, string sheetName = "Миграция")
        {
            var ipList = new List<IpMigration>();
            List<string> errorList = new List<string>();

            using (var stream = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var workbook = new XLWorkbook(filepath))
                {
                    // Ищем вкладку по имени. Если не нашли — выбрасываем исключение или берем первую.
                    if (!workbook.Worksheets.TryGetWorksheet(sheetName, out var worksheet))
                    {
                        throw new Exception($"Вкладка '{sheetName}' не найдена в файле {filepath}");
                    }
                    var range = worksheet.RangeUsed();

                    if (range == null || range.RowCount() <= 1)
                    {
                        Console.WriteLine("Файл Excel пуст или содержит только заголовок.");
                        return ipList;
                    }

                    foreach (var row in range.RowsUsed().Skip(1))
                    {
                        string number = row.Cell(1).GetString();
                        string oldZone = row.Cell(4).GetString();
                        string oldIpCellContent = row.Cell(5).GetString();
                        string newIpCellContent = row.Cell(6).GetString();
                        string newZone = row.Cell(7).GetString();

                        List<string> foundOldIps = FindAllIpV4Addresses(oldIpCellContent);
                        List<string> foundNewIps = FindAllIpV4Addresses(newIpCellContent);

                        // 1. Проверка на наличие данных
                        if (!foundOldIps.Any()) errorList.Add($"В поле под номером {number} отсутствует Old IP.");
                        if (!foundNewIps.Any()) errorList.Add($"В поле под номером {number} отсутствует New IP.");

                        if (!foundOldIps.Any() || !foundNewIps.Any()) errorList.Add($"В поле под номером {number} отсутствуют IP ");

                        foreach (string oldIpString in foundOldIps)
                        {
                            if (!IPAddress.TryParse(oldIpString, out IPAddress? oldIp)) continue;

                            // 2. Проверка: Old IP должен быть в сети 10.0.0.0/8
                            if (!IsInternalTenEight(oldIp))
                            {
                                errorList.Add($"В поле под номером {number} адрес Old IP ({oldIpString}) является внешним. Разрешена только сеть 10.0.0.0/8");
                            }

                            foreach (string newIpString in foundNewIps)
                            {
                                if (!IPAddress.TryParse(newIpString, out IPAddress? newIp)) continue;

                                // 2. Проверка: New IP должен быть в сети 10.0.0.0/8
                                if (!IsInternalTenEight(newIp))
                                {
                                    errorList.Add($"В поле под номером {number} адрес New IP ({newIpString}) является внешним. Разрешена только сеть 10.0.0.0/8");
                                }

                                ipList.Add(new IpMigration
                                {
                                    NewZone = newZone,
                                    NewIp = newIp,
                                    OldIp = oldIp,
                                    OldZone = oldZone,
                                    SourceNumber = number
                                });
                            }
                        }
                    }
                }
            }

            // 3. Удаление дубликатов
            // Группируем по OldIp и NewIp и берем по одному первому вхождению из каждой группы
            var uniqueIpList = ipList
                .GroupBy(x => new { Old = x.OldIp?.ToString(), New = x.NewIp?.ToString() })
                .Select(g => g.First())
                .ToList();

            // Выводим в консоль сообщение о том, что дубликаты были удалены
            int removedCount = ipList.Count - uniqueIpList.Count;
            if (removedCount > 0)
            {
                Console.WriteLine($"[Инфо] Удалено дубликатов: {removedCount}");
            }

            // 4. Итоговый вывод (теперь только критические ошибки валидации IP)
            //if (errorList.Any())
            //{
            Console.WriteLine("--- ОШИБКИ ВАЛИДАЦИИ СЕТЕЙ ---");
            errorList.Distinct().ToList().ForEach(e => Console.WriteLine($"- {e}"));
            //return new List<IpMigration>();
            //}

            return uniqueIpList;
        }

        // Вспомогательный метод для проверки сети 10.0.0.0/8
        private static bool IsInternalTenEight(IPAddress ip)
        {
            byte[] bytes = ip.GetAddressBytes();
            return bytes.Length == 4 && bytes[0] == 10;
        }

        /// <summary>
        /// Ищет все валидные IPv4-адреса в строке с помощью регулярного выражения.
        /// </summary>
        /// <param name="text">Строка для анализа (содержимое ячейки).</param>
        /// <returns>Список найденных валидных IP-адресов в виде строк.</returns>
        private static List<string> FindAllIpV4Addresses(string text)
        {
            // Если входная строка пуста, сразу возвращаем пустой список.
            if (string.IsNullOrEmpty(text))
            {
                return new List<string>();
            }

            var matches = IpV4Regex.Matches(text);
            var ipAddresses = new List<string>();

            foreach (Match match in matches)
            {
                // Используем TryParse для окончательной валидации IP-адреса (например, отбрасываем 999.999.999.999)
                if (IPAddress.TryParse(match.Value, out _))
                {
                    ipAddresses.Add(match.Value);
                }
            }

            return ipAddresses;
        }

    }
}
