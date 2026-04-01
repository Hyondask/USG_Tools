using System;
using System.Collections.Generic;
using System.Text;

namespace USG_Tools.Core.Models
{
    /// <summary>
    /// Класс-модель для хранения протоколов в правиле
    /// </summary>
    public class Serviceport
    {
        public string? ServiceName { get; set; }
        public string? Protocol { get; set; }
        public int? PortRangeStart { get; set; }
        public int? PortRangeEnd { get; set; }

    }
}
