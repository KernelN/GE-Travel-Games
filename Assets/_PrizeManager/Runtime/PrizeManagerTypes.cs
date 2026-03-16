using System;
using System.Collections.Generic;
using System.Linq;

namespace GETravelGames.PrizeManager
{
    public enum PrizeImportMode
    {
        Initialize = 0,
        Add = 1,
    }

    public enum CsvValidationSeverity
    {
        Error = 0,
        Warning = 1,
    }

    [Serializable]
    public sealed class PrizeSchedule
    {
        public int? PrizeStartMinutesOfDay;
        public int? PrizeEndMinutesOfDay;
        public bool HasToComeOutDuringHour;
        public List<int> PrizeDays = new();

        public PrizeSchedule Clone()
        {
            return new PrizeSchedule
            {
                PrizeStartMinutesOfDay = PrizeStartMinutesOfDay,
                PrizeEndMinutesOfDay = PrizeEndMinutesOfDay,
                HasToComeOutDuringHour = HasToComeOutDuringHour,
                PrizeDays = PrizeDays.ToList(),
            };
        }

        public bool SemanticallyEquals(PrizeSchedule other)
        {
            if (other == null)
            {
                return false;
            }

            return PrizeStartMinutesOfDay == other.PrizeStartMinutesOfDay
                && PrizeEndMinutesOfDay == other.PrizeEndMinutesOfDay
                && HasToComeOutDuringHour == other.HasToComeOutDuringHour
                && PrizeDays.OrderBy(day => day).SequenceEqual(other.PrizeDays.OrderBy(day => day));
        }
    }

    [Serializable]
    public sealed class PrizeTemplate
    {
        public ushort PrizeCategoryId;
        public string PrizeName = string.Empty;
        public string PrizeDescription = string.Empty;
        public ushort PrizePriority;
        public PrizeSchedule Schedule = new();

        public PrizeTemplate Clone()
        {
            return new PrizeTemplate
            {
                PrizeCategoryId = PrizeCategoryId,
                PrizeName = PrizeName,
                PrizeDescription = PrizeDescription,
                PrizePriority = PrizePriority,
                Schedule = Schedule.Clone(),
            };
        }

        public bool SemanticallyEquals(PrizeTemplate other)
        {
            if (other == null)
            {
                return false;
            }

            return PrizeCategoryId == other.PrizeCategoryId
                && string.Equals(PrizeName, other.PrizeName, StringComparison.Ordinal)
                && string.Equals(PrizeDescription, other.PrizeDescription, StringComparison.Ordinal)
                && PrizePriority == other.PrizePriority
                && Schedule.SemanticallyEquals(other.Schedule);
        }
    }

    [Serializable]
    public sealed class PrizeInstance
    {
        public string PrizeInstanceId = string.Empty;
        public ushort PrizeCategoryId;
        public string PrizeName = string.Empty;
        public string PrizeDescription = string.Empty;
        public ushort PrizePriority;
        public PrizeSchedule Schedule = new();

        public PrizeInstance Clone()
        {
            return new PrizeInstance
            {
                PrizeInstanceId = PrizeInstanceId,
                PrizeCategoryId = PrizeCategoryId,
                PrizeName = PrizeName,
                PrizeDescription = PrizeDescription,
                PrizePriority = PrizePriority,
                Schedule = Schedule.Clone(),
            };
        }

        public PrizeTemplate ToTemplate()
        {
            return new PrizeTemplate
            {
                PrizeCategoryId = PrizeCategoryId,
                PrizeName = PrizeName,
                PrizeDescription = PrizeDescription,
                PrizePriority = PrizePriority,
                Schedule = Schedule.Clone(),
            };
        }
    }

    [Serializable]
    public sealed class PrizeChanceThreshold
    {
        public int ThresholdPercent;
        public int ChancePercent;

        public PrizeChanceThreshold Clone()
        {
            return new PrizeChanceThreshold
            {
                ThresholdPercent = ThresholdPercent,
                ChancePercent = ChancePercent,
            };
        }
    }

