using System.Net;

namespace USG_Tools.Core.Helpers
{
    public static class IpHelper
    {
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
        /// <summary>
        /// Конвертирует строковую маску (например "255.255.255.0") в длину префикса (24).
        /// </summary>
        public static int GetCidrLengthFromMask(IPAddress maskAddress)
        {
            byte[] maskBytes = maskAddress.GetAddressBytes();
            int cidr = 0;

            foreach (byte b in maskBytes)
            {
                // Считаем количество единиц в битах
                switch (b)
                {
                    case 255: cidr += 8; break;
                    case 254: cidr += 7; break;
                    case 252: cidr += 6; break;
                    case 248: cidr += 5; break;
                    case 240: cidr += 4; break;
                    case 224: cidr += 3; break;
                    case 192: cidr += 2; break;
                    case 128: cidr += 1; break;
                    case 0: return cidr;
                }
                if (b != 255) break; // Прерываем цикл после первого неполного байта
            }
            return cidr;
        }
    }
}
