using UnityEngine;

namespace GETravelGames.Common
{
    /// <summary>
    /// PlayerPrefs-backed config for the prize tester scenes (PrizeGivingTester, PrizeTesterConfig).
    /// Uses a separate key namespace ("PrizeTester_*") so tester settings never collide with
    /// the production KioskConfig keys written by the main Config scene.
    /// </summary>
    public static class PrizeTesterKioskConfig
    {
        const string KeyImportFolderPath          = "PrizeTester_ImportFolderPath";
        const string KeyExportFolderPath          = "PrizeTester_ExportFolderPath";
        const string KeyPrizesCsvFileName         = "PrizeTester_PrizesCsvFileName";
        const string KeySettingsCsvFileName       = "PrizeTester_SettingsCsvFileName";
        const string KeyPlayersExportFileName     = "PrizeTester_PlayersExportFileName";
        const string KeySubtractionExportFileName = "PrizeTester_SubtractionExportFileName";
        const string KeyKioskId                   = "PrizeTester_KioskId";

        const string DefaultPrizesCsvFileName         = "Prizes.csv";
        const string DefaultSettingsCsvFileName       = "Settings.csv";
        const string DefaultPlayersExportFileName     = "Jugadores.csv";
        const string DefaultSubtractionExportFileName = "PrizePoolSubtraction";
        const int    DefaultKioskId = 1;

        // ── Getters ────────────────────────────────────────────────────────────

        public static string GetImportFolderPath()
        {
            var value = PlayerPrefs.GetString(KeyImportFolderPath, "");
            return string.IsNullOrWhiteSpace(value) ? Application.dataPath : value;
        }

        public static string GetExportFolderPath()
        {
            var value = PlayerPrefs.GetString(KeyExportFolderPath, "");
            return string.IsNullOrWhiteSpace(value) ? Application.dataPath : value;
        }

        public static string GetPrizesCsvFileName() =>
            PlayerPrefs.GetString(KeyPrizesCsvFileName, DefaultPrizesCsvFileName);

        public static string GetSettingsCsvFileName() =>
            PlayerPrefs.GetString(KeySettingsCsvFileName, DefaultSettingsCsvFileName);

        public static string GetPlayersExportFileName() =>
            PlayerPrefs.GetString(KeyPlayersExportFileName, DefaultPlayersExportFileName);

        public static string GetSubtractionExportFileName() =>
            PlayerPrefs.GetString(KeySubtractionExportFileName, DefaultSubtractionExportFileName);

        public static int GetKioskId() =>
            PlayerPrefs.GetInt(KeyKioskId, DefaultKioskId);

        // ── Setters ────────────────────────────────────────────────────────────

        public static void SetImportFolderPath(string value) =>
            PlayerPrefs.SetString(KeyImportFolderPath, value ?? "");

        public static void SetExportFolderPath(string value) =>
            PlayerPrefs.SetString(KeyExportFolderPath, value ?? "");

        public static void SetPrizesCsvFileName(string value) =>
            PlayerPrefs.SetString(KeyPrizesCsvFileName,
                string.IsNullOrWhiteSpace(value) ? DefaultPrizesCsvFileName : value);

        public static void SetSettingsCsvFileName(string value) =>
            PlayerPrefs.SetString(KeySettingsCsvFileName,
                string.IsNullOrWhiteSpace(value) ? DefaultSettingsCsvFileName : value);

        public static void SetPlayersExportFileName(string value) =>
            PlayerPrefs.SetString(KeyPlayersExportFileName,
                string.IsNullOrWhiteSpace(value) ? DefaultPlayersExportFileName : value);

        public static void SetSubtractionExportFileName(string value) =>
            PlayerPrefs.SetString(KeySubtractionExportFileName,
                string.IsNullOrWhiteSpace(value) ? DefaultSubtractionExportFileName : value);

        public static void SetKioskId(int value) =>
            PlayerPrefs.SetInt(KeyKioskId, Mathf.Max(1, value));

        public static void Save() => PlayerPrefs.Save();
    }
}
