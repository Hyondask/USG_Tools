using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace USG_Tools.CLI.Utils
{
    public class ConsoleUtils
    {

        /// <summary>
        /// Заполнение строки с чувствтительными данными.
        /// Прячет введенные пользователем символы 
        /// </summary>
        /// <returns>Возвращает <see cref="string"/> с заполненными данными от клиента </returns>
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

        /// <summary>
        /// Обязательное заполнение строки.
        /// При вводе пустой строки выход из метода не произойдет
        /// </summary>
        /// <returns>Возвращает <see cref="string"/> с заполненными данными от клиента</returns>
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

        /// <summary>
        /// Метод, интерпретирующий ответ y/n как True/False
        /// Выводит y/n в консоль и ждет ввода пользователя
        /// </summary>
        /// <returns> Возвращает <see cref="bool"/> ответ пользователя <see langword="true"/>/<see langword="false"/> "/></returns>
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
        /// <summary>
        /// Принимает строку клиента и проверяет является ли строка ip адресом или нет
        /// Если строка не является IP адресом, то просит клиента ввести IP Адрес
        /// </summary>
        /// <returns>Возвращает <see cref="string"/> Валидный Ip адрес</returns>
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
        /// <summary>
        /// Принимает от клиента список IP адресов с проверкой IP адреса на подлинность. В строке принимает по одному адресу
        /// Завершает сбор при вводе пустой строки
        /// </summary>
        /// <returns></returns>
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
        /// <summary>
        /// Проверка, является ли указанный символ специальным символом
        /// </summary>
        /// <param name="keyInfo">Символ для проверки</param>
        /// <returns>Возвращает <see cref="bool" /></returns>
        private static bool CheckSpecialKeys(ConsoleKeyInfo keyInfo)
        {
            if (char.IsControl(keyInfo.KeyChar)) { return true; }
            return false;
        }

        /// <summary>
        /// Метод для удаление последнего символа из строки
        /// </summary>
        /// <param name="sb"> <see cref="StringBuilder"/> строка</param>
        /// <returns><see cref="StringBuilder"/> строка без последнего символа</returns>
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
