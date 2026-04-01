using System;
using System.Collections.Generic;
using System.Net;


namespace USG_Tools.Core.Extensions
{
    public static class NetworkExtensions
    {
        /// <summary>
        /// Преобразует IPAddress в беззнаковое целое 32-бит.
        /// Учитывает порядок байтов (Big Endian -> Little Endian).
        /// </summary>
        public static uint ToUint(this IPAddress ipAddress)
        {
            var bytes = ipAddress.GetAddressBytes();

            // Если архитектура процессора Little Endian (Windows/Linux x64), 
            // переворачиваем массив байтов из сетевого порядка.
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return BitConverter.ToUInt32(bytes, 0);
        }

        /// <summary>
        /// Принимает строку вида "10.0.0.0/8" и возвращает границы диапазона в uint.
        /// </summary>
        /// <param name="cidr">Строка адреса с маской</param>
        /// <returns>Кортеж (StartIp, EndIp)</returns>
        public static (uint StartIp, uint EndIp) GetUintRange(string cidr)
        {
            try
            {
                var network = IPNetwork2.Parse(cidr);

                // Используем Network (первый адрес) и Broadcast (последний адрес)
                uint start = network.Network.ToUint();
                uint end = network.Broadcast.ToUint();

                return (start, end);
            }
            catch (Exception)
            {
                // Если парсинг не удался (битая строка), возвращаем нули
                return (0, 0);
            }
        }
        /// <summary>
        /// Анализирует список строковых IP-адресов, отфильтровывает невалидные, удаляет дубликаты 
        /// и группирует идущие подряд адреса в непрерывные диапазоны.
        /// </summary>
        /// <param name="ips">Коллекция строковых представлений IP-адресов (например, "10.0.0.1", "10.0.0.2").</param>
        /// <returns>
        /// Возвращает список кортежей <see cref="ValueTuple{IPAddress, IPAddress}"/>, представляющих диапазоны. 
        /// Если IP-адрес не имеет смежных адресов, значения <c>Start</c> и <c>End</c> в кортеже будут совпадать.
        /// </returns>
        /// <remarks>
        /// Метод автоматически сортирует адреса по возрастанию перед поиском диапазонов. 
        /// Некорректные строки, которые не удалось спарсить в <see cref="IPAddress"/>, молча игнорируются.
        /// </remarks>
        public static List<(IPAddress Start, IPAddress End)> DetectRanges(IEnumerable<string> ips)
        {
            // 1. Парсим и конвертируем IP в числа для удобного сравнения (+1)
            var validIps = ips
                .Select(ip => IPAddress.TryParse(ip.Trim(), out var parsed) ? parsed : null)
                .Where(ip => ip != null)
                .Select(ip => new { IpObj = ip, UIntVal = IpToUInt(ip) })
                .OrderBy(x => x.UIntVal)
                .GroupBy(x => x.UIntVal).Select(g => g.First()) // Убираем дубликаты
                .ToList();

            var ranges = new List<(IPAddress Start, IPAddress End)>();
            if (!validIps.Any()) return ranges;

            var startIp = validIps[0];
            var currentIp = validIps[0];

            // 2. Ищем идущие подряд адреса
            for (int i = 1; i < validIps.Count; i++)
            {
                if (validIps[i].UIntVal == currentIp.UIntVal + 1)
                {
                    currentIp = validIps[i]; // IP идут подряд, расширяем диапазон
                }
                else
                {
                    ranges.Add((startIp.IpObj, currentIp.IpObj)); // Диапазон прервался, сохраняем
                    startIp = validIps[i];
                    currentIp = validIps[i];
                }
            }
            ranges.Add((startIp.IpObj, currentIp.IpObj)); // Сохраняем последний хвост

            return ranges;
        }

        private static uint IpToUInt(IPAddress ip)
        {
            byte[] bytes = ip.GetAddressBytes();
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }

    }
}
