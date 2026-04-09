using System.Collections.Generic;
using System.Linq;
using System.Net;
using USG_Tools.Core.Models;

namespace USG_Tools.Core.Services
{
    public static class UsgSearchEngine
    {
        /// <summary>
        /// Универсальный поиск: находит все AddressSets, в которых есть хотя бы один IP из списка.
        /// </summary>
        public static List<AddressSet> FindAddressSetsByIps(IEnumerable<IPAddress> ipsToFind, List<AddressSet> allSets)
        {
            var result = new List<AddressSet>();
            var searchHash = new HashSet<IPAddress>(ipsToFind);

            foreach (var set in allSets)
            {
                foreach (var targetIp in searchHash)
                {
                    if (set.ContainsIp(targetIp))
                    {
                        result.Add(set);
                        break;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Универсальный поиск: находит все правила файрвола, где фигурируют искомые IP (напрямую).
        /// </summary>
        public static List<FirewallRule> FindRulesByIps(IEnumerable<IPAddress> ipsToFind, List<FirewallRule> allRules)
        {
            var result = new List<FirewallRule>();
            var searchHash = new HashSet<IPAddress>(ipsToFind);

            foreach (var rule in allRules)
            {
                foreach (var targetIp in searchHash)
                {
                    if (rule.ContainsIp(targetIp))
                    {
                        result.Add(rule);
                        break; 
                    }
                }
            }
            return result;
        }
    }
}