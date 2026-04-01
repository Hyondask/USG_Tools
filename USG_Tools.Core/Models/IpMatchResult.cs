using System;
using System.Collections.Generic;
using System.Text;

namespace USG_Tools.Core.Models
{
    public class IpMatchResult
    {
        public IpMigration MigrationData { get; set; }
        public string Direction { get; set; }
        public string MatchedByRange { get; set; } // Сюда запишем конкретный range из конфига
    }
}
