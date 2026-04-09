    using System.Collections.Generic;
    using System.Net;

    namespace USG_Tools.Core.Models
    {
        public class AddressSet
        {
            public string rawstring {  get; set; }
            public string Name { get; set; }
            public string? Description { get; set; }
            // Храним конкретные IP-адреса и диапазоны
            public Dictionary<int, IpRange> Address { get; set; } = new Dictionary<int, IpRange>();

            // Храним только ИМЕНА вложенных адрес-сетов
            public Dictionary<int, string> NestedGroups { get; set; } = new Dictionary<int, string>();

            /// <summary>
            /// Проверяет, входит ли указанный IP-адрес в собственные диапазоны этого сета (БЕЗ учета вложенных групп).
            /// </summary>
            /// <param name="targetIp">Искомый IP-адрес</param>
            /// <returns>True, если IP найден, иначе False</returns>
            public bool ContainsIp(IPAddress targetIp)
            {
                foreach (var range in this.Address.Values)
                {
                    if (range.ContainsIp(targetIp))
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
