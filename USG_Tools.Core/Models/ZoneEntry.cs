using System;
using System.Collections.Generic;
using System.Text;

namespace USG_Tools.Core.Models
{
    /// <summary>
    /// Представляет конфигурацию зоны безопасности, спарсенную с устройства.
    /// </summary>
    /// <param name="Name">Имя зоны безопасности (например, "TEMP_CORE_AUP").</param>
    /// <param name="Interfaces">Список имен интерфейсов, привязанных к данной зоне.</param>
    public record ZoneEntry(string Name, List<string> Interfaces);
}