using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using USG_Tools.Core.Helpers;
using USG_Tools.Core.Models;
using USG_Tools.Core.Parsers;
using USG_Tools.Core.Services;

namespace USG_Tools.Core.Managers
{
    public class RulesManager
    {
        private ILoggerFactory _loggerFactory;
        private ILogger _logger;
        private USGManager _manager;
        private static readonly HashSet<IPAddress> ExclusionStarts = new HashSet<IPAddress>
{
    IPAddress.Parse("0.0.0.0"),
    IPAddress.Parse("10.0.0.0"),
    IPAddress.Parse("10.1.0.0"),
    IPAddress.Parse("10.2.0.0"),
    IPAddress.Parse("10.4.0.0"),
    IPAddress.Parse("10.5.0.0"),
    IPAddress.Parse("10.6.0.0"),
    IPAddress.Parse("10.7.0.0"),
    IPAddress.Parse("10.192.0.0"),
    IPAddress.Parse("10.193.0.0")
    // Адреса, которые исключаем из поиска 
};
        // Список зон-источников, для которых всегда формируются правила OUT
        private static readonly HashSet<string> OutSourceZones = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TEMP_CORE_AUP",
            "TEMP_CORE_KSPD",
            "KSPD_Protected",
            "HQ1"
        };

        public RulesManager(USGManager manager, ILoggerFactory loggerFactory)
        {
            _manager = manager;
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<RulesManager>();
        }
        /// <summary>
        /// Запуск процесса демонтажа правил
        /// </summary>
        /// <returns></returns>
        public async Task RunTeardownWorkFlow()
        {
            _logger.LogInformation("Не реализовано");
        }

        /// <summary>
        /// Запуск процесса миграции адресов из Excel файла 
        /// </summary>
        /// <returns></returns>
        public async Task RunMigrationWorkFlow()
        {
            Console.Clear();
            _logger.LogInformation("Начат сбор отчета для миграции");
            List<IpMigration> ipMigrations = await Task.Run(() => ExcelParser.ReadMigrationIp("AddressBook.xlsx", "Миграция"));

            foreach (var ipMigration in ipMigrations)
            {
                _logger.LogDebug(ipMigration.ToString());
            }

            _logger.LogInformation($"Прочитано {ipMigrations.Count} записей миграции IP");

            _logger.LogInformation($"Начата выгрузка конфигурации с оборудования 10.7.219.11 ");

            await _manager.Connect("10.7.219.11");
            await _manager.UndoScreenLength();
            await _manager.GoToSystemView();
            await _manager.SwitchVsysInside();
            string fullconfig = await _manager.GetInsideCurrentConfig();
            List<FirewallRule> rules = USGParser.ParseSecurityPolicies(fullconfig);
            List<MatchedRule> rulesToMigrate = FindRulesToMigrate(rules, ipMigrations);
            _logger.LogInformation($"Найдено {rulesToMigrate.Count} правил, содержащих IP-адреса для миграции.");
            _logger.LogInformation($"Начата выгрузка address set с оборудования 10.7.219.11");
            List<AddressSet> addressSets = USGParser.ParseAddressSets(fullconfig);
            _logger.LogInformation($"{addressSets.Count} addresses");
            var matchedSets = FindAddressSetsToMigrate(addressSets, ipMigrations);
            string outputExcelFile = "Output/Migrated_rules.xlsx";
            await Task.Run(() =>ExportMigrationPlanToExcel(rulesToMigrate,matchedSets, outputExcelFile));
            _logger.LogInformation($"Отчет успешно сохранён в файл : {outputExcelFile}");

        }


