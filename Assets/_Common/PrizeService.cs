using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GETravelGames.PrizeManager;
using UnityEngine;

namespace GETravelGames.Common
{
    public sealed class PrizeService : MonoBehaviour
    {
        [Header("Fallback (used if PlayerPrefs empty)")]
        [SerializeField] int kioskId = 1;

        [Header("Tester mode")]
        [Tooltip("When true, reads config from PrizeTesterKioskConfig (separate PlayerPrefs keys) " +
                 "instead of the production KioskConfig. Enable this on the PrizeService in the " +
                 "PrizeGivingTester scene so tester and production settings stay independent.")]
        [SerializeField] bool useTesterConfig;
        [SerializeField] string importFolderPath;
        [SerializeField] string prizesCsvFileName = "Prizes.csv";
        [SerializeField] string settingsCsvFileName = "Settings.csv";
        [SerializeField] string exportFolderPath;
        [SerializeField] string subtractionExportFileName = "PrizePoolSubtraction";

        PrizeCsvService csvService;
        PrizeAdminStateStore stateStore;
        PrizeAdminService adminService;
        bool initialized;

        int activeKioskId;
        string activeExportFolderPath;
        string activePlayersExportFileName;
        string activeSubtractionExportFileName;
        readonly List<PlayerRecord> playerRegistry = new();

        public static PrizeService Instance { get; private set; }
        public bool IsInitialized => initialized;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            if (!initialized) Initialize();
        }

        public void Initialize()
        {
            csvService = new PrizeCsvService();
            stateStore = new PrizeAdminStateStore();
            adminService = new PrizeAdminService(csvService, stateStore);

            // Read from KioskConfig (PlayerPrefs) with serialized fields as fallback.
            // When useTesterConfig is true, read from PrizeTesterKioskConfig instead so
            // tester and production settings stay in completely separate PlayerPrefs keys.
            var importPath = useTesterConfig
                ? PrizeTesterKioskConfig.GetImportFolderPath()
                : KioskConfig.GetImportFolderPath();
            if (string.IsNullOrWhiteSpace(importPath))
                importPath = string.IsNullOrWhiteSpace(importFolderPath)
                    ? Application.dataPath
                    : importFolderPath;

            var prizesFile = useTesterConfig
                ? PrizeTesterKioskConfig.GetPrizesCsvFileName()
                : KioskConfig.GetPrizesCsvFileName();
            if (string.IsNullOrWhiteSpace(prizesFile)) prizesFile = prizesCsvFileName;

            var settingsFile = useTesterConfig
                ? PrizeTesterKioskConfig.GetSettingsCsvFileName()
                : KioskConfig.GetSettingsCsvFileName();
            if (string.IsNullOrWhiteSpace(settingsFile)) settingsFile = settingsCsvFileName;

            activeKioskId = useTesterConfig
                ? PrizeTesterKioskConfig.GetKioskId()
                : KioskConfig.GetKioskId();
            if (activeKioskId < 1) activeKioskId = kioskId;

            activeExportFolderPath = useTesterConfig
                ? PrizeTesterKioskConfig.GetExportFolderPath()
                : KioskConfig.GetExportFolderPath();
            if (string.IsNullOrWhiteSpace(activeExportFolderPath))
                activeExportFolderPath = string.IsNullOrWhiteSpace(exportFolderPath)
                    ? Application.dataPath
                    : exportFolderPath;

            activePlayersExportFileName = useTesterConfig
                ? PrizeTesterKioskConfig.GetPlayersExportFileName()
                : KioskConfig.GetPlayersExportFileName();
            if (string.IsNullOrWhiteSpace(activePlayersExportFileName))
                activePlayersExportFileName = "Jugadores.csv";

            activeSubtractionExportFileName = useTesterConfig
                ? PrizeTesterKioskConfig.GetSubtractionExportFileName()
                : KioskConfig.GetSubtractionExportFileName();
            if (string.IsNullOrWhiteSpace(activeSubtractionExportFileName))
                activeSubtractionExportFileName = subtractionExportFileName;

            // Import settings then prizes.
            var settingsPath = Path.Combine(importPath, settingsFile);
            if (File.Exists(settingsPath))
            {
                var result = adminService.ApplySettingsImport(settingsPath);
                if (result.Success)
                    Debug.Log($"[PrizeService] {result.Summary}");
                else
                    Debug.LogWarning($"[PrizeService] Settings import failed: {result.Summary}");
            }

            var prizesPath = Path.Combine(importPath, prizesFile);
            if (File.Exists(prizesPath))
            {
                var result = adminService.ApplyPrizeImport(prizesPath, PrizeImportMode.Initialize);
                if (result.Success)
                    Debug.Log($"[PrizeService] {result.Summary}");
                else
                    Debug.LogWarning($"[PrizeService] Prize import failed: {result.Summary}");
            }

            // Apply prize subtraction if a file from a previous session exists.
            var subtractionFileName = $"{activeSubtractionExportFileName}_{activeKioskId}.csv";
            var subtractionPath = Path.Combine(importPath, subtractionFileName);
            if (File.Exists(subtractionPath))
            {
                var result = adminService.ApplySubtractionImport(subtractionPath);
                if (result.Success)
                    Debug.Log($"[PrizeService] {result.Summary}");
                else
                    Debug.LogWarning($"[PrizeService] Prize subtraction import failed: {result.Summary}");
            }

            initialized = true;
        }

