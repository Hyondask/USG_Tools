using System;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace USG_Tools.Core.Extensions
{

    /// <summary>
    /// Компаратор для побайтового сравнения IPv4/IPv6 адресов.
    /// </summary>
    public class IpAddressRangeComparer : IComparer<IPAddress?>
    {
        public int Compare(IPAddress? x, IPAddress? y)
        {
            if (x == null || y == null)
                return x == null && y == null ? 0 : x == null ? -1 : 1;

            byte[] bytesX = x.GetAddressBytes();
            byte[] bytesY = y.GetAddressBytes();

            // Сравниваем байты IP-адресов
            for (int i = 0; i < bytesX.Length; i++)
            {
                if (bytesX[i] != bytesY[i])
                {
                    return bytesX[i].CompareTo(bytesY[i]);
                }
            }
            return 0;
        }
    }
}
