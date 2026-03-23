using System.IO;
using GETravelGames.PrizeManager;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GETravelGames.Common
{
    public sealed class ConfigManager : MonoBehaviour
    {
        [Header("Input Fields")]
        [SerializeField] TMP_InputField importFolderInput;
        [SerializeField] TMP_InputField exportFolderInput;
        [SerializeField] TMP_InputField prizesCsvInput;
        [SerializeField] TMP_InputField settingsCsvInput;
        [SerializeField] TMP_InputField wonPrizesInput;
        [SerializeField] TMP_InputField kioskIdInput;

        [Header("Buttons")]
        [SerializeField] Button saveButton;
        [SerializeField] Button verifyButton;
        [SerializeField] Button backButton;

        [Header("Status")]
        [SerializeField] TMP_Text statusLabel;

        [Header("Textos")]
        [SerializeField] string savedText = "Configuraci\u00f3n guardada";
        [SerializeField] string verifyOkFormat = "Carga verificada: {0} premios en {1} categor\u00edas";
        [SerializeField] string verifyFailFormat = "Error al verificar: {0}";

        void Start()
        {
            Time.timeScale = 1f;
            LoadFromPlayerPrefs();

            saveButton?.onClick.AddListener(OnSave);
            verifyButton?.onClick.AddListener(OnVerify);
            backButton?.onClick.AddListener(() => SceneManager.LoadScene("MainMenu"));
        }

        void LoadFromPlayerPrefs()
        {
            if (importFolderInput != null)
                importFolderInput.text = KioskConfig.GetImportFolderPath();
            if (exportFolderInput != null)
                exportFolderInput.text = KioskConfig.GetExportFolderPath();
            if (prizesCsvInput != null)
                prizesCsvInput.text = KioskConfig.GetPrizesCsvFileName();
            if (settingsCsvInput != null)
                settingsCsvInput.text = KioskConfig.GetSettingsCsvFileName();
            if (wonPrizesInput != null)
                wonPrizesInput.text = KioskConfig.GetWonPrizesExportFileName();
            if (kioskIdInput != null)
                kioskIdInput.text = KioskConfig.GetKioskId().ToString();

            SetStatus("");
        }

        void OnSave()
        {
            KioskConfig.SetImportFolderPath(importFolderInput?.text ?? "");
            KioskConfig.SetExportFolderPath(exportFolderInput?.text ?? "");
            KioskConfig.SetPrizesCsvFileName(prizesCsvInput?.text ?? "");
            KioskConfig.SetSettingsCsvFileName(settingsCsvInput?.text ?? "");
            KioskConfig.SetWonPrizesExportFileName(wonPrizesInput?.text ?? "");

            if (int.TryParse(kioskIdInput?.text, out var id))
                KioskConfig.SetKioskId(id);

            KioskConfig.Save();
            SetStatus(savedText);
        }

        void OnVerify()
        {
            var importFolder = importFolderInput?.text ?? "";
            if (string.IsNullOrWhiteSpace(importFolder))
                importFolder = Application.dataPath;

            var prizesFile = prizesCsvInput?.text ?? "Prizes.csv";
            var settingsFile = settingsCsvInput?.text ?? "Settings.csv";

            var csvService = new PrizeCsvService();
            var stateStore = new PrizeAdminStateStore();
            var adminService = new PrizeAdminService(csvService, stateStore);

            // Try importing settings.
            var settingsPath = Path.Combine(importFolder, settingsFile);
            if (File.Exists(settingsPath))
            {
                var settingsResult = adminService.ApplySettingsImport(settingsPath);
                if (!settingsResult.Success)
                {
                    SetStatus(string.Format(verifyFailFormat, settingsResult.Summary));
                    return;
                }
            }
            else
            {
                SetStatus(string.Format(verifyFailFormat,
                    $"No se encontr\u00f3 {settingsPath}"));
                return;
            }

            // Try importing prizes.
            var prizesPath = Path.Combine(importFolder, prizesFile);
            if (File.Exists(prizesPath))
            {
                var prizesResult = adminService.ApplyPrizeImport(prizesPath,
                    PrizeImportMode.Initialize);
                if (!prizesResult.Success)
                {
                    SetStatus(string.Format(verifyFailFormat, prizesResult.Summary));
                    return;
                }

                SetStatus(string.Format(verifyOkFormat,
                    prizesResult.AvailablePrizeCount,
                    prizesResult.TemplateCount));
            }
            else
            {
                SetStatus(string.Format(verifyFailFormat,
                    $"No se encontr\u00f3 {prizesPath}"));
            }
        }

        void SetStatus(string message)
        {
            if (statusLabel == null) return;
            statusLabel.text = message;
            statusLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
        }

#if UNITY_EDITOR
        [ContextMenu("Construir UI")]
        void BuildUi()
        {
            var existing = GetComponentInChildren<Canvas>(true);
            if (existing != null)
                UnityEditor.Undo.DestroyObjectImmediate(existing.gameObject);

            UnityEditor.Undo.RecordObject(this, "Construir UI Config");

            UIBuilderHelper.EnsureEventSystem();

            var canvas = UIBuilderHelper.MakeCanvas(transform, "ConfigCanvas");
            canvas.gameObject.AddComponent<Image>().color = UIBuilderHelper.ColBg;

            var panel = UIBuilderHelper.MakeView(canvas.transform, "Panel");
            UIBuilderHelper.AddVerticalLayout(panel, spacing: 10f,
                padding: new RectOffset(60, 60, 30, 30));

            // Title
            var title = UIBuilderHelper.MakeText(panel.transform, "Title",
                36, TMPro.FontStyles.Bold, UIBuilderHelper.ColTextPrimary);
            title.text = "Configuraci\u00f3n";
            UIBuilderHelper.AddLayout(title.gameObject, 50);

            // Fields
            importFolderInput = MakeLabeledField(panel.transform, "ImportFolder",
                "Carpeta de importaci\u00f3n", "Ruta de la carpeta");
            exportFolderInput = MakeLabeledField(panel.transform, "ExportFolder",
                "Carpeta de exportaci\u00f3n", "Ruta de la carpeta");
            prizesCsvInput = MakeLabeledField(panel.transform, "PrizesCsv",
                "Archivo de premios", "Prizes.csv");
            settingsCsvInput = MakeLabeledField(panel.transform, "SettingsCsv",
                "Archivo de configuraci\u00f3n", "Settings.csv");
            wonPrizesInput = MakeLabeledField(panel.transform, "WonPrizes",
                "Archivo de premios ganados", "WonPrizes.csv");
            kioskIdInput = MakeLabeledField(panel.transform, "KioskId",
                "ID del kiosco", "1");
            kioskIdInput.contentType = TMP_InputField.ContentType.IntegerNumber;

            // Status
            statusLabel = UIBuilderHelper.MakeText(panel.transform, "StatusLabel",
                18, TMPro.FontStyles.Normal, UIBuilderHelper.ColTextSecondary);
            statusLabel.text = "";
            UIBuilderHelper.AddLayout(statusLabel.gameObject, 28);

            // Button row
            var btnRow = new GameObject("ButtonRow", typeof(RectTransform));
            btnRow.transform.SetParent(panel.transform, false);
            var hlg = btnRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = false;
            UIBuilderHelper.AddLayout(btnRow, 44);

            backButton = UIBuilderHelper.MakeButton(btnRow.transform, "BackButton",
                "Volver", UIBuilderHelper.ColBtnSmall, UIBuilderHelper.ColTextSecondary,
                18, TMPro.FontStyles.Normal);

            saveButton = UIBuilderHelper.MakeButton(btnRow.transform, "SaveButton",
                "Guardar", UIBuilderHelper.ColBtn, UIBuilderHelper.ColTextPrimary,
                20, TMPro.FontStyles.Bold);

            verifyButton = UIBuilderHelper.MakeButton(btnRow.transform, "VerifyButton",
                "Verificar Carga", UIBuilderHelper.ColBtnSecondary, UIBuilderHelper.ColTextPrimary,
                20, TMPro.FontStyles.Bold);

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[ConfigManager] UI construida. Guard\u00e1 la escena.");
        }

        TMP_InputField MakeLabeledField(Transform parent, string name,
            string labelText, string placeholder)
        {
            var row = new GameObject(name + "Row", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var vlg = row.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            UIBuilderHelper.AddLayout(row, 58);

            var label = UIBuilderHelper.MakeText(row.transform, name + "Label",
                14, TMPro.FontStyles.Normal, UIBuilderHelper.ColTextMuted,
                TextAlignmentOptions.MidlineLeft);
            label.text = labelText;
            UIBuilderHelper.AddLayout(label.gameObject, 18);

            var field = UIBuilderHelper.MakeInputField(row.transform, name + "Input",
                placeholder);
            UIBuilderHelper.AddLayout(field.gameObject, 36);

            return field;
        }
#endif
    }
}
