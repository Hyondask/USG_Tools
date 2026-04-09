using System;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace USG_Tools.Core.Models
{
    /* Класс модель в котором будут находится правила на USG
      * 
      */
    public class FirewallRule
    {
        public string Name { get; set; }
        public string? Description { get; set; }
        public string? ParentGroup { get; set; }
        public List<string?> SourceZone { get; set; } = new List<string?>();
        public List<string?> DestinationZone { get; set; } = new List<string?>();
        public List<IpRange?> SourceAddressRange { get; set; } = new List<IpRange?>();
        public List<IpRange?> DestinationAddressRange { get; set; } = new List<IpRange?>();
        public List<Serviceport?> Service { get; set; } = new List<Serviceport?>();
        public string? Action { get; set; }

        public string FullRule { get; set; } // Храним фулл правило


        public bool ContainsIp(IPAddress targetIp)
        {
            // Проверяем список Source 
            if (SourceAddressRange != null)
            {
                foreach (IpRange ipRange in SourceAddressRange)
                {
                    if (ipRange != null && ipRange.ContainsIp(targetIp)) return true; 

                }
            }

            // Проверяем список Destination
            if (DestinationAddressRange != null)
            {
                foreach (IpRange ipRange in DestinationAddressRange)
                {
                    if (ipRange!= null &&  ipRange.ContainsIp(targetIp)) return true;
                }
            }

            return false;

        }
    }

}
