using System;
using System.Collections.Generic;
using System.Text;

namespace USG_Tools.Core.Models
{
    /// <summary>
    /// Представляет сырую запись маршрута, полученную после парсинга текста от устройства.
    /// </summary>
    /// <param name="minIp">Начальный IP-адрес назначения в виде числа.</param>
    /// <param name="maxIp">Конечный IP-адрес назначения в виде числа.</param>
    /// <param name="Destination">Сеть назначения в строковом формате (например, "10.0.0.0/8").</param>
    /// <param name="Protocol">Протокол маршрутизации (например, "O_ASE", "Static", "Direct").</param>
    /// <param name="Preference">Приоритет (Preference) маршрута.</param>
    /// <param name="Cost">Метрика (Cost) маршрута.</param>
    /// <param name="Flags">Флаги состояния маршрута (например, "D").</param>
    /// <param name="NextHop">IP-адрес шлюза (NextHop).</param>
    /// <param name="Interface">Исходящий интерфейс маршрута.</param>
    public record RouteEntry(
        uint minIp,
        uint maxIp,
        string Destination,
        string Protocol,
        int Preference,
        int Cost,
        string Flags,
        string NextHop,
        string Interface
    );
}