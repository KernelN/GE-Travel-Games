using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GETravelGames.PrizeManager
{
    public sealed class PrizeAdminService
    {
        private readonly PrizeCsvService csvService;
        private readonly PrizeAdminStateStore stateStore;

        public PrizeAdminService(PrizeCsvService csvService, PrizeAdminStateStore stateStore)
        {
            this.csvService = csvService ?? throw new ArgumentNullException(nameof(csvService));
            this.stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        }

        public PrizeAdminStateStore StateStore => stateStore;

        // ── Schedule eligibility ──────────────────────────────────────────────

        /// <summary>
        /// Returns true when the given schedule permits a prize to be issued right now,
        /// using local system time.  A null or empty schedule is always eligible.
        /// </summary>
        public static bool IsScheduleEligible(PrizeSchedule schedule)
        {
            if (schedule == null)
            {
                return true;
            }

            var now = DateTime.Now;

            // Day-of-week check (schedule uses Mon=1 … Sun=7).
            if (schedule.PrizeDays != null && schedule.PrizeDays.Count > 0)
            {
                var dow = (int)now.DayOfWeek; // Sunday=0 … Saturday=6
                var ourDay = dow == 0 ? 7 : dow; // convert to Mon=1 … Sun=7
                if (!schedule.PrizeDays.Contains(ourDay))
                {
                    return false;
                }
            }

            // Time-window check.
            if (schedule.PrizeStartMinutesOfDay.HasValue && schedule.PrizeEndMinutesOfDay.HasValue)
            {
                var currentMinutes = now.Hour * 60 + now.Minute;
                if (currentMinutes < schedule.PrizeStartMinutesOfDay.Value ||
                    currentMinutes >= schedule.PrizeEndMinutesOfDay.Value)
                {
                    return false;
                }
            }

            return true;
        }

        // ── Prize import ──────────────────────────────────────────────────────

        public PrizeCsvImportPreview PreviewPrizeImport(string filePath, PrizeImportMode importMode)
        {
            var preview = csvService.PreviewPrizeImport(filePath, importMode);
            if (importMode == PrizeImportMode.Add)
            {
                AppendTemplateConflictsAgainstState(preview);
            }

            return preview;
        }

        public PrizeAdminOperationResult ApplyPrizeImport(string filePath, PrizeImportMode importMode)
        {
            var preview = PreviewPrizeImport(filePath, importMode);
            if (!preview.IsValid)
            {
                return new PrizeAdminOperationResult
                {
                    Success = false,
                    Summary = importMode == PrizeImportMode.Initialize
                    ? "La importación de inicialización de premios tiene errores de validación."
                    : "La importación de adición de premios tiene errores de validación.",
                    Issues = CloneIssues(preview.Issues),
                    PrizePreview = preview,
                    TemplateCount = stateStore.Templates.Count,
                    AvailablePrizeCount = stateStore.AvailablePrizeInstances.Count,
                    WonPrizeCount = stateStore.WonPrizeHistory.Count,
                    SettingsSnapshot = stateStore.ActiveSettings,
                    ActiveReservation = stateStore.ActiveReservation,
                };
            }

            var sequencedInstances = BuildSequencedInstances(preview);
            if (importMode == PrizeImportMode.Initialize)
            {
                stateStore.ReplaceAvailablePrizes(preview.Templates, sequencedInstances);
            }
            else
            {
                stateStore.AddAvailablePrizes(preview.Templates, sequencedInstances);
            }

            var kioskCount = stateStore.ActiveSettings.KioskCount;
            return new PrizeAdminOperationResult
            {
                Success = true,
                Summary = $"Se importaron {sequencedInstances.Count} premios disponibles en " +
                $"{preview.Templates.Count} categorías (modo {(importMode == PrizeImportMode.Initialize ? "inicialización" : "adición")}), " +
                $"distribuidos en {kioskCount} kiosko(s).",
                TemplateCount = stateStore.Templates.Count,
                AvailablePrizeCount = stateStore.AvailablePrizeInstances.Count,
                WonPrizeCount = stateStore.WonPrizeHistory.Count,
                PrizePreview = preview,
                SettingsSnapshot = stateStore.ActiveSettings,
                ActiveReservation = stateStore.ActiveReservation,
            };
        }

        // ── Settings import ───────────────────────────────────────────────────

        public SettingsCsvPreview PreviewSettingsImport(string filePath)
        {
            return csvService.PreviewSettingsImport(filePath);
        }

        public PrizeAdminOperationResult ApplySettingsImport(string filePath)
        {
            var preview = PreviewSettingsImport(filePath);
            if (!preview.IsValid)
            {
                return new PrizeAdminOperationResult
                {
                    Success = false,
                    Summary = "La importación de configuración tiene errores de validación.",
                    Issues = CloneIssues(preview.Issues),
                    SettingsPreview = preview,
                    TemplateCount = stateStore.Templates.Count,
                    AvailablePrizeCount = stateStore.AvailablePrizeInstances.Count,
                    WonPrizeCount = stateStore.WonPrizeHistory.Count,
                    SettingsSnapshot = stateStore.ActiveSettings,
                    ActiveReservation = stateStore.ActiveReservation,
                };
            }

            stateStore.ReplaceRuntimeSettings(preview.Settings);
            return new PrizeAdminOperationResult
            {
                Success = true,
                Summary = $"Se importó la configuración para la zona horaria {preview.Settings.Timezone} " +
                $"con {preview.Settings.KioskCount} kiosko(s).",
                TemplateCount = stateStore.Templates.Count,
                AvailablePrizeCount = stateStore.AvailablePrizeInstances.Count,
                WonPrizeCount = stateStore.WonPrizeHistory.Count,
                SettingsPreview = preview,
                SettingsSnapshot = stateStore.ActiveSettings,
                ActiveReservation = stateStore.ActiveReservation,
            };
        }

        // ── Exports ───────────────────────────────────────────────────────────

        public PrizeAdminOperationResult ExportWonPrizes(string filePath)
        {
            return ExportCsvFile(
                filePath,
                () => csvService.ExportWonPrizesCsv(stateStore.WonPrizeHistory),
                $"Se exportaron {stateStore.WonPrizeHistory.Count} premio(s) ganado(s) a {filePath}.");
        }

        public PrizeAdminOperationResult ExportPrizePoolSubtraction(string filePath)
        {
            var wonCount = stateStore.WonPrizeHistory.Count;
            return ExportCsvFile(
                filePath,
                () => csvService.ExportPrizePoolSubtractionCsv(stateStore.WonPrizeHistory),
                $"Se exportó la sustracción del pool para {wonCount} premio(s) ganado(s) a {filePath}.");
        }

        // ── Debug claim operations ────────────────────────────────────────────

        /// <summary>
        /// Reserves the best available prize for the given kiosk.
        /// - If <paramref name="preferredCategoryId"/> &gt; 0, restricts to that category.
        /// - If <paramref name="ignoreSchedule"/> is false, only eligible prizes are considered.
        /// - If <paramref name="ignoreSchedule"/> is true, the schedule is bypassed entirely.
        /// </summary>
        public PrizeAdminOperationResult DebugClaimFromKiosk(
            int kioskId, ushort preferredCategoryId, bool ignoreSchedule)
        {
            if (stateStore.ActiveReservation != null)
            {
                return BuildStateResult(false, "Ya hay una reserva de depuración activa.");
            }

            var pool = stateStore.GetKioskPrizes(kioskId);

            // Optional category filter.
            var candidates = preferredCategoryId > 0
                ? pool.Where(p => p.PrizeCategoryId == preferredCategoryId).ToList()
                : pool.ToList();

            // Schedule filter unless forced.
            if (!ignoreSchedule)
            {
                candidates = candidates.Where(p => IsScheduleEligible(p.Schedule)).ToList();
            }

            if (candidates.Count == 0)
            {
                var who = preferredCategoryId > 0
                    ? $"categoría {preferredCategoryId} en el kiosko {kioskId}"
                    : $"kiosko {kioskId}";
                var hint = ignoreSchedule
                    ? string.Empty
                    : "  Use 'Forzar reclamo' para omitir las restricciones de horario.";
                return BuildStateResult(false, $"No hay premios elegibles disponibles en {who}.{hint}");
            }

            var target = candidates[0];
            if (!stateStore.TryReserveSpecificPrize(
                    target.PrizeInstanceId, "Admin Debug", "Debug Winner", "000-DEBUG",
                    kioskId, out var reservation))
            {
                return BuildStateResult(false, $"No se pudo reservar {target.PrizeInstanceId}.");
            }

            var wasForced = ignoreSchedule && !IsScheduleEligible(target.Schedule);
            var suffix = wasForced ? "  [horario omitido]" : string.Empty;
            return BuildStateResult(true,
                $"Se reservó {reservation.ReservedPrize.PrizeInstanceId} " +
                $"({reservation.ReservedPrize.PrizeName}) del kiosko {kioskId}.{suffix}");
        }

        public PrizeAdminOperationResult DebugCancelClaim()
        {
            if (!stateStore.CancelActiveReservation(out var cancelled))
            {
                return BuildStateResult(false, "No hay una reserva activa para cancelar.");
            }

            return BuildStateResult(true,
                $"Se canceló el reclamo de {cancelled.ReservedPrize.PrizeInstanceId} " +
                $"(kiosko {cancelled.KioskId}). Premio devuelto al pool.");
        }

        public PrizeAdminOperationResult DebugConfirmClaim()
        {
            if (!stateStore.ConfirmActiveReservation(out var wonRecord))
            {
                return BuildStateResult(false, "No hay una reserva activa para confirmar.");
            }

            return BuildStateResult(true,
                $"Se confirmó el reclamo de {wonRecord.WonPrizeInstanceId} (kiosko {wonRecord.KioskId}).");
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private void AppendTemplateConflictsAgainstState(PrizeCsvImportPreview preview)
        {
            foreach (var template in preview.Templates)
            {
                if (!stateStore.TryGetTemplate(template.PrizeCategoryId, out var storedTemplate))
                {
                    continue;
                }

                if (storedTemplate.SemanticallyEquals(template))
                {
                    continue;
                }

                var rowNumber = preview.SourceRowsByCategory.TryGetValue(template.PrizeCategoryId, out var storedRow)
                    ? storedRow : 0;

                preview.Issues.Add(new CsvValidationIssue
                {
                    Severity = CsvValidationSeverity.Error,
                    RowNumber = rowNumber,
                    ColumnIndex = 1,
                    ColumnName = "PrizeCategoryId",
                    Message = $"La categoría {template.PrizeCategoryId} entra en conflicto con la plantilla almacenada en Prize Manager.",
                });
            }
        }

        private List<PrizeInstance> BuildSequencedInstances(PrizeCsvImportPreview preview)
        {
            var instanceCounts = preview.Instances
                .GroupBy(i => i.PrizeCategoryId)
                .ToDictionary(g => g.Key, g => g.Count());
            var templatesByCategory = preview.Templates.ToDictionary(t => t.PrizeCategoryId, t => t);
            var sequenced = new List<PrizeInstance>(preview.Instances.Count);

            foreach (var categoryGroup in instanceCounts.OrderBy(g => g.Key))
            {
                var nextSeq = stateStore.GetNextInstanceSequence(categoryGroup.Key);
                var template = templatesByCategory[categoryGroup.Key];
                for (var offset = 0; offset < categoryGroup.Value; offset++)
                {
                    sequenced.Add(new PrizeInstance
                    {
                        PrizeInstanceId = PrizeAdminStateStore.FormatInstanceId(categoryGroup.Key, nextSeq + offset),
                        PrizeCategoryId = template.PrizeCategoryId,
                        PrizeName = template.PrizeName,
                        PrizeDescription = template.PrizeDescription,
                        PrizePriority = template.PrizePriority,
                        Schedule = template.Schedule.Clone(),
                    });
                }
            }

            return sequenced;
        }

        private PrizeAdminOperationResult ExportCsvFile(
            string filePath, Func<string> buildCsv, string successMessage)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return new PrizeAdminOperationResult
                {
                    Success = false,
                    Summary = "Se requiere una ruta de archivo de exportación local.",
                    TemplateCount = stateStore.Templates.Count,
                    AvailablePrizeCount = stateStore.AvailablePrizeInstances.Count,
                    WonPrizeCount = stateStore.WonPrizeHistory.Count,
                    SettingsSnapshot = stateStore.ActiveSettings,
                    ActiveReservation = stateStore.ActiveReservation,
                };
            }

            try
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(filePath, buildCsv(), Encoding.UTF8);

                return new PrizeAdminOperationResult
                {
                    Success = true,
                    Summary = successMessage,
                    TemplateCount = stateStore.Templates.Count,
                    AvailablePrizeCount = stateStore.AvailablePrizeInstances.Count,
                    WonPrizeCount = stateStore.WonPrizeHistory.Count,
                    SettingsSnapshot = stateStore.ActiveSettings,
                    ActiveReservation = stateStore.ActiveReservation,
                };
            }
            catch (Exception exception)
            {
                return new PrizeAdminOperationResult
                {
                    Success = false,
                    Summary = $"Error al exportar: {exception.Message}",
                    TemplateCount = stateStore.Templates.Count,
                    AvailablePrizeCount = stateStore.AvailablePrizeInstances.Count,
                    WonPrizeCount = stateStore.WonPrizeHistory.Count,
                    SettingsSnapshot = stateStore.ActiveSettings,
                    ActiveReservation = stateStore.ActiveReservation,
                };
            }
        }

        private static List<CsvValidationIssue> CloneIssues(IEnumerable<CsvValidationIssue> issues)
        {
            return issues.Select(i => i.Clone()).ToList();
        }

        private PrizeAdminOperationResult BuildStateResult(bool success, string summary)
        {
            return new PrizeAdminOperationResult
            {
                Success = success,
                Summary = summary,
                TemplateCount = stateStore.Templates.Count,
                AvailablePrizeCount = stateStore.AvailablePrizeInstances.Count,
                WonPrizeCount = stateStore.WonPrizeHistory.Count,
                SettingsSnapshot = stateStore.ActiveSettings,
                ActiveReservation = stateStore.ActiveReservation,
            };
        }
    }
}
