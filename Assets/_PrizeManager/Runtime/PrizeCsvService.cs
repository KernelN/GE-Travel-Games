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
                preview.Issues.Add(CreateIssue(0, 0, "File", "El contenido del CSV está vacío."));
                return preview;
            }

            var rows = ParseCsvRows(csvContent, out var delimiter);
            preview.Delimiter = delimiter;

            if (rows.Count < 2)
            {
                preview.Issues.Add(CreateIssue(0, 0, "File", "El CSV debe tener una fila de encabezado y al menos una fila de datos."));
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
                            $"La categoría {parsedRow.Template.PrizeCategoryId} reutiliza un ID de premio con valores de plantilla diferentes."));
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
                preview.Issues.Add(CreateIssue(0, 0, "File", "El contenido del CSV está vacío."));
                return preview;
            }

            var rows = ParseCsvRows(csvContent, out var delimiter);
            preview.Delimiter = delimiter;

            if (rows.Count < 2)
            {
                preview.Issues.Add(CreateIssue(0, 0, "File", "El CSV debe tener una fila de encabezado y al menos una fila de datos."));
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
                preview.Issues.Add(CreateIssue(0, 0, "File", "El CSV de configuración no contiene filas de datos."));
                return preview;
            }

            var settings = new PrizeRuntimeSettings();
            var baseRow = dataRows[0];
            TryParseBaseSettingsRow(baseRow.Columns, baseRow.RowNumber, settings, preview.Issues);

            for (var index = 1; index < dataRows.Count; index++)
            {
                // If this row looks like a boolean it is the AllowReroll row; otherwise it is a threshold row.
                if (TryParseAllowRerollRow(dataRows[index].Columns, settings))
                    continue;
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

        /// <summary>One row per unique player; prizes serialised as semicolon-delimited lists.</summary>
        public string ExportPlayersCsv(IEnumerable<PlayerRecord> players)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Nombre,Apellido,Telefono,Oficina,VecesJugado,CategoriasPremios,IdsPremios");

            foreach (var p in players)
            {
                var cats = string.Join(";", p.WonPrizes.Select(w => w.CategoryId.ToString(CultureInfo.InvariantCulture)));
                var ids  = string.Join(";", p.WonPrizes.Select(w => w.InstanceId));

                builder.Append(EscapeCsv(p.FirstName));  builder.Append(',');
                builder.Append(EscapeCsv(p.LastName));   builder.Append(',');
                builder.Append(EscapeCsv(p.Phone));      builder.Append(',');
                builder.Append(EscapeCsv(p.Office));     builder.Append(',');
                builder.Append(p.TimesPlayed.ToString(CultureInfo.InvariantCulture)); builder.Append(',');
                builder.Append(EscapeCsv(cats));         builder.Append(',');
                builder.Append(EscapeCsv(ids));
                builder.AppendLine();
            }

            return builder.ToString();
        }

        /// <summary>
        /// Parses a players CSV (as produced by <see cref="ExportPlayersCsv"/>) back into a
        /// <see cref="PlayerRecord"/> list. Silently skips malformed rows.
        /// </summary>
        public List<PlayerRecord> ImportPlayersCsv(string csvContent)
        {
            var result = new List<PlayerRecord>();
            if (string.IsNullOrWhiteSpace(csvContent)) return result;

            var rows = ParseCsvRows(csvContent, out _);
            // Row 0 is the header.
            for (var i = 1; i < rows.Count; i++)
            {
                var cols = NormalizeColumnCount(rows[i], 7);
                if (IsBlankRow(cols)) continue;

                var record = new PlayerRecord
                {
                    FirstName   = cols[0],
                    LastName    = cols[1],
                    Phone       = cols[2],
                    Office      = cols[3],
                    TimesPlayed = int.TryParse(cols[4], NumberStyles.Integer,
                                      CultureInfo.InvariantCulture, out var tp) ? tp : 0,
                };

                var cats = cols[5].Split(';', StringSplitOptions.RemoveEmptyEntries);
                var ids  = cols[6].Split(';', StringSplitOptions.RemoveEmptyEntries);
                var count = Math.Min(cats.Length, ids.Length);
                for (var j = 0; j < count; j++)
                {
                    if (ushort.TryParse(cats[j], NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out var catId))
                        record.WonPrizes.Add((catId, ids[j]));
                }

                result.Add(record);
            }

            return result;
        }

        /// <summary>
        /// Exports the current available prize pool as a standard prizes CSV so the
        /// admin can use it as the new master file after applying subtractions.
        /// Amounts reflect how many instances remain per category.
        /// Rows are ordered by PrizeCategoryId.
        /// </summary>
        public string ExportUpdatedPrizesCsv(
            IEnumerable<PrizeTemplate> templates,
            IEnumerable<PrizeInstance> availableInstances)
        {
            var countByCategory = availableInstances
                .GroupBy(i => i.PrizeCategoryId)
                .ToDictionary(g => g.Key, g => g.Count());
            return ExportUpdatedPrizesCsv(templates, countByCategory);
        }

        /// <summary>
        /// Exports prizes CSV using a pre-computed count per category.
        /// </summary>
        public string ExportUpdatedPrizesCsv(
            IEnumerable<PrizeTemplate> templates,
            IReadOnlyDictionary<ushort, int> countByCategory)
        {
            var builder = new StringBuilder();
            builder.AppendLine(
                "PrizeCategoryId,PrizeAmount,PrizeName,PrizeDescription," +
                "PrizePriority,PrizeLevel,PrizeHourStart,PrizeHourEnd,HasToComeOutDuringHour,PrizeDays");

            foreach (var template in templates.OrderBy(t => t.PrizeCategoryId))
            {
                countByCategory.TryGetValue(template.PrizeCategoryId, out var remaining);
                var s = template.Schedule;

                builder.Append(template.PrizeCategoryId.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(remaining.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(EscapeCsv(template.PrizeName));
                builder.Append(',');
                builder.Append(EscapeCsv(template.PrizeDescription));
                builder.Append(',');
                builder.Append(template.PrizePriority.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(template.PrizeLevel.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(s?.PrizeStartMinutesOfDay.HasValue == true
                    ? $"{s.PrizeStartMinutesOfDay.Value / 60:D2}:{s.PrizeStartMinutesOfDay.Value % 60:D2}"
                    : string.Empty);
                builder.Append(',');
                builder.Append(s?.PrizeEndMinutesOfDay.HasValue == true
                    ? $"{s.PrizeEndMinutesOfDay.Value / 60:D2}:{s.PrizeEndMinutesOfDay.Value % 60:D2}"
                    : string.Empty);
                builder.Append(',');
                builder.Append(s?.HasToComeOutDuringHour == true ? "true" : string.Empty);
                builder.Append(',');
                builder.Append(s?.PrizeDays != null && s.PrizeDays.Count > 0
                    ? string.Join("|", s.PrizeDays)
                    : string.Empty);
                builder.AppendLine();
            }

            return builder.ToString();
        }

        /// <summary>
        /// Parses a subtraction CSV (PrizeCategoryId,AmountToSubtract,PrizeName,PrizeDescription).
        /// Header row is skipped. Invalid rows are silently skipped.
        /// </summary>
        public List<PrizePoolSubtractionRecord> ParseSubtractionCsv(string csvContent)
        {
            var result = new List<PrizePoolSubtractionRecord>();
            if (string.IsNullOrWhiteSpace(csvContent))
            {
                return result;
            }

            var rows = ParseCsvRows(csvContent, out _);
            for (var i = 1; i < rows.Count; i++)
            {
                var cols = NormalizeColumnCount(rows[i], PrizeManagerConstants.SubtractionExportColumnCount);
                if (!ushort.TryParse(cols[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var catId))
                {
                    continue;
                }

                if (!int.TryParse(cols[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount))
                {
                    continue;
                }

                result.Add(new PrizePoolSubtractionRecord
                {
                    PrizeCategoryId  = catId,
                    AmountToSubtract = amount,
                    PrizeName        = cols[2].Trim(),
                    PrizeDescription = cols[3].Trim(),
                });
            }

            return result;
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
                PrizeLevel = template.PrizeLevel,
                Schedule = template.Schedule.Clone(),
            };
        }

        private static bool TryReadText(string filePath, out string csvContent, List<CsvValidationIssue> issues)
        {
            csvContent = string.Empty;
            if (string.IsNullOrWhiteSpace(filePath))
            {
                issues.Add(CreateIssue(0, 0, "File", "Se requiere una ruta de archivo local."));
                return false;
            }

            try
            {
                if (!File.Exists(filePath))
                {
                    issues.Add(CreateIssue(0, 0, "File", $"Archivo no encontrado: {filePath}"));
                    return false;
                }

                csvContent = File.ReadAllText(filePath, Encoding.UTF8);
                return true;
            }
            catch (Exception exception)
            {
                issues.Add(CreateIssue(0, 0, "File", $"No se pudo leer el archivo: {exception.Message}"));
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

            // Column 5 = PrizeLevel (new, optional — defaults to 0 if blank).
            ushort prizeLevel = 0;
            if (!string.IsNullOrWhiteSpace(columns[5]))
                TryParseUnsignedShort(columns[5], rowNumber, 6, "PrizeLevel", issues, out prizeLevel, false);

            var hasHourStart = !string.IsNullOrWhiteSpace(columns[6]);
            var hasHourEnd = !string.IsNullOrWhiteSpace(columns[7]);
            int? prizeStartMinutesOfDay = null;
            int? prizeEndMinutesOfDay = null;

            if (hasHourStart)
            {
                isValid &= TryParseTimeOfDay(columns[6], rowNumber, 7, "PrizeHourStart", issues, out var startMin);
                prizeStartMinutesOfDay = startMin;
            }

            if (hasHourEnd)
            {
                isValid &= TryParseTimeOfDay(columns[7], rowNumber, 8, "PrizeHourEnd", issues, out var endMin);
                prizeEndMinutesOfDay = endMin;
            }

            var hasForcedHourValue = !string.IsNullOrWhiteSpace(columns[8]);
            var hasToComeOutDuringHour = false;
            if (hasForcedHourValue)
            {
                isValid &= TryParseBoolean(columns[8], rowNumber, 9, "HasToComeOutDuringHour", issues, out hasToComeOutDuringHour);
            }

            isValid &= TryParsePrizeDays(columns[9], rowNumber, 10, issues, out var prizeDays);

            if (hasHourStart != hasHourEnd)
            {
                isValid = false;
                issues.Add(CreateIssue(rowNumber, 7, "PrizeHourStart",
                    "PrizeHourStart y PrizeHourEnd deben proporcionarse ambos al usar una ventana de tiempo."));
            }

            if (prizeStartMinutesOfDay.HasValue && prizeEndMinutesOfDay.HasValue
                && prizeEndMinutesOfDay.Value <= prizeStartMinutesOfDay.Value)
            {
                isValid = false;
                issues.Add(CreateIssue(rowNumber, 8, "PrizeHourEnd",
                    "Las ventanas de tiempo nocturnas o de duración cero no están contempladas en v1."));
            }

            if (hasToComeOutDuringHour && (!prizeStartMinutesOfDay.HasValue || !prizeEndMinutesOfDay.HasValue))
            {
                isValid = false;
                issues.Add(CreateIssue(rowNumber, 9, "HasToComeOutDuringHour",
                    "Los premios de hora forzada requieren PrizeHourStart y PrizeHourEnd."));
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
                    PrizeLevel = prizeLevel,
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

        /// <summary>
        /// Returns true and sets AllowReroll if the row's first non-blank cell contains a
        /// recognisable boolean value (true/false/1/0/yes/no). Backward-compatible: old
        /// Settings.csv files without this row simply leave AllowReroll = false.
        /// </summary>
        private static bool TryParseAllowRerollRow(IReadOnlyList<string> columns, PrizeRuntimeSettings settings)
        {
            var val = columns[0].Trim().ToLowerInvariant();
            switch (val)
            {
                case "true":  case "1": case "yes": settings.AllowReroll = true;  return true;
                case "false": case "0": case "no":  settings.AllowReroll = false; return true;
                default: return false;
            }
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
                issues.Add(CreateIssue(rowNumber, 1, "Timezone", "La zona horaria es obligatoria en la fila base."));
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
                    "FalsePrizeThresholdPercent debe estar vacío en la fila base."));
            }

            if (!string.IsNullOrWhiteSpace(columns[6]))
            {
                isValid = false;
                issues.Add(CreateIssue(rowNumber, 7, "ForcedHourThresholdPercent",
                    "ForcedHourThresholdPercent debe estar vacío en la fila base."));
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
                        "Las filas de umbral de premio falso deben incluir tanto el porcentaje como el umbral."));
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
                        "Las filas de umbral de hora forzada deben incluir tanto el porcentaje como el umbral."));
                }
            }

            if (!hasAnyThresholdPair && columns.Any(c => !string.IsNullOrWhiteSpace(c)))
            {
                issues.Add(CreateIssue(rowNumber, 4, "ThresholdRow",
                    "Las filas de umbral deben definir al menos un par completo de porcentaje/umbral."));
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
                    $"El umbral duplicado {group.Key} genera una selección de probabilidad ambigua."));
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
                issues.Add(CreateIssue(rowNumber, columnIndex, columnName, $"{columnName} es obligatorio."));
                return false;
            }

            if (!ushort.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedValue))
            {
                issues.Add(CreateIssue(rowNumber, columnIndex, columnName, $"{columnName} debe ser un número entero sin signo."));
                return false;
            }

            if (mustBePositive && parsedValue == 0)
            {
                issues.Add(CreateIssue(rowNumber, columnIndex, columnName, $"{columnName} debe ser mayor que cero."));
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
                issues.Add(CreateIssue(rowNumber, columnIndex, columnName, $"{columnName} es obligatorio."));
                return false;
            }

            if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedValue))
            {
                issues.Add(CreateIssue(rowNumber, columnIndex, columnName, $"{columnName} debe ser un número entero."));
                return false;
            }

            if (parsedValue <= 0)
            {
                issues.Add(CreateIssue(rowNumber, columnIndex, columnName, $"{columnName} debe ser mayor que cero."));
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
                    issues.Add(CreateIssue(rowNumber, columnIndex, columnName, $"{columnName} es obligatorio."));
                }

                return false;
            }

            if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedValue))
            {
                issues.Add(CreateIssue(rowNumber, columnIndex, columnName, $"{columnName} debe ser un porcentaje entero."));
                return false;
            }

            if (parsedValue < 0 || parsedValue > 100)
            {
                issues.Add(CreateIssue(rowNumber, columnIndex, columnName, $"{columnName} debe estar entre 0 y 100."));
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
                    $"{columnName} debe ser una hora entre 0 y 23, o un horario en formato HH:mm."));
                return false;
            }

            if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hourOnly))
            {
                if (hourOnly < 0 || hourOnly > 23)
                {
                    issues.Add(CreateIssue(rowNumber, columnIndex, columnName,
                        $"{columnName} debe ser una hora entre 0 y 23, o un horario en formato HH:mm."));
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
                $"{columnName} debe ser una hora entre 0 y 23, o un horario en formato HH:mm."));
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
                    issues.Add(CreateIssue(rowNumber, columnIndex, columnName, $"{columnName} debe ser un valor booleano."));
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
                        "PrizeDays debe usar valores del 1 (lunes) al 7 (domingo)."));
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
