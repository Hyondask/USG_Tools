using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace USG_Tools.CLI.Utils
{
    public class ConsoleUtils
    {

        /// <summary>
        /// Заполнение строки с закрытими данными
        /// </summary>
        /// <returns></returns>
        public static string ReadSecret()
        {
            var sb = new StringBuilder();

            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); break; }

                if (key.Key == ConsoleKey.Backspace)
                {
                    RemoveLastKey(sb); continue;
                }  // Удаление символа при нажатии backspace 
                if (!CheckSpecialKeys(key))
                {
                    sb.Append(key.KeyChar);
                    Console.Write('*');
                }

            }

            return sb.ToString();
        }

        public static string GetNotEmptyString()
        {
            string data = string.Empty;
            while (true)
            {
                data = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(data))
                {
                    break;
                }
            }
            return data;
        }

        public static bool GetYesOrNo()
        {
            while (true)
            {
                Console.Write("y/n");
                switch (Console.ReadLine().ToLower())
                {
                    case "y": return true;
                    case "n": return false;
                }
            }
        }

        public static string GetIp()
        {
            while (true)
            {
                string ip = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(ip))
                {
                    if (IPAddress.TryParse(ip, out IPAddress ipaddress))
                    {
                        return ipaddress.ToString();
                    }
                }
            }

        }
        public static List<string> GetIpList()
        {
            List<string> iplist = new List<string>();
            if (iplist.Count == 0)
            {
                while (true)
                {
                    string ip = Console.ReadLine();
                    if (!string.IsNullOrWhiteSpace(ip))
                    {
                        if (IPAddress.TryParse(ip, out IPAddress ipaddress))
                        {
                            iplist.Add(ipaddress.ToString());
                        }
                    }
                    else { break; }
                }
            }
            return iplist;
        }
        private static bool CheckSpecialKeys(ConsoleKeyInfo keyInfo)
        {
            if (char.IsControl(keyInfo.KeyChar)) { return true; }
            return false;
        }

        private static StringBuilder RemoveLastKey(StringBuilder sb)
        {
            if (sb.Length > 0)
            {
                sb.Remove(sb.Length - 1, 1);
                Console.Write("\b \b");
            }
            return sb;
        }
    }
}