    [Serializable]
    public sealed class PrizeRuntimeSettings
    {
        public string Timezone = string.Empty;
        public int PrizeReservationTimeoutMinutes;
        public int MaxPrizesPerDay;
        public int FalsePrizeChancePercent;
        public int ForcedHourChancePercent;
        public List<PrizeChanceThreshold> FalsePrizeThresholds = new();
        public List<PrizeChanceThreshold> ForcedHourThresholds = new();

        public PrizeRuntimeSettings Clone()
        {
            return new PrizeRuntimeSettings
            {
                Timezone = Timezone,
                PrizeReservationTimeoutMinutes = PrizeReservationTimeoutMinutes,
                MaxPrizesPerDay = MaxPrizesPerDay,
                FalsePrizeChancePercent = FalsePrizeChancePercent,
                ForcedHourChancePercent = ForcedHourChancePercent,
                FalsePrizeThresholds = FalsePrizeThresholds.Select(threshold => threshold.Clone()).ToList(),
                ForcedHourThresholds = ForcedHourThresholds.Select(threshold => threshold.Clone()).ToList(),
            };
        }
    }

    [Serializable]
    public sealed class WonPrizeRecord
    {
        public string WonPrizeInstanceId = string.Empty;
        public ushort PrizeCategoryId;
        public string PrizeName = string.Empty;
        public string PrizeDescription = string.Empty;
        public string WinnerOffice = string.Empty;
        public string WinnerName = string.Empty;
        public string WinnerPhoneNumber = string.Empty;

        public WonPrizeRecord Clone()
        {
            return new WonPrizeRecord
            {
                WonPrizeInstanceId = WonPrizeInstanceId,
                PrizeCategoryId = PrizeCategoryId,
                PrizeName = PrizeName,
                PrizeDescription = PrizeDescription,
                WinnerOffice = WinnerOffice,
                WinnerName = WinnerName,
                WinnerPhoneNumber = WinnerPhoneNumber,
            };
        }
    }

    [Serializable]
    public sealed class PrizeClaimReservation
    {
        public PrizeInstance ReservedPrize = new();
        public string WinnerOffice = string.Empty;
        public string WinnerName = string.Empty;
        public string WinnerPhoneNumber = string.Empty;

        public PrizeClaimReservation Clone()
        {
            return new PrizeClaimReservation
            {
                ReservedPrize = ReservedPrize?.Clone() ?? new PrizeInstance(),
                WinnerOffice = WinnerOffice,
                WinnerName = WinnerName,
                WinnerPhoneNumber = WinnerPhoneNumber,
            };
        }
    }

    [Serializable]
    public sealed class CsvValidationIssue
    {
        public CsvValidationSeverity Severity = CsvValidationSeverity.Error;
        public int RowNumber;
        public int ColumnIndex;
        public string ColumnName = string.Empty;
        public string Message = string.Empty;

        public bool IsError => Severity == CsvValidationSeverity.Error;

        public CsvValidationIssue Clone()
        {
            return new CsvValidationIssue
            {
                Severity = Severity,
                RowNumber = RowNumber,
                ColumnIndex = ColumnIndex,
                ColumnName = ColumnName,
                Message = Message,
            };
        }
    }

    [Serializable]
    public sealed class PrizeCsvImportPreview
    {
        public PrizeImportMode ImportMode;
        public char Delimiter;
        public List<PrizeTemplate> Templates = new();
        public List<PrizeInstance> Instances = new();
        public List<CsvValidationIssue> Issues = new();
        public Dictionary<ushort, int> SourceRowsByCategory = new();

        public bool IsValid => Issues.All(issue => !issue.IsError);
    }

    [Serializable]
    public sealed class SettingsCsvPreview
    {
        public char Delimiter;
        public PrizeRuntimeSettings Settings = new();
        public List<CsvValidationIssue> Issues = new();

        public bool IsValid => Issues.All(issue => !issue.IsError);
    }

    [Serializable]
    public sealed class PrizeAdminOperationResult
    {
        public bool Success;
        public string Summary = string.Empty;
        public List<CsvValidationIssue> Issues = new();
        public int TemplateCount;
        public int AvailablePrizeCount;
        public int WonPrizeCount;
        public PrizeRuntimeSettings SettingsSnapshot;
        public PrizeCsvImportPreview PrizePreview;
        public SettingsCsvPreview SettingsPreview;
        public PrizeClaimReservation ActiveReservation;
    }
}