        public void Reinitialize()
        {
            initialized = false;
            Initialize();
        }

        /// <summary>
        /// Rolls for a prize up to <paramref name="tries"/> times, keeping the best
        /// (highest-level) result. A single reservation is made for the final winner.
        /// </summary>
        /// <param name="tries">
        /// Number of attempts earned by the player (from PlayerSessionData.StageIndex).
        /// Pass 0 to force a false prize with no rolling.
        /// </param>
        public PrizePullResult TryPullPrize(int tries)
        {
            if (!initialized)
                return PrizePullResult.NoPrize("Sistema no inicializado");

            var settings = stateStore.ActiveSettings;

            // Daily cap check always overrides tries.
            if (settings.MaxPrizesPerDay > 0)
            {
                var totalWins = stateStore.WonPrizeHistory.Count(r => r.KioskId == activeKioskId);
                if (totalWins >= settings.MaxPrizesPerDay)
                {
                    var cap = PrizePullResult.FalsePrize();
                    cap.RollSequence = new List<PrizePullResult> { cap };
                    return cap;
                }
            }

            // No tries → forced false prize (player didn't clear any stage milestone).
            if (tries <= 0)
            {
                var forced = PrizePullResult.FalsePrize();
                forced.RollSequence = new List<PrizePullResult> { forced };
                return forced;
            }

            var rollSequence = new List<PrizePullResult>(tries);
            PrizePullResult bestResult = null;

            for (var i = 0; i < tries; i++)
            {
                var roll = RollOnce(settings);
                rollSequence.Add(roll);

                if (roll.Result != PrizePullResult.Outcome.RealPrize)
                {
                    // Track first false/no-prize as default if we have nothing better.
                    if (bestResult == null)
                        bestResult = roll;

                    // AllowReroll=false AND we are still stuck on a non-prize → stop rolling.
                    if (!settings.AllowReroll && bestResult.Result != PrizePullResult.Outcome.RealPrize)
                        break;
                    continue;
                }

                // This roll is a real prize — keep it if it beats the current best.
                if (bestResult == null
                    || bestResult.Result != PrizePullResult.Outcome.RealPrize
                    || roll.WinningLevel > bestResult.WinningLevel)
                {
                    bestResult = roll;
                }
            }

            // Make a single reservation for the best prize found.
            if (bestResult?.Result == PrizePullResult.Outcome.RealPrize)
            {
                var prizeId = bestResult.Reservation?.ReservedPrize?.PrizeInstanceId;
                if (!string.IsNullOrEmpty(prizeId) &&
                    stateStore.TryReserveSpecificPrize(prizeId, "", "", "", activeKioskId, out var reservation))
                {
                    bestResult = PrizePullResult.RealPrize(reservation);
                }
                else
                {
                    Debug.LogWarning("[PrizeService] No se pudo reservar el premio seleccionado.");
                    bestResult = PrizePullResult.FalsePrize();
                }
            }

            bestResult ??= PrizePullResult.FalsePrize();
            bestResult.RollSequence = rollSequence;
            return bestResult;
        }

