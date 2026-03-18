using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace GETravelGames.PrizeManager
{
    public sealed class PrizeCsvService
    {
        // ── Prize import ─────────────────────────────────────────────────────

        public PrizeCsvImportPreview PreviewPrizeImport(string filePath, PrizeImportMode importMode)
        {
            var preview = new PrizeCsvImportPreview { ImportMode = importMode };
            if (!TryReadText(filePath, out var csvContent, preview.Issues))
            {
                return preview;
            }

            return PreviewPrizeImportContent(csvContent, importMode);
        }

        public PrizeCsvImportPreview PreviewPrizeImportContent(string csvContent, PrizeImportMode importMode)
        {
            var preview = new PrizeCsvImportPreview { ImportMode = importMode };

            if (string.IsNullOrWhiteSpace(csvContent))
            {
                preview.Issues.Add(CreateIssue(0, 0, "File", "CSV content is empty."));
                return preview;
            }

            var rows = ParseCsvRows(csvContent, out var delimiter);
            preview.Delimiter = delimiter;

            if (rows.Count < 2)
            {
                preview.Issues.Add(CreateIssue(0, 0, "File", "CSV must contain a header row and at least one data row."));
                return preview;
            }

            var aggregatedTemplates = new Dictionary<ushort, AggregatedPrizeTemplate>();

            for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
            {
                var rowNumber = rowIndex + 1;
                var row = rows[rowIndex];
                if (IsBlankRow(row))
                {
                    continue;
                }

                var columns = NormalizeColumnCount(row, PrizeManagerConstants.PrizeCsvColumnCount);
                if (!TryParsePrizeRow(columns, rowNumber, out var parsedRow, preview.Issues))
                {
                    continue;
                }

                if (aggregatedTemplates.TryGetValue(parsedRow.Template.PrizeCategoryId, out var existing))
                {
                    if (!existing.Template.SemanticallyEquals(parsedRow.Template))
                    {
                        preview.Issues.Add(CreateIssue(
                            rowNumber, 1, "PrizeCategoryId",
                            $"Category {parsedRow.Template.PrizeCategoryId} reuses a prize id with different template values."));
                        continue;
                    }

                    existing.Amount += parsedRow.Amount;
                }
                else
                {
                    aggregatedTemplates[parsedRow.Template.PrizeCategoryId] =
                        new AggregatedPrizeTemplate(parsedRow.Template, parsedRow.Amount, rowNumber);
                    preview.SourceRowsByCategory[parsedRow.Template.PrizeCategoryId] = rowNumber;
                }
            }

            foreach (var pair in aggregatedTemplates.OrderBy(p => p.Key))
            {
                preview.Templates.Add(pair.Value.Template.Clone());
                for (var seq = 1; seq <= pair.Value.Amount; seq++)
                {
                    preview.Instances.Add(CreatePreviewInstance(pair.Value.Template, seq));
                }
            }

            return preview;
        }

        // ── Settings import ──────────────────────────────────────────────────

        public SettingsCsvPreview PreviewSettingsImport(string filePath)
        {
            var preview = new SettingsCsvPreview();
            if (!TryReadText(filePath, out var csvContent, preview.Issues))
            {
                return preview;
            }

            return PreviewSettingsImportContent(csvContent);
        }

        public SettingsCsvPreview PreviewSettingsImportContent(string csvContent)
        {
            var preview = new SettingsCsvPreview();

            if (string.IsNullOrWhiteSpace(csvContent))
            {
                preview.Issues.Add(CreateIssue(0, 0, "File", "CSV content is empty."));
                return preview;
            }

            var rows = ParseCsvRows(csvContent, out var delimiter);
            preview.Delimiter = delimiter;

            if (rows.Count < 2)
            {
                preview.Issues.Add(CreateIssue(0, 0, "File", "CSV must contain a header row and at least one data row."));
                return preview;
            }

            var dataRows = new List<(int RowNumber, List<string> Columns)>();
            for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
            {
                if (IsBlankRow(rows[rowIndex]))
                {
                    continue;
                }

                dataRows.Add((rowIndex + 1, NormalizeColumnCount(rows[rowIndex], PrizeManagerConstants.SettingsCsvColumnCount)));
            }

            if (dataRows.Count == 0)
            {
                preview.Issues.Add(CreateIssue(0, 0, "File", "Settings CSV does not contain any non-empty data rows."));
                return preview;
            }

            var settings = new PrizeRuntimeSettings();
            var baseRow = dataRows[0];
            TryParseBaseSettingsRow(baseRow.Columns, baseRow.RowNumber, settings, preview.Issues);

            for (var index = 1; index < dataRows.Count; index++)
            {
                TryParseThresholdRow(dataRows[index].Columns, dataRows[index].RowNumber, settings, preview.Issues);
            }

            ValidateDuplicateThresholds(settings.FalsePrizeThresholds, preview.Issues, "FalsePrizeThresholdPercent");
            ValidateDuplicateThresholds(settings.ForcedHourThresholds, preview.Issues, "ForcedHourThresholdPercent");

            settings.FalsePrizeThresholds = settings.FalsePrizeThresholds.OrderBy(t => t.ThresholdPercent).ToList();
            settings.ForcedHourThresholds = settings.ForcedHourThresholds.OrderBy(t => t.ThresholdPercent).ToList();
            preview.Settings = settings;
            return preview;
        }

        // ── Exports ──────────────────────────────────────────────────────────

        /// <summary>One row per claimed prize instance with kiosk attribution.</summary>
        public string ExportWonPrizesCsv(IEnumerable<WonPrizeRecord> wonPrizeRecords)
        {
            var builder = new StringBuilder();
            builder.AppendLine(
                "WonPrizeInstanceId,PrizeCategoryId,PrizeName,PrizeDescription," +
                "WinnerOffice,WinnerName,WinnerPhoneNumber,KioskId");

            foreach (var record in wonPrizeRecords)
            {
                builder.Append(EscapeCsv(record.WonPrizeInstanceId));
                builder.Append(',');
                builder.Append(EscapeCsv(record.PrizeCategoryId.ToString(CultureInfo.InvariantCulture)));
                builder.Append(',');
                builder.Append(EscapeCsv(record.PrizeName));
                builder.Append(',');
                builder.Append(EscapeCsv(record.PrizeDescription));
                builder.Append(',');
                builder.Append(EscapeCsv(record.WinnerOffice));
                builder.Append(',');
                builder.Append(EscapeCsv(record.WinnerName));
                builder.Append(',');
                builder.Append(EscapeCsv(record.WinnerPhoneNumber));
                builder.Append(',');
                builder.Append(record.KioskId.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine();
            }

            return builder.ToString();
        }

        /// <summary>
        /// End-of-day subtraction export.  Aggregates won prizes by category so the
        /// admin knows how many physical units to remove from each category's stock.
        /// Rows are ordered by PrizeCategoryId.
        /// </summary>
        public string ExportPrizePoolSubtractionCsv(IEnumerable<WonPrizeRecord> wonPrizeRecords)
        {
            var builder = new StringBuilder();
            builder.AppendLine("PrizeCategoryId,AmountToSubtract,PrizeName,PrizeDescription");

            var grouped = wonPrizeRecords
                .GroupBy(r => r.PrizeCategoryId)
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                var first = group.First();
                builder.Append(group.Key.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(group.Count().ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(EscapeCsv(first.PrizeName));
                builder.Append(',');
                builder.Append(EscapeCsv(first.PrizeDescription));
                builder.AppendLine();
            }

            return builder.ToString();
        }

        // ── Row parsing helpers ───────────────────────────────────────────────

        private static PrizeInstance CreatePreviewInstance(PrizeTemplate template, int sequence)
        {
            return new PrizeInstance
            {
                PrizeInstanceId = PrizeAdminStateStore.FormatInstanceId(template.PrizeCategoryId, sequence),
                PrizeCategoryId = template.PrizeCategoryId,
                PrizeName = template.PrizeName,
                PrizeDescription = template.PrizeDescription,
                PrizePriority = template.PrizePriority,
                Schedule = template.Schedule.Clone(),
            };
        }

        private static bool TryReadText(string filePath, out string csvContent, List<CsvValidationIssue> issues)
        {
            csvContent = string.Empty;
            if (string.IsNullOrWhiteSpace(filePath))
            {
                issues.Add(CreateIssue(0, 0, "File", "A local file path is required."));
                return false;
            }

            try
            {
                if (!File.Exists(filePath))
                {
                    issues.Add(CreateIssue(0, 0, "File", $"File not found: {filePath}"));
                    return false;
                }

                csvContent = File.ReadAllText(filePath, Encoding.UTF8);
                return true;
            }
            catch (Exception exception)
            {
                issues.Add(CreateIssue(0, 0, "File", $"Could not read file: {exception.Message}"));
                return false;
            }
        }

        private static bool TryParsePrizeRow(
            IReadOnlyList<string> columns,
            int rowNumber,
            out ParsedPrizeRow parsedRow,
            List<CsvValidationIssue> issues)
        {
            parsedRow = default;
            var isValid = true;

            isValid &= TryParseUnsignedShort(columns[0], rowNumber, 1, "PrizeCategoryId", issues, out var prizeCategoryId, false);
            isValid &= TryParseUnsignedShort(columns[1], rowNumber, 2, "PrizeAmount", issues, out var prizeAmount, true);

            var prizeName = columns[2].Trim();
            var prizeDescription = columns[3].Trim();

            isValid &= TryParseUnsignedShort(columns[4], rowNumber, 5, "PrizePriority", issues, out var prizePriority, false);

            var hasHourStart = !string.IsNullOrWhiteSpace(columns[5]);
            var hasHourEnd = !string.IsNullOrWhiteSpace(columns[6]);
            int? prizeStartMinutesOfDay = null;
            int? prizeEndMinutesOfDay = null;

            if (hasHourStart)
            {
                isValid &= TryParseTimeOfDay(columns[5], rowNumber, 6, "PrizeHourStart", issues, out var startMin);
                prizeStartMinutesOfDay = startMin;
            }

            if (hasHourEnd)
            {
                isValid &= TryParseTimeOfDay(columns[6], rowNumber, 7, "PrizeHourEnd", issues, out var endMin);
                prizeEndMinutesOfDay = endMin;
            }

            var hasForcedHourValue = !string.IsNullOrWhiteSpace(columns[7]);
            var hasToComeOutDuringHour = false;
            if (hasForcedHourValue)
            {
                isValid &= TryParseBoolean(columns[7], rowNumber, 8, "HasToComeOutDuringHour", issues, out hasToComeOutDuringHour);
            }

            isValid &= TryParsePrizeDays(columns[8], rowNumber, 9, issues, out var prizeDays);

            if (hasHourStart != hasHourEnd)
            {
                isValid = false;
                issues.Add(CreateIssue(rowNumber, 6, "PrizeHourStart",
                    "PrizeHourStart and PrizeHourEnd must both be provided when a time window is used."));
            }

            if (prizeStartMinutesOfDay.HasValue && prizeEndMinutesOfDay.HasValue
                && prizeEndMinutesOfDay.Value <= prizeStartMinutesOfDay.Value)
            {
                isValid = false;
                issues.Add(CreateIssue(rowNumber, 7, "PrizeHourEnd",
                    "Overnight or zero-length time windows are out of scope for v1."));
            }

            if (hasToComeOutDuringHour && (!prizeStartMinutesOfDay.HasValue || !prizeEndMinutesOfDay.HasValue))
            {
                isValid = false;
                issues.Add(CreateIssue(rowNumber, 8, "HasToComeOutDuringHour",
                    "Forced-hour prizes require both PrizeHourStart and PrizeHourEnd."));
            }

            if (!isValid)
            {
                return false;
            }

            parsedRow = new ParsedPrizeRow
            {
                Amount = prizeAmount,
                Template = new PrizeTemplate
                {
                    PrizeCategoryId = prizeCategoryId,
                    PrizeName = prizeName,
                    PrizeDescription = prizeDescription,
                    PrizePriority = prizePriority,
                    Schedule = new PrizeSchedule
                    {
                        PrizeStartMinutesOfDay = prizeStartMinutesOfDay,
                        PrizeEndMinutesOfDay = prizeEndMinutesOfDay,
                        HasToComeOutDuringHour = hasToComeOutDuringHour,
                        PrizeDays = prizeDays,
                    },
                },
            };

            return true;
        }

        private static void TryParseBaseSettingsRow(
            IReadOnlyList<string> columns,
            int rowNumber,
            PrizeRuntimeSettings settings,
            List<CsvValidationIssue> issues)
        {
            var isValid = true;

            var timezone = columns[0].Trim();
            if (string.IsNullOrWhiteSpace(timezone))
            {
                isValid = false;
                issues.Add(CreateIssue(rowNumber, 1, "Timezone", "Timezone is required on the base row."));
            }

            isValid &= TryParsePositiveInt(columns[1], rowNumber, 2, "PrizeReservationTimeoutMinutes", issues, out var reservationTimeoutMinutes);
            isValid &= TryParsePositiveInt(columns[2], rowNumber, 3, "MaxPrizesPerDay", issues, out var maxPrizesPerDay);
            isValid &= TryParsePercent(columns[3], rowNumber, 4, "FalsePrizeChancePercent", issues, out var falsePrizeChancePercent, true);
            isValid &= TryParsePercent(columns[5], rowNumber, 6, "ForcedHourChancePercent", issues, out var forcedHourChancePercent, true);

            // Column 4 (FalsePrizeThresholdPercent) and column 6 (ForcedHourThresholdPercent)
            // must be blank on the base row.
            if (!string.IsNullOrWhiteSpace(columns[4]))
            {
                isValid = false;
                issues.Add(CreateIssue(rowNumber, 5, "FalsePrizeThresholdPercent",
                    "FalsePrizeThresholdPercent must be blank on the base row."));
            }

            if (!string.IsNullOrWhiteSpace(columns[6]))
            {
                isValid = false;
                issues.Add(CreateIssue(rowNumber, 7, "ForcedHourThresholdPercent",
                    "ForcedHourThresholdPercent must be blank on the base row."));
            }

            // Column 7 = KioskCount (new).
            isValid &= TryParsePositiveInt(columns[7], rowNumber, 8, "KioskCount", issues, out var kioskCount);

            if (!isValid)
            {
                return;
            }

            settings.Timezone = timezone;
            settings.PrizeReservationTimeoutMinutes = reservationTimeoutMinutes;
            settings.MaxPrizesPerDay = maxPrizesPerDay;
            settings.FalsePrizeChancePercent = falsePrizeChancePercent;
            settings.ForcedHourChancePercent = forcedHourChancePercent;
            settings.KioskCount = kioskCount;
        }

        private static void TryParseThresholdRow(
            IReadOnlyList<string> columns,
            int rowNumber,
            PrizeRuntimeSettings settings,
            List<CsvValidationIssue> issues)
        {
            // Column 7 (KioskCount) is only meaningful on the base row; ignore here.
            var hasFalseChanceValue = !string.IsNullOrWhiteSpace(columns[3]);
            var hasFalseThresholdValue = !string.IsNullOrWhiteSpace(columns[4]);
            var hasForcedChanceValue = !string.IsNullOrWhiteSpace(columns[5]);
            var hasForcedThresholdValue = !string.IsNullOrWhiteSpace(columns[6]);
            var hasAnyThresholdPair = false;

            if (hasFalseChanceValue || hasFalseThresholdValue)
            {
                if (hasFalseChanceValue && hasFalseThresholdValue)
                {
                    var validFalseChance = TryParsePercent(columns[3], rowNumber, 4, "FalsePrizeChancePercent", issues, out var falseChance, false);
                    var validFalseThreshold = TryParsePercent(columns[4], rowNumber, 5, "FalsePrizeThresholdPercent", issues, out var falseThreshold, false);

                    if (validFalseChance && validFalseThreshold)
                    {
                        settings.FalsePrizeThresholds.Add(new PrizeChanceThreshold
                        {
                            ThresholdPercent = falseThreshold,
                            ChancePercent = falseChance,
                        });
                        hasAnyThresholdPair = true;
                    }
                }
                else
                {
                    issues.Add(CreateIssue(rowNumber, 4, "FalsePrizeChancePercent",
                        "False-prize threshold rows must provide both chance and threshold values."));
                }
            }

            if (hasForcedChanceValue || hasForcedThresholdValue)
            {
                if (hasForcedChanceValue && hasForcedThresholdValue)
                {
                    var validForcedChance = TryParsePercent(columns[5], rowNumber, 6, "ForcedHourChancePercent", issues, out var forcedChance, false);
                    var validForcedThreshold = TryParsePercent(columns[6], rowNumber, 7, "ForcedHourThresholdPercent", issues, out var forcedThreshold, false);

                    if (validForcedChance && validForcedThreshold)
                    {
                        settings.ForcedHourThresholds.Add(new PrizeChanceThreshold
                        {
                            ThresholdPercent = forcedThreshold,
                            ChancePercent = forcedChance,
                        });
                        hasAnyThresholdPair = true;
                    }
                }
                else
                {
                    issues.Add(CreateIssue(rowNumber, 6, "ForcedHourChancePercent",
                        "Forced-hour threshold rows must provide both chance and threshold values."));
                }
            }

            if (!hasAnyThresholdPair && columns.Any(c => !string.IsNullOrWhiteSpace(c)))
            {
                issues.Add(CreateIssue(rowNumber, 4, "ThresholdRow",
                    "Threshold rows must define at least one complete chance/threshold pair."));
            }
        }

        private static void ValidateDuplicateThresholds(
            IEnumerable<PrizeChanceThreshold> thresholds,
            List<CsvValidationIssue> issues,
            string columnName)
        {
            foreach (var group in thresholds.GroupBy(t => t.ThresholdPercent).Where(g => g.Count() > 1))
            {
                issues.Add(CreateIssue(0, 0, columnName,
                    $"Duplicate threshold value {group.Key} creates ambiguous chance selection."));
            }
        }

        // ── Primitive parsers ─────────────────────────────────────────────────

        private static bool TryParseUnsignedShort(
            string value, int rowNumber, int columnIndex, string columnName,
            List<CsvValidationIssue> issues, out ushort parsedValue, bool mustBePositive)
        {
            parsedValue = 0;
            if (string.IsNullOrWhiteSpace(value))
            {
                issues.Add(CreateIssue(rowNumber, columnIndex, columnName, $"{columnName} is required."));
                return false;
            }

            if (!ushort.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedValue))
            {
                issues.Add(CreateIssue(rowNumber, columnIndex, columnName, $"{columnName} must be an unsigned whole number."));
                return false;
            }

            if (mustBePositive && parsedValue == 0)
            {
                issues.Add(CreateIssue(rowNumber, columnIndex, columnName, $"{columnName} must be greater than zero."));
                return false;
            }

            return true;
        }

        private static bool TryParsePositiveInt(
            string value, int rowNumber, int columnIndex, string columnName,
            List<CsvValidationIssue> issues, out int parsedValue)
        {
            parsedValue = 0;
            if (string.IsNullOrWhiteSpace(value))
            {
                issues.Add(CreateIssue(rowNumber, columnIndex, columnName, $"{columnName} is required."));
                return false;
            }

            if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedValue))
            {
                issues.Add(CreateIssue(rowNumber, columnIndex, columnName, $"{columnName} must be a whole number."));
                return false;
            }

            if (parsedValue <= 0)
            {
                issues.Add(CreateIssue(rowNumber, columnIndex, columnName, $"{columnName} must be greater than zero."));
                return false;
            }

            return true;
        }

        private static bool TryParsePercent(
            string value, int rowNumber, int columnIndex, string columnName,
            List<CsvValidationIssue> issues, out int parsedValue, bool required)
        {
            parsedValue = 0;
            if (string.IsNullOrWhiteSpace(value))
            {
                if (required)
                {
                    issues.Add(CreateIssue(rowNumber, columnIndex, columnName, $"{columnName} is required."));
                }

                return false;
            }

            if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedValue))
            {
                issues.Add(CreateIssue(rowNumber, columnIndex, columnName, $"{columnName} must be a whole number percentage."));
                return false;
            }

            if (parsedValue < 0 || parsedValue > 100)
            {
                issues.Add(CreateIssue(rowNumber, columnIndex, columnName, $"{columnName} must be between 0 and 100."));
                return false;
            }

            return true;
        }

        private static bool TryParseTimeOfDay(
            string value, int rowNumber, int columnIndex, string columnName,
            List<CsvValidationIssue> issues, out int parsedValue)
        {
            parsedValue = 0;
            var normalized = value.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                issues.Add(CreateIssue(rowNumber, columnIndex, columnName,
                    $"{columnName} must be an hour between 0 and 23 or a time in HH:mm format."));
                return false;
            }

            if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hourOnly))
            {
                if (hourOnly < 0 || hourOnly > 23)
                {
                    issues.Add(CreateIssue(rowNumber, columnIndex, columnName,
                        $"{columnName} must be an hour between 0 and 23 or a time in HH:mm format."));
                    return false;
                }

                parsedValue = hourOnly * 60;
                return true;
            }

            var split = normalized.Split(':');
            if (split.Length == 2
                && int.TryParse(split[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours)
                && int.TryParse(split[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes)
                && hours >= 0 && hours <= 23
                && minutes >= 0 && minutes <= 59)
            {
                parsedValue = (hours * 60) + minutes;
                return true;
            }

            issues.Add(CreateIssue(rowNumber, columnIndex, columnName,
                $"{columnName} must be an hour between 0 and 23 or a time in HH:mm format."));
            return false;
        }

        private static bool TryParseBoolean(
            string value, int rowNumber, int columnIndex, string columnName,
            List<CsvValidationIssue> issues, out bool parsedValue)
        {
            parsedValue = false;
            switch (value.Trim().ToLowerInvariant())
            {
                case "true": case "1": case "yes": case "y":
                case "si": case "s": case "verdadero": case "v":
                    parsedValue = true;
                    return true;
                case "false": case "0": case "no": case "n":
                case "falso": case "f":
                    parsedValue = false;
                    return true;
                default:
                    issues.Add(CreateIssue(rowNumber, columnIndex, columnName, $"{columnName} must be a boolean value."));
                    return false;
            }
        }

        private static bool TryParsePrizeDays(
            string value, int rowNumber, int columnIndex,
            List<CsvValidationIssue> issues, out List<int> parsedDays)
        {
            parsedDays = new List<int>();
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            var uniqueDays = new HashSet<int>();
            var isValid = true;
            foreach (var token in value.Split('|'))
            {
                var trimmed = token.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var day)
                    || day < 1 || day > 7)
                {
                    isValid = false;
                    issues.Add(CreateIssue(rowNumber, columnIndex, "PrizeDays",
                        "PrizeDays must use values from 1 (Monday) through 7 (Sunday)."));
                    continue;
                }

                uniqueDays.Add(day);
            }

            parsedDays = uniqueDays.OrderBy(d => d).ToList();
            return isValid;
        }

        private static CsvValidationIssue CreateIssue(int rowNumber, int columnIndex, string columnName, string message)
        {
            return new CsvValidationIssue
            {
                Severity = CsvValidationSeverity.Error,
                RowNumber = rowNumber,
                ColumnIndex = columnIndex,
                ColumnName = columnName,
                Message = message,
            };
        }

        // ── CSV parsing ───────────────────────────────────────────────────────

        private static List<List<string>> ParseCsvRows(string csvContent, out char delimiter)
        {
            delimiter = DetectDelimiter(csvContent);
            var rows = new List<List<string>>();
            var currentRow = new List<string>();
            var currentField = new StringBuilder();
            var insideQuotes = false;

            for (var index = 0; index < csvContent.Length; index++)
            {
                var ch = csvContent[index];
                if (insideQuotes)
                {
                    if (ch == '"')
                    {
                        var hasEscaped = index + 1 < csvContent.Length && csvContent[index + 1] == '"';
                        if (hasEscaped)
                        {
                            currentField.Append('"');
                            index++;
                        }
                        else
                        {
                            insideQuotes = false;
                        }
                    }
                    else
                    {
                        currentField.Append(ch);
                    }

                    continue;
                }

                if (ch == '"')
                {
                    insideQuotes = true;
                    continue;
                }

                if (ch == delimiter)
                {
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    continue;
                }

                if (ch == '\r' || ch == '\n')
                {
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();

                    if (!IsBlankRow(currentRow))
                    {
                        rows.Add(currentRow);
                    }

                    currentRow = new List<string>();

                    if (ch == '\r' && index + 1 < csvContent.Length && csvContent[index + 1] == '\n')
                    {
                        index++;
                    }

                    continue;
                }

                currentField.Append(ch);
            }

            currentRow.Add(currentField.ToString());
            if (!IsBlankRow(currentRow))
            {
                rows.Add(currentRow);
            }

            return rows;
        }

        private static char DetectDelimiter(string csvContent)
        {
            var lines = csvContent
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Take(3);

            var commaScore = 0;
            var semicolonScore = 0;
            foreach (var line in lines)
            {
                commaScore += CountUnquotedCharacters(line, ',');
                semicolonScore += CountUnquotedCharacters(line, ';');
            }

            return semicolonScore > commaScore ? ';' : ',';
        }

        private static int CountUnquotedCharacters(string line, char candidate)
        {
            var count = 0;
            var insideQuotes = false;
            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (ch == '"')
                {
                    if (insideQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        i++;
                        continue;
                    }

                    insideQuotes = !insideQuotes;
                    continue;
                }

                if (!insideQuotes && ch == candidate)
                {
                    count++;
                }
            }

            return count;
        }

        private static List<string> NormalizeColumnCount(IReadOnlyList<string> columns, int requiredColumns)
        {
            var normalized = columns.Select(c => c ?? string.Empty).ToList();
            while (normalized.Count < requiredColumns)
            {
                normalized.Add(string.Empty);
            }

            return normalized;
        }

        private static bool IsBlankRow(IReadOnlyCollection<string> row)
        {
            return row.All(c => string.IsNullOrWhiteSpace(c));
        }

        private static string EscapeCsv(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (!value.Contains(",") && !value.Contains("\"")
                && !value.Contains("\n") && !value.Contains("\r"))
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private struct ParsedPrizeRow
        {
            public PrizeTemplate Template;
            public ushort Amount;
        }

        private sealed class AggregatedPrizeTemplate
        {
            public AggregatedPrizeTemplate(PrizeTemplate template, ushort amount, int firstSourceRow)
            {
                Template = template.Clone();
                Amount = amount;
                FirstSourceRow = firstSourceRow;
            }

            public PrizeTemplate Template { get; }
            public ushort Amount { get; set; }
            public int FirstSourceRow { get; }
        }
    }
}