        /// <summary>
        /// Поиск адрес-сетов для миграции
        /// </summary>
        private List<MatchedAddressSet> FindAddressSetsToMigrate(List<AddressSet> allSets, List<IpMigration> ipMigrations)
        {
            // Предполагаю, что у тебя есть класс MatchedAddressSet по аналогии с MatchedRule
            var setsToMigrate = new List<MatchedAddressSet>();
            var validMigrations = ipMigrations.Where(m => m.OldIp != null).ToList();

            // =================================================================
            // 1. БЫСТРАЯ ФИЛЬТРАЦИЯ (Используем UsgSearchEngine)
            // =================================================================
            var oldIpsToSearch = validMigrations.Select(m => m.OldIp);
            var affectedSets = UsgSearchEngine.FindAddressSetsByIps(oldIpsToSearch, allSets);

            // =================================================================
            // 2. ТОЧЕЧНЫЙ ПОИСК (Бизнес-логика)
            // Идем ТОЛЬКО по задетым сетам
            // =================================================================
            foreach (var set in affectedSets)
            {
                var currentSetMatch = new MatchedAddressSet { AddressSet = set };

                foreach (var migration in validMigrations)
                {
                    // Ищем конкретный IpRange внутри словаря Address текущего сета
                    // (Предполагаю, что в сетах логики исключений Any/0.0.0.0 обычно нет, 
                    // но если есть — можешь добавить !ExclusionStarts.Contains)
                    var match = set.Address.Values.FirstOrDefault(r => r != null && r.ContainsIp(migration.OldIp));

                    if (match != null)
                    {
                        // Проверяем на дубликаты
                        if (!currentSetMatch.Matches.Any(m => m.MigrationData.OldIp.Equals(migration.OldIp)))
                        {
                            currentSetMatch.Matches.Add(new IpMatchResult
                            {
                                MigrationData = migration,
                                Direction = "AddressSet", // Помечаем, что нашли в самом сете
                                MatchedByRange = match.ToString()
                            });
                        }
                    }
                }

                if (currentSetMatch.Matches.Count > 0)
                {
                    setsToMigrate.Add(currentSetMatch);
                }
            }

            return setsToMigrate;
        }

        /// <summary>
        /// Поиск правил с вхождением необходимых IP Адресов
        /// Проверяет каждое правило на вхождение Old IP с учетом настроенных фильтров
        /// </summary>
        /// <param name="rules">Список правил для поиска</param>
        /// <param name="ipMigrations">Список IP для миграции </param>
        /// <returns>List<MatchedRule></returns>
        private List<MatchedRule> FindRulesToMigrate(List<FirewallRule> rules, List<IpMigration> ipMigrations)
        {
            var rulesToMigrate = new List<MatchedRule>();
            var validMigrations = ipMigrations.Where(m => m.OldIp != null).ToList();

            // =================================================================
            // 1. БЫСТРАЯ ФИЛЬТРАЦИЯ
            // Достаем старые IP и отсеиваем 99% правил, в которых их вообще нет.
            // =================================================================
            var oldIpsToSearch = validMigrations.Select(m => m.OldIp);
            var affectedRules = UsgSearchEngine.FindRulesByIps(oldIpsToSearch, rules);

            // =================================================================
            // 2. БИЗНЕС-ЛОГИКА 
            // Идем ТОЛЬКО по найденным правилам (affectedRules вместо rules)
            // =================================================================
            foreach (var rule in affectedRules)
            {
                var currentRuleMatch = new MatchedRule { Rule = rule };

                foreach (var migration in validMigrations)
                {
                    // --- ПРОВЕРКА SOURCE ---
                    // Ищем диапазон в Source, который содержит IP и НЕ является исключением
                    var sMatch = rule.SourceAddressRange
                        .FirstOrDefault(r => r != null &&
                                             !ExclusionStarts.Contains(r.RangeStart) &&
                                             r.ContainsIp(migration.OldIp));

                    if (sMatch != null)
                    {
                        // Проверяем на дубликат внутри правила (один IP может быть в нескольких диапазонах)
                        if (!currentRuleMatch.Matches.Any(m => m.MigrationData.OldIp.Equals(migration.OldIp) && m.Direction == "Source"))
                        {
                            currentRuleMatch.Matches.Add(new IpMatchResult
                            {
                                MigrationData = migration,
                                Direction = "Source",
                                MatchedByRange = sMatch.ToString()
                            });
                        }
                    }

                    // --- ПРОВЕРКА DESTINATION ---
                    var dMatch = rule.DestinationAddressRange
                        .FirstOrDefault(r => r != null &&
                                             !ExclusionStarts.Contains(r.RangeStart) && // Логика исключений здесь
                                             r.ContainsIp(migration.OldIp));

                    if (dMatch != null)
                    {
                        if (!currentRuleMatch.Matches.Any(m => m.MigrationData.OldIp.Equals(migration.OldIp) && m.Direction == "Destination"))
                        {
                            currentRuleMatch.Matches.Add(new IpMatchResult
                            {
                                MigrationData = migration,
                                Direction = "Destination",
                                MatchedByRange = dMatch.ToString()
                            });
                        }
                    }
                }

                if (currentRuleMatch.Matches.Count > 0)
                {
                    rulesToMigrate.Add(currentRuleMatch);
                }
            }
            return rulesToMigrate;
        }


