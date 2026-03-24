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
        [SerializeField] string wonPrizesExportFileName = "WonPrizes.csv";

        PrizeCsvService csvService;
        PrizeAdminStateStore stateStore;
        PrizeAdminService adminService;
        bool initialized;

        int activeKioskId;
        string activeExportFolderPath;
        string activeWonPrizesExportFileName;
        string activePlayersExportFileName;
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

            activeWonPrizesExportFileName = KioskConfig.GetWonPrizesExportFileName();
            if (string.IsNullOrWhiteSpace(activeWonPrizesExportFileName))
                activeWonPrizesExportFileName = wonPrizesExportFileName;

            activePlayersExportFileName = KioskConfig.GetPlayersExportFileName();
            if (string.IsNullOrWhiteSpace(activePlayersExportFileName))
                activePlayersExportFileName = "Jugadores.csv";

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

            initialized = true;
        }

        public void Reinitialize()
        {
            initialized = false;
            Initialize();
        }

        public PrizePullResult TryPullPrize()
        {
            if (!initialized)
                return PrizePullResult.NoPrize("Sistema no inicializado");

            var settings = stateStore.ActiveSettings;

            // Daily cap check.
            if (settings.MaxPrizesPerDay > 0)
            {
                var totalWins = stateStore.WonPrizeHistory.Count(r => r.KioskId == activeKioskId);
                if (totalWins >= settings.MaxPrizesPerDay)
                    return PrizePullResult.FalsePrize();
            }

            // Schedule-eligible prizes for this kiosk.
            var pool = stateStore.GetKioskPrizes(activeKioskId);
            var eligible = pool
                .Where(p => PrizeAdminService.IsScheduleEligible(p.Schedule))
                .ToList();

            if (eligible.Count == 0)
                return PrizePullResult.FalsePrize();

            // False prize chance roll.
            var falsePrizeChance = ComputeEffectiveFalsePrizeChance(settings, eligible.Count);
            if (UnityEngine.Random.Range(0, 100) < falsePrizeChance)
                return PrizePullResult.FalsePrize();

            // Reserve the first eligible prize with placeholder winner data.
            var target = eligible[0];
            if (!stateStore.TryReserveSpecificPrize(
                    target.PrizeInstanceId, "", "", "", activeKioskId, out var reservation))
                return PrizePullResult.NoPrize("No se pudo reservar el premio");

            return PrizePullResult.RealPrize(reservation);
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
        /// both Jugadores.csv (full rewrite) and WonPrizes.csv (append).
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
            ExportWonPrizes();
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

        void ExportWonPrizes()
        {
            try
            {
                var path = Path.Combine(activeExportFolderPath, activeWonPrizesExportFileName);
                adminService.ExportWonPrizes(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PrizeService] Won prizes export failed: {e.Message}");
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
