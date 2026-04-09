using System;
using System.Net;
using System.Collections.Generic;
using System.Text;
using USG_Tools.Core.Extensions;

namespace USG_Tools.Core.Models
{
    /// <summary>
    /// Класс для представления одного правила диапазона IP-адресов.
    /// </summary>
    public class IpRange
    {
        public IPAddress RangeStart { get; set; }
        public IPAddress RangeEnd { get; set; }

        //public string RawString { get; set; }

        //public IpRange()
        //{
        //    //RangeStart = IPAddress.Parse("0.0.0.0");
        //    //RangeEnd = IPAddress.Parse("255.255.255.255");
        //}
        /// <summary>
        /// Конструктор, для добавления одиночного IP адреса 
        /// </summary>
        /// <param name="iPAddress"> IP адрес </param>
        public IpRange(IPAddress iPAddress)
        {
            RangeStart = iPAddress;
            RangeEnd = iPAddress;
        }

        /// <summary>
        /// Конструктор для добавления диапазона ип адресов 
        /// </summary>
        /// <param name="rangeStart">Начало диапазона IP Адреса </param>
        /// <param name="rangeEnd">Конец диапазона IP Адреса </param>
        public IpRange(IPAddress rangeStart, IPAddress rangeEnd)
        {
            RangeStart = rangeStart;
            RangeEnd = rangeEnd;
        }
        /// <summary>
        /// Проверяет, входит ли заданный IP-адрес в этот диапазон.
        /// </summary>
        public bool ContainsIp(IPAddress ipToCheck)
        {
            if (ipToCheck == null) return false;

            // Вспомогательный класс для сравнения IP-адресов
            var comparer = new IpAddressRangeComparer();

            // IP должен быть больше или равен началу диапазона
            bool isGreaterOrEqual = comparer.Compare(ipToCheck, this.RangeStart) >= 0;

            // IP должен быть меньше или равен концу диапазона
            bool isLessOrEqual = comparer.Compare(ipToCheck, this.RangeEnd) <= 0;

            return isGreaterOrEqual && isLessOrEqual;
        }

        /// <summary>
        /// Статический метод для расчета диапазона (Адрес сети и Бродкаст) из IP и Маски.
        /// </summary>
        /// <param name="ipAddress">IP-адрес, входящий в сеть.</param>
        /// <param name="subnetMask">Маска подсети.</param>
        /// <returns>Новый объект IpRange с адресом сети и бродкастом.</returns>
        public static IpRange FromCidrMask(IPAddress ipAddress, IPAddress subnetMask)
        {
            // 1. Получаем байты
            byte[] ipBytes = ipAddress.GetAddressBytes();
            byte[] maskBytes = subnetMask.GetAddressBytes();

            // Массив байтов для адреса сети и бродкаста
            byte[] networkBytes = new byte[ipBytes.Length];
            byte[] broadcastBytes = new byte[ipBytes.Length];

            // 2. Побитовые операции
            for (int i = 0; i < ipBytes.Length; i++)
            {
                // Адрес сети (Network Address): IP & Mask
                networkBytes[i] = (byte)(ipBytes[i] & maskBytes[i]);

                // Широковещательный адрес (Broadcast Address): IP | (~Mask)
                // ~Mask (Инверсия маски) - это маска хоста. 
                // Обратите внимание на "~" (побитовое НЕ)
                broadcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
            }

            // 3. Создаем объекты IPAddress и возвращаем диапазон
            IPAddress networkAddress = new IPAddress(networkBytes);
            IPAddress broadcastAddress = new IPAddress(broadcastBytes);

            return new IpRange(networkAddress, broadcastAddress);
        }
        public override string ToString()
        {
            // Если начало и конец совпадают — это одиночный IP
            if (RangeStart.Equals(RangeEnd))
            {
                return RangeStart.ToString();
            }
            // Если разные — это диапазон
            return $"{RangeStart} - {RangeEnd}";
        }

    }
}
