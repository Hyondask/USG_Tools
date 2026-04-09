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
    }
}