        /// <summary>
        /// Performs one independent prize roll (false-prize check + random eligible selection)
        /// WITHOUT making a reservation. Used internally for the multi-try loop.
        /// </summary>
        PrizePullResult RollOnce(PrizeRuntimeSettings settings)
        {
            IReadOnlyList<PrizeInstance> pool    = stateStore.GetKioskPrizes(activeKioskId);
            List<PrizeInstance> eligible = pool
                .Where(p => PrizeAdminService.IsScheduleEligible(p.Schedule))
                .ToList();

            if (eligible.Count == 0)
                return PrizePullResult.FalsePrize();

            int falsePrizeChance = ComputeEffectiveFalsePrizeChance(settings, eligible.Count);
            if (UnityEngine.Random.Range(0, 100) < falsePrizeChance)
                return PrizePullResult.FalsePrize();

            // Weighted selection by PrizePriority (higher priority = higher chance).
            var target = WeightedRandomPrize(eligible);
            return PrizePullResult.RealPrizeDry(target);
        }

        /// <summary>
        /// Selects a prize from <paramref name="eligible"/> using each prize's
        /// <see cref="PrizeInstance.PrizePriority"/> as a draw weight. Falls back to
        /// uniform random selection when all priorities are zero.
        /// </summary>
        static PrizeInstance WeightedRandomPrize(List<PrizeInstance> eligible)
        {
            float total = 0f;
            foreach (var p in eligible) total += p.PrizePriority;

            // All priorities zero → uniform selection (backward-compatible with existing data).
            if (total <= 0f)
                return eligible[UnityEngine.Random.Range(0, eligible.Count)];

            float roll = UnityEngine.Random.Range(0f, total);
            float cumulative = 0f;
            PrizeInstance last = null;
            foreach (var p in eligible)
            {
                last = p;
                cumulative += p.PrizePriority;
                if (roll < cumulative) return p;
            }
            return last;
        }

        public bool ClaimPrize(string winnerName, string winnerPhone, string winnerOffice)
        {
            if (!stateStore.UpdateActiveReservationWinnerData(
                    winnerName, winnerPhone, winnerOffice))
                return false;

            if (!stateStore.ConfirmActiveReservation(out _))
                return false;

            return true;
        }

        /// <summary>
        /// Records a completed play session: finds or creates the player by phone number,
        /// increments their play count, attaches prize info if one was won, then exports
        /// Jugadores.csv (full rewrite) and PrizePoolSubtraction (cumulative per-category totals).
        /// Call this once at the end of every PrizeGiving flow.
        /// </summary>
        public void RecordPlay(string firstName, string lastName, string phone, string office,
                               PrizePullResult pull)
        {
            var key = (phone ?? "").Trim();
            var player = playerRegistry.Find(p => p.Phone == key);
            if (player == null)
            {
                player = new PlayerRecord
                {
                    FirstName = (firstName ?? "").Trim(),
                    LastName  = (lastName  ?? "").Trim(),
                    Phone     = key,
                    Office    = (office    ?? "").Trim(),
                };
                playerRegistry.Add(player);
            }

            player.TimesPlayed++;

            if (pull != null && pull.IsRealPrize && pull.Reservation?.ReservedPrize != null)
            {
                var prize = pull.Reservation.ReservedPrize;
                player.WonPrizes.Add((prize.PrizeCategoryId, prize.PrizeInstanceId));
            }

            ExportPlayers();
            ExportPrizePoolSubtraction();
        }

        // ── Tester helpers ─────────────────────────────────────────────────────

        /// <summary>Returns all loaded prize categories, for use by tester tooling.</summary>
        public IReadOnlyList<PrizeTemplate> GetCategories()
        {
            if (!initialized) return Array.Empty<PrizeTemplate>();
            return stateStore.Templates;
        }

        /// <summary>Returns all prize instances assigned to the active kiosk, for use by tester tooling.</summary>
        public IReadOnlyList<PrizeInstance> GetKioskInstances()
        {
            if (!initialized) return Array.Empty<PrizeInstance>();
            return stateStore.GetKioskPrizes(activeKioskId);
        }

