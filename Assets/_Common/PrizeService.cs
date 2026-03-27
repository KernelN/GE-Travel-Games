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

        public void Initialize()
        {
            csvService = new PrizeCsvService();
            stateStore = new PrizeAdminStateStore();
            adminService = new PrizeAdminService(csvService, stateStore);

            // Read from KioskConfig (PlayerPrefs) with serialized fields as fallback.
            var importPath = KioskConfig.GetImportFolderPath();
            if (string.IsNullOrWhiteSpace(importPath))
                importPath = string.IsNullOrWhiteSpace(importFolderPath)
                    ? Application.dataPath
                    : importFolderPath;

            var prizesFile = KioskConfig.GetPrizesCsvFileName();
            if (string.IsNullOrWhiteSpace(prizesFile)) prizesFile = prizesCsvFileName;

            var settingsFile = KioskConfig.GetSettingsCsvFileName();
            if (string.IsNullOrWhiteSpace(settingsFile)) settingsFile = settingsCsvFileName;

            activeKioskId = KioskConfig.GetKioskId();
            if (activeKioskId < 1) activeKioskId = kioskId;

            activeExportFolderPath = KioskConfig.GetExportFolderPath();
            if (string.IsNullOrWhiteSpace(activeExportFolderPath))
                activeExportFolderPath = string.IsNullOrWhiteSpace(exportFolderPath)
                    ? Application.dataPath
                    : exportFolderPath;

            activePlayersExportFileName = KioskConfig.GetPlayersExportFileName();
            if (string.IsNullOrWhiteSpace(activePlayersExportFileName))
                activePlayersExportFileName = "Jugadores.csv";

            activeSubtractionExportFileName = KioskConfig.GetSubtractionExportFileName();
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
            var pool    = stateStore.GetKioskPrizes(activeKioskId);
            var eligible = pool
                .Where(p => PrizeAdminService.IsScheduleEligible(p.Schedule))
                .ToList();

            if (eligible.Count == 0)
                return PrizePullResult.FalsePrize();

            var falsePrizeChance = ComputeEffectiveFalsePrizeChance(settings, eligible.Count);
            if (UnityEngine.Random.Range(0, 100) < falsePrizeChance)
                return PrizePullResult.FalsePrize();

            // Random selection (not always eligible[0]) so rerolls can yield different prizes.
            var target = eligible[UnityEngine.Random.Range(0, eligible.Count)];
            return PrizePullResult.RealPrizeDry(target);
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
