using System;
using System.Collections.Generic;
using System.Text;

namespace USG_Tools.Core.Models
{
    /// <summary>
    /// Представляет конфигурацию правил из файла zone_mappings.json, 
    /// которая используется для обогащения зон родительскими группами.
    /// </summary>
    public class ZoneMapping
    {
        /// <summary>Имя группы или правила для входящего трафика зоны.</summary>
        public string In { get; set; }

        /// <summary>Имя группы или правила для исходящего трафика зоны.</summary>
        public string Out { get; set; }
    }
}