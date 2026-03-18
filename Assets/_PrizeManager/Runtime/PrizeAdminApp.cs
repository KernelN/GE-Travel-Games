using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GETravelGames.PrizeManager
{
    /// <summary>
    /// Five-panel admin UI.
    ///
    /// Two modes of operation:
    ///  1. Runtime-only: canvasRoot is null → BuildUi() constructs the hierarchy,
    ///     WireCallbacks() adds runtime listeners.  Everything happens in Start().
    ///  2. Pre-linked (preferred): run "Link Canvas References" context menu once
    ///     after a first Play-mode build.  Persistent Button listeners are written
    ///     into the scene so Start() only needs to sync field text and refresh panels.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PrizeAdminApp : MonoBehaviour
    {
        // ── Service / state ───────────────────────────────────────────────────
        private PrizeAdminService adminService;
        private PrizeManagerBootstrapState state;
        private bool isInitialized;

        // ─────────────────────────────────────────────────────────────────────
        //  Serialised UI references
        // ─────────────────────────────────────────────────────────────────────

        [Header("Canvas Root")]
        [SerializeField] private GameObject canvasRoot;

        [Header("Title Row")]
        [SerializeField] private TextMeshProUGUI statusLabel;

        [Header("Controls – Path Fields")]
        [SerializeField] private TMP_InputField importFolderPathField;
        [SerializeField] private TMP_InputField prizesCsvFileNameField;
        [SerializeField] private TMP_InputField settingsCsvFileNameField;
        [SerializeField] private TMP_InputField exportFolderPathField;
        [SerializeField] private TMP_InputField wonPrizesFileNameField;
        [SerializeField] private TMP_InputField subtractionFileNameField;

        [Header("Controls – Buttons")]
        [SerializeField] private Button btnPreviewInitialize;
        [SerializeField] private Button btnApplyInitialize;
        [SerializeField] private Button btnPreviewAdd;
        [SerializeField] private Button btnApplyAdd;
        [SerializeField] private Button btnPreviewSettings;
        [SerializeField] private Button btnApplySettings;
        [SerializeField] private Button btnExportWonPrizes;
        [SerializeField] private Button btnExportSubtraction;
        [SerializeField] private Button btnClaimPrize;
        [SerializeField] private Button btnForceClaim;
        [SerializeField] private Button btnCancelClaim;
        [SerializeField] private Button btnConfirmClaim;

        [Header("Controls – Kiosk Spinner")]
        [SerializeField] private Button kioskDecrBtn;
        [SerializeField] private TextMeshProUGUI kioskSpinnerLabel;
        [SerializeField] private Button kioskIncrBtn;

        [Header("Store Summary")]
        [SerializeField] private TMP_InputField storeSummaryField;

        [Header("Settings Preview")]
        [SerializeField] private TMP_InputField settingsPreviewField;

        [Header("Kiosk Prizes")]
        [SerializeField] private TextMeshProUGUI kioskPanelTitle;
        [SerializeField] private TextMeshProUGUI kioskSelectedLabel;
        [SerializeField] private RectTransform kioskCategoryContent;

        [Header("Preview Output")]
        [SerializeField] private TMP_InputField previewOutputField;

        // ── Colours ───────────────────────────────────────────────────────────
        private static readonly Color32 ColPanelBg        = new(28,  37,  48,  255);
        private static readonly Color32 ColRootBg         = new(19,  26,  34,  255);
        private static readonly Color32 ColInputBg        = new(15,  20,  27,  255);
        private static readonly Color32 ColBtn            = new(61,  99,  140, 255);
        private static readonly Color32 ColBtnSecondary   = new(45,  72,  105, 255);
        private static readonly Color32 ColBtnDanger      = new(130, 48,  48,  255);
        private static readonly Color32 ColBtnForce       = new(145, 90,  30,  255);
        private static readonly Color32 ColBtnConfirm     = new(45,  110, 60,  255);
        private static readonly Color32 ColSpinnerBtn     = new(50,  70,  95,  255);
        private static readonly Color32 ColCardNormal     = new(22,  32,  44,  255);
        private static readonly Color32 ColCardSelected   = new(28,  68,  42,  255);
        private static readonly Color32 ColCardIneligible = new(17,  22,  30,  255);
        private static readonly Color32 ColSelectBtnOff   = new(44,  66,  92,  255);
        private static readonly Color32 ColSelectBtnOn    = new(42,  105, 58,  255);
        private static readonly Color32 ColTextPrimary    = new(231, 238, 244, 255);
        private static readonly Color32 ColTextSecondary  = new(155, 172, 190, 255);
        private static readonly Color32 ColTextMuted      = new(100, 115, 130, 255);
        private static readonly Color32 ColTextPlaceholder= new(80,  95,  110, 255);
        private static readonly Color32 ColEligible       = new(90,  185, 100, 255);
        private static readonly Color32 ColIneligible     = new(185, 110, 55,  255);

        private static readonly string[] DayAbbr = { "", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

        private const float ControlsWidth = 370f;
        private const float SummaryWidth  = 240f;
        private const float SettingsWidth = 230f;
        private const float KioskWidth    = 310f;

        // ═════════════════════════════════════════════════════════════════════
        //  Initialisation
        // ═════════════════════════════════════════════════════════════════════

        public void Initialize(PrizeAdminService resolvedService, PrizeManagerBootstrapState bootstrapState)
        {
            adminService  = resolvedService;
            state         = bootstrapState ?? new PrizeManagerBootstrapState();
            isInitialized = true;
        }

        private void Start()
        {
            // Fallback: if Bootstrap's Awake didn't already call Initialize, try it now.
            if (!isInitialized)
            {
                var bootstrap = GetComponent<PrizeManagerBootstrap>();
                if (bootstrap != null) Initialize(bootstrap.AdminService, bootstrap.State);
            }

            if (!isInitialized)
            {
                Debug.LogError("[PrizeAdminApp] Not initialized. Ensure PrizeManagerBootstrap is present.", this);
                return;
            }

            // Build hierarchy only when canvas was not pre-linked.
            if (canvasRoot == null) BuildUi();

            // Always add runtime listeners for input fields (not wired as persistent)
            // and safety-fallback runtime listeners for any buttons not yet wired.
            WireRuntimeListeners();

            SyncInputFieldTextsFromState();
            RefreshAllPanels();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Public input field change handlers
        //  (public so they appear in the Inspector and can be wired persistently)
        // ═════════════════════════════════════════════════════════════════════

        public void OnImportFolderPathChanged(string value)  { state.importFolderPath                  = value; }
        public void OnPrizesCsvFileNameChanged(string value) { state.prizesCsvFileName                 = value; }
        public void OnSettingsCsvFileNameChanged(string value){ state.settingsCsvFileName              = value; }
        public void OnExportFolderPathChanged(string value)  { state.exportFolderPath                  = value; }
        public void OnWonPrizesFileNameChanged(string value) { state.wonPrizesExportFileName            = value; }
        public void OnSubtractionFileNameChanged(string value){ state.prizePoolSubtractionExportFileName= value; }

        // ═════════════════════════════════════════════════════════════════════
        //  Public button handlers
        //  (public so they appear in the Inspector and can be wired persistently)
        // ═════════════════════════════════════════════════════════════════════

        public void OnPreviewInitialize()
        {
            if (!CheckReady()) return;
            var preview = adminService.PreviewPrizeImport(state.PrizesCsvPath, PrizeImportMode.Initialize);
            state.previewText = FormatPrizePreview(preview);
            SetStatus(preview.IsValid ? "Initialize preview ready." : "Initialize preview has errors.");
            RefreshAllPanels();
        }

        public void OnApplyInitialize()
        {
            if (!CheckReady()) return;
            var result = adminService.ApplyPrizeImport(state.PrizesCsvPath, PrizeImportMode.Initialize);
            state.previewText = result.PrizePreview != null ? FormatPrizePreview(result.PrizePreview) : FormatIssues(result);
            SetStatus(result.Summary);
            RefreshAllPanels();
        }

        public void OnPreviewAdd()
        {
            if (!CheckReady()) return;
            var preview = adminService.PreviewPrizeImport(state.PrizesCsvPath, PrizeImportMode.Add);
            state.previewText = FormatPrizePreview(preview);
            SetStatus(preview.IsValid ? "Add preview ready." : "Add preview has errors.");
            RefreshAllPanels();
        }

        public void OnApplyAdd()
        {
            if (!CheckReady()) return;
            var result = adminService.ApplyPrizeImport(state.PrizesCsvPath, PrizeImportMode.Add);
            state.previewText = result.PrizePreview != null ? FormatPrizePreview(result.PrizePreview) : FormatIssues(result);
            SetStatus(result.Summary);
            RefreshAllPanels();
        }

        public void OnPreviewSettings()
        {
            if (!CheckReady()) return;
            var preview = adminService.PreviewSettingsImport(state.SettingsCsvPath);
            state.settingsPreviewText = FormatSettingsPreview(preview);
            state.previewText = preview.Issues.Count > 0
                ? FormatValidationIssueList(preview.Issues)
                : "Settings preview — no validation issues.";
            SetStatus(preview.IsValid ? "Settings preview ready." : "Settings preview has errors.");
            RefreshAllPanels();
        }

        public void OnApplySettings()
        {
            if (!CheckReady()) return;
            var result = adminService.ApplySettingsImport(state.SettingsCsvPath);
            if (result.SettingsPreview != null)
                state.settingsPreviewText = FormatSettingsPreview(result.SettingsPreview);
            state.previewText = FormatIssues(result);
            SetStatus(result.Summary);
            RefreshAllPanels();
        }

        public void OnExportWonPrizes()
        {
            if (!CheckReady()) return;
            var result = adminService.ExportWonPrizes(state.WonPrizesExportPath);
            state.previewText = FormatIssues(result);
            SetStatus(result.Summary);
            RefreshAllPanels();
        }

        public void OnExportPrizePoolSubtraction()
        {
            if (!CheckReady()) return;
            var result = adminService.ExportPrizePoolSubtraction(state.SubtractionExportPath);
            state.previewText = FormatIssues(result);
            SetStatus(result.Summary);
            RefreshAllPanels();
        }

        /// <summary>Claim Prize button — respects schedule restrictions.</summary>
        public void OnDebugClaimNormal()
        {
            if (!CheckReady()) return;
            var result = adminService.DebugClaimFromKiosk(state.debugKioskId, state.debugPrizeCategoryId, false);
            if (result.Success) state.debugPrizeCategoryId = 0;
            state.previewText = FormatIssues(result);
            SetStatus(result.Summary);
            RefreshAllPanels();
        }

        /// <summary>Force Claim button — bypasses schedule restrictions.</summary>
        public void OnDebugClaimForce()
        {
            if (!CheckReady()) return;
            var result = adminService.DebugClaimFromKiosk(state.debugKioskId, state.debugPrizeCategoryId, true);
            if (result.Success) state.debugPrizeCategoryId = 0;
            state.previewText = FormatIssues(result);
            SetStatus(result.Summary);
            RefreshAllPanels();
        }

        public void OnDebugCancelClaim()
        {
            if (!CheckReady()) return;
            var result = adminService.DebugCancelClaim();
            state.previewText = FormatIssues(result);
            SetStatus(result.Summary);
            RefreshAllPanels();
        }

        public void OnDebugConfirmClaim()
        {
            if (!CheckReady()) return;
            var result = adminService.DebugConfirmClaim();
            state.previewText = FormatIssues(result);
            SetStatus(result.Summary);
            RefreshAllPanels();
        }

        public void OnKioskDecrement()
        {
            if (!CheckReady() || state.debugKioskId <= 1) return;
            state.debugKioskId--;
            state.debugPrizeCategoryId = 0;
            RefreshKioskSpinner();
            RefreshKioskPrizesPanel();
        }

        public void OnKioskIncrement()
        {
            if (!CheckReady()) return;
            var maxKiosk = Mathf.Max(1, adminService.StateStore.ActiveSettings.KioskCount);
            if (state.debugKioskId >= maxKiosk) return;
            state.debugKioskId++;
            state.debugPrizeCategoryId = 0;
            RefreshKioskSpinner();
            RefreshKioskPrizesPanel();
        }

        /// <summary>Called by dynamically-built category card Select buttons.</summary>
        public void OnSelectCategory(ushort categoryId)
        {
            state.debugPrizeCategoryId = (state.debugPrizeCategoryId == categoryId) ? (ushort)0 : categoryId;
            RefreshKioskPrizesPanel();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Runtime listener wiring
        //  Wires input fields (always runtime) and provides fallback wiring for
        //  any buttons that don't yet have persistent listeners.
        // ═════════════════════════════════════════════════════════════════════

        public void WireRuntimeListeners()
        {
            // Input field onValueChanged — always runtime (string events are
            // awkward to persist; the public methods are the authoritative target).
            BindField(importFolderPathField,    OnImportFolderPathChanged);
            BindField(prizesCsvFileNameField,   OnPrizesCsvFileNameChanged);
            BindField(settingsCsvFileNameField, OnSettingsCsvFileNameChanged);
            BindField(exportFolderPathField,    OnExportFolderPathChanged);
            BindField(wonPrizesFileNameField,   OnWonPrizesFileNameChanged);
            BindField(subtractionFileNameField, OnSubtractionFileNameChanged);

            // Buttons: only add a runtime listener when the button has no
            // persistent listeners at all (i.e. not yet wired via the editor).
            BindButton(btnPreviewInitialize, OnPreviewInitialize);
            BindButton(btnApplyInitialize,   OnApplyInitialize);
            BindButton(btnPreviewAdd,        OnPreviewAdd);
            BindButton(btnApplyAdd,          OnApplyAdd);
            BindButton(btnPreviewSettings,   OnPreviewSettings);
            BindButton(btnApplySettings,     OnApplySettings);
            BindButton(btnExportWonPrizes,   OnExportWonPrizes);
            BindButton(btnExportSubtraction, OnExportPrizePoolSubtraction);
            BindButton(btnClaimPrize,        OnDebugClaimNormal);
            BindButton(btnForceClaim,        OnDebugClaimForce);
            BindButton(btnCancelClaim,       OnDebugCancelClaim);
            BindButton(btnConfirmClaim,      OnDebugConfirmClaim);
            BindButton(kioskDecrBtn,         OnKioskDecrement);
            BindButton(kioskIncrBtn,         OnKioskIncrement);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Public refresh methods
        // ═════════════════════════════════════════════════════════════════════

        public void RefreshAllPanels()
        {
            if (!isInitialized || canvasRoot == null) return;

            var maxKiosk = Mathf.Max(1, adminService.StateStore.ActiveSettings.KioskCount);
            state.debugKioskId = Mathf.Clamp(state.debugKioskId, 1, maxKiosk);

            if (statusLabel != null) statusLabel.text = state.statusText;
            RefreshKioskSpinner();
            RefreshStoreSummaryPanel();
            RefreshSettingsPreviewPanel();
            RefreshKioskPrizesPanel();
            RefreshPreviewOutputPanel();
        }

        public void RefreshKioskSpinner()
        {
            if (kioskSpinnerLabel == null) return;
            var maxKiosk = Mathf.Max(1, adminService.StateStore.ActiveSettings.KioskCount);
            kioskSpinnerLabel.text = state.debugKioskId.ToString();
            if (kioskDecrBtn) kioskDecrBtn.interactable = state.debugKioskId > 1;
            if (kioskIncrBtn) kioskIncrBtn.interactable = state.debugKioskId < maxKiosk;
        }

        public void RefreshStoreSummaryPanel()
        {
            if (storeSummaryField == null) return;
            storeSummaryField.SetTextWithoutNotify(BuildStoreSummaryText());
        }

        public void RefreshSettingsPreviewPanel()
        {
            if (settingsPreviewField == null) return;
            settingsPreviewField.SetTextWithoutNotify(state.settingsPreviewText);
        }

        public void RefreshKioskPrizesPanel()
        {
            if (kioskCategoryContent == null) return;

            var kioskId = state.debugKioskId;
            var prizes  = adminService.StateStore.GetKioskPrizes(kioskId);

            var categories = prizes
                .GroupBy(p => p.PrizeCategoryId)
                .OrderBy(g => g.Key)
                .Select(g => new CategorySummary
                {
                    CategoryId  = g.Key,
                    Name        = g.First().PrizeName,
                    Description = g.First().PrizeDescription,
                    Count       = g.Count(),
                    Schedule    = g.First().Schedule,
                    IsEligible  = PrizeAdminService.IsScheduleEligible(g.First().Schedule),
                    IsSelected  = g.Key == state.debugPrizeCategoryId,
                })
                .ToList();

            if (kioskPanelTitle != null)
                kioskPanelTitle.text =
                    $"Kiosk {kioskId}  ·  {categories.Count} categor{(categories.Count == 1 ? "y" : "ies")}";

            if (kioskSelectedLabel != null)
                kioskSelectedLabel.text = BuildSelectedCategoryText(categories);

            for (var i = kioskCategoryContent.childCount - 1; i >= 0; i--)
                DestroyImmediate(kioskCategoryContent.GetChild(i).gameObject);

            if (categories.Count == 0)
            {
                var empty = MakeText(kioskCategoryContent, "Empty", 14f, FontStyles.Italic, TextAlignmentOptions.Center);
                empty.text = "No prizes assigned to this kiosk.";
                empty.color = ColTextMuted;
                empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
                return;
            }

            foreach (var cat in categories) BuildCategoryCard(kioskCategoryContent, cat);
        }

        public void RefreshPreviewOutputPanel()
        {
            if (previewOutputField == null) return;
            previewOutputField.SetTextWithoutNotify(BuildPreviewOutputText());
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Listener bind helpers
        // ═════════════════════════════════════════════════════════════════════

        private static void BindField(TMP_InputField field, UnityEngine.Events.UnityAction<string> callback)
        {
            if (field == null) return;
            field.onValueChanged.RemoveAllListeners();
            field.onValueChanged.AddListener(callback);
        }

        /// <summary>
        /// Adds a runtime listener only when the button has zero persistent calls.
        /// This avoids double-firing when persistent listeners are already wired.
        /// </summary>
        private static void BindButton(Button btn, UnityEngine.Events.UnityAction callback)
        {
            if (btn == null) return;
            if (btn.onClick.GetPersistentEventCount() == 0)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(callback);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Safety guard
        // ═════════════════════════════════════════════════════════════════════

        private bool CheckReady()
        {
            if (isInitialized && adminService != null) return true;
            Debug.LogWarning("[PrizeAdminApp] Not yet initialized — button press ignored.", this);
            return false;
        }

        private void SyncInputFieldTextsFromState()
        {
            SetField(importFolderPathField,    state.importFolderPath);
            SetField(prizesCsvFileNameField,   state.prizesCsvFileName);
            SetField(settingsCsvFileNameField, state.settingsCsvFileName);
            SetField(exportFolderPathField,    state.exportFolderPath);
            SetField(wonPrizesFileNameField,   state.wonPrizesExportFileName);
            SetField(subtractionFileNameField, state.prizePoolSubtractionExportFileName);
        }

        private static void SetField(TMP_InputField field, string value)
        {
            if (field != null) field.SetTextWithoutNotify(value ?? string.Empty);
        }

        private void SetStatus(string text)
        {
            state.statusText = text;
            if (statusLabel != null) statusLabel.text = text;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  UI construction  (runtime-only path)
        // ═════════════════════════════════════════════════════════════════════

        private void BuildUi()
        {
            EnsureEventSystem();

            canvasRoot = new GameObject(
                "PrizeManagerAdminCanvas",
                typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasRoot.transform.SetParent(transform, false);

            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution  = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight   = 0.5f;

            var root = MakeRect("Root", canvasRoot.transform);
            StretchFill(root);
            root.gameObject.AddComponent<Image>().color = ColRootBg;
            var rootV = root.gameObject.AddComponent<VerticalLayoutGroup>();
            rootV.padding = new RectOffset(18, 18, 14, 14);
            rootV.spacing = 10f;
            rootV.childControlWidth = rootV.childControlHeight = true;
            rootV.childForceExpandWidth = rootV.childForceExpandHeight = true;

            BuildTitleRow(root);

            var body = MakeRect("Body", root);
            var bodyH = body.gameObject.AddComponent<HorizontalLayoutGroup>();
            bodyH.spacing = 10f;
            bodyH.childControlWidth = bodyH.childControlHeight = true;
            bodyH.childForceExpandWidth  = false;
            bodyH.childForceExpandHeight = true;

            BuildControlsPanel(body);
            BuildStoreSummaryPanel(body);
            BuildSettingsPreviewPanel(body);
            BuildKioskPrizesPanel(body);
            BuildPreviewOutputPanel(body);
        }

        private void BuildTitleRow(Transform parent)
        {
            var row = MakeRect("TitleRow", parent);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 16f;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;

            var title = MakeText(row, "Title", 28f, FontStyles.Bold, TextAlignmentOptions.Left);
            title.text = "Prize Manager Admin"; title.color = Color.white;
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            statusLabel = MakeText(row, "StatusLabel", 16f, FontStyles.Normal, TextAlignmentOptions.Right);
            statusLabel.color = new Color32(170, 195, 215, 255);
            statusLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 720f;
        }

        private void BuildControlsPanel(Transform parent)
        {
            var panel = MakePanel(parent, "Controls", ControlsWidth, 0f);
            MakePanelTitle(panel, "Controls");
            var scrollContent = MakeScrollView(panel, "ControlsScroll");
            scrollContent.parent.parent.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            BuildControlsContent(scrollContent);
        }

        private void BuildControlsContent(Transform p)
        {
            MakeSectionTitle(p, "Import");
            importFolderPathField    = MakeLabeledField(p, "Import Folder",  state.importFolderPath,                   "Path to folder",               "ImportFolderPathField");
            prizesCsvFileNameField   = MakeLabeledField(p, "Prizes CSV",     state.prizesCsvFileName,                  "e.g. Prizes.csv",              "PrizesCsvFileNameField");
            settingsCsvFileNameField = MakeLabeledField(p, "Settings CSV",   state.settingsCsvFileName,                "e.g. Settings.csv",            "SettingsCsvFileNameField");

            MakeSectionTitle(p, "Export");
            exportFolderPathField    = MakeLabeledField(p, "Export Folder",  state.exportFolderPath,                   "Path to export folder",        "ExportFolderPathField");
            wonPrizesFileNameField   = MakeLabeledField(p, "Won Prizes",     state.wonPrizesExportFileName,            "e.g. WonPrizes.csv",           "WonPrizesFileNameField");
            subtractionFileNameField = MakeLabeledField(p, "Subtraction",    state.prizePoolSubtractionExportFileName, "e.g. PrizePoolSubtraction.csv","SubtractionFileNameField");

            MakeSectionTitle(p, "Prize Pool");
            (btnPreviewInitialize, btnApplyInitialize) = MakeButtonRow(p, "Preview Initialize", ColBtn,       "Apply Initialize", ColBtnSecondary);
            (btnPreviewAdd,        btnApplyAdd)        = MakeButtonRow(p, "Preview Add",        ColBtn,       "Apply Add",        ColBtnSecondary);

            MakeSectionTitle(p, "Settings");
            (btnPreviewSettings, btnApplySettings) = MakeButtonRow(p, "Preview Settings", ColBtn, "Apply Settings", ColBtnSecondary);

            MakeSectionTitle(p, "End-of-Day Exports");
            (btnExportWonPrizes, btnExportSubtraction) = MakeButtonRow(p, "Export Won Prizes", ColBtn, "Export Subtraction", ColBtn);

            MakeSectionTitle(p, "Debug");
            BuildKioskSpinner(p);
            (btnClaimPrize, btnForceClaim)    = MakeButtonRow(p, "Claim Prize",  ColBtn,       "Force Claim",   ColBtnForce);
            (btnCancelClaim, btnConfirmClaim) = MakeButtonRow(p, "Cancel Claim", ColBtnDanger, "Confirm Claim", ColBtnConfirm);
        }

        private void BuildKioskSpinner(Transform parent)
        {
            var lbl = MakeText(parent, "KioskLabel", 13f, FontStyles.Normal, TextAlignmentOptions.Left);
            lbl.text = "Kiosk"; lbl.color = ColTextSecondary;
            lbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 16f;

            var row = MakeRect("KioskSpinner", parent);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 4f;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;

            kioskDecrBtn = MakeSmallButton(row, "KioskDecrBtn", "◄", 36f);

            var display = MakeRect("KioskDisplay", row);
            display.gameObject.AddComponent<LayoutElement>().preferredWidth = 64f;
            display.gameObject.AddComponent<Image>().color = ColInputBg;
            kioskSpinnerLabel = MakeText(display, "KioskSpinnerLabel", 18f, FontStyles.Bold, TextAlignmentOptions.Center);
            StretchFill(kioskSpinnerLabel.rectTransform);
            kioskSpinnerLabel.color = ColTextPrimary;

            kioskIncrBtn = MakeSmallButton(row, "KioskIncrBtn", "►", 36f);
        }

        private void BuildStoreSummaryPanel(Transform parent)
        {
            var panel = MakePanel(parent, "StoreSummary", SummaryWidth, 0f);
            MakePanelTitle(panel, "Store Summary");
            storeSummaryField = MakeReadonlyTextField(panel, string.Empty, "Store state will appear here.", "StoreSummaryField");
            storeSummaryField.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
        }

        private void BuildSettingsPreviewPanel(Transform parent)
        {
            var panel = MakePanel(parent, "SettingsPreview", SettingsWidth, 0f);
            MakePanelTitle(panel, "Settings");
            settingsPreviewField = MakeReadonlyTextField(panel, state.settingsPreviewText, "Preview or apply settings to see them here.", "SettingsPreviewField");
            settingsPreviewField.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
        }

        private void BuildKioskPrizesPanel(Transform parent)
        {
            var panel = MakePanel(parent, "KioskPrizes", KioskWidth, 0f);
            panel.gameObject.GetComponent<VerticalLayoutGroup>().spacing = 4f;

            kioskPanelTitle = MakeText(panel, "KioskPanelTitle", 19f, FontStyles.Bold, TextAlignmentOptions.Left);
            kioskPanelTitle.color = Color.white;
            kioskPanelTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;

            kioskSelectedLabel = MakeText(panel, "KioskSelectedLabel", 13f, FontStyles.Italic, TextAlignmentOptions.Left);
            kioskSelectedLabel.color = ColTextSecondary;
            kioskSelectedLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 16f;

            kioskCategoryContent = MakeScrollView(panel, "KioskScroll");
            kioskCategoryContent.name = "KioskCategoryContent";
            kioskCategoryContent.parent.parent.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
        }

        private void BuildPreviewOutputPanel(Transform parent)
        {
            var panel = MakePanel(parent, "PreviewOutput", 0f, 1f);
            MakePanelTitle(panel, "Preview Output");
            previewOutputField = MakeReadonlyTextField(panel, state.previewText, "Operation output will appear here.", "PreviewOutputField");
            previewOutputField.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Kiosk category card  (always built at runtime)
        // ═════════════════════════════════════════════════════════════════════

        private struct CategorySummary
        {
            public ushort CategoryId;
            public string Name, Description;
            public int Count;
            public PrizeSchedule Schedule;
            public bool IsEligible, IsSelected;
        }

        private void BuildCategoryCard(RectTransform parent, CategorySummary cat)
        {
            var cardBg = cat.IsSelected ? ColCardSelected
                       : cat.IsEligible ? ColCardNormal
                                        : ColCardIneligible;

            var card = MakeRect($"Cat_{cat.CategoryId}", parent);
            card.gameObject.AddComponent<Image>().color = cardBg;
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = 82f;
            var cardV = card.gameObject.AddComponent<VerticalLayoutGroup>();
            cardV.padding = new RectOffset(8, 8, 6, 6); cardV.spacing = 3f;
            cardV.childControlWidth = cardV.childControlHeight = true;
            cardV.childForceExpandWidth = true; cardV.childForceExpandHeight = false;

            // Row 1: ID badge | name | count badge | select button
            var row1 = MakeRect("Row1", card);
            row1.gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;
            var row1H = row1.gameObject.AddComponent<HorizontalLayoutGroup>();
            row1H.spacing = 5f;
            row1H.childControlWidth = row1H.childControlHeight = true;
            row1H.childForceExpandWidth = false; row1H.childForceExpandHeight = true;

            var idBadge = MakeRect("IdBadge", row1);
            idBadge.gameObject.AddComponent<LayoutElement>().preferredWidth = 36f;
            idBadge.gameObject.AddComponent<Image>().color = new Color32(40, 65, 95, 255);
            var idText = MakeText(idBadge, "Id", 13f, FontStyles.Bold, TextAlignmentOptions.Center);
            StretchFill(idText.rectTransform, 2f, 2f, 2f, 2f);
            idText.text = cat.CategoryId.ToString();
            idText.color = new Color32(180, 200, 220, 255);

            var nameText = MakeText(row1, "Name", 15f, FontStyles.Bold, TextAlignmentOptions.Left);
            nameText.text = cat.Name;
            nameText.color = cat.IsEligible ? ColTextPrimary : ColTextMuted;
            nameText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var countBg = cat.Count == 0 ? new Color32(100, 40, 40, 255)
                        : cat.Count <= 2  ? new Color32(120, 80, 20, 255)
                                          : new Color32(30,  85, 50, 255);
            var countBadge = MakeRect("CountBadge", row1);
            countBadge.gameObject.AddComponent<LayoutElement>().preferredWidth = 32f;
            countBadge.gameObject.AddComponent<Image>().color = countBg;
            var countText = MakeText(countBadge, "Count", 14f, FontStyles.Bold, TextAlignmentOptions.Center);
            StretchFill(countText.rectTransform, 2f, 2f, 2f, 2f);
            countText.text = cat.Count.ToString(); countText.color = Color.white;

            var selectRoot = MakeRect("SelectBtn", row1);
            selectRoot.gameObject.AddComponent<LayoutElement>().preferredWidth = 54f;
            var selectImg = selectRoot.gameObject.AddComponent<Image>();
            selectImg.color = cat.IsSelected ? ColSelectBtnOn : ColSelectBtnOff;
            var selectBtn = selectRoot.gameObject.AddComponent<Button>();
            selectBtn.targetGraphic = selectImg;
            var capturedId = cat.CategoryId;
            selectBtn.onClick.AddListener(() => OnSelectCategory(capturedId));
            var selectLabel = MakeText(selectRoot, "SelectLabel", 14f, FontStyles.Bold, TextAlignmentOptions.Center);
            StretchFill(selectLabel.rectTransform, 3f, 3f, 3f, 3f);
            selectLabel.text = cat.IsSelected ? "✓" : "Select"; selectLabel.color = Color.white;

            // Row 2: description
            var descText = MakeText(card, "Desc", 12f, FontStyles.Normal, TextAlignmentOptions.Left);
            descText.text = string.IsNullOrWhiteSpace(cat.Description) ? "—" : cat.Description;
            descText.color = ColTextSecondary;
            descText.textWrappingMode = TextWrappingModes.Normal;
            descText.gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;

            // Row 3: eligibility
            var eligText = MakeText(card, "Eligibility", 11f, FontStyles.Normal, TextAlignmentOptions.Left);
            eligText.text  = BuildEligibilityText(cat.Schedule, cat.IsEligible);
            eligText.color = cat.IsEligible ? ColEligible : ColIneligible;
            eligText.gameObject.AddComponent<LayoutElement>().preferredHeight = 14f;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Text format helpers
        // ═════════════════════════════════════════════════════════════════════

        private string BuildStoreSummaryText()
        {
            var store    = adminService.StateStore;
            var settings = store.ActiveSettings;
            var sb       = new StringBuilder();

            sb.AppendLine($"Templates:   {store.Templates.Count}");
            sb.AppendLine($"Available:   {store.AvailablePrizeInstances.Count} total");
            foreach (var kvp in new SortedDictionary<int, int>(store.KioskPrizeCounts))
                sb.AppendLine($"  Kiosk {kvp.Key}: {kvp.Value}");
            sb.AppendLine($"Won history: {store.WonPrizeHistory.Count}");
            sb.AppendLine();

            var res = store.ActiveReservation;
            if (res?.ReservedPrize != null && !string.IsNullOrWhiteSpace(res.ReservedPrize.PrizeInstanceId))
            {
                sb.AppendLine("Reservation:");
                sb.AppendLine($"  {res.ReservedPrize.PrizeInstanceId}");
                sb.AppendLine($"  {res.ReservedPrize.PrizeName}");
                sb.AppendLine($"  Kiosk {res.KioskId}");
            }
            else { sb.AppendLine("Reservation: none"); }
            sb.AppendLine();

            if (string.IsNullOrWhiteSpace(settings.Timezone)) { sb.Append("Settings: not imported"); }
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
                sb.AppendLine($"[{t.PrizeCategoryId}] {t.PrizeName} — {count}× — {FormatScheduleSummary(t.Schedule)}");
            }
            AppendValidationIssues(sb, preview.Issues);
            return sb.ToString();
        }

        private static string FormatSettingsPreview(SettingsCsvPreview preview)
        {
            var s = preview.Settings; var sb = new StringBuilder();
            sb.AppendLine($"Delimiter:   '{preview.Delimiter}'");
            sb.AppendLine($"Timezone:    {s.Timezone}");
            sb.AppendLine($"Kiosks:      {s.KioskCount}");
            sb.AppendLine($"Timeout:     {s.PrizeReservationTimeoutMinutes} min");
            sb.AppendLine($"Max/day:     {s.MaxPrizesPerDay}");
            sb.AppendLine(); sb.AppendLine($"False:       {s.FalsePrizeChancePercent}% base");
            foreach (var t in s.FalsePrizeThresholds) sb.AppendLine($"  ≥{t.ThresholdPercent}% → {t.ChancePercent}%");
            sb.AppendLine($"Forced:      {s.ForcedHourChancePercent}% base");
            foreach (var t in s.ForcedHourThresholds) sb.AppendLine($"  ≥{t.ThresholdPercent}% → {t.ChancePercent}%");
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

        private static string FormatValidationIssueList(IReadOnlyList<CsvValidationIssue> issues)
        {
            var sb = new StringBuilder(); AppendValidationIssues(sb, issues); return sb.ToString();
        }

        private static void AppendValidationIssues(StringBuilder sb, IReadOnlyList<CsvValidationIssue> issues)
        {
            if (issues == null || issues.Count == 0) return;
            sb.AppendLine(); sb.AppendLine($"── {issues.Count} issue(s) ──────────────────────");
            foreach (var issue in issues)
            {
                var row = issue.RowNumber > 0 ? $"row {issue.RowNumber}" : "file";
                sb.AppendLine($"  {row}, {issue.ColumnName}: {issue.Message}");
            }
        }

        private string BuildSelectedCategoryText(IReadOnlyList<CategorySummary> categories)
        {
            if (state.debugPrizeCategoryId == 0) return "Category: none — will claim first eligible";
            foreach (var cat in categories)
                if (cat.CategoryId == state.debugPrizeCategoryId)
                    return $"Category: {cat.CategoryId} — {cat.Name}";
            return $"Category: {state.debugPrizeCategoryId} (not in this kiosk)";
        }

        private static string BuildEligibilityText(PrizeSchedule schedule, bool isEligible)
        {
            var hasTime   = schedule?.PrizeStartMinutesOfDay.HasValue == true;
            var hasDays   = schedule?.PrizeDays?.Count > 0 && schedule.PrizeDays.Count < 7;
            var hasForced = schedule?.HasToComeOutDuringHour == true;
            if (!hasTime && !hasDays) return "✓  No restrictions";
            var sb = new StringBuilder(isEligible ? "✓  " : "⊘  ");
            if (hasTime)
            {
                sb.Append($"{Mints(schedule.PrizeStartMinutesOfDay.Value)}–{Mints(schedule.PrizeEndMinutesOfDay.Value)}");
                if (hasForced) sb.Append(" (forced)");
            }
            if (hasDays) { if (hasTime) sb.Append("  "); sb.Append(string.Join(" ", schedule.PrizeDays.Select(d => DayAbbr[d]))); }
            if (!isEligible) { var n = DateTime.Now; sb.Append($"  (now: {n:ddd} {n:HH:mm})"); }
            return sb.ToString();
        }

        private static string FormatScheduleSummary(PrizeSchedule s)
        {
            if (s == null || (!s.PrizeStartMinutesOfDay.HasValue && (s.PrizeDays == null || s.PrizeDays.Count == 0)))
                return "unrestricted";
            var parts = new List<string>();
            if (s.PrizeStartMinutesOfDay.HasValue)
                parts.Add($"{Mints(s.PrizeStartMinutesOfDay.Value)}–{Mints(s.PrizeEndMinutesOfDay.Value)}");
            if (s.PrizeDays != null && s.PrizeDays.Count > 0 && s.PrizeDays.Count < 7)
                parts.Add(string.Join(" ", s.PrizeDays.Select(d => DayAbbr[d])));
            return string.Join(", ", parts);
        }

        private string BuildPreviewOutputText()
        {
            var sb = new StringBuilder();
            sb.AppendLine(state.previewText); sb.AppendLine();
            sb.AppendLine("── Import Paths ──────────────────────────────────────");
            sb.AppendLine($"Folder:    {state.importFolderPath}");
            sb.AppendLine($"Prizes:    {state.prizesCsvFileName}");
            sb.AppendLine($"Settings:  {state.settingsCsvFileName}");
            sb.AppendLine($"→  {state.PrizesCsvPath}"); sb.AppendLine($"→  {state.SettingsCsvPath}");
            sb.AppendLine();
            sb.AppendLine("── Export Paths ──────────────────────────────────────");
            sb.AppendLine($"Folder:    {state.exportFolderPath}");
            sb.AppendLine($"Won:       {state.wonPrizesExportFileName}");
            sb.AppendLine($"Subtract:  {state.prizePoolSubtractionExportFileName}");
            sb.AppendLine($"→  {state.WonPrizesExportPath}"); sb.Append($"→  {state.SubtractionExportPath}");
            return sb.ToString();
        }

        private static string Mints(int m) => $"{m / 60:D2}:{m % 60:D2}";

        // ═════════════════════════════════════════════════════════════════════
        //  UI factory helpers
        // ═════════════════════════════════════════════════════════════════════

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private static RectTransform MakePanel(Transform parent, string name, float preferredWidth, float flexibleWidth)
        {
            var panel = MakeRect(name, parent);
            panel.gameObject.AddComponent<Image>().color = ColPanelBg;
            var le = panel.gameObject.AddComponent<LayoutElement>();
            if (preferredWidth > 0f) { le.preferredWidth = preferredWidth; le.flexibleWidth = 0f; }
            if (flexibleWidth  > 0f) { le.flexibleWidth = flexibleWidth; }
            var vl = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(12, 12, 12, 12); vl.spacing = 8f;
            vl.childControlWidth = vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            return panel;
        }

        private static TextMeshProUGUI MakePanelTitle(Transform parent, string text)
        {
            var t = MakeText(parent, $"{text}Title", 19f, FontStyles.Bold, TextAlignmentOptions.Left);
            t.text = text; t.color = Color.white;
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;
            return t;
        }

        private static void MakeSectionTitle(Transform parent, string text)
        {
            var t = MakeText(parent, $"{text}Section", 12f, FontStyles.Bold, TextAlignmentOptions.Left);
            t.text = text.ToUpperInvariant(); t.color = new Color32(100, 135, 170, 255);
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 16f;
        }

        private static TMP_InputField MakeLabeledField(
            Transform parent, string label, string initialValue,
            string placeholder, string fieldGoName)
        {
            var container = MakeRect($"{label}Container", parent);
            var vl = container.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 2f; vl.childControlWidth = vl.childControlHeight = true;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;

            var lbl = MakeText(container, $"{label}Label", 12f, FontStyles.Normal, TextAlignmentOptions.Left);
            lbl.text = label; lbl.color = ColTextSecondary;
            lbl.gameObject.AddComponent<LayoutElement>().preferredHeight = 15f;

            return MakeInputField(container, initialValue, placeholder, false, false, 32f, fieldGoName);
        }

        private static Button MakeButton(Transform parent, string label, Color32 color)
        {
            var root = MakeRect(label, parent);
            root.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
            var img = root.gameObject.AddComponent<Image>(); img.color = color;
            var btn = root.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
            var text = MakeText(root, "Label", 14f, FontStyles.Bold, TextAlignmentOptions.Center);
            StretchFill(text.rectTransform, 4f, 4f, 4f, 4f);
            text.text = label; text.color = Color.white;
            return btn;
        }

        private static (Button, Button) MakeButtonRow(Transform parent,
            string labelA, Color32 colorA, string labelB, Color32 colorB)
        {
            var row = MakeRect($"{labelA}Row", parent);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 5f; hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = true;
            return (MakeButton(row, labelA, colorA), MakeButton(row, labelB, colorB));
        }

        private static Button MakeSmallButton(Transform parent, string goName, string displayText, float width)
        {
            var root = MakeRect(goName, parent);
            root.gameObject.AddComponent<LayoutElement>().preferredWidth = width;
            var img = root.gameObject.AddComponent<Image>(); img.color = ColSpinnerBtn;
            var btn = root.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
            var text = MakeText(root, "Label", 18f, FontStyles.Bold, TextAlignmentOptions.Center);
            StretchFill(text.rectTransform, 2f, 2f, 2f, 2f);
            text.text = displayText; text.color = Color.white;
            return btn;
        }

        private static TMP_InputField MakeReadonlyTextField(Transform parent, string initialValue, string placeholder, string goName)
            => MakeInputField(parent, initialValue, placeholder, true, true, 0f, goName);

        private static TMP_InputField MakeInputField(Transform parent, string initialValue, string placeholder,
            bool multiline, bool readOnly, float preferredHeight, string goName = "InputField")
        {
            var root = MakeRect(goName, parent);
            if (preferredHeight > 0f) root.gameObject.AddComponent<LayoutElement>().preferredHeight = preferredHeight;
            root.gameObject.AddComponent<Image>().color = ColInputBg;

            var field = root.gameObject.AddComponent<TMP_InputField>();
            field.readOnly = readOnly;
            field.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;

            var viewport = MakeRect("Viewport", root);
            StretchFill(viewport, 8f, 8f, 6f, 6f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var fs = multiline ? 13f : 14f;
            var textComp = MakeText(viewport, "Text", fs, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            StretchFill(textComp.rectTransform);
            textComp.color = ColTextPrimary; textComp.textWrappingMode = TextWrappingModes.Normal;
            textComp.text = initialValue ?? string.Empty;

            var phComp = MakeText(viewport, "Placeholder", fs, FontStyles.Italic, TextAlignmentOptions.TopLeft);
            StretchFill(phComp.rectTransform);
            phComp.color = ColTextPlaceholder; phComp.textWrappingMode = TextWrappingModes.Normal;
            phComp.text = placeholder;

            field.textViewport = viewport; field.textComponent = textComp; field.placeholder = phComp;
            field.SetTextWithoutNotify(initialValue ?? string.Empty);
            return field;
        }

        private static RectTransform MakeScrollView(Transform parent, string name)
        {
            var scrollRoot = MakeRect(name, parent);
            var sr = scrollRoot.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false; sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 28f;

            var viewport = MakeRect("Viewport", scrollRoot);
            StretchFill(viewport); viewport.gameObject.AddComponent<RectMask2D>(); sr.viewport = viewport;

            var content = MakeRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);   content.sizeDelta = Vector2.zero;

            var cl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            cl.padding = new RectOffset(4, 4, 4, 8); cl.spacing = 5f;
            cl.childControlWidth = cl.childControlHeight = true;
            cl.childForceExpandWidth = true; cl.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = content;
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
            t.fontSize = fontSize; t.fontStyle = style; t.alignment = align;
            t.raycastTarget = false; t.color = Color.white;
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
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
            rt.anchoredPosition = Vector2.zero; rt.localScale = Vector3.one;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Editor helpers
        // ═════════════════════════════════════════════════════════════════════

        private static T FindDescendant<T>(Transform root, string goName) where T : Component
        {
            foreach (var comp in root.GetComponentsInChildren<T>(includeInactive: true))
                if (comp.gameObject.name == goName) return comp;
            return null;
        }

        private static RectTransform FindDescendantRect(Transform root, string goName)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(includeInactive: true))
                if (child.name == goName) return child.GetComponent<RectTransform>();
            return null;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Scans the child canvas, assigns all serialised UI references, and wires
        /// every button as a PERSISTENT listener pointing to this component's public
        /// methods.  Persistent listeners survive domain reloads and serialization —
        /// no runtime wiring is needed for buttons after this runs.
        /// </summary>
        [ContextMenu("Link Canvas References")]
        private void EditorLinkCanvasReferences()
        {
            var canvas = GetComponentInChildren<Canvas>(includeInactive: true);
            if (canvas == null)
            {
                Debug.LogWarning(
                    "[PrizeAdminApp] No child Canvas found.\n" +
                    "Enter Play mode once so BuildUi() creates the canvas, exit, then run this menu again.", this);
                return;
            }

            UnityEditor.Undo.RecordObject(this, "Link PrizeAdminApp Canvas References");

            var root = canvas.transform;
            canvasRoot = canvas.gameObject;

            // Text labels
            statusLabel        = FindDescendant<TextMeshProUGUI>(root, "StatusLabel");
            kioskPanelTitle    = FindDescendant<TextMeshProUGUI>(root, "KioskPanelTitle");
            kioskSelectedLabel = FindDescendant<TextMeshProUGUI>(root, "KioskSelectedLabel");
            kioskSpinnerLabel  = FindDescendant<TextMeshProUGUI>(root, "KioskSpinnerLabel");

            // Input fields
            importFolderPathField    = FindDescendant<TMP_InputField>(root, "ImportFolderPathField");
            prizesCsvFileNameField   = FindDescendant<TMP_InputField>(root, "PrizesCsvFileNameField");
            settingsCsvFileNameField = FindDescendant<TMP_InputField>(root, "SettingsCsvFileNameField");
            exportFolderPathField    = FindDescendant<TMP_InputField>(root, "ExportFolderPathField");
            wonPrizesFileNameField   = FindDescendant<TMP_InputField>(root, "WonPrizesFileNameField");
            subtractionFileNameField = FindDescendant<TMP_InputField>(root, "SubtractionFileNameField");
            storeSummaryField        = FindDescendant<TMP_InputField>(root, "StoreSummaryField");
            settingsPreviewField     = FindDescendant<TMP_InputField>(root, "SettingsPreviewField");
            previewOutputField       = FindDescendant<TMP_InputField>(root, "PreviewOutputField");

            // Buttons
            btnPreviewInitialize = FindDescendant<Button>(root, "Preview Initialize");
            btnApplyInitialize   = FindDescendant<Button>(root, "Apply Initialize");
            btnPreviewAdd        = FindDescendant<Button>(root, "Preview Add");
            btnApplyAdd          = FindDescendant<Button>(root, "Apply Add");
            btnPreviewSettings   = FindDescendant<Button>(root, "Preview Settings");
            btnApplySettings     = FindDescendant<Button>(root, "Apply Settings");
            btnExportWonPrizes   = FindDescendant<Button>(root, "Export Won Prizes");
            btnExportSubtraction = FindDescendant<Button>(root, "Export Subtraction");
            btnClaimPrize        = FindDescendant<Button>(root, "Claim Prize");
            btnForceClaim        = FindDescendant<Button>(root, "Force Claim");
            btnCancelClaim       = FindDescendant<Button>(root, "Cancel Claim");
            btnConfirmClaim      = FindDescendant<Button>(root, "Confirm Claim");
            kioskDecrBtn         = FindDescendant<Button>(root, "KioskDecrBtn");
            kioskIncrBtn         = FindDescendant<Button>(root, "KioskIncrBtn");

            // Kiosk content rect
            kioskCategoryContent = FindDescendantRect(root, "KioskCategoryContent");

            // Wire persistent listeners on every button so they survive domain reloads.
            WirePersistentButton(btnPreviewInitialize, nameof(OnPreviewInitialize));
            WirePersistentButton(btnApplyInitialize,   nameof(OnApplyInitialize));
            WirePersistentButton(btnPreviewAdd,        nameof(OnPreviewAdd));
            WirePersistentButton(btnApplyAdd,          nameof(OnApplyAdd));
            WirePersistentButton(btnPreviewSettings,   nameof(OnPreviewSettings));
            WirePersistentButton(btnApplySettings,     nameof(OnApplySettings));
            WirePersistentButton(btnExportWonPrizes,   nameof(OnExportWonPrizes));
            WirePersistentButton(btnExportSubtraction, nameof(OnExportPrizePoolSubtraction));
            WirePersistentButton(btnClaimPrize,        nameof(OnDebugClaimNormal));
            WirePersistentButton(btnForceClaim,        nameof(OnDebugClaimForce));
            WirePersistentButton(btnCancelClaim,       nameof(OnDebugCancelClaim));
            WirePersistentButton(btnConfirmClaim,      nameof(OnDebugConfirmClaim));
            WirePersistentButton(kioskDecrBtn,         nameof(OnKioskDecrement));
            WirePersistentButton(kioskIncrBtn,         nameof(OnKioskIncrement));

            UnityEditor.EditorUtility.SetDirty(this);
            ReportMissingRefs();

            Debug.Log(
                "[PrizeAdminApp] Canvas references linked and persistent button listeners wired.\n" +
                "Input field callbacks are wired at runtime (Start). The canvas will not be rebuilt at runtime.",
                this);
        }

        /// <summary>
        /// Clears existing persistent calls on the button then adds one pointing to
        /// the named public method on this component.
        /// </summary>
        private void WirePersistentButton(Button btn, string methodName)
        {
            if (btn == null) return;

            UnityEditor.Undo.RecordObject(btn, $"Wire {methodName}");

            // Remove any stale persistent calls first.
            var so    = new UnityEditor.SerializedObject(btn);
            var calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            calls.ClearArray();
            so.ApplyModifiedProperties();

            // Add the new persistent call.
            var method = typeof(PrizeAdminApp).GetMethod(methodName);
            if (method == null)
            {
                Debug.LogWarning($"[PrizeAdminApp] Method '{methodName}' not found. Skipping.", this);
                return;
            }

            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(
                btn.onClick,
                (UnityEngine.Events.UnityAction)Delegate.CreateDelegate(
                    typeof(UnityEngine.Events.UnityAction), this, method));

            UnityEditor.EditorUtility.SetDirty(btn);
        }

        private void ReportMissingRefs()
        {
            var missing = new List<string>();
            if (!canvasRoot)           missing.Add("canvasRoot");
            if (!statusLabel)          missing.Add("statusLabel");
            if (!importFolderPathField)    missing.Add("importFolderPathField");
            if (!prizesCsvFileNameField)   missing.Add("prizesCsvFileNameField");
            if (!settingsCsvFileNameField) missing.Add("settingsCsvFileNameField");
            if (!exportFolderPathField)    missing.Add("exportFolderPathField");
            if (!wonPrizesFileNameField)   missing.Add("wonPrizesFileNameField");
            if (!subtractionFileNameField) missing.Add("subtractionFileNameField");
            if (!storeSummaryField)    missing.Add("storeSummaryField");
            if (!settingsPreviewField) missing.Add("settingsPreviewField");
            if (!previewOutputField)   missing.Add("previewOutputField");
            if (!btnPreviewInitialize) missing.Add("btnPreviewInitialize");
            if (!btnApplyInitialize)   missing.Add("btnApplyInitialize");
            if (!btnPreviewAdd)        missing.Add("btnPreviewAdd");
            if (!btnApplyAdd)          missing.Add("btnApplyAdd");
            if (!btnPreviewSettings)   missing.Add("btnPreviewSettings");
            if (!btnApplySettings)     missing.Add("btnApplySettings");
            if (!btnExportWonPrizes)   missing.Add("btnExportWonPrizes");
            if (!btnExportSubtraction) missing.Add("btnExportSubtraction");
            if (!btnClaimPrize)        missing.Add("btnClaimPrize");
            if (!btnForceClaim)        missing.Add("btnForceClaim");
            if (!btnCancelClaim)       missing.Add("btnCancelClaim");
            if (!btnConfirmClaim)      missing.Add("btnConfirmClaim");
            if (!kioskDecrBtn)         missing.Add("kioskDecrBtn");
            if (!kioskSpinnerLabel)    missing.Add("kioskSpinnerLabel");
            if (!kioskIncrBtn)         missing.Add("kioskIncrBtn");
            if (!kioskPanelTitle)      missing.Add("kioskPanelTitle");
            if (!kioskSelectedLabel)   missing.Add("kioskSelectedLabel");
            if (!kioskCategoryContent) missing.Add("kioskCategoryContent");

            if (missing.Count > 0)
                Debug.LogWarning(
                    $"[PrizeAdminApp] {missing.Count} ref(s) not found — hierarchy may be stale.\n" +
                    $"Missing: {string.Join(", ", missing)}\n" +
                    "Enter Play mode to rebuild, then run 'Link Canvas References' again.", this);
        }
#endif
    }
}
