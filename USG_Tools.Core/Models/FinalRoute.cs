using System;
using System.Collections.Generic;
using System.Text;

namespace USG_Tools.Core.Models
{
    public record FinalRoute(
        uint ip_min,
        uint ip_max,
        string cidr,     // Например, "10.1.0.0/16"
        string route,    // NextHop (например, "10.0.8.66")
        string zone,     // Имя зоны (например, "TEMP_CORE_AUP")
        string interface_name,
        string zone_in,
        string zone_out
    );
}
