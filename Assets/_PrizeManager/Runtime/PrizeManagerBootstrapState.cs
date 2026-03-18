using System;
using System.IO;

namespace GETravelGames.PrizeManager
{
    [Serializable]
    public sealed class PrizeManagerBootstrapState
    {
        // ── Import config ─────────────────────────────────────────────────────
        public string importFolderPath = string.Empty;
        public string prizesCsvFileName = "Prizes.csv";
        public string settingsCsvFileName = "Settings.csv";

        // ── Export config ─────────────────────────────────────────────────────
        public string exportFolderPath = string.Empty;
        public string wonPrizesExportFileName = "WonPrizes.csv";
        public string prizePoolSubtractionExportFileName = "PrizePoolSubtraction.csv";

        // ── Debug state ───────────────────────────────────────────────────────
        public int debugKioskId = 1;
        /// <summary>0 = no category selected (claim first eligible).</summary>
        public ushort debugPrizeCategoryId = 0;

        // ── Display state ─────────────────────────────────────────────────────
        public string statusText = PrizeManagerConstants.InitialStatusText;
        public string previewText = PrizeManagerConstants.InitialPreviewText;
        public string settingsPreviewText = string.Empty;

        // ── Computed paths ────────────────────────────────────────────────────

        public string PrizesCsvPath =>
            string.IsNullOrWhiteSpace(importFolderPath)
                ? prizesCsvFileName
                : Path.Combine(importFolderPath, prizesCsvFileName);

        public string SettingsCsvPath =>
            string.IsNullOrWhiteSpace(importFolderPath)
                ? settingsCsvFileName
                : Path.Combine(importFolderPath, settingsCsvFileName);

        public string WonPrizesExportPath =>
            string.IsNullOrWhiteSpace(exportFolderPath)
                ? wonPrizesExportFileName
                : Path.Combine(exportFolderPath, wonPrizesExportFileName);

        public string SubtractionExportPath =>
            string.IsNullOrWhiteSpace(exportFolderPath)
                ? prizePoolSubtractionExportFileName
                : Path.Combine(exportFolderPath, prizePoolSubtractionExportFileName);

        // ── Defaults ──────────────────────────────────────────────────────────

        public void EnsureDefaults(string projectDataPath)
        {
            if (string.IsNullOrWhiteSpace(importFolderPath))
            {
                importFolderPath = projectDataPath;
            }

            if (string.IsNullOrWhiteSpace(exportFolderPath))
            {
                exportFolderPath = projectDataPath;
            }

            if (string.IsNullOrWhiteSpace(previewText))
            {
                previewText = PrizeManagerConstants.InitialPreviewText;
            }

            if (string.IsNullOrWhiteSpace(statusText))
            {
                statusText = PrizeManagerConstants.InitialStatusText;
            }
        }
    }
}
