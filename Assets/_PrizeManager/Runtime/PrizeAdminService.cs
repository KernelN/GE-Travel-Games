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
                    Summary = $"Prize {importMode.ToString().ToLowerInvariant()} import has validation errors.",
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
                Summary = $"Imported {sequencedInstances.Count} available prizes across " +
                          $"{preview.Templates.Count} categories ({importMode.ToString().ToLowerInvariant()} mode), " +
                          $"distributed across {kioskCount} kiosk(s).",
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
                    Summary = "Settings import has validation errors.",
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
                Summary = $"Imported runtime settings for timezone {preview.Settings.Timezone} " +
                          $"with {preview.Settings.KioskCount} kiosk(s).",
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
                $"Exported {stateStore.WonPrizeHistory.Count} won prize(s) to {filePath}.");
        }

        /// <summary>
        /// End-of-day export.  Aggregates won prizes by category and writes one row
        /// per category containing the count to subtract from physical stock.
        /// </summary>
        public PrizeAdminOperationResult ExportPrizePoolSubtraction(string filePath)
        {
            var wonCount = stateStore.WonPrizeHistory.Count;
            return ExportCsvFile(
                filePath,
                () => csvService.ExportPrizePoolSubtractionCsv(stateStore.WonPrizeHistory),
                $"Exported prize pool subtraction for {wonCount} won prize(s) to {filePath}.");
        }

        // ── Debug operations ──────────────────────────────────────────────────

        /// <summary>Reserves the first available prize from kiosk 1.</summary>
        public PrizeAdminOperationResult DebugClaimPrize()
        {
            return DebugClaimPrizeForKiosk(1);
        }

        /// <summary>Reserves the first available prize from the specified kiosk.</summary>
        public PrizeAdminOperationResult DebugClaimPrizeForKiosk(int kioskId)
        {
            if (stateStore.ActiveReservation != null)
            {
                return BuildStateResult(false, "A debug reservation is already active.");
            }

            if (!stateStore.TryReserveNextAvailablePrize("Admin Debug", "Debug Winner", "000-DEBUG", kioskId, out var reservation))
            {
                return BuildStateResult(false, $"No available prizes remain in kiosk {kioskId}.");
            }

            return BuildStateResult(true,
                $"Reserved prize {reservation.ReservedPrize.PrizeInstanceId} " +
                $"from kiosk {kioskId} for debug claim.");
        }

        /// <summary>Reserves a specific prize instance from the specified kiosk.</summary>
        public PrizeAdminOperationResult DebugClaimSpecificPrize(int kioskId, string instanceId)
        {
            if (stateStore.ActiveReservation != null)
            {
                return BuildStateResult(false, "A debug reservation is already active.");
            }

            if (!stateStore.TryReserveSpecificPrize(
                    instanceId, "Admin Debug", "Debug Winner", "000-DEBUG", kioskId, out var reservation))
            {
                return BuildStateResult(false,
                    $"Prize {instanceId} is not available in kiosk {kioskId}. " +
                    $"It may have been claimed or assigned to a different kiosk.");
            }

            return BuildStateResult(true,
                $"Reserved specific prize {reservation.ReservedPrize.PrizeInstanceId} " +
                $"from kiosk {kioskId}.");
        }

        public PrizeAdminOperationResult DebugCancelClaim()
        {
            if (!stateStore.CancelActiveReservation(out var cancelled))
            {
                return BuildStateResult(false, "There is no active debug reservation to cancel.");
            }

            return BuildStateResult(true,
                $"Cancelled debug claim for {cancelled.ReservedPrize.PrizeInstanceId} " +
                $"(kiosk {cancelled.KioskId}). Prize returned to pool.");
        }

        public PrizeAdminOperationResult DebugConfirmClaim()
        {
            if (!stateStore.ConfirmActiveReservation(out var wonRecord))
            {
                return BuildStateResult(false, "There is no active debug reservation to confirm.");
            }

            return BuildStateResult(true,
                $"Confirmed debug claim for {wonRecord.WonPrizeInstanceId} (kiosk {wonRecord.KioskId}).");
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
                    ? storedRow
                    : 0;

                preview.Issues.Add(new CsvValidationIssue
                {
                    Severity = CsvValidationSeverity.Error,
                    RowNumber = rowNumber,
                    ColumnIndex = 1,
                    ColumnName = "PrizeCategoryId",
                    Message = $"Category {template.PrizeCategoryId} conflicts with the template already stored in Prize Manager.",
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
            string filePath,
            Func<string> buildCsv,
            string successMessage)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return new PrizeAdminOperationResult
                {
                    Success = false,
                    Summary = "A local export file path is required.",
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
                    Summary = $"Export failed: {exception.Message}",
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