        //private void ExportMigrationPlanToExcel(List<MatchedRule> rules, string outputPath)
        //{
        //    var fullCliConfig = new System.Text.StringBuilder();

        //    using (var workbook = new XLWorkbook())
        //    {
        //        var worksheet = workbook.Worksheets.Add("План Миграции");
        //        string[] headers = {
        //            "Изменения",
        //            "Правило до изменений",
        //            "Найденные IP",
        //            "Добавленные IP",
        //            "Диагностика: Зона (Старая -> Новая)",
        //            "Диагностика: Направление",
        //            "Диагностика: Триггер (Matched By)"
        //        };

        //        for (int i = 0; i < headers.Length; i++)
        //        {
        //            worksheet.Cell(1, i + 1).Value = headers[i];
        //            worksheet.Cell(1, i + 1).Style.Font.Bold = true;
        //        }

        //        int rowNum = 2;
        //        foreach (var matchedRule in rules)
        //        {
        //            // 1. Формируем список уникальных ЗАПИСЕЙ для всего правила
        //            var uniqueRecords = matchedRule.Matches
        //                .GroupBy(m => new
        //                {
        //                    Old = m.MigrationData.OldIp?.ToString(),
        //                    New = m.MigrationData.NewIp?.ToString(),
        //                    OldZ = m.MigrationData.OldZone,
        //                    NewZ = m.MigrationData.NewZone,
        //                    Dir = m.Direction,
        //                    Matched = m.MatchedByRange
        //                })
        //                .Select(g => g.First())
        //                .ToList();

        //            // 2. Раскладываем синхронизированные списки
        //            var oldIps = uniqueRecords.Select(m => m.MigrationData.OldIp?.ToString() ?? "").ToList();
        //            var newIps = uniqueRecords.Select(m => m.MigrationData.NewIp?.ToString() ?? "").ToList();
        //            var zones = uniqueRecords.Select(m => $"{m.MigrationData.OldZone ?? "-"} -> {m.MigrationData.NewZone ?? "-"}").ToList();
        //            var directions = uniqueRecords.Select(m => m.Direction ?? "").ToList();
        //            var matchedBys = uniqueRecords.Select(m => m.MatchedByRange ?? "").ToList();

        //            // 3. ГЕНЕРАЦИЯ ЛИСТИНГА (ИЗМЕНЕНИЙ) В ОДНУ ЯЧЕЙКУ
        //            string generatedListing = GenerateMigrationCommands(matchedRule);

        //            // --- Сохраняем полный конфиг для текстового файла ---
        //            fullCliConfig.AppendLine($"! --- ИЗМЕНЕНИЯ ДЛЯ ПРАВИЛА: {matchedRule.Rule.Name} ---");
        //            fullCliConfig.AppendLine(generatedListing);
        //            fullCliConfig.AppendLine();

        //            // --- ЗАЩИТА ОТ ПАДЕНИЯ EXCEL (Лимит 32 767 символов) ---
        //            const int ExcelCellLimit = 32000;
        //            string excelListing = generatedListing;
        //            if (excelListing.Length > ExcelCellLimit)
        //            {
        //                // Выводим яркое предупреждение в консоль
        //                Console.ForegroundColor = ConsoleColor.Yellow;
        //                Console.WriteLine($"[ВНИМАНИЕ] Превышен лимит символов Excel для правила '{matchedRule.Rule.Name}'.");
        //                Console.WriteLine($"           Ячейка в строке {rowNum} будет обрезана! Полный конфиг ищите в TXT-файле.");
        //                Console.ResetColor();

        //                excelListing = excelListing.Substring(0, ExcelCellLimit) +
        //                    "\n\n... [ВНИМАНИЕ: ТЕКСТ ОБРЕЗАН!] ...\n" +
        //                    $"... [ПРЕВЫШЕН ЛИМИТ СИМВОЛОВ EXCEL ДЛЯ ПРАВИЛА {matchedRule.Rule.Name}] ...\n" +
        //                    "... [ПОЛНЫЙ КОНФИГ ДЛЯ ЭТОГО ПРАВИЛА СМОТРИТЕ В TXT-ФАЙЛЕ] ...";
        //            }
        //            // 4. ЗАПИСЬ В EXCEL
        //            worksheet.Cell(rowNum, 1).Value = excelListing; // Пишем безопасную (обрезанную) строку
        //            worksheet.Cell(rowNum, 2).Value = matchedRule.Rule.FullRule ?? "";

