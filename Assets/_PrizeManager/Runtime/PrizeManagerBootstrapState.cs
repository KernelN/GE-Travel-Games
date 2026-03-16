using System;
using System.IO;

namespace GETravelGames.PrizeManager
{
    [Serializable]
    public sealed class PrizeManagerBootstrapState
    {
        public string prizeCsvPath = string.Empty;
        public string settingsCsvPath = string.Empty;
        public string wonPrizesExportPath = string.Empty;
        public string previewText = PrizeManagerConstants.InitialPreviewText;
        public string statusText = PrizeManagerConstants.InitialStatusText;

        public void EnsureDefaults(string projectDataPath)
        {
            if (string.IsNullOrWhiteSpace(wonPrizesExportPath))
            {
                wonPrizesExportPath = Path.Combine(projectDataPath, PrizeManagerConstants.DefaultWonPrizesExportFileName);
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
