using System.Collections.Generic;
using System.Linq;

namespace GETravelGames.PrizeManager
{
    public sealed class PrizeAdminStateStore
    {
        private readonly Dictionary<ushort, PrizeTemplate> templates = new();
        private readonly List<PrizeInstance> availablePrizeInstances = new();
        private readonly List<WonPrizeRecord> wonPrizeHistory = new();
        private PrizeRuntimeSettings activeSettings = new();
        private PrizeClaimReservation activeReservation;

        public IReadOnlyList<PrizeTemplate> Templates => templates.Values.OrderBy(template => template.PrizeCategoryId).Select(template => template.Clone()).ToList();

        public IReadOnlyList<PrizeInstance> AvailablePrizeInstances => availablePrizeInstances.Select(instance => instance.Clone()).ToList();

        public IReadOnlyList<WonPrizeRecord> WonPrizeHistory => wonPrizeHistory.Select(record => record.Clone()).ToList();

        public PrizeRuntimeSettings ActiveSettings => activeSettings.Clone();

        public PrizeClaimReservation ActiveReservation => activeReservation?.Clone();

        public void ReplaceAvailablePrizes(IEnumerable<PrizeTemplate> incomingTemplates, IEnumerable<PrizeInstance> incomingInstances)
        {
            templates.Clear();
            availablePrizeInstances.Clear();

            foreach (var template in incomingTemplates)
            {
                templates[template.PrizeCategoryId] = template.Clone();
            }

            foreach (var instance in incomingInstances)
            {
                availablePrizeInstances.Add(instance.Clone());
            }

            SortAvailablePrizeInstances();
        }

        public void AddAvailablePrizes(IEnumerable<PrizeTemplate> incomingTemplates, IEnumerable<PrizeInstance> incomingInstances)
        {
            foreach (var template in incomingTemplates)
            {
                templates[template.PrizeCategoryId] = template.Clone();
            }

            foreach (var instance in incomingInstances)
            {
                availablePrizeInstances.Add(instance.Clone());
            }

            SortAvailablePrizeInstances();
        }

        public void ReplaceRuntimeSettings(PrizeRuntimeSettings settings)
        {
            activeSettings = settings.Clone();
        }

        public void AddWonPrizeRecord(WonPrizeRecord record)
        {
            wonPrizeHistory.Add(record.Clone());
        }

        public void ReplaceWonPrizeHistory(IEnumerable<WonPrizeRecord> records)
        {
            wonPrizeHistory.Clear();

            foreach (var record in records)
            {
                wonPrizeHistory.Add(record.Clone());
            }
        }

        public bool TryReserveNextAvailablePrize(string winnerOffice, string winnerName, string winnerPhoneNumber, out PrizeClaimReservation reservation)
        {
            reservation = null;
            if (activeReservation != null || availablePrizeInstances.Count == 0)
            {
                return false;
            }

            var reservedPrize = availablePrizeInstances[0].Clone();
            availablePrizeInstances.RemoveAt(0);

            activeReservation = new PrizeClaimReservation
            {
                ReservedPrize = reservedPrize,
                WinnerOffice = winnerOffice ?? string.Empty,
                WinnerName = winnerName ?? string.Empty,
                WinnerPhoneNumber = winnerPhoneNumber ?? string.Empty,
            };

            reservation = activeReservation.Clone();
            return true;
        }

        public bool CancelActiveReservation(out PrizeClaimReservation cancelledReservation)
        {
            cancelledReservation = activeReservation?.Clone();
            if (activeReservation == null)
            {
                return false;
            }

            availablePrizeInstances.Add(activeReservation.ReservedPrize.Clone());
            SortAvailablePrizeInstances();
            activeReservation = null;
            return true;
        }

        public bool ConfirmActiveReservation(out WonPrizeRecord wonPrizeRecord)
        {
            wonPrizeRecord = null;
            if (activeReservation == null)
            {
                return false;
            }

            wonPrizeRecord = new WonPrizeRecord
            {
                WonPrizeInstanceId = activeReservation.ReservedPrize.PrizeInstanceId,
                PrizeCategoryId = activeReservation.ReservedPrize.PrizeCategoryId,
                PrizeName = activeReservation.ReservedPrize.PrizeName,
                PrizeDescription = activeReservation.ReservedPrize.PrizeDescription,
                WinnerOffice = activeReservation.WinnerOffice,
                WinnerName = activeReservation.WinnerName,
                WinnerPhoneNumber = activeReservation.WinnerPhoneNumber,
            };

            wonPrizeHistory.Add(wonPrizeRecord.Clone());
            activeReservation = null;
            return true;
        }

        public bool TryGetTemplate(ushort prizeCategoryId, out PrizeTemplate template)
        {
            if (templates.TryGetValue(prizeCategoryId, out var storedTemplate))
            {
                template = storedTemplate.Clone();
                return true;
            }

            template = null;
            return false;
        }

        public int GetNextInstanceSequence(ushort prizeCategoryId)
        {
            var maxSequence = 0;

            foreach (var instance in availablePrizeInstances)
            {
                if (TryExtractSequence(instance.PrizeInstanceId, prizeCategoryId, out var sequence))
                {
                    maxSequence = System.Math.Max(maxSequence, sequence);
                }
            }

            foreach (var record in wonPrizeHistory)
            {
                if (TryExtractSequence(record.WonPrizeInstanceId, prizeCategoryId, out var sequence))
                {
                    maxSequence = System.Math.Max(maxSequence, sequence);
                }
            }

            return maxSequence + 1;
        }

        public static string FormatInstanceId(ushort prizeCategoryId, int sequence)
        {
            return $"{prizeCategoryId}-{sequence:D4}";
        }

        private void SortAvailablePrizeInstances()
        {
            availablePrizeInstances.Sort(ComparePrizeInstances);
        }

        private static int ComparePrizeInstances(PrizeInstance left, PrizeInstance right)
        {
            var categoryComparison = left.PrizeCategoryId.CompareTo(right.PrizeCategoryId);
            if (categoryComparison != 0)
            {
                return categoryComparison;
            }

            var leftSequence = TryExtractSequence(left.PrizeInstanceId, left.PrizeCategoryId, out var parsedLeftSequence)
                ? parsedLeftSequence
                : int.MaxValue;
            var rightSequence = TryExtractSequence(right.PrizeInstanceId, right.PrizeCategoryId, out var parsedRightSequence)
                ? parsedRightSequence
                : int.MaxValue;
            return leftSequence.CompareTo(rightSequence);
        }

        private static bool TryExtractSequence(string instanceId, ushort expectedCategoryId, out int sequence)
        {
            sequence = 0;
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                return false;
            }

            var split = instanceId.Split('-');
            if (split.Length != 2)
            {
                return false;
            }

            if (!ushort.TryParse(split[0], out var parsedCategoryId) || parsedCategoryId != expectedCategoryId)
            {
                return false;
            }

            return int.TryParse(split[1], out sequence);
        }
    }
}
