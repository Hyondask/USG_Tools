using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
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
    }
}