        //            worksheet.Cell(rowNum, 3).Value = string.Join("\n", oldIps);
        //            worksheet.Cell(rowNum, 4).Value = string.Join("\n", newIps);
        //            worksheet.Cell(rowNum, 5).Value = string.Join("\n", zones);
        //            worksheet.Cell(rowNum, 6).Value = string.Join("\n", directions);
        //            worksheet.Cell(rowNum, 7).Value = string.Join("\n", matchedBys);

        //            // Выравнивание по верхнему краю и перенос текста
        //            worksheet.Row(rowNum).Style.Alignment.WrapText = true;
        //            worksheet.Row(rowNum).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

        //            rowNum++;
        //        }

        //        worksheet.Column(1).Width = 55;
        //        worksheet.Column(2).Width = 60;
        //        worksheet.Column(3).Width = 15;
        //        worksheet.Column(4).Width = 15;
        //        worksheet.Column(5).Width = 35;
        //        worksheet.Columns(6, 7).AdjustToContents();

        //        workbook.SaveAs(outputPath);
        //    }

        //    // --- НОВОВВЕДЕНИЕ: Сохраняем текстовый файл рядом с Excel ---
        //    string txtOutputPath = outputPath.Replace(".xlsx", "_CLI.txt");
        //    System.IO.File.WriteAllText(txtOutputPath, fullCliConfig.ToString());
        //    Console.WriteLine($"[ИНФО] Полный CLI-конфиг для вставки на файрвол сохранен в: {txtOutputPath}");
        //}

        // Обновленный вызов в основном коде:
        // await Task.Run(() => ExportMigrationPlanToExcel(rulesToMigrate, matchedSets, outputExcelFile));

        private void ExportMigrationPlanToExcel(List<MatchedRule> rules, List<MatchedAddressSet> addressSets, string outputPath)
        {
            var fullCliConfig = new System.Text.StringBuilder();

            using (var workbook = new XLWorkbook())
            {
                // --- ЛИСТ 1: ПРАВИЛА ФАЙРВОЛА ---
                var ruleSheet = workbook.Worksheets.Add("Правила (Security Rules)");
                WriteRulesSheet(ruleSheet, rules, fullCliConfig);

                // --- ЛИСТ 2: АДРЕС-СЕТЫ ---
                var setSheet = workbook.Worksheets.Add("Группы (Address Sets)");
                WriteAddressSetsSheet(setSheet, addressSets);

                workbook.SaveAs(outputPath);
            }

            // Сохраняем текстовый файл (в нем будет и то, и другое)
            string txtOutputPath = outputPath.Replace(".xlsx", "_CLI.txt");
            System.IO.File.WriteAllText(txtOutputPath, fullCliConfig.ToString());
            Console.WriteLine($"[ИНФО] Полный CLI-конфиг сохранен в: {txtOutputPath}");
        }

