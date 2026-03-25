using System;
using System.Collections.Generic;
using System.Text;

namespace USG_Tools.Core.Models
{
    /// <summary>
    /// Представляет финальную, обогащенную запись маршрута, готовую для сохранения в базу данных SQLite.
    /// Содержит как данные из таблицы маршрутизации, так и информацию о зонах из JSON-конфигурации.
    /// </summary>
    /// <param name="ip_min">Начальный IP-адрес подсети в числовом представлении (uint).</param>
    /// <param name="ip_max">Конечный IP-адрес подсети в числовом представлении (uint).</param>
    /// <param name="cidr">Строковое представление подсети с маской (например, "10.1.0.0/16").</param>
    /// <param name="route">IP-адрес следующего перехода (NextHop) (например, "10.0.8.66").</param>
    /// <param name="zone">Имя зоны безопасности, которой принадлежит интерфейс (например, "TEMP_CORE_AUP").</param>
    /// <param name="interface_name">Имя интерфейса, через который доступен маршрут (например, "Eth-Trunk1.3005").</param>
    /// <param name="zone_in">Родительская группа зоны для входящего трафика (из конфигурации).</param>
    /// <param name="zone_out">Родительская группа зоны для исходящего трафика (из конфигурации).</param>
    public record FinalRoute(
        uint ip_min,
        uint ip_max,
        string cidr,
        string route,
        string zone,
        string interface_name,
        string zone_in,
        string zone_out
    );
}