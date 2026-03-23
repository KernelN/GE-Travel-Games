using System;
using System.Collections.Generic;
using System.Linq;

namespace GETravelGames.PrizeManager
{
    /// <summary>
    /// In-memory store for prize state.  Prize instances are distributed round-robin
    /// per category across all configured kiosks.  Reservation and confirmation
    /// track which kiosk owns the reservation so the prize is returned to the
    /// correct kiosk slot if the claim is cancelled.
    /// </summary>
    public sealed class PrizeAdminStateStore
    {
        private readonly Dictionary<ushort, PrizeTemplate> templates = new();

        // Primary flat list – the single source of truth for available instances.
        private readonly List<PrizeInstance> allAvailableInstances = new();

        // Derived kiosk pools, rebuilt whenever instances or settings change.
        // Key = kiosk ID (1-based).
        private Dictionary<int, List<PrizeInstance>> kioskPrizePools = new();

        private readonly List<WonPrizeRecord> wonPrizeHistory = new();
        private PrizeRuntimeSettings activeSettings = new();
        private PrizeClaimReservation activeReservation;

        // ── Read-only accessors ──────────────────────────────────────────────

        public IReadOnlyList<PrizeTemplate> Templates =>
            templates.Values
                     .OrderBy(t => t.PrizeCategoryId)
                     .Select(t => t.Clone())
                     .ToList();

        /// <summary>All available instances across every kiosk, sorted by category then sequence.</summary>
        public IReadOnlyList<PrizeInstance> AvailablePrizeInstances =>
            allAvailableInstances.Select(i => i.Clone()).ToList();

        public IReadOnlyList<WonPrizeRecord> WonPrizeHistory =>
            wonPrizeHistory.Select(r => r.Clone()).ToList();

        public PrizeRuntimeSettings ActiveSettings => activeSettings.Clone();

        public PrizeClaimReservation ActiveReservation => activeReservation?.Clone();

        /// <summary>
        /// Returns available instance counts keyed by kiosk ID.
        /// Always returns an entry for every kiosk from 1 to KioskCount.
        /// </summary>
        public IDictionary<int, int> KioskPrizeCounts
        {
            get
            {
                var result = new Dictionary<int, int>();
                foreach (var kvp in kioskPrizePools)
                {
                    result[kvp.Key] = kvp.Value.Count;
                }

                return result;
            }
        }

        // ── Prize pool mutations ─────────────────────────────────────────────

        public void ReplaceAvailablePrizes(
            IEnumerable<PrizeTemplate> incomingTemplates,
            IEnumerable<PrizeInstance> incomingInstances)
        {
            templates.Clear();
            allAvailableInstances.Clear();

            foreach (var template in incomingTemplates)
            {
                templates[template.PrizeCategoryId] = template.Clone();
            }

            foreach (var instance in incomingInstances)
            {
                allAvailableInstances.Add(instance.Clone());
            }

            SortAllInstances();
            RebuildKioskPools();
        }

        public void AddAvailablePrizes(
            IEnumerable<PrizeTemplate> incomingTemplates,
            IEnumerable<PrizeInstance> incomingInstances)
        {
            foreach (var template in incomingTemplates)
            {
                templates[template.PrizeCategoryId] = template.Clone();
            }

            foreach (var instance in incomingInstances)
            {
                allAvailableInstances.Add(instance.Clone());
            }

            SortAllInstances();
            RebuildKioskPools();
        }

        public void ReplaceRuntimeSettings(PrizeRuntimeSettings settings)
        {
            var previousKioskCount = activeSettings.KioskCount;
            activeSettings = settings.Clone();

            if (activeSettings.KioskCount != previousKioskCount)
            {
                RebuildKioskPools();
            }
        }

        // ── Won-prize history ────────────────────────────────────────────────

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

        // ── Reservation lifecycle ────────────────────────────────────────────

        /// <summary>
        /// Reserves the first available prize from the specified kiosk.
        /// </summary>
        public bool TryReserveNextAvailablePrize(
            string winnerOffice,
            string winnerName,
            string winnerPhoneNumber,
            int kioskId,
            out PrizeClaimReservation reservation)
        {
            reservation = null;

            if (activeReservation != null)
            {
                return false;
            }

            EnsureKioskPool(kioskId);

            var pool = kioskPrizePools[kioskId];
            if (pool.Count == 0)
            {
                return false;
            }

            var reservedPrize = pool[0].Clone();
            pool.RemoveAt(0);
            RemoveFromFlatList(reservedPrize.PrizeInstanceId);

            activeReservation = new PrizeClaimReservation
            {
                ReservedPrize = reservedPrize,
                WinnerOffice = winnerOffice ?? string.Empty,
                WinnerName = winnerName ?? string.Empty,
                WinnerPhoneNumber = winnerPhoneNumber ?? string.Empty,
                KioskId = kioskId,
            };

            reservation = activeReservation.Clone();
            return true;
        }

