using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;

namespace USG_Tools.Core.Models
{
    public class IpMigration
    {
        public string SourceNumber { get; set; }
        public string? OldZone { get; set; }
        public IPAddress? OldIp { get; set; }
        public string? NewZone { get; set; }
        public IPAddress? NewIp { get; set; } = IPAddress.Any;
        // Константы для форматирования, чтобы легко менять ширину
        private const int IpColumnWidth = 20;
        private const int ZoneColumnWidth = 15;

        /// <summary>
        /// Предоставляет строковое представление объекта IpMigration в формате таблицы 
        /// с фиксированной шириной столбцов для ровного вывода.
        /// </summary>
        /// <returns>Строка с информацией о миграции IP-адресов в ровном табличном стиле.</returns>
        [SuppressMessage("ReSharper", "NotNullReferenceMemberIsNotInitialized")]
        public override string ToString()
        {
            // 1. Подготовка данных: заменяем null на маркер "-"
            string oldIpString = OldIp?.ToString() ?? "-";
            string newIpString = NewIp?.ToString() ?? "-";
            string zoneString = string.IsNullOrWhiteSpace(OldZone) ? "-" : OldZone;

            // 2. Сборка строки с использованием string.Format()
            // {0,-20} : 0-й аргумент, выровнен по левому краю (-), ширина 20.
            string formatString = $"| {{0,-{IpColumnWidth}}} | {{1,-{IpColumnWidth}}} | {{2,-{ZoneColumnWidth}}} |";

            return string.Format(
                formatString,
                oldIpString,
                newIpString,
                zoneString
            );
        }
    }
}
