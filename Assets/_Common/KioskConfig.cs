using UnityEngine;

namespace GETravelGames.Common
{
    public static class KioskConfig
    {
        const string KeyImportFolderPath = "KioskConfig_ImportFolderPath";
        const string KeyExportFolderPath = "KioskConfig_ExportFolderPath";
        const string KeyPrizesCsvFileName = "KioskConfig_PrizesCsvFileName";
        const string KeySettingsCsvFileName = "KioskConfig_SettingsCsvFileName";
        const string KeyWonPrizesExportFileName = "KioskConfig_WonPrizesExportFileName";
        const string KeyKioskId = "KioskConfig_KioskId";

        // ── Defaults ───────────────────────────────────────────────────────────

        const string DefaultPrizesCsvFileName = "Prizes.csv";
        const string DefaultSettingsCsvFileName = "Settings.csv";
        const string DefaultWonPrizesExportFileName = "WonPrizes.csv";
        const int DefaultKioskId = 1;

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

        public static string GetWonPrizesExportFileName() =>
            PlayerPrefs.GetString(KeyWonPrizesExportFileName, DefaultWonPrizesExportFileName);

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

        public static void SetWonPrizesExportFileName(string value) =>
            PlayerPrefs.SetString(KeyWonPrizesExportFileName,
                string.IsNullOrWhiteSpace(value) ? DefaultWonPrizesExportFileName : value);

        public static void SetKioskId(int value) =>
            PlayerPrefs.SetInt(KeyKioskId, Mathf.Max(1, value));

        public static void Save() => PlayerPrefs.Save();
    }
}