        private void WriteAddressSetsSheet(IXLWorksheet worksheet, List<MatchedAddressSet> addressSets)
        {
            // 1. Добавили "Address-Set до изменений" в массив заголовков
            string[] headers = {
                "Address-Set до изменений",
                "Имя Address-Set",
                "Описание",
                "Старые IP",
                "Новые IP",
                "Триггер"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(1, i + 1).Value = headers[i];
                worksheet.Cell(1, i + 1).Style.Font.Bold = true;
            }

            int rowNum = 2;

            foreach (var mSet in addressSets)
            {
                var uniqueMatches = mSet.Matches
                    .GroupBy(m => new { m.MigrationData.OldIp, m.MigrationData.NewIp, m.MatchedByRange })
                    .Select(g => g.First())
                    .ToList();

                //string generatedListing = GenerateAddressSetMigrationCommands(mSet);

                //// Добавляем в общий конфиг
                //fullCliConfig.AppendLine(generatedListing);
                //fullCliConfig.AppendLine();

                // 2. ЗАПИСЬ В EXCEL СО СМЕЩЕНИЕМ
                //worksheet.Cell(rowNum, 1).Value = generatedListing.Length > 32000 ? generatedListing.Substring(0, 32000) + "..." : generatedListing;

                // Вставляем оригинальный конфиг (свойство rawstring из твоего класса AddressSet)
                worksheet.Cell(rowNum, 1).Value = mSet.AddressSet.rawstring ?? "";

                worksheet.Cell(rowNum, 2).Value = mSet.AddressSet.Name;
                worksheet.Cell(rowNum, 3).Value = mSet.AddressSet.Description ?? "";
                worksheet.Cell(rowNum, 4).Value = string.Join("\n", uniqueMatches.Select(m => m.MigrationData.OldIp?.ToString()));
                worksheet.Cell(rowNum, 5).Value = string.Join("\n", uniqueMatches.Select(m => m.MigrationData.NewIp?.ToString()));
                worksheet.Cell(rowNum, 6).Value = string.Join("\n", uniqueMatches.Select(m => m.MatchedByRange));

                worksheet.Row(rowNum).Style.Alignment.WrapText = true;
                worksheet.Row(rowNum).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                rowNum++;
            }

            // 3. Красивое форматирование ширины колонок
            worksheet.Column(1).Width = 60; // Оригинальный сет
            worksheet.Column(2).Width = 30; // Имя сета
            worksheet.Columns(4, 7).AdjustToContents();
        }

        // Вспомогательный метод для заполнения вкладки с правилами (Security Rules)
        private void WriteRulesSheet(IXLWorksheet worksheet, List<MatchedRule> rules, System.Text.StringBuilder fullCliConfig)
        {
            // --- 1. ЗАГОЛОВКИ ---
            string[] headers = {
                "Изменения",
                "Правило до изменений",
                "Найденные IP",
                "Добавленные IP",
                "Диагностика: Зона (Старая -> Новая)",
                "Диагностика: Направление",
                "Диагностика: Триггер (Matched By)"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(1, i + 1).Value = headers[i];
                worksheet.Cell(1, i + 1).Style.Font.Bold = true;
            }

            // Добавляем красивый разделитель в общий TXT файл
            fullCliConfig.AppendLine("! ==========================================================");
            fullCliConfig.AppendLine("! === ИЗМЕНЕНИЯ В ПРАВИЛАХ (SECURITY POLICIES) ===");
            fullCliConfig.AppendLine("! ==========================================================");

            int rowNum = 2;

            // --- 2. ЗАПОЛНЕНИЕ ДАННЫМИ ---
            foreach (var matchedRule in rules)
            {
                // 1. Формируем список уникальных ЗАПИСЕЙ для всего правила
                var uniqueRecords = matchedRule.Matches
                    .GroupBy(m => new
                    {
                        Old = m.MigrationData.OldIp?.ToString(),
                        New = m.MigrationData.NewIp?.ToString(),
                        OldZ = m.MigrationData.OldZone,
                        NewZ = m.MigrationData.NewZone,
                        Dir = m.Direction,
                        Matched = m.MatchedByRange
                    })
                    .Select(g => g.First())
                    .ToList();

                // 2. Раскладываем синхронизированные списки
                var oldIps = uniqueRecords.Select(m => m.MigrationData.OldIp?.ToString() ?? "").ToList();
                var newIps = uniqueRecords.Select(m => m.MigrationData.NewIp?.ToString() ?? "").ToList();
                var zones = uniqueRecords.Select(m => $"{m.MigrationData.OldZone ?? "-"} -> {m.MigrationData.NewZone ?? "-"}").ToList();
                var directions = uniqueRecords.Select(m => m.Direction ?? "").ToList();
                var matchedBys = uniqueRecords.Select(m => m.MatchedByRange ?? "").ToList();

                // 3. ГЕНЕРАЦИЯ ЛИСТИНГА (ИЗМЕНЕНИЙ) В ОДНУ ЯЧЕЙКУ
                string generatedListing = GenerateMigrationCommands(matchedRule);

                // --- Сохраняем полный конфиг для текстового файла ---
                fullCliConfig.AppendLine($"! --- ИЗМЕНЕНИЯ ДЛЯ ПРАВИЛА: {matchedRule.Rule.Name} ---");
                fullCliConfig.AppendLine(generatedListing);
                fullCliConfig.AppendLine();

                // --- ЗАЩИТА ОТ ПАДЕНИЯ EXCEL (Лимит 32 767 символов) ---
                const int ExcelCellLimit = 32000;
                string excelListing = generatedListing;
                if (excelListing.Length > ExcelCellLimit)
                {
                    // Выводим яркое предупреждение в консоль
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[ВНИМАНИЕ] Превышен лимит символов Excel для правила '{matchedRule.Rule.Name}'.");
                    Console.WriteLine($"           Ячейка в строке {rowNum} будет обрезана! Полный конфиг ищите в TXT-файле.");
                    Console.ResetColor();

                    excelListing = excelListing.Substring(0, ExcelCellLimit) +
                        "\n\n... [ВНИМАНИЕ: ТЕКСТ ОБРЕЗАН!] ...\n" +
                        $"... [ПРЕВЫШЕН ЛИМИТ СИМВОЛОВ EXCEL ДЛЯ ПРАВИЛА {matchedRule.Rule.Name}] ...\n" +
                        "... [ПОЛНЫЙ КОНФИГ ДЛЯ ЭТОГО ПРАВИЛА СМОТРИТЕ В TXT-ФАЙЛЕ] ...";
                }

                // 4. ЗАПИСЬ В EXCEL
                worksheet.Cell(rowNum, 1).Value = excelListing; // Пишем безопасную (обрезанную) строку
                worksheet.Cell(rowNum, 2).Value = matchedRule.Rule.FullRule ?? "";

                worksheet.Cell(rowNum, 3).Value = string.Join("\n", oldIps);
                worksheet.Cell(rowNum, 4).Value = string.Join("\n", newIps);
                worksheet.Cell(rowNum, 5).Value = string.Join("\n", zones);
                worksheet.Cell(rowNum, 6).Value = string.Join("\n", directions);
                worksheet.Cell(rowNum, 7).Value = string.Join("\n", matchedBys);

                // Выравнивание по верхнему краю и перенос текста
                worksheet.Row(rowNum).Style.Alignment.WrapText = true;
                worksheet.Row(rowNum).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

                rowNum++;
            }

            // --- 3. ФОРМАТИРОВАНИЕ КОЛОНОК ---
            worksheet.Column(1).Width = 55;
            worksheet.Column(2).Width = 60;
            worksheet.Column(3).Width = 15;
            worksheet.Column(4).Width = 15;
            worksheet.Column(5).Width = 35;
            worksheet.Columns(6, 7).AdjustToContents();
        }

