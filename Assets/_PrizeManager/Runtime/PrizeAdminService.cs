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

            return new PrizeAdminOperationResult
            {
                Success = true,
                Summary = $"Imported {sequencedInstances.Count} available prizes across {preview.Templates.Count} categories using {importMode.ToString().ToLowerInvariant()} mode.",
                TemplateCount = stateStore.Templates.Count,
                AvailablePrizeCount = stateStore.AvailablePrizeInstances.Count,
                WonPrizeCount = stateStore.WonPrizeHistory.Count,
                PrizePreview = preview,
                SettingsSnapshot = stateStore.ActiveSettings,
                ActiveReservation = stateStore.ActiveReservation,
            };
        }

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
                Summary = $"Imported runtime settings for timezone {preview.Settings.Timezone}.",
                TemplateCount = stateStore.Templates.Count,
                AvailablePrizeCount = stateStore.AvailablePrizeInstances.Count,
                WonPrizeCount = stateStore.WonPrizeHistory.Count,
                SettingsPreview = preview,
                SettingsSnapshot = stateStore.ActiveSettings,
                ActiveReservation = stateStore.ActiveReservation,
            };
        }

        public PrizeAdminOperationResult ExportWonPrizes(string filePath)
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

                var csv = csvService.ExportWonPrizesCsv(stateStore.WonPrizeHistory);
                File.WriteAllText(filePath, csv, Encoding.UTF8);

                return new PrizeAdminOperationResult
                {
                    Success = true,
                    Summary = $"Exported {stateStore.WonPrizeHistory.Count} won prizes to {filePath}.",
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
                    Summary = $"Won prize export failed: {exception.Message}",
                    TemplateCount = stateStore.Templates.Count,
                    AvailablePrizeCount = stateStore.AvailablePrizeInstances.Count,
                    WonPrizeCount = stateStore.WonPrizeHistory.Count,
                    SettingsSnapshot = stateStore.ActiveSettings,
                    ActiveReservation = stateStore.ActiveReservation,
                };
            }
        }

        public PrizeAdminOperationResult DebugClaimPrize()
        {
            if (stateStore.ActiveReservation != null)
            {
                return BuildStateResult(false, "A debug reservation is already active.");
            }

            if (!stateStore.TryReserveNextAvailablePrize("Admin Debug", "Debug Winner", "000-DEBUG", out var reservation))
            {
                return BuildStateResult(false, "No available prizes remain to reserve.");
            }

            return BuildStateResult(true, $"Reserved prize {reservation.ReservedPrize.PrizeInstanceId} for debug claim.");
        }

        public PrizeAdminOperationResult DebugCancelClaim()
        {
            if (!stateStore.CancelActiveReservation(out var cancelledReservation))
            {
                return BuildStateResult(false, "There is no active debug reservation to cancel.");
            }

            return BuildStateResult(true, $"Cancelled debug claim for {cancelledReservation.ReservedPrize.PrizeInstanceId}.");
        }

        public PrizeAdminOperationResult DebugConfirmClaim()
        {
            if (!stateStore.ConfirmActiveReservation(out var wonPrizeRecord))
            {
                return BuildStateResult(false, "There is no active debug reservation to confirm.");
            }

            return BuildStateResult(true, $"Confirmed debug claim for {wonPrizeRecord.WonPrizeInstanceId}.");
        }

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

                var rowNumber = preview.SourceRowsByCategory.TryGetValue(template.PrizeCategoryId, out var storedRowNumber)
                    ? storedRowNumber
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
                .GroupBy(instance => instance.PrizeCategoryId)
                .ToDictionary(group => group.Key, group => group.Count());
            var templatesByCategory = preview.Templates.ToDictionary(template => template.PrizeCategoryId, template => template);
            var sequencedInstances = new List<PrizeInstance>(preview.Instances.Count);

            foreach (var categoryGroup in instanceCounts.OrderBy(group => group.Key))
            {
                var nextSequence = stateStore.GetNextInstanceSequence(categoryGroup.Key);
                var template = templatesByCategory[categoryGroup.Key];
                for (var offset = 0; offset < categoryGroup.Value; offset++)
                {
                    sequencedInstances.Add(new PrizeInstance
                    {
                        PrizeInstanceId = PrizeAdminStateStore.FormatInstanceId(categoryGroup.Key, nextSequence + offset),
                        PrizeCategoryId = template.PrizeCategoryId,
                        PrizeName = template.PrizeName,
                        PrizeDescription = template.PrizeDescription,
                        PrizePriority = template.PrizePriority,
                        Schedule = template.Schedule.Clone(),
                    });
                }
            }

            return sequencedInstances;
        }

        private static List<CsvValidationIssue> CloneIssues(IEnumerable<CsvValidationIssue> issues)
        {
            return issues.Select(issue => issue.Clone()).ToList();
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
