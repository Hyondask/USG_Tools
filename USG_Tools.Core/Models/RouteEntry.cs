using System;
using System.Collections.Generic;
using System.Text;

namespace USG_Tools.Core.Models
{
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
