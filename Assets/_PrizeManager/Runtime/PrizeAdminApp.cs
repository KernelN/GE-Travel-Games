using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GETravelGames.PrizeManager
{
    [DisallowMultipleComponent]
    public sealed class PrizeAdminApp : MonoBehaviour
    {
        private PrizeAdminService adminService;
        private PrizeManagerBootstrapState state;
        private GameObject canvasRoot;
        private TMP_InputField prizeCsvPathField;
        private TMP_InputField settingsCsvPathField;
        private TMP_InputField exportCsvPathField;
        private TMP_InputField previewOutputField;
        private TextMeshProUGUI summaryText;
        private bool isInitialized;

        public void Initialize(PrizeAdminService resolvedAdminService, PrizeManagerBootstrapState bootstrapState)
        {
            adminService = resolvedAdminService;
            state = bootstrapState ?? new PrizeManagerBootstrapState();
            isInitialized = true;
        }

        private void Start()
        {
            if (!isInitialized)
            {
                var bootstrap = GetComponent<PrizeManagerBootstrap>();
                if (bootstrap != null)
                {
                    Initialize(bootstrap.AdminService, bootstrap.State);
                }
            }

            if (isInitialized && canvasRoot == null)
            {
                BuildUi();
                RefreshAllText();
            }
        }

        private void BuildUi()
        {
            EnsureEventSystem();

            canvasRoot = new GameObject("PrizeManagerAdminCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasRoot.transform.SetParent(transform, false);

            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var root = CreateUiObject("Root", canvasRoot.transform);
            StretchToParent(root);
            var rootImage = root.gameObject.AddComponent<Image>();
            rootImage.color = new Color32(19, 26, 34, 255);

            var rootLayout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(24, 24, 24, 24);
            rootLayout.spacing = 16f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandHeight = true;
            rootLayout.childForceExpandWidth = true;

            var title = CreateText(root, "Title", 34f, FontStyles.Bold, TextAlignmentOptions.Left);
            title.text = "Prize Manager Admin";
            title.color = Color.white;

            var body = CreateUiObject("Body", root);
            var bodyLayoutElement = body.gameObject.AddComponent<LayoutElement>();
            bodyLayoutElement.flexibleHeight = 1f;

            var bodyLayout = body.gameObject.AddComponent<HorizontalLayoutGroup>();
            bodyLayout.spacing = 16f;
            bodyLayout.childControlHeight = true;
            bodyLayout.childControlWidth = true;
            bodyLayout.childForceExpandHeight = true;
            bodyLayout.childForceExpandWidth = false;

            var controls = CreatePanel(body, "ControlsPanel", new Color32(28, 37, 48, 255));
            var controlsLayoutElement = controls.gameObject.AddComponent<LayoutElement>();
            controlsLayoutElement.preferredWidth = 480f;
            controlsLayoutElement.flexibleWidth = 0f;

            var preview = CreatePanel(body, "PreviewPanel", new Color32(28, 37, 48, 255));
            preview.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            BuildControlsColumn(controls);
            BuildPreviewColumn(preview);
        }

        private void BuildControlsColumn(RectTransform parent)
        {
            CreateSectionTitle(parent, "File Paths");
            prizeCsvPathField = CreatePathField(parent, "Prizes CSV", state.prizeCsvPath, value => state.prizeCsvPath = value);
            settingsCsvPathField = CreatePathField(parent, "Settings CSV", state.settingsCsvPath, value => state.settingsCsvPath = value);
            exportCsvPathField = CreatePathField(parent, "Won Prizes Export", state.wonPrizesExportPath, value => state.wonPrizesExportPath = value);

            CreateSectionTitle(parent, "Prize Imports");
            CreateButton(parent, "Preview Initialize Prizes", () => PreviewPrizeImport(PrizeImportMode.Initialize));
            CreateButton(parent, "Apply Initialize Prizes", () => ApplyPrizeImport(PrizeImportMode.Initialize));
            CreateButton(parent, "Preview Add Prizes", () => PreviewPrizeImport(PrizeImportMode.Add));
            CreateButton(parent, "Apply Add Prizes", () => ApplyPrizeImport(PrizeImportMode.Add));

            CreateSectionTitle(parent, "Settings");
            CreateButton(parent, "Preview Settings", PreviewSettingsImport);
            CreateButton(parent, "Apply Settings", ApplySettingsImport);

            CreateSectionTitle(parent, "Exports");
            CreateButton(parent, "Export Won Prizes", ExportWonPrizes);

            CreateSectionTitle(parent, "Kiosk Debug");
            CreateButton(parent, "Debug Claim Prize", DebugClaimPrize);
            CreateButton(parent, "Debug Cancel Claim", DebugCancelClaim);
            CreateButton(parent, "Debug Confirm Claim", DebugConfirmClaim);

            CreateSectionTitle(parent, "Store Summary");
            summaryText = CreateText(parent, "Summary", 20f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            summaryText.textWrappingMode = TextWrappingModes.Normal;
            summaryText.color = new Color32(215, 224, 235, 255);
        }

        private void BuildPreviewColumn(RectTransform parent)
        {
            CreateSectionTitle(parent, "Preview Output");
            previewOutputField = CreateInputField(
                parent,
                state.previewText,
                "Operation output will appear here.",
                _ => { },
                true,
                true,
                720f);
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(transform, false);
        }

        private void PreviewPrizeImport(PrizeImportMode importMode)
        {
            var preview = adminService.PreviewPrizeImport(prizeCsvPathField.text, importMode);
            state.previewText = FormatPrizePreview(preview);
            state.statusText = preview.IsValid
                ? $"{importMode} preview is ready."
                : $"{importMode} preview has validation errors.";
            RefreshAllText();
        }

        private void ApplyPrizeImport(PrizeImportMode importMode)
        {
            var result = adminService.ApplyPrizeImport(prizeCsvPathField.text, importMode);
            state.statusText = result.Summary;
            state.previewText = result.PrizePreview != null
                ? FormatPrizePreview(result.PrizePreview)
                : FormatOperationIssues(result);
            RefreshAllText();
        }

        private void PreviewSettingsImport()
        {
            var preview = adminService.PreviewSettingsImport(settingsCsvPathField.text);
            state.previewText = FormatSettingsPreview(preview);
            state.statusText = preview.IsValid
                ? "Settings preview is ready."
                : "Settings preview has validation errors.";
            RefreshAllText();
        }

        private void ApplySettingsImport()
        {
            var result = adminService.ApplySettingsImport(settingsCsvPathField.text);
            state.statusText = result.Summary;
            state.previewText = result.SettingsPreview != null
                ? FormatSettingsPreview(result.SettingsPreview)
                : FormatOperationIssues(result);
            RefreshAllText();
        }

        private void ExportWonPrizes()
        {
            var result = adminService.ExportWonPrizes(exportCsvPathField.text);
            state.statusText = result.Summary;
            state.previewText = FormatOperationIssues(result);
            RefreshAllText();
        }

        private void DebugClaimPrize()
        {
            var result = adminService.DebugClaimPrize();
            state.statusText = result.Summary;
            state.previewText = FormatOperationIssues(result);
            RefreshAllText();
        }

        private void DebugCancelClaim()
        {
            var result = adminService.DebugCancelClaim();
            state.statusText = result.Summary;
            state.previewText = FormatOperationIssues(result);
            RefreshAllText();
        }

        private void DebugConfirmClaim()
        {
            var result = adminService.DebugConfirmClaim();
            state.statusText = result.Summary;
            state.previewText = FormatOperationIssues(result);
            RefreshAllText();
        }

        private void RefreshAllText()
        {
            if (summaryText == null || previewOutputField == null)
            {
                return;
            }

            summaryText.text = BuildSummary();
            previewOutputField.SetTextWithoutNotify(BuildPreviewOutput());
        }

        private string BuildSummary()
        {
            var activeSettings = adminService.StateStore.ActiveSettings;
            var builder = new StringBuilder();
            builder.AppendLine(state.statusText);
            builder.AppendLine();
            builder.AppendLine($"Templates: {adminService.StateStore.Templates.Count}");
            builder.AppendLine($"Available prizes: {adminService.StateStore.AvailablePrizeInstances.Count}");
            builder.AppendLine($"Won history: {adminService.StateStore.WonPrizeHistory.Count}");
            builder.AppendLine($"Active reservation: {FormatReservationSummary(adminService.StateStore.ActiveReservation)}");

            if (string.IsNullOrWhiteSpace(activeSettings.Timezone))
            {
                builder.Append("Settings: not imported yet");
            }
            else
            {
                builder.Append($"Settings: {activeSettings.Timezone}, max/day {activeSettings.MaxPrizesPerDay}, reservation {activeSettings.PrizeReservationTimeoutMinutes} min");
            }

            return builder.ToString();
        }

        private string BuildPreviewOutput()
        {
            var builder = new StringBuilder();
            builder.AppendLine(state.previewText);
            builder.AppendLine();
            builder.AppendLine("Current Paths");
            builder.AppendLine($"Prizes: {state.prizeCsvPath}");
            builder.AppendLine($"Settings: {state.settingsCsvPath}");
            builder.AppendLine($"Export: {state.wonPrizesExportPath}");
            return builder.ToString();
        }

        private static string FormatPrizePreview(PrizeCsvImportPreview preview)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Mode: {preview.ImportMode}");
            builder.AppendLine($"Delimiter: {preview.Delimiter}");
            builder.AppendLine($"Templates: {preview.Templates.Count}");
            builder.AppendLine($"Available prize instances: {preview.Instances.Count}");

            foreach (var template in preview.Templates)
            {
                var instanceCount = preview.Instances.FindAll(instance => instance.PrizeCategoryId == template.PrizeCategoryId).Count;
                builder.AppendLine($"- Category {template.PrizeCategoryId}: {template.PrizeName} ({instanceCount} instances, {FormatSchedule(template.Schedule)})");
            }

            AppendIssues(builder, preview.Issues);
            return builder.ToString();
        }

        private static string FormatSettingsPreview(SettingsCsvPreview preview)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Delimiter: {preview.Delimiter}");
            builder.AppendLine($"Timezone: {preview.Settings.Timezone}");
            builder.AppendLine($"Reservation timeout: {preview.Settings.PrizeReservationTimeoutMinutes}");
            builder.AppendLine($"Max prizes per day: {preview.Settings.MaxPrizesPerDay}");
            builder.AppendLine($"Base false-prize chance: {preview.Settings.FalsePrizeChancePercent}%");
            builder.AppendLine($"Base forced-hour chance: {preview.Settings.ForcedHourChancePercent}%");
            builder.AppendLine($"False-prize steps: {preview.Settings.FalsePrizeThresholds.Count}");
            builder.AppendLine($"Forced-hour steps: {preview.Settings.ForcedHourThresholds.Count}");

            AppendIssues(builder, preview.Issues);
            return builder.ToString();
        }

        private static string FormatOperationIssues(PrizeAdminOperationResult result)
        {
            var builder = new StringBuilder();
            builder.AppendLine(result.Summary);
            builder.AppendLine();
            builder.AppendLine($"Active reservation: {FormatReservationSummary(result.ActiveReservation)}");
            AppendIssues(builder, result.Issues);
            return builder.ToString();
        }

        private static string FormatSchedule(PrizeSchedule schedule)
        {
            if (schedule == null || !schedule.PrizeStartMinutesOfDay.HasValue || !schedule.PrizeEndMinutesOfDay.HasValue)
            {
                return "no hour window";
            }

            return $"{FormatMinutesOfDay(schedule.PrizeStartMinutesOfDay.Value)}-{FormatMinutesOfDay(schedule.PrizeEndMinutesOfDay.Value)}";
        }

        private static string FormatReservationSummary(PrizeClaimReservation reservation)
        {
            if (reservation?.ReservedPrize == null || string.IsNullOrWhiteSpace(reservation.ReservedPrize.PrizeInstanceId))
            {
                return "none";
            }

            return $"{reservation.ReservedPrize.PrizeInstanceId} ({reservation.ReservedPrize.PrizeName}) for {reservation.WinnerName}";
        }

        private static string FormatMinutesOfDay(int minutesOfDay)
        {
            var hours = minutesOfDay / 60;
            var minutes = minutesOfDay % 60;
            return $"{hours:D2}:{minutes:D2}";
        }

        private static void AppendIssues(StringBuilder builder, System.Collections.Generic.IReadOnlyList<CsvValidationIssue> issues)
        {
            builder.AppendLine();
            if (issues == null || issues.Count == 0)
            {
                builder.Append("No validation issues.");
                return;
            }

            builder.AppendLine("Issues");
            foreach (var issue in issues)
            {
                var rowPrefix = issue.RowNumber > 0 ? $"row {issue.RowNumber}" : "file";
                builder.AppendLine($"- {rowPrefix}, {issue.ColumnName}: {issue.Message}");
            }
        }

        private static RectTransform CreatePanel(Transform parent, string name, Color backgroundColor)
        {
            var panel = CreateUiObject(name, parent);
            var image = panel.gameObject.AddComponent<Image>();
            image.color = backgroundColor;

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 18, 18);
            layout.spacing = 10f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            return panel;
        }

        private static TextMeshProUGUI CreateSectionTitle(Transform parent, string text)
        {
            var label = CreateText(parent, $"{text}Title", 24f, FontStyles.Bold, TextAlignmentOptions.Left);
            label.text = text;
            label.color = Color.white;
            return label;
        }

        private static TMP_InputField CreatePathField(Transform parent, string labelText, string initialValue, Action<string> onValueChanged)
        {
            CreateText(parent, $"{labelText}Label", 18f, FontStyles.Normal, TextAlignmentOptions.Left).text = labelText;
            return CreateInputField(parent, initialValue, "Enter a local file path", onValueChanged, false, false, 54f);
        }

        private static void CreateButton(Transform parent, string label, Action onClick)
        {
            var buttonRoot = CreateUiObject(label, parent);
            buttonRoot.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;

            var buttonImage = buttonRoot.gameObject.AddComponent<Image>();
            buttonImage.color = new Color32(61, 99, 140, 255);

            var button = buttonRoot.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.onClick.AddListener(() => onClick?.Invoke());

            var text = CreateText(buttonRoot, "Label", 18f, FontStyles.Bold, TextAlignmentOptions.Center);
            StretchToParent(text.rectTransform, 10f, 10f, 8f, 8f);
            text.text = label;
            text.color = Color.white;
        }

        private static TMP_InputField CreateInputField(
            Transform parent,
            string initialValue,
            string placeholderText,
            Action<string> onValueChanged,
            bool multiline,
            bool readOnly,
            float preferredHeight)
        {
            var root = CreateUiObject("InputField", parent);
            var layoutElement = root.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;

            var background = root.gameObject.AddComponent<Image>();
            background.color = new Color32(15, 20, 27, 255);

            var inputField = root.gameObject.AddComponent<TMP_InputField>();
            inputField.readOnly = readOnly;
            inputField.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
            inputField.text = initialValue ?? string.Empty;

            var viewport = CreateUiObject("Viewport", root);
            StretchToParent(viewport, 12f, 12f, 10f, 10f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var text = CreateText(viewport, "Text", multiline ? 18f : 20f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            StretchToParent(text.rectTransform);
            text.text = initialValue ?? string.Empty;
            text.color = new Color32(231, 238, 244, 255);
            text.textWrappingMode = TextWrappingModes.Normal;

            var placeholder = CreateText(viewport, "Placeholder", multiline ? 18f : 20f, FontStyles.Italic, TextAlignmentOptions.TopLeft);
            StretchToParent(placeholder.rectTransform);
            placeholder.text = placeholderText;
            placeholder.color = new Color32(124, 139, 153, 255);
            placeholder.textWrappingMode = TextWrappingModes.Normal;

            inputField.textViewport = viewport;
            inputField.textComponent = text;
            inputField.placeholder = placeholder;
            inputField.onValueChanged.AddListener(value => onValueChanged?.Invoke(value));
            inputField.SetTextWithoutNotify(initialValue ?? string.Empty);

            return inputField;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
        {
            var textRoot = CreateUiObject(name, parent);
            var text = textRoot.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset != null
                ? TMP_Settings.defaultFontAsset
                : Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.color = Color.white;
            return text;
        }

        private static RectTransform CreateUiObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<RectTransform>();
        }

        private static void StretchToParent(RectTransform rectTransform, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(left, bottom);
            rectTransform.offsetMax = new Vector2(-right, -top);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }
    }
}