        /// <summary>
        /// Rolls up to <paramref name="tries"/> times restricted to one prize category.
        /// When <paramref name="saveResult"/> is false: dry run — no reservation, no CSV writes.
        /// When <paramref name="saveResult"/> is true: reserves the prize, claims it, and calls
        /// RecordPlay (writes both Jugadores.csv and PrizePoolSubtraction CSV).
        /// </summary>
        public PrizePullResult TryPullFromCategory(
            ushort categoryId, int tries, bool saveResult,
            string firstName = "Test", string lastName = "Player",
            string phone = "000-0000", string office = "Test Office")
        {
            if (!initialized)
                return PrizePullResult.NoPrize("Sistema no inicializado");

            var settings = stateStore.ActiveSettings;
            int effectiveTries = Mathf.Max(1, tries);
            var rollSequence = new List<PrizePullResult>(effectiveTries);
            PrizePullResult bestResult = null;

            for (var i = 0; i < effectiveTries; i++)
            {
                var roll = RollOnceFromCategory(settings, categoryId);
                rollSequence.Add(roll);

                if (roll.Result != PrizePullResult.Outcome.RealPrize)
                {
                    if (bestResult == null) bestResult = roll;
                    if (!settings.AllowReroll) break;
                    continue;
                }

                if (bestResult == null
                    || bestResult.Result != PrizePullResult.Outcome.RealPrize
                    || roll.WinningLevel > bestResult.WinningLevel)
                    bestResult = roll;
            }

            bestResult ??= PrizePullResult.FalsePrize();
            bestResult.RollSequence = rollSequence;

            if (!saveResult)
                return bestResult;  // dry — no state mutation

            // Save path: reserve → claim → record
            if (bestResult.IsRealPrize)
            {
                var prizeId = bestResult.Reservation?.ReservedPrize?.PrizeInstanceId;
                if (!string.IsNullOrEmpty(prizeId) &&
                    stateStore.TryReserveSpecificPrize(
                        prizeId, office, $"{firstName} {lastName}", phone,
                        activeKioskId, out var reservation))
                {
                    bestResult = PrizePullResult.RealPrize(reservation);
                    bestResult.RollSequence = rollSequence;
                    // ClaimPrize confirms the reservation → adds to wonPrizeHistory,
                    // clears activeReservation so bulk loops can reserve on the next iteration.
                    ClaimPrize($"{firstName} {lastName}", phone, office);
                }
                else
                {
                    Debug.LogWarning("[PrizeService] TryPullFromCategory: reserva fallida.");
                    bestResult = PrizePullResult.FalsePrize();
                    bestResult.RollSequence = rollSequence;
                }
            }

            RecordPlay(firstName, lastName, phone, office, bestResult);
            return bestResult;
        }

        /// <summary>
        /// Like <see cref="RollOnce"/> but restricts the eligible pool to a single category.
        /// Used internally by <see cref="TryPullFromCategory"/>.
        /// </summary>
        PrizePullResult RollOnceFromCategory(PrizeRuntimeSettings settings, ushort categoryId)
        {
            IReadOnlyList<PrizeInstance> pool = stateStore.GetKioskPrizes(activeKioskId);
            List<PrizeInstance> eligible = pool
                .Where(p => p.PrizeCategoryId == categoryId
                         && PrizeAdminService.IsScheduleEligible(p.Schedule))
                .ToList();

            if (eligible.Count == 0)
                return PrizePullResult.FalsePrize();

            int falsePrizeChance = ComputeEffectiveFalsePrizeChance(settings, eligible.Count);
            if (UnityEngine.Random.Range(0, 100) < falsePrizeChance)
                return PrizePullResult.FalsePrize();

            return PrizePullResult.RealPrizeDry(WeightedRandomPrize(eligible));
        }

        int ComputeEffectiveFalsePrizeChance(PrizeRuntimeSettings settings, int eligibleCount)
        {
            var chance = settings.FalsePrizeChancePercent;

            if (settings.FalsePrizeThresholds != null)
            {
                foreach (var threshold in settings.FalsePrizeThresholds
                             .OrderByDescending(t => t.ThresholdPercent))
                {
                    if (eligibleCount <= threshold.ThresholdPercent)
                    {
                        chance = Math.Max(chance, threshold.ChancePercent);
                        break;
                    }
                }
            }

            return chance;
        }

        public void ExportAll()
        {
            ExportPlayers();
            ExportPrizePoolSubtraction();
        }

        void ExportPrizePoolSubtraction()
        {
            try
            {
                var fileName = $"{activeSubtractionExportFileName}_{activeKioskId}.csv";
                var path = Path.Combine(activeExportFolderPath, fileName);
                adminService.ExportPrizePoolSubtraction(path, activeKioskId);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PrizeService] Prize pool subtraction export failed: {e.Message}");
            }
        }

        void ExportPlayers()
        {
            try
            {
                var path = Path.Combine(activeExportFolderPath, activePlayersExportFileName);
                var csv  = csvService.ExportPlayersCsv(playerRegistry);
                File.WriteAllText(path, csv, Encoding.UTF8);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PrizeService] Players export failed: {e.Message}");
            }
        }
    }
}