        private string GenerateMigrationCommands(MatchedRule matchedRule)
        {
            var listingLines = new List<string>();
            var ruleName = matchedRule.Rule.Name;

            var existingRuleSourceIps = new List<string>();
            var existingRuleDestIps = new List<string>();
            var newRulesByZone = new Dictionary<string, (List<string> SourceIps, List<string> DestIps)>();

            // Список для сбора логов аудита по Корзине 1
            var auditMessages = new List<string>();

            var globalNewSourceIps = matchedRule.Matches.Where(m => m.Direction == "Source").Select(m => m.MigrationData.NewIp?.ToString()).Where(ip => !string.IsNullOrEmpty(ip)).Distinct().ToList();
            var globalNewDestIps = matchedRule.Matches.Where(m => m.Direction == "Destination").Select(m => m.MigrationData.NewIp?.ToString()).Where(ip => !string.IsNullOrEmpty(ip)).Distinct().ToList();
            var globalNewSourceZones = matchedRule.Matches.Where(m => m.Direction == "Source").Select(m => m.MigrationData.NewZone).Where(z => !string.IsNullOrEmpty(z)).Distinct().ToList();
            var globalNewDestZones = matchedRule.Matches.Where(m => m.Direction == "Destination").Select(m => m.MigrationData.NewZone).Where(z => !string.IsNullOrEmpty(z)).Distinct().ToList();

            // 1. Сортируем хосты
            foreach (var match in matchedRule.Matches)
            {
                string newZone = match.MigrationData.NewZone ?? "";
                string newIp = match.MigrationData.NewIp?.ToString() ?? "";

                if (string.IsNullOrWhiteSpace(newIp) || string.IsNullOrWhiteSpace(newZone)) continue;

                if (match.Direction == "Source")
                {
                    // Проверка: Станет ли Source IP внутризонным в старом правиле?
                    bool isIntraExisting = matchedRule.Rule.DestinationZone.Count == 1 &&
                                           matchedRule.Rule.DestinationZone[0].Equals(newZone, StringComparison.OrdinalIgnoreCase);

                    if (matchedRule.Rule.SourceZone.Count == 0 || matchedRule.Rule.SourceZone.Contains(newZone, StringComparer.OrdinalIgnoreCase))
                    {
                        if (isIntraExisting)
                            auditMessages.Add($"[АУДИТ] ПРОПУСК: Source IP ({newIp}) становится внутризонным ({newZone} -> {newZone}). Добавление в старое правило отменено.");
                        else
                            existingRuleSourceIps.Add(newIp);
                    }
                    else
                    {
                        if (!newRulesByZone.ContainsKey(newZone)) newRulesByZone[newZone] = (new List<string>(), new List<string>());
                        newRulesByZone[newZone].SourceIps.Add(newIp);
                    }
                }
                else if (match.Direction == "Destination")
                {
                    // Проверка: Станет ли Dest IP внутризонным в старом правиле?
                    bool isIntraExisting = matchedRule.Rule.SourceZone.Count == 1 &&
                                           matchedRule.Rule.SourceZone[0].Equals(newZone, StringComparison.OrdinalIgnoreCase);

                    if (matchedRule.Rule.DestinationZone.Count == 0 || matchedRule.Rule.DestinationZone.Contains(newZone, StringComparer.OrdinalIgnoreCase))
                    {
                        if (isIntraExisting)
                            auditMessages.Add($"[АУДИТ] ПРОПУСК: Destination IP ({newIp}) становится внутризонным ({newZone} -> {newZone}). Добавление в старое правило отменено.");
                        else
                            existingRuleDestIps.Add(newIp);
                    }
                    else
                    {
                        if (!newRulesByZone.ContainsKey(newZone)) newRulesByZone[newZone] = (new List<string>(), new List<string>());
                        newRulesByZone[newZone].DestIps.Add(newIp);
                    }
                }
            }

            // 2. Команды для СУЩЕСТВУЮЩЕГО правила
            if (existingRuleSourceIps.Any() || existingRuleDestIps.Any())
            {
                listingLines.Add($"rule name {ruleName}");
                AppendAddressLines(listingLines, existingRuleSourceIps, "source-address");
                AppendAddressLines(listingLines, existingRuleDestIps, "destination-address");
                listingLines.Add(""); // Разделитель
            }

            // Выводим логи аудита, если какие-то IP были отброшены
            if (auditMessages.Any())
            {
                listingLines.AddRange(auditMessages.Distinct());
                listingLines.Add(""); // Разделитель
            }

            // 3. Команды для НОВЫХ правил
            foreach (var kvp in newRulesByZone)
            {
                string targetZone = kvp.Key;
                var sourceIps = kvp.Value.SourceIps;
                var destIps = kvp.Value.DestIps;

                var finalSourceZones = sourceIps.Any() ? new List<string> { targetZone } :
                                       (globalNewSourceZones.Any() ? globalNewSourceZones : matchedRule.Rule.SourceZone.Where(z => !string.IsNullOrWhiteSpace(z)).ToList());

                var finalDestZones = destIps.Any() ? new List<string> { targetZone } :
                                     (globalNewDestZones.Any() ? globalNewDestZones : matchedRule.Rule.DestinationZone.Where(z => !string.IsNullOrWhiteSpace(z)).ToList());

                bool isIntraZone = finalSourceZones.Count == 1 && finalDestZones.Count == 1 && finalSourceZones[0].Equals(finalDestZones[0], StringComparison.OrdinalIgnoreCase);
                if (isIntraZone)
                {
                    listingLines.Add($"[АУДИТ] ПРОПУСК: Трафик нового правила становится внутризонным ({finalSourceZones[0]} -> {finalDestZones[0]}). Правило не требуется.");
                    listingLines.Add("");
                    continue;
                }

                bool isOutRule = finalSourceZones.Any(z => OutSourceZones.Contains(z));
                string direction = isOutRule ? "OUT" : "IN";
                string namingZone = isOutRule ? (finalDestZones.FirstOrDefault() ?? targetZone) : targetZone;

                string newRuleName = $"{namingZone}_{direction}_X";
                string newParentGroup = $"{namingZone}_{direction}";

                listingLines.Add($"rule name {newRuleName}");
                listingLines.Add($"    parent-group {newParentGroup}");

                if (!string.IsNullOrEmpty(matchedRule.Rule.Description))
                    listingLines.Add($"    description {matchedRule.Rule.Description}");

                if (isOutRule)
                {
                    if (finalDestZones.Any()) foreach (var dz in finalDestZones) listingLines.Add($"    destination-zone {dz}");
                    else listingLines.Add($"    destination-zone {targetZone}");

                    listingLines.Add($"    destination-zone INTER_DC");
                }
                else
                {
                    if (finalSourceZones.Any()) foreach (var sz in finalSourceZones) listingLines.Add($"    source-zone {sz}");
                    else listingLines.Add($"    source-zone {targetZone}");
                }

                if (sourceIps.Any())
                {
                    AppendAddressLines(listingLines, sourceIps, "source-address");
                }
                else if (globalNewSourceIps.Any())
                {
                    AppendAddressLines(listingLines, globalNewSourceIps, "source-address");
                }
                else
                {
                    foreach (var origSrc in matchedRule.Rule.SourceAddressRange)
                        if (origSrc != null) listingLines.Add(FormatOriginalRange("source-address", origSrc));
                }

                if (destIps.Any())
                {
                    AppendAddressLines(listingLines, destIps, "destination-address");
                }
                else if (globalNewDestIps.Any())
                {
                    AppendAddressLines(listingLines, globalNewDestIps, "destination-address");
                }
                else
                {
                    foreach (var origDest in matchedRule.Rule.DestinationAddressRange)
                        if (origDest != null) listingLines.Add(FormatOriginalRange("destination-address", origDest));
                }

                foreach (var service in matchedRule.Rule.Service)
                {
                    if (service != null)
                    {
                        if (!string.IsNullOrEmpty(service.ServiceName))
                            listingLines.Add($"    service {service.ServiceName}");
                        else if (!string.IsNullOrEmpty(service.Protocol) && service.PortRangeStart.HasValue)
                        {
                            if (service.PortRangeStart == service.PortRangeEnd)
                                listingLines.Add($"    service protocol {service.Protocol} destination-port {service.PortRangeStart}");
                            else
                                listingLines.Add($"    service protocol {service.Protocol} destination-port {service.PortRangeStart} to {service.PortRangeEnd}");
                        }
                    }
                }

                if (!string.IsNullOrEmpty(matchedRule.Rule.Action))
                    listingLines.Add($"    action {matchedRule.Rule.Action}");

                listingLines.Add("");
            }

            return string.Join("\n", listingLines).TrimEnd();
        }
        private static void AppendAddressLines(List<string> lines, List<string> ips, string addressType)
        {
            var distinctIps = ips.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            if (distinctIps.Any())
            {
                var ranges = IpHelper.DetectRanges(distinctIps);
                foreach (var r in ranges)
                {
                    if (r.Start.Equals(r.End))
                        lines.Add($"    {addressType} {r.Start} mask 255.255.255.255");
                    else
                        lines.Add($"    {addressType} range {r.Start} {r.End}");
                }
            }
        }

        private static string FormatOriginalRange(string addressType, IpRange range)
        {
            if (range.RangeStart.Equals(range.RangeEnd))
                return $"    {addressType} {range.RangeStart} mask 255.255.255.255";
            else
                return $"    {addressType} range {range.RangeStart} {range.RangeEnd}";
        }

        /// <summary>
        /// Генерирует CLI команды для замены IP-адресов внутри адрес-сета
        /// </summary>
        private string GenerateAddressSetMigrationCommands(MatchedAddressSet mSet)
        {
            var sb = new System.Text.StringBuilder();

            // Заходим в конфигурацию адрес-сета
            sb.AppendLine($"ip address-set {mSet.AddressSet.Name} type object");

            foreach (var match in mSet.Matches)
            {
                // Для Huawei USG: удаляем старый, добавляем новый
                // Маска 32 используется для одиночного хоста
                sb.AppendLine($" undo address {match.MigrationData.OldIp} 32");
                sb.AppendLine($" address {match.MigrationData.NewIp} 32");
            }

            // Выходим из конфигурации адрес-сета
            sb.AppendLine("quit");

            return sb.ToString();
        }

    }
}
