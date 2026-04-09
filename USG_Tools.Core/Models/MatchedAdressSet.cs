using System.Collections.Generic;

namespace USG_Tools.Core.Models // Убедись, что namespace совпадает с твоим
{
    /// <summary>
    /// Класс для хранения адрес-сета и списка совпадений по IP-адресам для миграции
    /// </summary>
    public class MatchedAddressSet
    {
        // Ссылка на сам найденный адрес-сет
        public AddressSet AddressSet { get; set; }

        // Список совпадений (используем тот же класс IpMatchResult, что и в правилах)
        public List<IpMatchResult> Matches { get; set; } = new List<IpMatchResult>();
    }
}