namespace GETravelGames.PrizeManager
{
    public static class PrizeManagerConstants
    {
        public const int PrizeCsvColumnCount = 9;
        public const int SettingsCsvColumnCount = 8; // col 7 = KioskCount
        public const int WonPrizeExportColumnCount = 8; // col 7 = KioskId
        public const int SubtractionExportColumnCount = 4;
        public const string AdminSceneName = "Prize Admin Scene";
        public const string DefaultWonPrizesExportFileName = "WonPrizes.csv";
        public const string DefaultPrizePoolSubtractionExportFileName = "PrizePoolSubtraction.csv";
        public const string InitialPreviewText = "Enter local CSV paths, then preview or apply an import.";
        public const string InitialStatusText = "Prize Manager admin tool ready.";
    }
}