        /// <summary>
        /// Reserves a specific prize instance from the specified kiosk pool.
        /// Returns false if the instance is not found in that kiosk's pool.
        /// </summary>
        public bool TryReserveSpecificPrize(
            string prizeInstanceId,
            string winnerOffice,
            string winnerName,
            string winnerPhoneNumber,
            int kioskId,
            out PrizeClaimReservation reservation)
        {
            reservation = null;

            if (activeReservation != null || string.IsNullOrWhiteSpace(prizeInstanceId))
            {
                return false;
            }

            EnsureKioskPool(kioskId);

            var pool = kioskPrizePools[kioskId];
            var poolIndex = pool.FindIndex(i => i.PrizeInstanceId == prizeInstanceId);
            if (poolIndex < 0)
            {
                return false;
            }

            var reservedPrize = pool[poolIndex].Clone();
            pool.RemoveAt(poolIndex);
            RemoveFromFlatList(prizeInstanceId);

            activeReservation = new PrizeClaimReservation
            {
                ReservedPrize = reservedPrize,
                WinnerOffice = winnerOffice ?? string.Empty,
                WinnerName = winnerName ?? string.Empty,
                WinnerPhoneNumber = winnerPhoneNumber ?? string.Empty,
                KioskId = kioskId,
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

            // Return instance to flat list, then rebuild so kiosk assignment is recalculated.
            allAvailableInstances.Add(activeReservation.ReservedPrize.Clone());
            SortAllInstances();
            RebuildKioskPools();

            activeReservation = null;
            return true;
        }

        public bool UpdateActiveReservationWinnerData(
            string winnerName, string winnerPhoneNumber, string winnerOffice)
        {
            if (activeReservation == null)
            {
                return false;
            }

            activeReservation.WinnerName = winnerName ?? string.Empty;
            activeReservation.WinnerPhoneNumber = winnerPhoneNumber ?? string.Empty;
            activeReservation.WinnerOffice = winnerOffice ?? string.Empty;
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
                KioskId = activeReservation.KioskId,
            };

            wonPrizeHistory.Add(wonPrizeRecord.Clone());
            activeReservation = null;
            return true;
        }

        // ── Kiosk query ───────────────────────────────────────────────────────

        /// <summary>
        /// Returns the available prizes assigned to a specific kiosk, sorted by category then sequence.
        /// Returns an empty list if the kiosk has no prizes or the kiosk ID is out of range.
        /// </summary>
        public IReadOnlyList<PrizeInstance> GetKioskPrizes(int kioskId)
        {
            EnsureKioskPool(kioskId);
            return kioskPrizePools[kioskId].Select(i => i.Clone()).ToList();
        }

        // ── Template and sequence helpers ────────────────────────────────────

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

        /// <summary>
        /// Returns the next sequence number to use for new instances of this category,
        /// taking both the available pool and won-prize history into account.
        /// </summary>
        public int GetNextInstanceSequence(ushort prizeCategoryId)
        {
            var max = 0;

            foreach (var instance in allAvailableInstances)
            {
                if (TryExtractSequence(instance.PrizeInstanceId, prizeCategoryId, out var seq))
                {
                    max = Math.Max(max, seq);
                }
            }

            foreach (var record in wonPrizeHistory)
            {
                if (TryExtractSequence(record.WonPrizeInstanceId, prizeCategoryId, out var seq))
                {
                    max = Math.Max(max, seq);
                }
            }

            return max + 1;
        }

        public static string FormatInstanceId(ushort prizeCategoryId, int sequence)
        {
            return $"{prizeCategoryId}-{sequence:D4}";
        }

        // ── Kiosk pool management ────────────────────────────────────────────

        /// <summary>
        /// Rebuilds kiosk pools from the flat available list using round-robin
        /// distribution per prize category.
        /// </summary>
        private void RebuildKioskPools()
        {
            var kioskCount = Math.Max(1, activeSettings.KioskCount);

            kioskPrizePools = new Dictionary<int, List<PrizeInstance>>();
            for (var k = 1; k <= kioskCount; k++)
            {
                kioskPrizePools[k] = new List<PrizeInstance>();
            }

            // Distribute round-robin within each category.
            var grouped = allAvailableInstances
                .GroupBy(i => i.PrizeCategoryId)
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                var slotIndex = 0;
                foreach (var instance in group)
                {
                    var kioskId = (slotIndex % kioskCount) + 1;
                    kioskPrizePools[kioskId].Add(instance.Clone());
                    slotIndex++;
                }
            }
        }

        private void EnsureKioskPool(int kioskId)
        {
            if (!kioskPrizePools.ContainsKey(kioskId))
            {
                kioskPrizePools[kioskId] = new List<PrizeInstance>();
            }
        }

        private void RemoveFromFlatList(string instanceId)
        {
            var flatIndex = allAvailableInstances.FindIndex(i => i.PrizeInstanceId == instanceId);
            if (flatIndex >= 0)
            {
                allAvailableInstances.RemoveAt(flatIndex);
            }
        }

        private void SortAllInstances()
        {
            allAvailableInstances.Sort(ComparePrizeInstances);
        }

        private static int ComparePrizeInstances(PrizeInstance left, PrizeInstance right)
        {
            var categoryComparison = left.PrizeCategoryId.CompareTo(right.PrizeCategoryId);
            if (categoryComparison != 0)
            {
                return categoryComparison;
            }

            var leftSeq = TryExtractSequence(left.PrizeInstanceId, left.PrizeCategoryId, out var ls) ? ls : int.MaxValue;
            var rightSeq = TryExtractSequence(right.PrizeInstanceId, right.PrizeCategoryId, out var rs) ? rs : int.MaxValue;
            return leftSeq.CompareTo(rightSeq);
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
