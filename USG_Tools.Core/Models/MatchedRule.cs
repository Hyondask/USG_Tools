using System;
using System.Collections.Generic;
using System.Text;

namespace USG_Tools.Core.Models
{
    public class MatchedRule
    {
        public FirewallRule Rule { get; set; }
        // Список всех найденных совпадений для этого правила
        public List<IpMatchResult> Matches { get; set; } = new List<IpMatchResult>();


    }
}
