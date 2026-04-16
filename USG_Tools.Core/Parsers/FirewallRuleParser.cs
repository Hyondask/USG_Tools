using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using USG_Tools.Core.Models;
using Microsoft.VisualBasic;

namespace USG_Tools.Core.Parsers
{
    public class FirewallRuleParser
    {
        public FirewallRule ParseRule (string rule)
        {
            FirewallRule firewallRule = new FirewallRule() { FullRule = rule};
            var splitrule = rule.Split("\r\n");
            foreach (string raw in splitrule)
            {
                string cleanraw = raw.Trim();
                if (cleanraw.Contains("rule name")) firewallRule.Name = cleanraw.Split(' ')[2];
                if (cleanraw.Contains("parent-group")) firewallRule.ParentGroup = cleanraw.Split(' ')[1];
                if (cleanraw.Contains("description")) firewallRule.Description = cleanraw.Split(' ')[1];
                if (cleanraw.Contains("source-zone")) firewallRule.SourceZone.Add(cleanraw.Split(' ')[1]);
                if (cleanraw.Contains("destination-zone")) firewallRule.DestinationZone.Add(cleanraw.Split(' ')[1]);
                if (cleanraw.Contains("source-address")) firewallRule.SourceAddressRange.Add(ExtractIp(cleanraw));
                if (cleanraw.Contains("destination-address")) firewallRule.DestinationAddressRange.Add(ExtractIp(cleanraw));
                if (cleanraw.Contains("service")) firewallRule.Service.Add(ExtractService(cleanraw));
                if (cleanraw.Contains("action")) firewallRule.Action = cleanraw.Split(' ')[1];
            }
                    return firewallRule;
        }

        private Serviceport ExtractService (string raw)
        {
            Serviceport service = new Serviceport();
            var words = raw.Split(' ');
            if (words.Length >= 5) // Если указывается конкретный порт или диапазон, то сохраняем Прим: service protocol tcp destination-port 10050 ( to 10051 )
            {
                service.Protocol = words[2];
                service.PortRangeStart = int.Parse(words[4]);
                if (words.Length == 7)
                {
                    service.PortRangeEnd = int.Parse(words[6]);
                }
                else service.PortRangeEnd = service.PortRangeStart;


            }
            if (words.Length ==2 ) // Прим: service https_https Сохраняем в Service только название сервиса в ServiceName
            {
                service.ServiceName = words[1]; 
            }
            return service;
        }
        private IpRange ExtractIp(string raw)
        {
            // Объявляем переменную в начале, чтобы она была доступна для возврата
            IpRange iprange = null;

            // Используем самый безопасный способ разбиения на слова
            var words = raw.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (raw.Contains("range") && words.Length == 4)
            {
                // Парсинг формата '... range StartIP EndIP'
                // Предполагаем, что StartIP и EndIP находятся под индексами 2 и 3
                IPAddress start = IPAddress.Parse(words[2]);
                IPAddress end = IPAddress.Parse(words[3]);

                iprange = new IpRange(start, end)
                {
                    RawString = raw
                };
            }
            else if (raw.Contains("mask") && words.Length == 4)
            {
                // Парсинг формата '... IPAddress mask SubnetMask'

                // 1. Извлекаем IP-адрес и Маску
                // В вашем примере: words[1] = "10.1.0.0", words[3] = "255.255.0.0"
                IPAddress ip = IPAddress.Parse(words[1]);
                IPAddress mask = IPAddress.Parse(words[3]);

                // 2. Используем статический метод для расчета диапазона
                iprange = IpRange.FromCidrMask(ip, mask, raw);
            }

            // Если ни одно условие не сработало, iprange останется null.
            // Нужно решить, что возвращать в этом случае (null или, например, одиночный IP-адрес)
            if (iprange == null)
            {
                // Опционально: Если это строка с адресом, но без маски/range, берем одиночный IP
                if (words.Length >= 2 && IPAddress.TryParse(words[1], out IPAddress singleIp))
                {
                    iprange = new IpRange(singleIp)
                    {
                        RawString = raw
                    }; ;
                }
                else
                {
                    // Если не удалось ничего найти, возвращаем null (или выбрасываем исключение)
                    return null;
                }
            }

            return iprange;
        }
    }
}
