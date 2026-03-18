using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GETravelGames.PrizeManager
{
    /// <summary>
    /// Five-panel admin UI:
    ///   Controls (scrollable) | Store Summary | Settings Preview | Kiosk Prizes | Preview Output
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PrizeAdminApp : MonoBehaviour
    {
        // ── Service / state ───────────────────────────────────────────────────
        private PrizeAdminService adminService;
        private PrizeManagerBootstrapState state;
        private bool isInitialized;

        // ── Root ──────────────────────────────────────────────────────────────
        private GameObject canvasRoot;

        // ── Controls panel fields ─────────────────────────────────────────────
        private TMP_InputField importFolderPathField;
        private TMP_InputField prizesCsvFileNameField;
        private TMP_InputField settingsCsvFileNameField;
        private TMP_InputField exportFolderPathField;
        private TMP_InputField wonPrizesFileNameField;
        private TMP_InputField subtractionFileNameField;
        private TMP_InputField debugKioskIdField;
        private TextMeshProUGUI statusLabel;

        // ── Panel content references ──────────────────────────────────────────
        private TMP_InputField storeSummaryField;
        private TMP_InputField settingsPreviewField;
        private TextMeshProUGUI kioskPanelHeader;   // "Kiosk N — X available"
        private TextMeshProUGUI selectedPrizeLabel; // "Selected: id (name)" or "none"
        private RectTransform kioskPrizesContent;   // scroll content — rebuilt dynamically
        private TMP_InputField previewOutputField;

        // ── Colours ───────────────────────────────────────────────────────────
        private static readonly Color32 ColPanelBg      = new(28,  37,  48,  255);
        private static readonly Color32 ColRootBg       = new(19,  26,  34,  255);
        private static readonly Color32 ColInputBg      = new(15,  20,  27,  255);
        private static readonly Color32 ColBtnNormal    = new(61,  99,  140, 255);
        private static readonly Color32 ColBtnDanger    = new(130, 50,  50,  255);
        private static readonly Color32 ColRowNormal    = new(22,  32,  44,  255);
        private static readonly Color32 ColRowSelected  = new(30,  70,  45,  255);
        private static readonly Color32 ColSelectBtn    = new(45,  80,  110, 255);
        private static readonly Color32 ColSelectBtnOn  = new(55,  120, 65,  255);
        private static readonly Color32 ColTextPrimary  = new(231, 238, 244, 255);
        private static readonly Color32 ColTextSecondary= new(160, 175, 190, 255);
        private static readonly Color32 ColTextPlaceholder = new(100, 115, 130, 255);

        // ── Panel widths ──────────────────────────────────────────────────────
        private const float ControlsWidth   = 380f;
        private const float SummaryWidth    = 250f;
        private const float SettingsWidth   = 240f;
        private const float KioskWidth      = 280f;
        // Preview output is flexible.

        // ═════════════════════════════════════════════════════════════════════
        //  Initialisation
        // ═════════════════════════════════════════════════════════════════════

        public void Initialize(PrizeAdminService resolvedService, PrizeManagerBootstrapState bootstrapState)
        {
            adminService = resolvedService;
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
                RefreshAllPanels();
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  UI construction – root
        // ═════════════════════════════════════════════════════════════════════

        private void BuildUi()
        {
            EnsureEventSystem();

            // Canvas
            canvasRoot = new GameObject(
                "PrizeManagerAdminCanvas",
                typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasRoot.transform.SetParent(transform, false);

            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Root panel
            var root = MakeRect("Root", canvasRoot.transform);
            StretchFill(root);
            root.gameObject.AddComponent<Image>().color = ColRootBg;

            var rootV = root.gameObject.AddComponent<VerticalLayoutGroup>();
            rootV.padding = new RectOffset(20, 20, 16, 16);
            rootV.spacing = 10f;
            rootV.childControlWidth = rootV.childControlHeight = true;
            rootV.childForceExpandWidth = rootV.childForceExpandHeight = true;

            // Title row
            var titleRow = MakeRect("TitleRow", root);
            titleRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
            var titleRowH = titleRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            titleRowH.childControlWidth = titleRowH.childControlHeight = true;
            titleRowH.childForceExpandWidth = false;
            titleRowH.childForceExpandHeight = true;
            titleRowH.spacing = 16f;

            var titleText = MakeText(titleRow, "Title", 30f, FontStyles.Bold, TextAlignmentOptions.Left);
            titleText.text = "Prize Manager Admin";
            titleText.color = Color.white;
            titleText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            statusLabel = MakeText(titleRow, "Status", 18f, FontStyles.Normal, TextAlignmentOptions.Right);
            statusLabel.text = state.statusText;
            statusLabel.color = new Color32(180, 200, 220, 255);
            statusLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 700f;

            // Body
            var body = MakeRect("Body", root);
            var bodyH = body.gameObject.AddComponent<HorizontalLayoutGroup>();
            bodyH.spacing = 12f;
            bodyH.childControlWidth = bodyH.childControlHeight = true;
            bodyH.childForceExpandWidth = false;
            bodyH.childForceExpandHeight = true;

            BuildControlsPanel(body);
            BuildStoreSummaryPanel(body);
            BuildSettingsPreviewPanel(body);
            BuildKioskPrizesPanel(body);
            BuildPreviewOutputPanel(body);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Panel builders
        // ═════════════════════════════════════════════════════════════════════

        // ── Controls ─────────────────────────────────────────────────────────

        private void BuildControlsPanel(Transform parent)
        {
            var panel = MakePanel(parent, "Controls", ControlsWidth, 0f);
            var panelV = panel.gameObject.GetComponent<VerticalLayoutGroup>();
            panelV.spacing = 6f;

            MakePanelTitle(panel, "Controls");

            // Scroll view fills the rest of the panel.
            var scrollContent = MakeScrollView(panel, out _);
            var scrollLE = scrollContent.parent.parent.GetComponent<LayoutElement>() // ScrollView root
                           ?? scrollContent.parent.parent.gameObject.AddComponent<LayoutElement>();
            scrollLE.flexibleHeight = 1f;

            BuildControlsContent(scrollContent);
        }

        private void BuildControlsContent(Transform p)
        {
            // ── Import paths ─────────────────────────────────────────────
            MakeSectionTitle(p, "Import");
            importFolderPathField = MakeLabeledField(p, "Folder",
                state.importFolderPath, "Path to folder containing CSV files",
                v => state.importFolderPath = v);
            prizesCsvFileNameField = MakeLabeledField(p, "Prizes CSV",
                state.prizesCsvFileName, "e.g. Prizes.csv",
                v => state.prizesCsvFileName = v);
            settingsCsvFileNameField = MakeLabeledField(p, "Settings CSV",
                state.settingsCsvFileName, "e.g. Settings.csv",
                v => state.settingsCsvFileName = v);

            // ── Export paths ─────────────────────────────────────────────
            MakeSectionTitle(p, "Export");
            exportFolderPathField = MakeLabeledField(p, "Folder",
                state.exportFolderPath, "Path to export folder",
                v => state.exportFolderPath = v);
            wonPrizesFileNameField = MakeLabeledField(p, "Won Prizes",
                state.wonPrizesExportFileName, "e.g. WonPrizes.csv",
                v => state.wonPrizesExportFileName = v);
            subtractionFileNameField = MakeLabeledField(p, "Subtraction",
                state.prizePoolSubtractionExportFileName, "e.g. PrizePoolSubtraction.csv",
                v => state.prizePoolSubtractionExportFileName = v);

            // ── Prize imports ─────────────────────────────────────────────
            MakeSectionTitle(p, "Prize Pool");
            MakeButtonRow(p, "Preview Initialize", OnPreviewInitialize,
                             "Apply Initialize",  OnApplyInitialize);
            MakeButtonRow(p, "Preview Add",        OnPreviewAdd,
                             "Apply Add",          OnApplyAdd);

            // ── Settings ──────────────────────────────────────────────────
            MakeSectionTitle(p, "Settings");
            MakeButtonRow(p, "Preview Settings", OnPreviewSettings,
                             "Apply Settings",   OnApplySettings);

            // ── End-of-day exports ────────────────────────────────────────
            MakeSectionTitle(p, "End-of-Day Exports");
            MakeButton(p, "Export Won Prizes",             OnExportWonPrizes,             ColBtnNormal);
            MakeButton(p, "Export Prize Pool Subtraction", OnExportPrizePoolSubtraction,  ColBtnNormal);

            // ── Debug ─────────────────────────────────────────────────────
            MakeSectionTitle(p, "Debug");
            debugKioskIdField = MakeLabeledField(p, "Kiosk ID",
                state.debugKioskId.ToString(), "1",
                v =>
                {
                    if (int.TryParse(v, out var id) && id > 0)
                    {
                        state.debugKioskId = id;
                        RefreshKioskPrizesPanel();
                    }
                });

            MakeButtonRow(p, "Claim Prize",   OnDebugClaimPrize,
                             "Cancel Claim",  OnDebugCancelClaim);
            MakeButton(p, "Confirm Claim", OnDebugConfirmClaim, new Color32(55, 110, 65, 255));
        }

        // ── Store Summary ────────────────────────────────────────────────────

        private void BuildStoreSummaryPanel(Transform parent)
        {
            var panel = MakePanel(parent, "StoreSummary", SummaryWidth, 0f);
            MakePanelTitle(panel, "Store Summary");
            storeSummaryField = MakeReadonlyTextField(panel, string.Empty, "Store state will appear here.");
            storeSummaryField.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
        }

        // ── Settings Preview ─────────────────────────────────────────────────

        private void BuildSettingsPreviewPanel(Transform parent)
        {
            var panel = MakePanel(parent, "SettingsPreview", SettingsWidth, 0f);
            MakePanelTitle(panel, "Settings Preview");
            settingsPreviewField = MakeReadonlyTextField(panel,
                state.settingsPreviewText,
                "Import or preview settings to see them here.");
            settingsPreviewField.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
        }

        // ── Kiosk Prizes ─────────────────────────────────────────────────────

        private void BuildKioskPrizesPanel(Transform parent)
        {
            var panel = MakePanel(parent, "KioskPrizes", KioskWidth, 0f);
            var panelV = panel.gameObject.GetComponent<VerticalLayoutGroup>();
            panelV.spacing = 4f;

            // Header: title + selected-prize label
            kioskPanelHeader = MakePanelTitle(panel, $"Kiosk {state.debugKioskId} — 0 available");
            kioskPanelHeader.fontSize = 18f;

            selectedPrizeLabel = MakeText(panel, "SelectedPrize", 14f,
                FontStyles.Italic, TextAlignmentOptions.Left);
            selectedPrizeLabel.text = FormatSelectedPrize();
            selectedPrizeLabel.color = ColTextSecondary;
            selectedPrizeLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 18f;

            // Scroll view for the prize list
            kioskPrizesContent = MakeScrollView(panel, out _);
            var scrollLE = kioskPrizesContent.parent.parent.gameObject.AddComponent<LayoutElement>();
            scrollLE.flexibleHeight = 1f;
        }

        // ── Preview Output ────────────────────────────────────────────────────

        private void BuildPreviewOutputPanel(Transform parent)
        {
            var panel = MakePanel(parent, "PreviewOutput", 0f, 1f);
            MakePanelTitle(panel, "Preview Output");
            previewOutputField = MakeReadonlyTextField(panel, state.previewText, "Operation output will appear here.");
            previewOutputField.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Button handlers
        // ═════════════════════════════════════════════════════════════════════

        private void OnPreviewInitialize()
        {
            var preview = adminService.PreviewPrizeImport(state.PrizesCsvPath, PrizeImportMode.Initialize);
            state.previewText = FormatPrizePreview(preview);
            SetStatus(preview.IsValid ? "Initialize preview ready." : "Initialize preview has errors.");
            RefreshAllPanels();
        }

        private void OnApplyInitialize()
        {
            var result = adminService.ApplyPrizeImport(state.PrizesCsvPath, PrizeImportMode.Initialize);
            state.previewText = result.PrizePreview != null
                ? FormatPrizePreview(result.PrizePreview)
                : FormatIssues(result);
            SetStatus(result.Summary);
            RefreshAllPanels();
        }

        private void OnPreviewAdd()
        {
            var preview = adminService.PreviewPrizeImport(state.PrizesCsvPath, PrizeImportMode.Add);
            state.previewText = FormatPrizePreview(preview);
            SetStatus(preview.IsValid ? "Add preview ready." : "Add preview has errors.");
            RefreshAllPanels();
        }

        private void OnApplyAdd()
        {
            var result = adminService.ApplyPrizeImport(state.PrizesCsvPath, PrizeImportMode.Add);
            state.previewText = result.PrizePreview != null
                ? FormatPrizePreview(result.PrizePreview)
                : FormatIssues(result);
            SetStatus(result.Summary);
            RefreshAllPanels();
        }

        private void OnPreviewSettings()
        {
            var preview = adminService.PreviewSettingsImport(state.SettingsCsvPath);
            state.settingsPreviewText = FormatSettingsPreview(preview);
            SetStatus(preview.IsValid ? "Settings preview ready." : "Settings preview has errors.");
            state.previewText = FormatValidationIssues(preview.Issues);
            RefreshAllPanels();
        }

        private void OnApplySettings()
        {
            var result = adminService.ApplySettingsImport(state.SettingsCsvPath);
            if (result.SettingsPreview != null)
            {
                state.settingsPreviewText = FormatSettingsPreview(result.SettingsPreview);
            }

            state.previewText = FormatIssues(result);
            SetStatus(result.Summary);
            RefreshAllPanels();
        }

        private void OnExportWonPrizes()
        {
            var result = adminService.ExportWonPrizes(state.WonPrizesExportPath);
            state.previewText = FormatIssues(result);
            SetStatus(result.Summary);
            RefreshAllPanels();
        }

        private void OnExportPrizePoolSubtraction()
        {
            var result = adminService.ExportPrizePoolSubtraction(state.SubtractionExportPath);
            state.previewText = FormatIssues(result);
            SetStatus(result.Summary);
            RefreshAllPanels();
        }

        private void OnDebugClaimPrize()
        {
            PrizeAdminOperationResult result;

            if (!string.IsNullOrWhiteSpace(state.debugPrizeInstanceId))
            {
                result = adminService.DebugClaimSpecificPrize(state.debugKioskId, state.debugPrizeInstanceId);
            }
            else
            {
                result = adminService.DebugClaimPrizeForKiosk(state.debugKioskId);
            }

            if (result.Success)
            {
                state.debugPrizeInstanceId = string.Empty;
            }

            state.previewText = FormatIssues(result);
            SetStatus(result.Summary);
            RefreshAllPanels();
        }

        private void OnDebugCancelClaim()
        {
            var result = adminService.DebugCancelClaim();
            state.previewText = FormatIssues(result);
            SetStatus(result.Summary);
            RefreshAllPanels();
        }

        private void OnDebugConfirmClaim()
        {
            var result = adminService.DebugConfirmClaim();
            state.previewText = FormatIssues(result);
            SetStatus(result.Summary);
            RefreshAllPanels();
        }

        /// <summary>Toggles selection of a prize in the kiosk prizes list.</summary>
        private void OnSelectPrize(string instanceId)
        {
            state.debugPrizeInstanceId = (state.debugPrizeInstanceId == instanceId)
                ? string.Empty
                : instanceId;
            RefreshKioskPrizesPanel();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Refresh
        // ═════════════════════════════════════════════════════════════════════

        private void RefreshAllPanels()
        {
            if (!isInitialized || canvasRoot == null) return;

            statusLabel.text = state.statusText;
            RefreshStoreSummaryPanel();
            RefreshSettingsPreviewPanel();
            RefreshKioskPrizesPanel();
            RefreshPreviewOutputPanel();
        }

        private void RefreshStoreSummaryPanel()
        {
            if (storeSummaryField == null) return;
            storeSummaryField.SetTextWithoutNotify(BuildStoreSummary());
        }

        private void RefreshSettingsPreviewPanel()
        {
            if (settingsPreviewField == null) return;
            settingsPreviewField.SetTextWithoutNotify(state.settingsPreviewText);
        }

        private void RefreshPreviewOutputPanel()
        {
            if (previewOutputField == null) return;

            var sb = new StringBuilder();
            sb.AppendLine(state.previewText);
            sb.AppendLine();
            sb.AppendLine("── Paths ───────────────────────────────");
            sb.AppendLine($"Prizes:      {state.PrizesCsvPath}");
            sb.AppendLine($"Settings:    {state.SettingsCsvPath}");
            sb.AppendLine($"Won Prizes:  {state.WonPrizesExportPath}");
            sb.AppendLine($"Subtraction: {state.SubtractionExportPath}");
            previewOutputField.SetTextWithoutNotify(sb.ToString());
        }

        private void RefreshKioskPrizesPanel()
        {
            if (kioskPrizesContent == null) return;

            var kioskId = state.debugKioskId;
            var prizes = adminService.StateStore.GetKioskPrizes(kioskId);

            // Update header
            kioskPanelHeader.text = $"Kiosk {kioskId} — {prizes.Count} available";

            // Update selected label
            selectedPrizeLabel.text = FormatSelectedPrize();

            // Rebuild prize rows
            for (var i = kioskPrizesContent.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(kioskPrizesContent.GetChild(i).gameObject);
            }

            if (prizes.Count == 0)
            {
                var empty = MakeText(kioskPrizesContent, "Empty", 15f,
                    FontStyles.Italic, TextAlignmentOptions.Center);
                empty.text = "No prizes assigned to this kiosk.";
                empty.color = ColTextSecondary;
                empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
                return;
            }

            foreach (var prize in prizes)
            {
                BuildPrizeRow(kioskPrizesContent, prize);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Kiosk prize row
        // ═════════════════════════════════════════════════════════════════════

        private void BuildPrizeRow(RectTransform parent, PrizeInstance prize)
        {
            var isSelected = prize.PrizeInstanceId == state.debugPrizeInstanceId;

            var row = MakeRect($"Row_{prize.PrizeInstanceId}", parent);
            row.gameObject.AddComponent<Image>().color = isSelected ? ColRowSelected : ColRowNormal;

            var rowH = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowH.padding = new RectOffset(8, 6, 4, 4);
            rowH.spacing = 6f;
            rowH.childControlWidth = rowH.childControlHeight = true;
            rowH.childForceExpandHeight = true;
            rowH.childForceExpandWidth = false;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;

            // Info column (flex)
            var info = MakeRect("Info", row);
            info.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var infoV = info.gameObject.AddComponent<VerticalLayoutGroup>();
            infoV.childControlWidth = infoV.childControlHeight = true;
            infoV.childForceExpandWidth = true;
            infoV.childForceExpandHeight = false;
            infoV.spacing = 1f;

            var idText = MakeText(info, "Id", 14f, FontStyles.Bold, TextAlignmentOptions.Left);
            idText.text = prize.PrizeInstanceId;
            idText.color = isSelected ? Color.white : ColTextPrimary;

            var nameText = MakeText(info, "Name", 12f, FontStyles.Normal, TextAlignmentOptions.Left);
            nameText.text = prize.PrizeName;
            nameText.color = ColTextSecondary;

            // Select button
            var btnRoot = MakeRect("SelectBtn", row);
            btnRoot.gameObject.AddComponent<LayoutElement>().preferredWidth = 68f;

            var btnImg = btnRoot.gameObject.AddComponent<Image>();
            btnImg.color = isSelected ? ColSelectBtnOn : ColSelectBtn;

            var btn = btnRoot.gameObject.AddComponent<Button>();
            btn.targetGraphic = btnImg;

            var capturedId = prize.PrizeInstanceId;
            btn.onClick.AddListener(() => OnSelectPrize(capturedId));

            var btnLabel = MakeText(btnRoot, "Label", 13f, FontStyles.Bold, TextAlignmentOptions.Center);
            StretchFill(btnLabel.rectTransform, 4f, 4f, 4f, 4f);
            btnLabel.text = isSelected ? "✓" : "Select";
            btnLabel.color = Color.white;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Text format helpers
        // ═════════════════════════════════════════════════════════════════════

        private string BuildStoreSummary()
        {
            var store = adminService.StateStore;
            var settings = store.ActiveSettings;
            var sb = new StringBuilder();

            sb.AppendLine($"Templates:    {store.Templates.Count}");
            sb.AppendLine($"Available:    {store.AvailablePrizeInstances.Count} total");

            var counts = store.KioskPrizeCounts;
            if (counts.Count > 0)
            {
                var sorted = new SortedDictionary<int, int>(counts);
                foreach (var kvp in sorted)
                {
                    sb.AppendLine($"  Kiosk {kvp.Key}: {kvp.Value}");
                }
            }

            sb.AppendLine($"Won history:  {store.WonPrizeHistory.Count}");
            sb.AppendLine();

            var res = store.ActiveReservation;
            if (res?.ReservedPrize != null && !string.IsNullOrWhiteSpace(res.ReservedPrize.PrizeInstanceId))
            {
                sb.AppendLine($"Reservation:");
                sb.AppendLine($"  {res.ReservedPrize.PrizeInstanceId}");
                sb.AppendLine($"  {res.ReservedPrize.PrizeName}");
                sb.AppendLine($"  Kiosk {res.KioskId}");
            }
            else
            {
                sb.AppendLine("Reservation: none");
            }

            sb.AppendLine();

            if (string.IsNullOrWhiteSpace(settings.Timezone))
            {
                sb.Append("Settings: not imported");
            }
            else
            {
                sb.AppendLine($"Timezone: {settings.Timezone}");
                sb.AppendLine($"Kiosks:   {settings.KioskCount}");
                sb.AppendLine($"Max/day:  {settings.MaxPrizesPerDay}");
                sb.Append(    $"Timeout:  {settings.PrizeReservationTimeoutMinutes} min");
            }

            return sb.ToString();
        }

        private static string FormatPrizePreview(PrizeCsvImportPreview preview)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Mode: {preview.ImportMode}  |  Delimiter: '{preview.Delimiter}'");
            sb.AppendLine($"Templates: {preview.Templates.Count}  |  Instances: {preview.Instances.Count}");
            sb.AppendLine();

            foreach (var t in preview.Templates)
            {
                var count = preview.Instances.FindAll(i => i.PrizeCategoryId == t.PrizeCategoryId).Count;
                sb.AppendLine($"[{t.PrizeCategoryId}] {t.PrizeName} — {count}× — {FormatSchedule(t.Schedule)}");
            }

            AppendValidationIssues(sb, preview.Issues);
            return sb.ToString();
        }

        private static string FormatSettingsPreview(SettingsCsvPreview preview)
        {
            var s = preview.Settings;
            var sb = new StringBuilder();
            sb.AppendLine($"Delimiter:   '{preview.Delimiter}'");
            sb.AppendLine($"Timezone:    {s.Timezone}");
            sb.AppendLine($"Kiosks:      {s.KioskCount}");
            sb.AppendLine($"Timeout:     {s.PrizeReservationTimeoutMinutes} min");
            sb.AppendLine($"Max/day:     {s.MaxPrizesPerDay}");
            sb.AppendLine($"False:       {s.FalsePrizeChancePercent}% base");
            sb.AppendLine($"Forced:      {s.ForcedHourChancePercent}% base");

            if (s.FalsePrizeThresholds.Count > 0)
            {
                sb.AppendLine("False steps:");
                foreach (var t in s.FalsePrizeThresholds)
                {
                    sb.AppendLine($"  ≥{t.ThresholdPercent}% → {t.ChancePercent}%");
                }
            }

            if (s.ForcedHourThresholds.Count > 0)
            {
                sb.AppendLine("Forced steps:");
                foreach (var t in s.ForcedHourThresholds)
                {
                    sb.AppendLine($"  ≥{t.ThresholdPercent}% → {t.ChancePercent}%");
                }
            }

            AppendValidationIssues(sb, preview.Issues);
            return sb.ToString();
        }

        private static string FormatIssues(PrizeAdminOperationResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine(result.Summary);
            AppendValidationIssues(sb, result.Issues);
            return sb.ToString();
        }

        private static string FormatValidationIssues(IReadOnlyList<CsvValidationIssue> issues)
        {
            var sb = new StringBuilder();
            AppendValidationIssues(sb, issues);
            return sb.ToString();
        }

        private static void AppendValidationIssues(StringBuilder sb, IReadOnlyList<CsvValidationIssue> issues)
        {
            if (issues == null || issues.Count == 0) return;
            sb.AppendLine();
            sb.AppendLine($"── {issues.Count} issue(s) ──");
            foreach (var issue in issues)
            {
                var row = issue.RowNumber > 0 ? $"row {issue.RowNumber}" : "file";
                sb.AppendLine($"  {row}, {issue.ColumnName}: {issue.Message}");
            }
        }

        private string FormatSelectedPrize()
        {
            if (string.IsNullOrWhiteSpace(state.debugPrizeInstanceId))
            {
                return "Selected: none — will claim first available";
            }

            // Try to look up the prize name.
            var prizes = adminService?.StateStore.GetKioskPrizes(state.debugKioskId);
            if (prizes != null)
            {
                foreach (var p in prizes)
                {
                    if (p.PrizeInstanceId == state.debugPrizeInstanceId)
                    {
                        return $"Selected: {p.PrizeInstanceId} ({p.PrizeName})";
                    }
                }
            }

            return $"Selected: {state.debugPrizeInstanceId} (not in kiosk pool)";
        }

        private static string FormatSchedule(PrizeSchedule s)
        {
            if (s == null || !s.PrizeStartMinutesOfDay.HasValue || !s.PrizeEndMinutesOfDay.HasValue)
            {
                return "any time";
            }

            return $"{Mints(s.PrizeStartMinutesOfDay.Value)}-{Mints(s.PrizeEndMinutesOfDay.Value)}";
        }

        private static string Mints(int m) => $"{m / 60:D2}:{m % 60:D2}";

        private void SetStatus(string text)
        {
            state.statusText = text;
            if (statusLabel != null) statusLabel.text = text;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  UI factory helpers
        // ═════════════════════════════════════════════════════════════════════

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        /// <summary>
        /// Creates a panel with a VerticalLayoutGroup.
        /// Pass preferredWidth > 0 for fixed width, flexibleWidth > 0 for flex.
        /// </summary>
        private static RectTransform MakePanel(Transform parent, string name,
            float preferredWidth, float flexibleWidth)
        {
            var panel = MakeRect(name, parent);
            panel.gameObject.AddComponent<Image>().color = ColPanelBg;

            var le = panel.gameObject.AddComponent<LayoutElement>();
            if (preferredWidth > 0f) { le.preferredWidth = preferredWidth; le.flexibleWidth = 0f; }
            if (flexibleWidth > 0f)  { le.flexibleWidth = flexibleWidth; }

            var vl = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(14, 14, 14, 14);
            vl.spacing = 8f;
            vl.childControlWidth = vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;

            return panel;
        }

        private static TextMeshProUGUI MakePanelTitle(Transform parent, string text)
        {
            var t = MakeText(parent, $"{text}Title", 20f, FontStyles.Bold, TextAlignmentOptions.Left);
            t.text = text;
            t.color = Color.white;
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 26f;
            return t;
        }

        private static void MakeSectionTitle(Transform parent, string text)
        {
            var t = MakeText(parent, $"{text}Section", 14f, FontStyles.Bold, TextAlignmentOptions.Left);
            t.text = text.ToUpperInvariant();
            t.color = new Color32(120, 150, 180, 255);
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 18f;
        }

        /// <summary>
        /// Creates a label + single-line input field pair.
        /// </summary>
        private static TMP_InputField MakeLabeledField(
            Transform parent, string label, string initialValue,
            string placeholder, Action<string> onChanged)
        {
            var container = MakeRect($"{label}Container", parent);
            var vl = container.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 2f;
            vl.childControlWidth = vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;

            var lbl = MakeText(container, $"{label}Label", 13f, FontStyles.Normal, TextAlignmentOptions.Left);
            lbl.text = label;
            lbl.color = ColTextSecondary;
            lbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 16f;

            return MakeInputField(container, initialValue, placeholder, onChanged, false, false, 34f);
        }

        private static void MakeButton(Transform parent, string label, Action onClick, Color32 color)
        {
            var root = MakeRect(label, parent);
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;

            var img = root.gameObject.AddComponent<Image>();
            img.color = color;

            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick?.Invoke());

            var text = MakeText(root, "Label", 15f, FontStyles.Bold, TextAlignmentOptions.Center);
            StretchFill(text.rectTransform, 6f, 6f, 4f, 4f);
            text.text = label;
            text.color = Color.white;
        }

        private static void MakeButtonRow(Transform parent,
            string labelA, Action onClickA,
            string labelB, Action onClickB)
        {
            var row = MakeRect($"{labelA}Row", parent);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;

            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 6f;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = true;

            MakeButton(row, labelA, onClickA, ColBtnNormal);
            MakeButton(row, labelB, onClickB, new Color32(45, 75, 110, 255));
        }

        /// <summary>
        /// Creates a readonly, multiline, scrollable TMP_InputField that fills its parent.
        /// </summary>
        private static TMP_InputField MakeReadonlyTextField(
            Transform parent, string initialValue, string placeholder)
        {
            return MakeInputField(parent, initialValue, placeholder, _ => { }, true, true, 0f);
        }

        private static TMP_InputField MakeInputField(
            Transform parent, string initialValue, string placeholder,
            Action<string> onChanged, bool multiline, bool readOnly, float preferredHeight)
        {
            var root = MakeRect("InputField", parent);
            if (preferredHeight > 0f)
            {
                root.gameObject.AddComponent<LayoutElement>().preferredHeight = preferredHeight;
            }

            root.gameObject.AddComponent<Image>().color = ColInputBg;

            var field = root.gameObject.AddComponent<TMP_InputField>();
            field.readOnly = readOnly;
            field.lineType = multiline
                ? TMP_InputField.LineType.MultiLineNewline
                : TMP_InputField.LineType.SingleLine;

            var viewport = MakeRect("Viewport", root);
            StretchFill(viewport, 8f, 8f, 6f, 6f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var fontSize = multiline ? 14f : 15f;

            var textComp = MakeText(viewport, "Text", fontSize, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            StretchFill(textComp.rectTransform);
            textComp.color = ColTextPrimary;
            textComp.textWrappingMode = TextWrappingModes.Normal;
            textComp.text = initialValue ?? string.Empty;

            var phComp = MakeText(viewport, "Placeholder", fontSize, FontStyles.Italic, TextAlignmentOptions.TopLeft);
            StretchFill(phComp.rectTransform);
            phComp.color = ColTextPlaceholder;
            phComp.textWrappingMode = TextWrappingModes.Normal;
            phComp.text = placeholder;

            field.textViewport = viewport;
            field.textComponent = textComp;
            field.placeholder = phComp;
            field.onValueChanged.AddListener(v => onChanged?.Invoke(v));
            field.SetTextWithoutNotify(initialValue ?? string.Empty);

            return field;
        }

        /// <summary>
        /// Creates a ScrollRect.  Returns the Content RectTransform (VerticalLayoutGroup + ContentSizeFitter).
        /// The ScrollView root is a direct child of <paramref name="parent"/> with no LayoutElement set —
        /// callers should add their own LayoutElement to the returned content's grandparent.
        /// </summary>
        private static RectTransform MakeScrollView(Transform parent, out ScrollRect scrollRect)
        {
            var scrollRoot = MakeRect("ScrollView", parent);

            scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 24f;

            var viewport = MakeRect("Viewport", scrollRoot);
            StretchFill(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            scrollRect.viewport = viewport;

            var content = MakeRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot    = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;

            var cl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            cl.padding = new RectOffset(6, 6, 4, 8);
            cl.spacing = 4f;
            cl.childControlWidth = cl.childControlHeight = true;
            cl.childForceExpandWidth = true;
            cl.childForceExpandHeight = false;

            var csf = content.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = content;
            return content;
        }

        private static TextMeshProUGUI MakeText(Transform parent, string name,
            float fontSize, FontStyles style, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = TMP_Settings.defaultFontAsset != null
                ? TMP_Settings.defaultFontAsset
                : Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = align;
            t.raycastTarget = false;
            t.color = Color.white;
            return t;
        }

        private static RectTransform MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void StretchFill(RectTransform rt,
            float l = 0f, float r = 0f, float t = 0f, float b = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
        }
    }
}
